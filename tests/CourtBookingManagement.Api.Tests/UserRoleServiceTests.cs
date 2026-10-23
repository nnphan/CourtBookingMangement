using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.UserRoles.Models.Requests;
using CourtBookingManagement.Application.UserRoles.Models.Responses;
using CourtBookingManagement.Application.UserRoles.Repositories;
using CourtBookingManagement.Application.UserRoles.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class UserRoleServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AssignedBy = Guid.NewGuid();
    private static readonly Guid ExistingRoleId = Guid.NewGuid();
    private static readonly Guid NewRoleId = Guid.NewGuid();

    [Fact]
    public async Task AssignRoles_adds_only_unassigned_roles_and_preserves_existing_roles()
    {
        var repository = new FakeUserRoleRepository
        {
            AssignedRoleIds = [ExistingRoleId],
            ActiveRoleIds = [ExistingRoleId, NewRoleId],
            Assignment = new UserRoleAssignmentResponse
            {
                UserId = UserId,
                Roles =
                [
                    new RoleItemResponse { RoleId = ExistingRoleId, RoleCode = "CUSTOMER", RoleName = "Customer" },
                    new RoleItemResponse { RoleId = NewRoleId, RoleCode = "STAFF", RoleName = "Staff" }
                ]
            }
        };
        var connection = new FakeConnection();
        var service = CreateService(repository, connection);
        var request = new AssignUserRolesRequest { RoleIds = [ExistingRoleId, NewRoleId, NewRoleId] };

        var result = await service.AssignRolesAsync(UserId, request, AssignedBy, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { NewRoleId }, repository.InsertedRoleIds);
        Assert.Equal(AssignedBy, repository.AssignedBy);
        Assert.Equal(new[] { ExistingRoleId, NewRoleId }, result.Value.Roles.Select(role => role.RoleId));
        Assert.Equal(1, connection.BeginTransactionCount);
        Assert.Equal(1, connection.Transaction.CommitCount);
        Assert.Equal(0, connection.Transaction.RollbackCount);
    }

    [Fact]
    public async Task AssignRoles_is_idempotent_when_role_is_already_assigned()
    {
        var repository = new FakeUserRoleRepository
        {
            AssignedRoleIds = [ExistingRoleId],
            ActiveRoleIds = [ExistingRoleId],
            Assignment = new UserRoleAssignmentResponse { UserId = UserId }
        };
        var connection = new FakeConnection();
        var service = CreateService(repository, connection);

        var result = await service.AssignRolesAsync(
            UserId,
            new AssignUserRolesRequest { RoleIds = [ExistingRoleId] },
            AssignedBy,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.InsertedRoleIds);
        Assert.Equal(0, connection.BeginTransactionCount);
    }

    [Fact]
    public async Task AssignRoles_accepts_the_single_role_request_contract()
    {
        var repository = new FakeUserRoleRepository
        {
            ActiveRoleIds = [NewRoleId],
            Assignment = new UserRoleAssignmentResponse { UserId = UserId }
        };
        var connection = new FakeConnection();
        var service = CreateService(repository, connection);

        var result = await service.AssignRolesAsync(
            UserId,
            new AssignUserRolesRequest { RoleId = NewRoleId },
            AssignedBy,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { NewRoleId }, repository.InsertedRoleIds);
        Assert.Equal(1, connection.Transaction.CommitCount);
    }

    [Fact]
    public async Task AssignRoles_rejects_ambiguous_single_and_batch_role_requests()
    {
        var repository = new FakeUserRoleRepository();
        var connection = new FakeConnection();
        var service = CreateService(repository, connection);

        var result = await service.AssignRolesAsync(
            UserId,
            new AssignUserRolesRequest { RoleId = NewRoleId, RoleIds = [NewRoleId] },
            AssignedBy,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("USER_ROLE.ROLE_IDS_REQUIRED", result.Error.Code);
        Assert.Empty(repository.InsertedRoleIds);
        Assert.Equal(0, connection.BeginTransactionCount);
    }

    [Fact]
    public async Task AssignRoles_returns_not_found_when_target_user_does_not_exist()
    {
        var repository = new FakeUserRoleRepository { UserExists = false };
        var connection = new FakeConnection();
        var service = CreateService(repository, connection);

        var result = await service.AssignRolesAsync(
            UserId,
            new AssignUserRolesRequest { RoleIds = [NewRoleId] },
            AssignedBy,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Empty(repository.InsertedRoleIds);
        Assert.Equal(0, connection.BeginTransactionCount);
    }

    [Fact]
    public async Task AssignRoles_rejects_missing_or_inactive_roles_without_inserting()
    {
        var repository = new FakeUserRoleRepository { ActiveRoleIds = [] };
        var connection = new FakeConnection();
        var service = CreateService(repository, connection);

        var result = await service.AssignRolesAsync(
            UserId,
            new AssignUserRolesRequest { RoleIds = [NewRoleId] },
            AssignedBy,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Role.NotFound", result.Error.Code);
        Assert.Equal(StatusCodes.Status404NotFound, ErrorStatusMapper.ToStatusCode(result.Error.Code));
        Assert.Empty(repository.InsertedRoleIds);
        Assert.Equal(0, connection.BeginTransactionCount);
    }

    [Fact]
    public async Task AssignRoles_propagates_cancellation_instead_of_returning_a_business_failure()
    {
        var repository = new FakeUserRoleRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(repository, new FakeConnection());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AssignRolesAsync(
            UserId,
            new AssignUserRolesRequest { RoleIds = [NewRoleId] },
            AssignedBy,
            cancellation.Token));
    }

    private static UserRoleService CreateService(FakeUserRoleRepository repository, FakeConnection connection) =>
        new(repository, new FakeSqlConnectionFactory(connection));

    private sealed class FakeUserRoleRepository : IUserRoleRepository
    {
        public bool UserExists { get; init; } = true;
        public IReadOnlyCollection<Guid> ActiveRoleIds { get; init; } = [NewRoleId];
        public IReadOnlyCollection<Guid> AssignedRoleIds { get; init; } = [];
        public UserRoleAssignmentResponse? Assignment { get; init; } =
            new() { UserId = UserId };
        public IReadOnlyCollection<Guid> InsertedRoleIds { get; private set; } = [];
        public Guid? AssignedBy { get; private set; }

        public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(UserExists);
        }

        public Task<IReadOnlyList<UserRoleResponse>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RoleItemResponse>> GetActiveRolesAsync(
            IReadOnlyCollection<Guid> roleIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RoleItemResponse>>(ActiveRoleIds
                .Where(roleId => roleIds.Contains(roleId))
                .Select(roleId => new RoleItemResponse { RoleId = roleId })
                .ToArray());

        public Task<IReadOnlyList<Guid>> GetAssignedRoleIdsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(AssignedRoleIds.ToArray());

        public Task AssignRolesAsync(
            Guid userId,
            IReadOnlyCollection<Guid> roleIds,
            Guid assignedBy,
            IDbTransaction transaction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InsertedRoleIds = roleIds.ToArray();
            AssignedBy = assignedBy;
            return Task.CompletedTask;
        }

        public Task<UserRoleAssignmentResponse?> GetUserRoleAssignmentAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Assignment);

        public Task AssignRoleAsync(
            Guid userId,
            Guid roleId,
            Guid assignedBy,
            IDbTransaction transaction,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSqlConnectionFactory(FakeConnection connection) : ISqlConnectionFactory
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
