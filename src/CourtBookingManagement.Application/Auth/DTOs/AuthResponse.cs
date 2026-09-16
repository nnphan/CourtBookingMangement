namespace CourtBookingManagement.Application.Auth.DTOs;

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public int ExpiresIn { get; init; }

    public CurrentUserResponse User { get; init; } = new();
}
