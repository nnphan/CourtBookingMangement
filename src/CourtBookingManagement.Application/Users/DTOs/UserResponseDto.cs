namespace CourtBookingManagement.Application.Users.DTOs;

public sealed class UserResponseDto
{
    public Guid Id { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; }

    public bool IsEmailVerified { get; init; }

    public DateTime? LastLoginAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public Guid? CreatedBy { get; init; }

    public DateTime UpdatedAt { get; init; }

    public Guid? UpdatedBy { get; init; }
}