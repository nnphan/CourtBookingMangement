using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.DTOs.Admin;
using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.DTOs.Responses;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Branches.Services;

public sealed class BranchService(
    IBranchRepository branchRepository,
    IBranchQueryRepository branchQueryRepository,
    IAmenityRepository amenityRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateBranchRequest> validator,
    ISqlConnectionFactory sqlConnectionFactory) : IBranchService
{
    private static readonly Error BranchNotFound = new("Branch.NotFound", "Branch not found.");

    private static readonly Error BranchAlreadyDeleted = new("Branch.AlreadyDeleted", "Branch has already been deleted.");

    public Task<BranchAdminSearchResult> SearchAsync(
        BranchAdminSearchRequest request,
        CancellationToken cancellationToken) =>
        branchQueryRepository.SearchBranchesAsync(request with
        {
            Keyword = request.Keyword?.Trim(),
            City = request.City?.Trim(),
            District = request.District?.Trim()
        }, cancellationToken);

    public async Task<CreateBranchResponse> CreateAsync(
        CreateBranchRequest request,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var resolvedOwnerId = await branchRepository.GetOwnerIdForUserAsync(ownerId, cancellationToken)
            ?? throw new BranchOwnerNotFoundException();

        if (await branchRepository.ExistsAsync(resolvedOwnerId, request.Name, cancellationToken))
        {
            throw new BranchAlreadyExistsException();
        }

        var requestedAmenityIds = request.AmenityIds.Distinct().ToArray();
        var existingAmenityIds = await amenityRepository.GetExistingIdsAsync(requestedAmenityIds, cancellationToken);
        if (existingAmenityIds.Count != requestedAmenityIds.Length)
        {
            throw new InvalidBranchAmenitiesException();
        }

        var operatingHours = request.OperatingHours.Count == 0
            ? [new CreateOperatingHourRequest
            {
                OpenTime = TimeSpan.FromHours(6),
                CloseTime = TimeSpan.FromHours(22),
                IsClosed = false
            }]
            : request.OperatingHours;

        var branchId = Guid.Empty;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            branchId = Guid.NewGuid();
            branchId = await branchRepository.AddAsync(new CreateBranchData(
                branchId,
                resolvedOwnerId,
                ownerId,
                request.Name,
                request.Description,
                request.Address,
                request.City,
                request.District,
                request.Latitude,
                request.Longitude,
                request.PhoneNumber,
                request.TimeZone,
                request.SupportsInstantBooking,
                requestedAmenityIds,
                request.Images,
                operatingHours,
                request.Courts,
                request.BranchPricings), token);
            await branchRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return new CreateBranchResponse
        {
            Id = branchId
        };
    }

    public async Task<Result> DeleteAsync(
        Guid branchId,
        Guid deletedBy,
        CancellationToken cancellationToken)
    {
        if (branchId == Guid.Empty)
        {
            return Result.Failure(new Error("Branch.InvalidId", "A valid branch id is required."));
        }

        if (deletedBy == Guid.Empty)
        {
            return Result.Failure(new Error("Branch.InvalidUser", "The current user is invalid."));
        }

        var isDeleted = await branchRepository.IsDeletedAsync(branchId, cancellationToken);
        if (isDeleted is null)
        {
            return Result.Failure(BranchNotFound);
        }

        if (isDeleted.Value)
        {
            return Result.Failure(BranchAlreadyDeleted);
        }

        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            if (!await branchRepository.SoftDeleteAsync(branchId, deletedBy, transaction, cancellationToken))
            {
                transaction.Rollback();
                return Result.Failure(BranchAlreadyDeleted);
            }

            transaction.Commit();
            return Result.Success();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}