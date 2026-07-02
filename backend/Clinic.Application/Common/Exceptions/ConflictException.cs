namespace Clinic.Application.Common.Exceptions;

/// <summary>The request conflicts with existing state (e.g. duplicate email/phone). Maps to HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);
