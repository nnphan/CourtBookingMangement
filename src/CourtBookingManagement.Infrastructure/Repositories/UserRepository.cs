using DomainUser = CourtBookingManagement.Domain.Users.User;
using EfUser = CourtBookingManagement.Infrastructure.Persistence.Entities.User;
using CourtBookingManagement.Domain.Users;
using CourtBookingManagement.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DomainUser?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext
            .Set<EfUser>()
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task AddAsync(
        DomainUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await _dbContext
            .Set<EfUser>()
            .AddAsync(user.ToPersistence(), cancellationToken);
    }

    public async Task UpdateAsync(
        DomainUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var entity = await _dbContext
            .Set<EfUser>()
            .SingleOrDefaultAsync(entity => entity.Id == user.Id, cancellationToken)
            ?? throw new InvalidOperationException($"User '{user.Id}' was not found.");

        user.ApplyTo(entity);
    }
}