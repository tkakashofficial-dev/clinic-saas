namespace Clinic.Application.Features.Reports.DTOs;

/// <summary>Practice analytics for the admin dashboard/reports page.</summary>
public class PracticeOverviewDto
{
    public int TotalPatients { get; set; }
    public int NewPatientsLast30Days { get; set; }
    public int AppointmentsToday { get; set; }
    public int CompletedLast30Days { get; set; }
    public int CancelledLast30Days { get; set; }
    public List<DayCountDto> AppointmentsPerDay { get; set; } = new();
    public List<StatusCountDto> ByStatusLast30Days { get; set; } = new();
    public List<DoctorLoadDto> PerDoctorLast30Days { get; set; } = new();
}

public class DayCountDto
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}

public class StatusCountDto
{
    public string Status { get; set; } = default!;
    public int Count { get; set; }
}

public class DoctorLoadDto
{
    public string DoctorName { get; set; } = default!;
    public int Total { get; set; }
    public int Completed { get; set; }
}
