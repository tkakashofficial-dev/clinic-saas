using Clinic.Application.Common.Interfaces;
using Clinic.Application.Features.Appointments.Services;
using Clinic.Application.Features.Auth.Services;
using Clinic.Application.Features.Patients.Services;
using Clinic.Application.Features.Staff.Services;
using Clinic.Infrastructure.Persistence;
using Clinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddDbContext<ClinicDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "clinic")
            ));

        services.Configure<JwtSettings>(
            configuration.GetSection(nameof(JwtSettings)));

        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        return services;
    }
}