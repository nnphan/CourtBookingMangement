using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Application.Branches.Services;
using CourtBookingManagement.Infrastructure.Persistence;
using CourtBookingManagement.Infrastructure.Persistence.Entities;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class BranchRepository(
    ApplicationDbContext dbContext,
    ISqlConnectionFactory sqlConnectionFactory) : IBranchRepository
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

    public async Task<bool?> IsDeletedAsync(Guid branchId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT b.deleted_at IS NOT NULL
            FROM core.branches b
            WHERE b.id = @BranchId;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SoftDeleteAsync(
        Guid branchId,
        Guid deletedBy,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string deleteBranchSql = """
            UPDATE core.branches
            SET
                is_active = FALSE,
                deleted_at = NOW(),
                deleted_by = @DeletedBy,
                updated_at = NOW(),
                updated_by = @DeletedBy
            WHERE id = @BranchId
              AND deleted_at IS NULL;
            """;

        const string deleteCourtsSql = """
            UPDATE core.courts
            SET
                is_active = FALSE,
                deleted_at = NOW(),
                deleted_by = @DeletedBy,
                updated_at = NOW(),
                updated_by = @DeletedBy
            WHERE branch_id = @BranchId
              AND deleted_at IS NULL;
            """;

        const string deactivateImagesSql = """
            UPDATE core.branch_images
            SET is_active = FALSE
            WHERE branch_id = @BranchId
              AND is_active = TRUE;
            """;

        const string deactivatePricingsSql = """
            UPDATE core.branch_pricings
            SET
                is_active = FALSE,
                updated_at = NOW()
            WHERE branch_id = @BranchId
              AND is_active = TRUE;
            """;

        var connection = transaction.Connection!;
        var parameters = new { BranchId = branchId, DeletedBy = deletedBy };

        var affectedBranches = await connection.ExecuteAsync(
            new CommandDefinition(deleteBranchSql, parameters, transaction, cancellationToken: cancellationToken));

        if (affectedBranches == 0)
        {
            return false;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(deleteCourtsSql, parameters, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(
            new CommandDefinition(deactivateImagesSql, parameters, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(
            new CommandDefinition(deactivatePricingsSql, parameters, transaction, cancellationToken: cancellationToken));

        return true;
    }
}