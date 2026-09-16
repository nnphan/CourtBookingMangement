using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Auth;

public sealed class PermissionService(ApplicationDbContext dbContext) : IPermissionService
{
    public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var permissions = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.Permissions)
            .Select(p => p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissions;
    }

    public async Task<IReadOnlyCollection<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return roles;
    }
}
