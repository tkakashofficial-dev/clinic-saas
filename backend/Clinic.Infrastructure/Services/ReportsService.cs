using Clinic.Application.Features.Reports.DTOs;
using Clinic.Application.Features.Reports.Services;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class ReportsService : IReportsService
{
    private readonly ClinicDbContext _context;

    public ReportsService(ClinicDbContext context)
    {
        _context = context;
    }

    public async Task<PracticeOverviewDto> GetPracticeOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        // All queries are auto-scoped to the current clinic by the tenant filter
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var last30 = now.AddDays(-30);
        var last14Start = todayStart.AddDays(-13);

        var totalPatients = await _context.Patients.CountAsync(cancellationToken);

        var newPatients = await _context.Patients
            .CountAsync(p => p.CreatedAt >= last30, cancellationToken);

        var appointmentsToday = await _context.Appointments
            .CountAsync(a => a.AppointmentDate >= todayStart
                && a.AppointmentDate < todayStart.AddDays(1), cancellationToken);

        var byStatus = await _context.Appointments
            .Where(a => a.AppointmentDate >= last30)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var perDayRaw = await _context.Appointments
            .Where(a => a.AppointmentDate >= last14Start)
            .GroupBy(a => a.AppointmentDate.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Fill missing days with zero so the chart has a continuous axis
        var perDay = Enumerable.Range(0, 14)
            .Select(offset =>
            {
                var day = last14Start.AddDays(offset);
                return new DayCountDto
                {
                    Date = DateOnly.FromDateTime(day),
                    Count = perDayRaw.FirstOrDefault(d => d.Date == day)?.Count ?? 0
                };
            })
            .ToList();

        var perDoctor = await _context.Appointments
            .Where(a => a.AppointmentDate >= last30)
            .GroupBy(a => new
            {
                a.DoctorTenantUserId,
                Name = a.Doctor.SystemUser.FirstName + " " + a.Doctor.SystemUser.LastName
            })
            .Select(g => new DoctorLoadDto
            {
                DoctorName = g.Key.Name,
                Total = g.Count(),
                Completed = g.Count(a => a.Status == AppointmentStatus.Completed)
            })
            .OrderByDescending(d => d.Total)
            .ToListAsync(cancellationToken);

        return new PracticeOverviewDto
        {
            TotalPatients = totalPatients,
            NewPatientsLast30Days = newPatients,
            AppointmentsToday = appointmentsToday,
            CompletedLast30Days = byStatus
                .FirstOrDefault(s => s.Status == AppointmentStatus.Completed)?.Count ?? 0,
            CancelledLast30Days = byStatus
                .FirstOrDefault(s => s.Status == AppointmentStatus.Cancelled)?.Count ?? 0,
            AppointmentsPerDay = perDay,
            ByStatusLast30Days = byStatus
                .Select(s => new StatusCountDto { Status = s.Status.ToString(), Count = s.Count })
                .ToList(),
            PerDoctorLast30Days = perDoctor
        };
    }
}
