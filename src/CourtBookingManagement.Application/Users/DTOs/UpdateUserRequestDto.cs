namespace CourtBookingManagement.Application.Users.DTOs;

public sealed class UpdateUserRequestDto
{
    public string Email { get; init; } = string.Empty;

    public string? PasswordHash { get; init; }

    public string? PhoneNumber { get; init; }

    public Guid? UpdatedBy { get; init; }
}