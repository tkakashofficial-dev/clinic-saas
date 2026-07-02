namespace Clinic.Application.Features.Billing.DTOs;

public class BillingSummaryDto
{
    public string Plan { get; set; } = default!;
    public bool IsInTrial { get; set; }
    /// <summary>Trial lapsed without choosing a plan — clinic is on the Solo floor.</summary>
    public bool TrialExpired { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public int StaffCount { get; set; }
    public int MaxStaff { get; set; }
    public int DoctorCount { get; set; }
    public int MaxDoctors { get; set; }
}

public class ChangePlanRequest
{
    public string Plan { get; set; } = default!;
}
