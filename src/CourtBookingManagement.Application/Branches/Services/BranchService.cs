using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.DTOs.Responses;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Branches.Services;

public sealed class BranchService(
    IBranchRepository branchRepository,
    IAmenityRepository amenityRepository,
    IUnitOfWork unitOfWork,
    IValidator<CreateBranchRequest> validator) : IBranchService
{
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
                operatingHours), token);
            await branchRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return new CreateBranchResponse
        {
            Id = branchId,
            Name = request.Name,
            IsActive = true
        };
    }
}