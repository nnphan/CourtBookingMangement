using System.Data;
using System.Diagnostics.CodeAnalysis;
using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Auth.DTOs;
using CourtBookingManagement.Application.Auth.Exceptions;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Auth.Services;
using CourtBookingManagement.Application.Auth.Validators;
using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Models.Responses;
using CourtBookingManagement.Application.Customers.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;
using CourtBookingManagement.Application.Roles.Repositories;
using CourtBookingManagement.Application.UserRoles.Models.Responses;
using CourtBookingManagement.Application.UserRoles.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class AuthRegistrationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();
    private static readonly Guid MembershipLevelId = Guid.NewGuid();
    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!");

    [Fact]
    public async Task Register_creates_customer_with_normalized_data_and_optional_membership()
    {
        var fixture = new RegistrationFixture();
        fixture.Memberships.MembershipLevelId = MembershipLevelId;

        var result = await fixture.Service.RegisterAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal("member@example.com", fixture.Users.LastCheckedEmail);
        Assert.Equal("member@example.com", fixture.Customers.CustomerRequest!.Email);
        Assert.Equal("Jamie Player", fixture.Customers.CustomerRequest.FullName);
        Assert.Equal("+15551234567", fixture.Customers.CustomerRequest.PhoneNumber);
        Assert.Equal(UserId, fixture.Customers.CustomerRequest.UserId);
        Assert.Equal(UserId, fixture.Customers.CustomerRequest.CreatedBy);
        Assert.Equal(MembershipLevelId, fixture.Customers.CustomerRequest.MembershipLevelId);
        Assert.False(fixture.Customers.CustomerRequest.IsGuest);
        Assert.Equal(0, fixture.Customers.CustomerRequest.LoyaltyPointsBalance);
        Assert.True(fixture.Customers.CustomerRequest.IsActive);
        Assert.Equal(UserId, fixture.UserRoles.AssignedBy);
        Assert.Equal(1, fixture.UserRoles.AssignmentCount);
        Assert.Equal(1, fixture.Connection.Transaction.CommitCount);
        Assert.Equal(0, fixture.Connection.Transaction.RollbackCount);
    }

    [Fact]
    public async Task Register_succeeds_without_a_default_membership_level()
    {
        var fixture = new RegistrationFixture();

        var result = await fixture.Service.RegisterAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Null(fixture.Customers.CustomerRequest!.MembershipLevelId);
        Assert.Equal(1, fixture.Connection.Transaction.CommitCount);
    }

    [Fact]
    public async Task Register_rejects_existing_email_before_opening_transaction()
    {
        var fixture = new RegistrationFixture();
        fixture.Users.EmailExists = true;

        var result = await fixture.Service.RegisterAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_EMAIL_ALREADY_EXISTS", result.Error.Code);
        Assert.Equal(0, fixture.Connection.BeginTransactionCount);
        Assert.Null(fixture.Customers.CustomerRequest);
    }

    [Fact]
    public async Task Register_rejects_existing_phone_before_user_creation()
    {
        var fixture = new RegistrationFixture();
        fixture.Users.PhoneNumberExists = true;

        var result = await fixture.Service.RegisterAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_PHONE_ALREADY_EXISTS", result.Error.Code);
        Assert.Equal("+15551234567", fixture.Users.LastCheckedPhoneNumber);
        Assert.Equal(0, fixture.Users.CreateUserCount);
        Assert.Equal(0, fixture.Connection.BeginTransactionCount);
        Assert.Null(fixture.Customers.CustomerRequest);
    }

    [Fact]
    public async Task Register_prioritizes_email_when_both_credentials_exist()
    {
        var fixture = new RegistrationFixture();
        fixture.Users.EmailExists = true;
        fixture.Users.PhoneNumberExists = true;

        var result = await fixture.Service.RegisterAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_EMAIL_ALREADY_EXISTS", result.Error.Code);
        Assert.Null(fixture.Users.LastCheckedPhoneNumber);
    }

    [Fact]
    public async Task Register_checks_case_insensitive_normalized_email()
    {
        var fixture = new RegistrationFixture();
        fixture.Users.EmailExists = true;
        var request = ValidRequest();
        request.Email = " User@Test.com ";

        var result = await fixture.Service.RegisterAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_EMAIL_ALREADY_EXISTS", result.Error.Code);
        Assert.Equal("user@test.com", fixture.Users.LastCheckedEmail);
    }

    [Fact]
    public async Task Register_maps_phone_unique_violation_to_phone_conflict()
    {
        var fixture = new RegistrationFixture();
        fixture.Users.CreateException = new RegistrationConflictException(
            "ux_users_active_phone_number",
            new InvalidOperationException("Unique violation."));

        var result = await fixture.Service.RegisterAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_PHONE_ALREADY_EXISTS", result.Error.Code);
        Assert.Equal(1, fixture.Connection.Transaction.RollbackCount);
        Assert.Equal(0, fixture.Connection.Transaction.CommitCount);
    }

    [Fact]
    public async Task Register_rejects_missing_customer_role_before_opening_transaction()
    {
        var fixture = new RegistrationFixture();
        fixture.Roles.RoleId = null;

        var result = await fixture.Service.RegisterAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.CustomerRoleNotConfigured", result.Error.Code);
        Assert.Equal(0, fixture.Connection.BeginTransactionCount);
        Assert.Null(fixture.Customers.CustomerRequest);
    }

    [Fact]
    public async Task Register_rolls_back_when_customer_creation_fails()
    {
        var fixture = new RegistrationFixture();
        fixture.Customers.CreateException = new InvalidOperationException("Customer insert failed.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RegisterAsync(ValidRequest()));

        Assert.Equal(0, fixture.Connection.Transaction.CommitCount);
        Assert.Equal(1, fixture.Connection.Transaction.RollbackCount);
    }

    [Fact]
    public async Task Register_rolls_back_and_propagates_cancellation_during_customer_creation()
    {
        var fixture = new RegistrationFixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Customers.BeforeCreate = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Service.RegisterAsync(ValidRequest(), cancellation.Token));

        Assert.Equal(0, fixture.Connection.Transaction.CommitCount);
        Assert.Equal(1, fixture.Connection.Transaction.RollbackCount);
    }

    [Fact]
    public async Task Register_maps_concurrent_email_unique_violation_to_existing_error()
    {
        var fixture = new RegistrationFixture();
        fixture.Users.CreateException = new RegistrationConflictException(
            "ux_users_email",
            new InvalidOperationException("Unique violation."));

        var result = await fixture.Service.RegisterAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_EMAIL_ALREADY_EXISTS", result.Error.Code);
        Assert.Equal(1, fixture.Connection.Transaction.RollbackCount);
        Assert.Equal(0, fixture.Connection.Transaction.CommitCount);
    }

    [Fact]
    public async Task Register_rejects_invalid_email_format()
    {
        var fixture = new RegistrationFixture();
        var request = ValidRequest();
        request.Email = "not-an-email";

        var result = await fixture.Service.RegisterAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.InvalidRequest", result.Error.Code);
        Assert.Equal(0, fixture.Connection.BeginTransactionCount);
    }

    [Fact]
    public async Task Login_by_phone_trims_phone_and_uses_shared_token_and_permission_pipeline()
    {
        var fixture = new RegistrationFixture();
        var user = LoginUser();
        fixture.Auth.PhoneNumberUser = user;
        fixture.Permissions.Roles = ["CUSTOMER"];
        fixture.Permissions.Permissions = ["booking.read"];

        var result = await fixture.Service.LoginByPhoneNumberAsync(new LoginByPhoneNumberRequest
        {
            PhoneNumber = "  +15551234567  ",
            Password = "CorrectPassword1!"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("+15551234567", fixture.Auth.LastLookedUpPhoneNumber);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
        Assert.Equal("CUSTOMER", Assert.Single(result.Value.User.Roles));
        Assert.Equal("booking.read", Assert.Single(result.Value.User.Permissions));
        Assert.Equal(UserId, fixture.Jwt.LastAccessTokenUserId);
        Assert.Equal("CUSTOMER", Assert.Single(fixture.Jwt.LastRoles));
        Assert.Equal("booking.read", Assert.Single(fixture.Jwt.LastPermissions));
        Assert.NotNull(fixture.Users.UpdatedUser?.LastLoginAt);
        Assert.NotNull(fixture.Auth.AddedRefreshToken);
        Assert.Equal(1, fixture.Auth.CreatedSessionCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Login_by_phone_returns_generic_invalid_credentials_for_unknown_phone_and_wrong_password()
    {
        var unknownPhoneFixture = new RegistrationFixture();
        var unknownPhoneResult = await unknownPhoneFixture.Service.LoginByPhoneNumberAsync(ValidPhoneLoginRequest());

        Assert.True(unknownPhoneResult.IsFailure);
        Assert.Equal("Auth.InvalidCredentials", unknownPhoneResult.Error.Code);
        Assert.Equal("Invalid email or password.", unknownPhoneResult.Error.Description);

        var wrongPasswordFixture = new RegistrationFixture();
        wrongPasswordFixture.Auth.PhoneNumberUser = LoginUser();
        var wrongPasswordResult = await wrongPasswordFixture.Service.LoginByPhoneNumberAsync(new LoginByPhoneNumberRequest
        {
            PhoneNumber = "+15551234567",
            Password = "WrongPassword1!"
        });

        Assert.True(wrongPasswordResult.IsFailure);
        Assert.Equal(unknownPhoneResult.Error.Code, wrongPasswordResult.Error.Code);
        Assert.Equal(unknownPhoneResult.Error.Description, wrongPasswordResult.Error.Description);
    }

    [Fact]
    public async Task Login_by_phone_rejects_inactive_and_soft_deleted_users()
    {
        var inactiveFixture = new RegistrationFixture();
        inactiveFixture.Auth.PhoneNumberUser = LoginUser(isActive: false);
        var inactiveResult = await inactiveFixture.Service.LoginByPhoneNumberAsync(ValidPhoneLoginRequest());

        Assert.True(inactiveResult.IsFailure);
        Assert.Equal("Auth.UserDisabled", inactiveResult.Error.Code);

        var deletedFixture = new RegistrationFixture();
        deletedFixture.Auth.PhoneNumberUser = LoginUser(deletedAt: DateTime.UtcNow);
        var deletedResult = await deletedFixture.Service.LoginByPhoneNumberAsync(ValidPhoneLoginRequest());

        Assert.True(deletedResult.IsFailure);
        Assert.Equal("Auth.InvalidCredentials", deletedResult.Error.Code);
    }

    [Fact]
    public async Task Login_by_phone_fails_closed_when_phone_matches_multiple_users()
    {
        var fixture = new RegistrationFixture();
        fixture.Auth.PhoneNumberUser = LoginUser();
        fixture.Auth.PhoneNumberMatchCount = 2;

        var result = await fixture.Service.LoginByPhoneNumberAsync(ValidPhoneLoginRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("Auth.InvalidCredentials", result.Error.Code);
        Assert.Null(fixture.Jwt.LastAccessTokenUserId);
    }

    [Fact]
    public async Task Login_by_email_and_phone_use_equivalent_authorization_claims()
    {
        var emailFixture = new RegistrationFixture();
        emailFixture.Auth.EmailUser = LoginUser();
        emailFixture.Permissions.Roles = ["CUSTOMER"];
        emailFixture.Permissions.Permissions = ["booking.read"];
        var emailResult = await emailFixture.Service.LoginAsync(new LoginRequest
        {
            Email = "member@example.com",
            Password = "CorrectPassword1!"
        });

        var phoneFixture = new RegistrationFixture();
        phoneFixture.Auth.PhoneNumberUser = LoginUser();
        phoneFixture.Permissions.Roles = ["CUSTOMER"];
        phoneFixture.Permissions.Permissions = ["booking.read"];
        var phoneResult = await phoneFixture.Service.LoginByPhoneNumberAsync(ValidPhoneLoginRequest());

        Assert.True(emailResult.IsSuccess);
        Assert.True(phoneResult.IsSuccess);
        Assert.Equal(emailFixture.Jwt.LastAccessTokenUserId, phoneFixture.Jwt.LastAccessTokenUserId);
        Assert.Equal(emailFixture.Jwt.LastRoles, phoneFixture.Jwt.LastRoles);
        Assert.Equal(emailFixture.Jwt.LastPermissions, phoneFixture.Jwt.LastPermissions);
        Assert.Equal(emailResult.Value.User.Roles, phoneResult.Value.User.Roles);
        Assert.Equal(emailResult.Value.User.Permissions, phoneResult.Value.User.Permissions);
    }

    [Fact]
    public async Task Login_by_phone_validator_requires_credentials_and_limits_phone_length()
    {
        var validator = new LoginByPhoneNumberRequestValidator();

        Assert.False((await validator.ValidateAsync(new LoginByPhoneNumberRequest())).IsValid);
        Assert.False((await validator.ValidateAsync(new LoginByPhoneNumberRequest
        {
            PhoneNumber = new string('1', 21),
            Password = "password"
        })).IsValid);
        Assert.True((await validator.ValidateAsync(ValidPhoneLoginRequest())).IsValid);
    }

    private static LoginByPhoneNumberRequest ValidPhoneLoginRequest() => new()
    {
        PhoneNumber = "+15551234567",
        Password = "CorrectPassword1!"
    };

    private static User LoginUser(bool isActive = true, DateTime? deletedAt = null)
    {
        var now = DateTime.UtcNow;
        return User.Rehydrate(
            UserId,
            "member@example.com",
            "Jamie Player",
            "+15551234567",
            PasswordHash,
            isActive,
            false,
            null,
            now,
            null,
            now,
            null,
            deletedAt,
            null).Value;
    }

    private static RegisterRequest ValidRequest() => new()
    {
        Email = "  Member@Example.com  ",
        FullName = " Jamie Player ",
        Password = "StrongPassword1!",
        PhoneNumber = " +15551234567 "
    };

    private sealed class RegistrationFixture
    {
        public FakeUserRepository Users { get; } = new();
        public FakeRoleRepository Roles { get; } = new();
        public FakeUserRoleRepository UserRoles { get; } = new();
        public FakeCustomerRepository Customers { get; } = new();
        public FakeMembershipLevelRepository Memberships { get; } = new();
        public FakeConnection Connection { get; } = new();
        public FakeAuthRepository Auth { get; } = new();
        public FakePermissionService Permissions { get; } = new();
        public FakeJwtTokenService Jwt { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public AuthService Service { get; }

        public RegistrationFixture()
        {
            Service = new AuthService(
                Users,
                Jwt,
                Permissions,
                UnitOfWork,
                new FakeDateTimeProvider(),
                Auth,
                Roles,
                UserRoles,
                Customers,
                Memberships,
                new FakeConnectionFactory(Connection),
                NullLogger<AuthService>.Instance,
                new LoginByPhoneNumberRequestValidator());
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public bool EmailExists { get; set; }
        public bool PhoneNumberExists { get; set; }
        public Exception? CreateException { get; set; }
        public string? LastCheckedEmail { get; private set; }
        public string? LastCheckedPhoneNumber { get; private set; }
        public int CreateUserCount { get; private set; }
        public User? UpdatedUser { get; private set; }

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludingId = null, CancellationToken cancellationToken = default)
        {
            LastCheckedEmail = email;
            return Task.FromResult(EmailExists);
        }

        public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCheckedPhoneNumber = phoneNumber;
            return Task.FromResult(PhoneNumberExists);
        }

        public Task<Guid> CreateUserAsync(User user, IDbTransaction transaction, CancellationToken cancellationToken = default)
        {
            CreateUserCount++;
            if (CreateException is not null)
            {
                throw CreateException;
            }

            return Task.FromResult(AuthRegistrationTests.UserId);
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserListItem>> GetListWithRolesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            UpdatedUser = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public Guid? RoleId { get; set; } = AuthRegistrationTests.RoleId;
        public Task<Guid?> GetRoleIdByCodeAsync(string roleCode, CancellationToken cancellationToken) => Task.FromResult(RoleId);
        public Task<PagedResult<RoleResponse>> SearchAsync(RoleSearchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeUserRoleRepository : IUserRoleRepository
    {
        public Guid? AssignedBy { get; private set; }
        public int AssignmentCount { get; private set; }

        public Task AssignRoleAsync(Guid userId, Guid roleId, Guid assignedBy, IDbTransaction transaction, CancellationToken cancellationToken)
        {
            AssignedBy = assignedBy;
            AssignmentCount++;
            return Task.CompletedTask;
        }

        public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserRoleResponse>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoleItemResponse>> GetActiveRolesAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAssignedRoleIdsAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AssignRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, Guid assignedBy, IDbTransaction transaction, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserRoleAssignmentResponse?> GetUserRoleAssignmentAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public CreateCustomerInternalRequest? CustomerRequest { get; private set; }
        public Exception? CreateException { get; set; }
        public Action? BeforeCreate { get; set; }

        public Task<Guid> CreateCustomerAsync(CreateCustomerInternalRequest request, IDbTransaction transaction, CancellationToken cancellationToken)
        {
            BeforeCreate?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            if (CreateException is not null)
            {
                throw CreateException;
            }

            CustomerRequest = request;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<bool> ExistsByUserIdAsync(Guid userId, IDbTransaction transaction, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<PagedResult<CustomerListItemResponse>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CustomerDetailResponse?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeMembershipLevelRepository : IMembershipLevelRepository
    {
        public Guid? MembershipLevelId { get; set; }
        public Task<Guid?> GetDefaultMembershipLevelIdAsync(CancellationToken cancellationToken) => Task.FromResult(MembershipLevelId);
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public User? EmailUser { get; set; }
        public User? PhoneNumberUser { get; set; }
        public int PhoneNumberMatchCount { get; set; } = 1;
        public string? LastLookedUpPhoneNumber { get; private set; }
        public RefreshTokenRecord? AddedRefreshToken { get; private set; }
        public int CreatedSessionCount { get; private set; }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(EmailUser);
        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            LastLookedUpPhoneNumber = phoneNumber;
            var user = PhoneNumberUser;
            return Task.FromResult(
                user is not null
                && PhoneNumberMatchCount == 1
                && user.DeletedAt is null
                && user.PhoneNumber == phoneNumber
                    ? user
                    : null);
        }
        public Task<RefreshTokenRecord?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRefreshTokenAsync(RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default)
        {
            AddedRefreshToken = refreshToken;
            return Task.CompletedTask;
        }
        public Task UpdateRefreshTokenAsync(RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeAllActiveRefreshTokensAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CloseAllSessionsAsync(Guid userId, DateTime endedAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateUserSessionAsync(Guid userId, string? ipAddress, string? userAgent, DateTime startedAt, CancellationToken cancellationToken = default)
        {
            CreatedSessionCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public Guid? LastAccessTokenUserId { get; private set; }
        public string[] LastRoles { get; private set; } = [];
        public string[] LastPermissions { get; private set; } = [];

        public string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            LastAccessTokenUserId = userId;
            LastRoles = roles.ToArray();
            LastPermissions = permissions.ToArray();
            return "access-token";
        }
        public string GenerateRefreshToken() => "refresh-token";
        public string HashToken(string token) => string.Empty;
    }

    private sealed class FakePermissionService : IPermissionService
    {
        public IReadOnlyCollection<string> Roles { get; set; } = [];
        public IReadOnlyCollection<string> Permissions { get; set; } = [];
        public Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Permissions);
        public Task<IReadOnlyCollection<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Roles);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class FakeConnectionFactory(FakeConnection connection) : ISqlConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class FakeConnection : IDbConnection
    {
        public FakeTransaction Transaction { get; }
        public int BeginTransactionCount { get; private set; }
        [AllowNull]
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State { get; private set; }

        public FakeConnection() => Transaction = new FakeTransaction(this);
        public IDbTransaction BeginTransaction() => BeginTransaction(IsolationLevel.Unspecified);
        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            BeginTransactionCount++;
            return Transaction;
        }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public void Close() => State = ConnectionState.Closed;
        public void Open() => State = ConnectionState.Open;
        public void Dispose() => Close();
    }

    private sealed class FakeTransaction(FakeConnection connection) : IDbTransaction
    {
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public IDbConnection Connection => connection;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => CommitCount++;
        public void Rollback() => RollbackCount++;
        public void Dispose() { }
    }

}