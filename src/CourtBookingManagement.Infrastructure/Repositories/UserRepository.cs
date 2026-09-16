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
            .SingleOrDefaultAsync(
                user => user.Id == id && user.DeletedAt == null,
                cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<DomainUser>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext
            .Set<EfUser>()
            .AsNoTracking()
            .Where(user => user.DeletedAt == null)
            .OrderByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(entity => entity.ToDomain()).ToList();
    }

    public Task<bool> ExistsByEmailAsync(
        string email,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return _dbContext
            .Set<EfUser>()
            .AnyAsync(
                user => user.DeletedAt == null
                    && user.Email.ToLower() == email.ToLower()
                    && (excludingId == null || user.Id != excludingId),
                cancellationToken);
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