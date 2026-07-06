using Clinic.Api.Middleware;
using Clinic.Api.Workers;
using Clinic.Application;
using Clinic.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

namespace Clinic.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ValidationFilter runs FluentValidation on every request body before
        // the controller executes — invalid input never reaches business logic
        builder.Services.AddControllers(options =>
            options.Filters.Add<ValidationFilter>());
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddEndpointsApiExplorer();

        // Centralized error handling: exceptions -> RFC 7807 Problem Details
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        // Reminder engine: notifies doctors/front desk about upcoming appointments
        builder.Services.AddHostedService<AppointmentReminderWorker>();

        // CORS: browsers block cross-origin calls (Angular on :4200 -> API) unless
        // the API explicitly allows the origin. Origins come from config so
        // production can list the real frontend URL without a code change.
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        builder.Services.AddCors(options =>
            options.AddPolicy("Frontend", policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()));

        // Rate limit auth endpoints per client IP — makes password
        // brute-forcing impractical (10 attempts/minute, then 429)
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Clinic API",
                Version = "v1"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token here. Example: Bearer eyJhbGci..."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // Fail fast for deployment-critical config so a bad env setup surfaces
        // immediately instead of becoming a generic 500 during signup or clinic creation.
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is missing. Set it in Render or your hosting environment.");

        var jwtSecret = builder.Configuration["JwtSettings:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
            throw new InvalidOperationException(
                "JwtSettings:Secret is missing or shorter than 32 characters. " +
                "Set it via 'dotnet user-secrets set \"JwtSettings:Secret\" \"<value>\"' (dev) " +
                "or an environment variable (prod).");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    ValidAudience = builder.Configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret))
                };
            });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        // Hosted environments (Render etc.): apply pending EF migrations at
        // startup so deploys are one step. Local dev keeps explicit
        // `dotnet ef database update` (flag stays off).
        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider
                .GetRequiredService<Clinic.Infrastructure.Persistence.ClinicDbContext>()
                .Database.Migrate();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Clinic API v1");
                options.RoutePrefix = string.Empty;
            });
        }

        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseCors("Frontend");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // Health probe for hosting platforms (and humans checking a deploy)
        app.MapGet("/health", async (IServiceProvider services) =>
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Clinic.Infrastructure.Persistence.ClinicDbContext>();
                var dbOk = await db.Database.CanConnectAsync();

                return Results.Ok(new
                {
                    status = dbOk ? "ok" : "degraded",
                    database = dbOk,
                    timeUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "error",
                    database = false,
                    error = ex.Message,
                    timeUtc = DateTime.UtcNow
                }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.Run();
    }
}