namespace CourtBookingManagement.Application.Auth.Exceptions;

public sealed class RegistrationConflictException(string constraintName, Exception innerException)
    : Exception("A registration uniqueness constraint was violated.", innerException)
{
    public string ConstraintName { get; } = constraintName;
}