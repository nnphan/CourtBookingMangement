using System.Data;
using System.ComponentModel.DataAnnotations;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.UserRoles.Models.Requests;
using CourtBookingManagement.Application.UserRoles.Models.Responses;
using CourtBookingManagement.Application.UserRoles.Repositories;
using CourtBookingManagement.Application.UserRoles.Services;
using Moq;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class UserRoleServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Mock<IUserRoleRepository> _repository = new();
    private readonly Mock<IDbConnection> _connection = new();
    private readonly Mock<IDbTransaction> _transaction = new();
    private readonly UserRoleService _service;

    public UserRoleServiceTests()
    {
        var factory = new Mock<ISqlConnectionFactory>();
        factory.Setup(x => x.CreateConnection()).Returns(_connection.Object);
        _connection.Setup(x => x.BeginTransaction(IsolationLevel.ReadCommitted)).Returns(_transaction.Object);
        _repository.Setup(x => x.LockUserAsync(_userId, _transaction.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(x => x.GetActiveRolesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), _transaction.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, IDbTransaction _, CancellationToken _) =>
                ids.Select(id => new RoleItemResponse { RoleId = id }).ToList());
        _repository.Setup(x => x.GetUserRoleAssignmentAsync(_userId, _transaction.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleAssignmentResponse { UserId = _userId });
        _service = new UserRoleService(_repository.Object, factory.Object);
    }

    [Fact]
    public async Task ReplacementDeduplicatesRolesAndCommitsWithinOneTransaction()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        var result = await _service.UpdateRolesAsync(_userId,
            new UpdateUserRolesRequest { RoleIds = [first, second, first] }, _actorId, cts.Token);

        Assert.True(result.IsSuccess);
        _repository.Verify(x => x.RemoveOtherRolesAsync(_userId,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { first, second })),
            _transaction.Object, cts.Token), Times.Once);
        _repository.Verify(x => x.AssignRolesAsync(_userId,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { first, second })),
            _actorId, _transaction.Object, cts.Token), Times.Once);
        _transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public async Task EmptyRolesRemovesAllAssignments()
    {
        var result = await _service.UpdateRolesAsync(_userId,
            new UpdateUserRolesRequest { RoleIds = [] }, _actorId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(x => x.RemoveOtherRolesAsync(_userId,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0),
            _transaction.Object, CancellationToken.None), Times.Once);
        _transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public async Task MissingUserOrInvalidRoleDoesNotMutateAssignments()
    {
        _repository.Setup(x => x.LockUserAsync(_userId, _transaction.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var missingUser = await _service.UpdateRolesAsync(_userId,
            new UpdateUserRolesRequest { RoleIds = [] }, _actorId, CancellationToken.None);
        Assert.Equal("User.NotFound", missingUser.Error.Code);

        _repository.Setup(x => x.LockUserAsync(_userId, _transaction.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(x => x.GetActiveRolesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), _transaction.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleItemResponse>());
        var missingRole = await _service.UpdateRolesAsync(_userId,
            new UpdateUserRolesRequest { RoleIds = [Guid.NewGuid()] }, _actorId, CancellationToken.None);
        Assert.Equal("Role.NotFound", missingRole.Error.Code);
        _repository.Verify(x => x.RemoveOtherRolesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.Commit(), Times.Never);
    }

    [Fact]
    public async Task MissingOrInvalidRoleIdsAreRejectedBeforeOpeningConnection()
    {
        var missing = new UpdateUserRolesRequest();
        var empty = new UpdateUserRolesRequest { RoleIds = [] };
        Assert.False(Validator.TryValidateObject(missing, new ValidationContext(missing), [], true));
        Assert.True(Validator.TryValidateObject(empty, new ValidationContext(empty), [], true));
        var missingResult = await _service.UpdateRolesAsync(_userId, missing, _actorId, CancellationToken.None);
        var invalidResult = await _service.UpdateRolesAsync(_userId,
            new UpdateUserRolesRequest { RoleIds = [Guid.Empty] }, _actorId, CancellationToken.None);
        Assert.Equal("USER_ROLE.INVALID_ROLES", missingResult.Error.Code);
        Assert.Equal("USER_ROLE.INVALID_ROLES", invalidResult.Error.Code);
        _connection.Verify(x => x.Open(), Times.Never);
    }

    [Fact]
    public async Task FailureAfterRemovalDoesNotCommitAndDisposesTransaction()
    {
        _repository.Setup(x => x.AssignRolesAsync(_userId, It.IsAny<IReadOnlyCollection<Guid>>(),
                _actorId, _transaction.Object, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateRolesAsync(_userId,
            new UpdateUserRolesRequest { RoleIds = [Guid.NewGuid()] }, _actorId, CancellationToken.None));

        _transaction.Verify(x => x.Commit(), Times.Never);
        _transaction.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task CancellationBeforeCommitDisposesWithoutCommitting()
    {
        using var cts = new CancellationTokenSource();
        _repository.Setup(x => x.GetUserRoleAssignmentAsync(_userId, _transaction.Object, cts.Token))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new UserRoleAssignmentResponse { UserId = _userId });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.UpdateRolesAsync(_userId,
            new UpdateUserRolesRequest { RoleIds = [] }, _actorId, cts.Token));

        _transaction.Verify(x => x.Commit(), Times.Never);
        _transaction.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task AdditiveAssignmentUsesSameUserLockAsReplacement()
    {
        var roleId = Guid.NewGuid();
        var result = await _service.AssignRolesAsync(_userId,
            new AssignUserRolesRequest { RoleIds = [roleId] }, _actorId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repository.Verify(x => x.LockUserAsync(_userId, _transaction.Object, CancellationToken.None), Times.Once);
        _repository.Verify(x => x.RemoveOtherRolesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(x => x.Commit(), Times.Once);
    }
}
