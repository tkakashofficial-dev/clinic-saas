using Clinic.Application.Features.Appointments.DTOs;
using Clinic.Application.Features.Appointments.Validators;
using Clinic.Application.Features.Auth.DTOs;
using Clinic.Application.Features.Auth.Validators;
using Clinic.Application.Features.Patients.DTOs;
using Clinic.Application.Features.Patients.Validators;
using Clinic.Domain.Constants;

namespace Clinic.Tests;

/// <summary>
/// Pure in-memory tests — validators need no database, so these are instant.
/// </summary>
public class ValidatorTests
{
    // ---------- RegisterRequest ----------

    [Fact]
    public void Register_WeakPassword_Fails()
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest
        {
            FirstName = "A", LastName = "B",
            Email = "owner@clinic.com",
            Password = "short",           // < 8 chars, no digit
            ClinicName = "Clinic"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Register_ValidRequest_Passes()
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest
        {
            FirstName = "Owner", LastName = "Person",
            Email = "owner@clinic.com",
            Password = "Str0ng-Pass-123",
            ClinicName = "Bright Smiles"
        });

        Assert.True(result.IsValid);
    }

    // ---------- RegisterPatientRequest ----------

    [Fact]
    public void Patient_FutureDateOfBirth_Fails()
    {
        var request = ValidPatientRequest();
        request.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);

        var result = new RegisterPatientRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterPatientRequest.DateOfBirth));
    }

    [Fact]
    public void Patient_InvalidPhone_Fails()
    {
        var request = ValidPatientRequest();
        request.Phone = "call me maybe";

        var result = new RegisterPatientRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterPatientRequest.Phone));
    }

    [Fact]
    public void Patient_ValidRequest_Passes()
    {
        var result = new RegisterPatientRequestValidator().Validate(ValidPatientRequest());
        Assert.True(result.IsValid);
    }

    // ---------- CreateAppointmentRequest ----------

    [Fact]
    public void Appointment_PastDate_Fails()
    {
        var result = new CreateAppointmentRequestValidator().Validate(new CreateAppointmentRequest
        {
            PatientId = Guid.NewGuid(),
            DoctorTenantUserId = Guid.NewGuid(),
            AppointmentDate = DateTime.UtcNow.AddDays(-1)
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateAppointmentRequest.AppointmentDate));
    }

    [Fact]
    public void Appointment_MissingIds_Fails()
    {
        var result = new CreateAppointmentRequestValidator().Validate(new CreateAppointmentRequest
        {
            PatientId = Guid.Empty,
            DoctorTenantUserId = Guid.Empty,
            AppointmentDate = DateTime.UtcNow.AddDays(1)
        });

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    // ---------- RoleNames ----------

    [Theory]
    [InlineData("doctor", RoleNames.Doctor)]
    [InlineData("ADMIN", RoleNames.Admin)]
    [InlineData("Receptionist", RoleNames.Receptionist)]
    public void RoleNames_TryNormalize_MatchesCaseInsensitively(string input, string expected)
    {
        Assert.True(RoleNames.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("Docter")]
    [InlineData("SuperAdmin")]
    [InlineData("")]
    [InlineData(null)]
    public void RoleNames_TryNormalize_RejectsUnknownRoles(string? input)
    {
        Assert.False(RoleNames.TryNormalize(input, out _));
    }

    private static RegisterPatientRequest ValidPatientRequest() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        Phone = "+31612345678",
        Gender = "Male",
        DateOfBirth = new DateOnly(1985, 3, 15)
    };
}
