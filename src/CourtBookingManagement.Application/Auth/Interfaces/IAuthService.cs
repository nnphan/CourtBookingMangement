using CourtBookingManagement.Application.Auth.DTOs;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Auth.Interfaces;

public interface IAuthService
{
    Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(string refreshToken, Guid? userId = null, CancellationToken cancellationToken = default);

    Task<Result> LogoutAllDevicesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<CurrentUserResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
