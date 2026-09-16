using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Auth.DTOs;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;

namespace CourtBookingManagement.Application.Auth.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPermissionService _permissionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuthRepository _authRepository;

    public AuthService(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IPermissionService permissionService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuthRepository authRepository)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _permissionService = permissionService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _authRepository = authRepository;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result.Failure<AuthResponse>(new Error("Auth.InvalidRequest", "Email and password are required."));
        }

        var normalizedEmail = request.Email.Trim();
        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken: cancellationToken))
        {
            return Result.Failure<AuthResponse>(UserErrors.EmailAlreadyExists(normalizedEmail));
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var userResult = User.Create(
            normalizedEmail,
            passwordHash,
            _dateTimeProvider.UtcNow,
            request.PhoneNumber,
            null);

        if (userResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(userResult.Error);
        }

        await _userRepository.AddAsync(userResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await CreateAuthResponseAsync(userResult.Value, cancellationToken);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result.Failure<AuthResponse>(new Error("Auth.InvalidRequest", "Email and password are required."));
        }

        var user = await _authRepository.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.InvalidCredentials", "Invalid email or password."));
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.UserDisabled", "User account is disabled."));
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResponse>(new Error("Auth.InvalidCredentials", "Invalid email or password."));
        }

        if (!user.IsEmailVerified)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.EmailNotVerified", "Email is not verified."));
        }

        user.RecordLogin(_dateTimeProvider.UtcNow);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Failure<AuthResponse>(new Error("Auth.InvalidRequest", "Refresh token is required."));
        }

        var refreshToken = await _authRepository.GetRefreshTokenByHashAsync(
            _jwtTokenService.HashToken(request.RefreshToken),
            cancellationToken);

        if (refreshToken is null)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.RefreshTokenInvalid", "Refresh token is invalid."));
        }

        if (refreshToken.RevokedAt is not null)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.RefreshTokenRevoked", "Refresh token has been revoked."));
        }

        if (refreshToken.ExpiresAt <= _dateTimeProvider.UtcNow)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.RefreshTokenExpired", "Refresh token has expired."));
        }

        var user = await _userRepository.GetByIdAsync(refreshToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthResponse>(new Error("Auth.UserDisabled", "User account is disabled."));
        }

        var oldToken = refreshToken;
        var newRefreshTokenValue = _jwtTokenService.GenerateRefreshToken();
        var newRefreshTokenId = Guid.NewGuid();

        oldToken.RevokedAt = _dateTimeProvider.UtcNow;
        oldToken.ReplacedByToken = newRefreshTokenId;

        var newRefreshToken = new RefreshTokenRecord
        {
            Id = newRefreshTokenId,
            UserId = user.Id,
            TokenHash = _jwtTokenService.HashToken(newRefreshTokenValue),
            IssuedAt = _dateTimeProvider.UtcNow,
            ExpiresAt = _dateTimeProvider.UtcNow.AddDays(30),
            DeviceInfo = oldToken.DeviceInfo,
            ReplacedByToken = oldToken.Id
        };

        await _authRepository.AddRefreshTokenAsync(newRefreshToken, cancellationToken);
        await _authRepository.UpdateRefreshTokenAsync(oldToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var roles = await _permissionService.GetUserRolesAsync(user.Id, cancellationToken);
        var permissions = await _permissionService.GetUserPermissionsAsync(user.Id, cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, roles, permissions);

        return Result.Success(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshTokenValue,
            ExpiresIn = 900,
            User = new CurrentUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified,
                Roles = roles,
                Permissions = permissions
            }
        });
    }

    public async Task<Result> LogoutAsync(string refreshToken, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result.Failure(new Error("Auth.InvalidRequest", "Refresh token is required."));
        }

        var tokenHash = _jwtTokenService.HashToken(refreshToken);
        var storedToken = await _authRepository.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (storedToken is null)
        {
            return Result.Success();
        }

        if (userId is not null && storedToken.UserId != userId.Value)
        {
            return Result.Failure(new Error("Auth.Unauthorized", "You cannot revoke another user's session."));
        }

        storedToken.RevokedAt = _dateTimeProvider.UtcNow;
        await _authRepository.UpdateRefreshTokenAsync(storedToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> LogoutAllDevicesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _authRepository.RevokeAllActiveRefreshTokensAsync(userId, _dateTimeProvider.UtcNow, cancellationToken);
        await _authRepository.CloseAllSessionsAsync(userId, _dateTimeProvider.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<CurrentUserResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<CurrentUserResponse>(UserErrors.NotFound(userId));
        }

        var roles = await _permissionService.GetUserRolesAsync(user.Id, cancellationToken);
        var permissions = await _permissionService.GetUserPermissionsAsync(user.Id, cancellationToken);

        return Result.Success(new CurrentUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            Roles = roles,
            Permissions = permissions
        });
    }

    private async Task<Result<AuthResponse>> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var roles = await _permissionService.GetUserRolesAsync(user.Id, cancellationToken);
        var permissions = await _permissionService.GetUserPermissionsAsync(user.Id, cancellationToken);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, roles, permissions);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenHash = _jwtTokenService.HashToken(refreshToken);

        var newRefreshToken = new RefreshTokenRecord
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            IssuedAt = _dateTimeProvider.UtcNow,
            ExpiresAt = _dateTimeProvider.UtcNow.AddDays(30),
            DeviceInfo = "web"
        };

        await _authRepository.AddRefreshTokenAsync(newRefreshToken, cancellationToken);
        await _authRepository.CreateUserSessionAsync(user.Id, "127.0.0.1", "system", _dateTimeProvider.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 900,
            User = new CurrentUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified,
                Roles = roles,
                Permissions = permissions
            }
        });
    }
}
