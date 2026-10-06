using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Application.Branches.Services;
using CourtBookingManagement.Infrastructure.Persistence;
using CourtBookingManagement.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BranchRepository(ApplicationDbContext dbContext) : IBranchRepository
{
    public Task<Guid?> GetOwnerIdForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Owners
            .Where(owner => owner.UserId == userId && owner.IsActive && owner.DeletedAt == null)
            .Select(owner => (Guid?)owner.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid ownerId, string name, CancellationToken cancellationToken) =>
        dbContext.Branches.AnyAsync(
            branch => branch.OwnerId == ownerId && branch.Name == name && branch.DeletedAt == null,
            cancellationToken);

    public async Task<Guid> AddAsync(CreateBranchData data, CancellationToken cancellationToken)
    {
        var branch = new Branch
        {
            Id = data.Id,
            OwnerId = data.OwnerId,
            Name = data.Name,
            Address = data.Address,
            City = data.City,
            District = data.District,
            Latitude = data.Latitude,
            Longitude = data.Longitude,
            PhoneNumber = data.PhoneNumber,
            TimeZone = data.TimeZone,
            SupportsInstantBooking = data.SupportsInstantBooking,
            IsActive = true,
            CreatedBy = data.CreatedByUserId,
            UpdatedBy = data.CreatedByUserId,
            BranchImages = data.Images.Select(image => new BranchImage
            {
                ImageUrl = image.ImageUrl,
                SortOrder = image.SortOrder,
                IsActive = true
            }).ToList(),
            OperatingHours = data.OperatingHours.Select(hour => new OperatingHour
            {
                OpenTime = TimeOnly.FromTimeSpan(hour.OpenTime),
                CloseTime = TimeOnly.FromTimeSpan(hour.CloseTime),
                IsClosed = hour.IsClosed
            }).ToList(),
            Amenities = await dbContext.Amenities
                .Where(amenity => data.AmenityIds.Contains(amenity.Id))
                .ToListAsync(cancellationToken)
        };

        await dbContext.Branches.AddAsync(branch, cancellationToken);
        return branch.Id;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException
            && postgresException.ConstraintName == "ux_branches_owner_name_active")
        {
            throw new BranchAlreadyExistsException();
        }
    }
}