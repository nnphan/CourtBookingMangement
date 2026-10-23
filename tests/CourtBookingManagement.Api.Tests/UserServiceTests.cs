using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Users.DTOs;
using CourtBookingManagement.Application.Users.Services;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using FluentValidation;
using Xunit;

namespace CourtBookingManagement.Api.Tests;

public sealed class UserServiceTests
{
    [Fact]
    public async Task GetListAsync_returns_users_with_all_roles_and_preserves_nullable_and_inactive_fields()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var repository = new FakeUserRepository
        {
            UserList =
            [
                new UserListItem(
                    userId,
                    "Jamie Player",
                    "jamie@example.com",
                    null,
                    false,
                    [
                        new UserListRole(roleId, "PLAYER", "Player"),
                        new UserListRole(Guid.NewGuid(), "CUSTOMER", "Customer")
                    ])
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetListAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(result.Value);
        Assert.Equal(userId, user.Id);
        Assert.Equal("Jamie Player", user.FullName);
        Assert.Equal("jamie@example.com", user.Email);
        Assert.Null(user.PhoneNumber);
        Assert.False(user.IsActive);
        Assert.Collection(
            user.Roles,
            role =>
            {
                Assert.Equal(roleId, role.Id);
                Assert.Equal("PLAYER", role.Code);
                Assert.Equal("Player", role.Name);
            },
            role =>
            {
                Assert.Equal("CUSTOMER", role.Code);
                Assert.Equal("Customer", role.Name);
            });
    }

    [Fact]
    public async Task GetListAsync_returns_an_empty_list_when_no_users_exist()
    {
        var result = await CreateService(new FakeUserRepository()).GetListAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetListAsync_propagates_cancellation_to_the_repository()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new FakeUserRepository();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(repository).GetListAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, repository.ReceivedCancellationToken);
    }

    private static UserService CreateService(FakeUserRepository repository) =>
        new(
            repository,
            new FakeUnitOfWork(),
            new FakeDateTimeProvider(),
            new InlineValidator<CreateUserRequestDto>());

    private sealed class FakeUserRepository : IUserRepository
    {
        public IReadOnlyList<UserListItem> UserList { get; init; } = [];

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UserListItem>> GetListWithRolesAsync(CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(UserList);
        }

        public Task<bool> ExistsByEmailAsync(
            string email,
            Guid? excludingId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guid> CreateUserAsync(
            User user,
            System.Data.IDbTransaction transaction,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
