namespace Clinic.Domain.Enums;

/// <summary>
/// Real clinic front-desk flow:
/// Scheduled -> CheckedIn (patient arrived, waiting) -> InProgress (in the chair)
/// -> Completed (consultation recorded). Cancelled can happen before InProgress.
/// Stored as string in the DB, so adding values needs no migration.
/// </summary>
public enum AppointmentStatus
{
    Scheduled,
    CheckedIn,
    InProgress,
    Completed,
    Cancelled
}
