namespace Clinic.Application.Common.Exceptions;

/// <summary>The requested resource does not exist (or belongs to another tenant). Maps to HTTP 404.</summary>
public class NotFoundException(string message) : Exception(message);
