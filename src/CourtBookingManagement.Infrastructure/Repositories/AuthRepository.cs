using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Domain.Users;
using CourtBookingManagement.Infrastructure.Persistence;
using CourtBookingManagement.Infrastructure.Persistence.Entities;
using CourtBookingManagement.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class AuthRepository(ApplicationDbContext dbContext) : IAuthRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<CourtBookingManagement.Domain.Users.User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<CourtBookingManagement.Infrastructure.Persistence.Entities.User>()
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower() && u.DeletedAt == null, cancellationToken);

        return entity is null ? null : entity.ToDomain();
    }

    public async Task<RefreshTokenRecord?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new RefreshTokenRecord
        {
            Id = entity.Id,
            UserId = entity.UserId,
            TokenHash = entity.TokenHash,
            IssuedAt = entity.IssuedAt,
            ExpiresAt = entity.ExpiresAt,
            RevokedAt = entity.RevokedAt,
            ReplacedByToken = entity.ReplacedByToken,
            DeviceInfo = entity.DeviceInfo
        };
    }

    public async Task AddRefreshTokenAsync(RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default)
    {
        await _dbContext.RefreshTokens.AddAsync(new Infrastructure.Persistence.Entities.RefreshToken
        {
            Id = refreshToken.Id,
            UserId = refreshToken.UserId,
            TokenHash = refreshToken.TokenHash,
            IssuedAt = refreshToken.IssuedAt,
            ExpiresAt = refreshToken.ExpiresAt,
            RevokedAt = refreshToken.RevokedAt,
            ReplacedByToken = refreshToken.ReplacedByToken,
            DeviceInfo = refreshToken.DeviceInfo
        }, cancellationToken);
    }

    public async Task UpdateRefreshTokenAsync(RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.Id == refreshToken.Id, cancellationToken);

        if (entity is null)
        {
            entity = await _dbContext.RefreshTokens
                .SingleOrDefaultAsync(rt => rt.TokenHash == refreshToken.TokenHash, cancellationToken)
                ?? throw new InvalidOperationException($"Refresh token '{refreshToken.Id}' was not found.");
        }

        entity.RevokedAt = refreshToken.RevokedAt;
        entity.ReplacedByToken = refreshToken.ReplacedByToken;
        entity.TokenHash = refreshToken.TokenHash;
        entity.ExpiresAt = refreshToken.ExpiresAt;
        entity.DeviceInfo = refreshToken.DeviceInfo;
        entity.UserId = refreshToken.UserId;
    }

    public async Task RevokeAllActiveRefreshTokensAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.RevokedAt = revokedAt;
        }
    }

    public async Task CloseAllSessionsAsync(Guid userId, DateTime endedAt, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.EndedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.EndedAt = endedAt;
            session.LastSeenAt = endedAt;
        }
    }

    public async Task CreateUserSessionAsync(Guid userId, string? ipAddress, string? userAgent, DateTime startedAt, CancellationToken cancellationToken = default)
    {
        await _dbContext.UserSessions.AddAsync(new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartedAt = startedAt,
            LastSeenAt = startedAt,
            UserAgent = userAgent,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : System.Net.IPAddress.Parse(ipAddress)
        }, cancellationToken);
    }
}
