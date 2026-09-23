namespace CourtBookingManagement.Application.Users.DTOs;

public sealed class CreateUserRequestDto
{
    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public Guid? CreatedBy { get; init; }
}