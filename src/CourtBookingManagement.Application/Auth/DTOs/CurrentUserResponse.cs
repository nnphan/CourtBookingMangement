namespace CourtBookingManagement.Application.Auth.DTOs;

public sealed class CurrentUserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? FullName { get; init; }

    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; }

    public bool IsEmailVerified { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();
}
