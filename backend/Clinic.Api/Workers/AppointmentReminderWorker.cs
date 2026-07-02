using Clinic.Domain.Constants;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Workers;

/// <summary>
/// The reminder engine: every few minutes it finds appointments starting
/// within the next 24 hours that haven't been reminded yet, and notifies the
/// doctor and the person who booked. Runs across ALL tenants (background jobs
/// have no current user, so the tenant filter is explicitly skipped and each
/// notification carries its appointment's own TenantId).
/// WhatsApp/SMS delivery later = extra channels for these same records.
/// </summary>
public class AppointmentReminderWorker : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderWorker> _logger;

    public AppointmentReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder scan failed — will retry next interval");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var now = DateTime.UtcNow;
        var horizon = now.Add(ReminderWindow);

        var dueAppointments = await context.Appointments
            .IgnoreQueryFilters([QueryFilters.Tenant])   // background job: all clinics
            .Where(a => a.ReminderSentAt == null
                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.CheckedIn)
                && a.AppointmentDate > now
                && a.AppointmentDate <= horizon)
            .Select(a => new
            {
                Appointment = a,
                PatientName = a.Patient.FirstName + " " + a.Patient.LastName
            })
            .Take(200) // batch cap per scan
            .ToListAsync(cancellationToken);

        if (dueAppointments.Count == 0)
            return;

        foreach (var due in dueAppointments)
        {
            var appointment = due.Appointment;
            var message = $"{due.PatientName} · {appointment.AppointmentDate:d MMM, HH:mm}";

            context.Notifications.Add(new Notification(
                appointment.TenantId,
                appointment.DoctorTenantUserId,
                NotificationTypes.Reminder,
                "Upcoming appointment",
                message));

            if (appointment.CreatedByTenantUserId != appointment.DoctorTenantUserId)
            {
                context.Notifications.Add(new Notification(
                    appointment.TenantId,
                    appointment.CreatedByTenantUserId,
                    NotificationTypes.Reminder,
                    "Upcoming appointment",
                    message));
            }

            appointment.MarkReminderSent();
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Reminder engine: {Count} appointments reminded", dueAppointments.Count);
    }
}
