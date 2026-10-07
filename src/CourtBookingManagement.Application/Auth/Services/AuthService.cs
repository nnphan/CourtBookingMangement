using System.Data;
using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Auth.DTOs;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Repositories;
using CourtBookingManagement.Application.Roles.Repositories;
using CourtBookingManagement.Application.UserRoles.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;

namespace CourtBookingManagement.Application.Auth.Services;

public sealed class AuthService : IAuthService
{
    private const string CustomerRoleCode = "CUSTOMER";
    private const int MaxCustomerFullNameLength = 150;
    private const int MaxPhoneNumberLength = 20;

    private static readonly Error CustomerRoleNotConfigured = new(
        "Auth.CustomerRoleNotConfigured",
        "Configuration error. CUSTOMER role does not exist.");

    private static readonly Error DefaultMembershipNotConfigured = new(
        "Auth.DefaultMembershipNotConfigured",
        "Configuration error. Default membership level does not exist.");

    private static readonly Error CustomerProfileAlreadyExists = new(
        "Customer.ProfileAlreadyExists",
        "A customer profile already exists for this user.");

    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPermissionService _permissionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuthRepository _authRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMembershipLevelRepository _membershipLevelRepository;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public AuthService(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IPermissionService permissionService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuthRepository authRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        ICustomerRepository customerRepository,
        IMembershipLevelRepository membershipLevelRepository,
        ISqlConnectionFactory sqlConnectionFactory)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _permissionService = permissionService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _authRepository = authRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _customerRepository = customerRepository;
        _membershipLevelRepository = membershipLevelRepository;
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRegisterRequest(request);
        if (validationError != Error.None)
        {
            return Result.Failure<RegisterResponse>(validationError);
        }

        var normalizedEmail = request.Email.Trim();
        var fullName = request.FullName!.Trim();
        var phoneNumber = request.PhoneNumber!.Trim();

        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken: cancellationToken))
        {
            return Result.Failure<RegisterResponse>(UserErrors.EmailAlreadyExists(normalizedEmail));
        }

        // Resolve reference data before writing anything so a misconfigured system fails without side effects.
        var customerRoleId = await _roleRepository.GetRoleIdByCodeAsync(CustomerRoleCode, cancellationToken);
        if (customerRoleId is null)
        {
            return Result.Failure<RegisterResponse>(CustomerRoleNotConfigured);
        }

        var membershipLevelId = await _membershipLevelRepository.GetDefaultMembershipLevelIdAsync(cancellationToken);
        if (membershipLevelId is null)
        {
            return Result.Failure<RegisterResponse>(DefaultMembershipNotConfigured);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var userResult = User.Create(
            normalizedEmail,
            fullName,
            passwordHash,
            _dateTimeProvider.UtcNow,
            phoneNumber,
            null);

        if (userResult.IsFailure)
        {
            return Result.Failure<RegisterResponse>(userResult.Error);
        }

        var user = userResult.Value;

        using var connection = _sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            var userId = await _userRepository.CreateUserAsync(user, transaction, cancellationToken);

            await _userRoleRepository.AssignRoleAsync(userId, customerRoleId.Value, transaction, cancellationToken);

            if (await _customerRepository.ExistsByUserIdAsync(userId, transaction, cancellationToken))
            {
                transaction.Rollback();
                return Result.Failure<RegisterResponse>(CustomerProfileAlreadyExists);
            }

            await _customerRepository.CreateCustomerAsync(
                new CreateCustomerInternalRequest
                {
                    UserId = userId,
                    MembershipLevelId = membershipLevelId.Value,
                    FullName = fullName,
                    Email = normalizedEmail,
                    PhoneNumber = phoneNumber,
                    IsGuest = false,
                    LoyaltyPointsBalance = 0,
                    IsActive = true
                },
                transaction,
                cancellationToken);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return await CreateRegisterResponseAsync(user, cancellationToken);
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

        //if (!user.IsEmailVerified)
        //{
        //    return Result.Failure<AuthResponse>(new Error("Auth.EmailNotVerified", "Email is not verified."));
        //}

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
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified,
                Roles = roles,
                Permissions = permissions
            }
        });
    }

    // customer.customers requires full_name and phone_number, so registration must collect both.
    private static Error ValidateRegisterRequest(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new Error("Auth.InvalidRequest", "Email and password are required.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return new Error("Auth.InvalidRequest", "Full name is required.");
        }

        if (request.FullName.Trim().Length > MaxCustomerFullNameLength)
        {
            return new Error("Auth.InvalidRequest", $"Full name cannot exceed {MaxCustomerFullNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return new Error("Auth.InvalidRequest", "Phone number is required.");
        }

        if (request.PhoneNumber.Trim().Length > MaxPhoneNumberLength)
        {
            return new Error("Auth.InvalidRequest", $"Phone number cannot exceed {MaxPhoneNumberLength} characters.");
        }

        return Error.None;
    }

    private async Task<Result<RegisterResponse>> CreateRegisterResponseAsync(User user, CancellationToken cancellationToken)
    {
        return Result.Success(new RegisterResponse
        {
            User = new CurrentUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified
            }
        });
    }
}
