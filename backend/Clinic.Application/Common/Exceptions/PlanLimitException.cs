namespace Clinic.Application.Common.Exceptions;

/// <summary>
/// The tenant's plan doesn't allow this action. Maps to HTTP 402 Payment
/// Required — the UI turns it into an "Upgrade plan" prompt.
/// </summary>
public class PlanLimitException(string message) : Exception(message);
