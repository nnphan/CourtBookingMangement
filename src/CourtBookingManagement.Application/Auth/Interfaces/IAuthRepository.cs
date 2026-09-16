using CourtBookingManagement.Domain.Users;

namespace CourtBookingManagement.Application.Auth.Interfaces;

public sealed class RefreshTokenRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByToken { get; set; }
    public string? DeviceInfo { get; set; }
}

public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<RefreshTokenRecord?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddRefreshTokenAsync(RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default);

    Task UpdateRefreshTokenAsync(RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllActiveRefreshTokensAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken = default);

    Task CloseAllSessionsAsync(Guid userId, DateTime endedAt, CancellationToken cancellationToken = default);

    Task CreateUserSessionAsync(Guid userId, string? ipAddress, string? userAgent, DateTime startedAt, CancellationToken cancellationToken = default);
}
