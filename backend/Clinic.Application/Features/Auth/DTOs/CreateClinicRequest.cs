namespace Clinic.Application.Features.Auth.DTOs;

/// <summary>An existing user opens an additional clinic (they become its Admin).</summary>
public class CreateClinicRequest
{
    public string Name { get; set; } = default!;
    public bool OwnerIsDoctor { get; set; }
}
