namespace Clinic.Application.Common.Exceptions;

/// <summary>The request is invalid (bad enum value, unknown reference, etc.). Maps to HTTP 400.</summary>
public class BadRequestException(string message) : Exception(message);
