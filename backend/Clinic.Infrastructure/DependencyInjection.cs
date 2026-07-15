using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Appointments.Services;
using Clinic.Application.Features.Auth.Services;
using Clinic.Application.Features.Consultations.Services;
using Clinic.Application.Features.Notifications.Services;
using Clinic.Application.Features.Patients.Services;
using Clinic.Application.Features.Reports.Services;
using Clinic.Application.Features.Staff.Services;
using Clinic.Infrastructure.Persistence;
using Clinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;

namespace Clinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // QuestPDF Community license (free for organizations < $1M revenue)
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddDbContext<ClinicDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "clinic")
            ));

        services.Configure<JwtSettings>(
            configuration.GetSection(nameof(JwtSettings)));

        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.Configure<BrevoSettings>(configuration.GetSection("Brevo"));
        services.Configure<FrontendSettings>(configuration.GetSection("Frontend"));
        services.Configure<PlatformSettings>(configuration.GetSection("Platform"));

        // Email provider: Brevo (HTTPS API — no SMTP blocks, no app passwords
        // to expire) wins automatically when its key is configured; otherwise
        // Gmail SMTP. Set Brevo__ApiKey + Brevo__FromEmail env vars to switch.
        services.AddHttpClient<BrevoEmailSender>();
        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<IEmailSender>(sp =>
            sp.GetRequiredService<IOptions<BrevoSettings>>().Value.IsConfigured
                ? sp.GetRequiredService<BrevoEmailSender>()
                : sp.GetRequiredService<SmtpEmailSender>());
        services.AddScoped<Clinic.Application.Features.Platform.Services.IPlatformService, PlatformService>();

        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IConsultationService, ConsultationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportsService, ReportsService>();
        services.AddScoped<Clinic.Application.Features.Billing.Services.IBillingService, BillingService>();
        services.AddScoped<Clinic.Application.Features.Inventory.Services.IInventoryService, InventoryService>();
        services.AddScoped<Clinic.Application.Features.Settings.Services.IClinicSettingsService, ClinicSettingsService>();
        return services;
    }
}