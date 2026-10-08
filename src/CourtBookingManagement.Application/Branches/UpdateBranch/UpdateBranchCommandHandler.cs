using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.DTOs.Responses;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Application.Abstractions.Messaging;

namespace CourtBookingManagement.Application.Branches.UpdateBranch;

public sealed class UpdateBranchCommandHandler(
    IBranchRepository branchRepository,
    IAmenityRepository amenityRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateBranchCommand, CreateBranchResponse>
{
    public async Task<Result<CreateBranchResponse>> Handle(
        UpdateBranchCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var requestedAmenityIds = request.AmenityIds.Distinct().ToArray();
        var data = new UpdateBranchData(
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
            request.OperatingHours,
            request.Courts,
            request.BranchPricings);

        var branchExists = false;
        var invalidAmenities = false;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await branchRepository.ExistsByIdAsync(command.Id, token))
            {
                return;
            }

            var existingAmenityIds = await amenityRepository.GetExistingIdsAsync(requestedAmenityIds, token);
            if (existingAmenityIds.Count != requestedAmenityIds.Length)
            {
                invalidAmenities = true;
                return;
            }

            branchExists = await branchRepository.UpdateAsync(
                command.Id,
                data,
                command.UpdatedByUserId,
                token);
        }, cancellationToken);

        if (!branchExists)
        {
            return Result.Failure<CreateBranchResponse>(new Error("Branch.NotFound", "Branch not found"));
        }

        return invalidAmenities
            ? Result.Failure<CreateBranchResponse>(
                new Error("Branch.InvalidAmenities", "One or more amenities do not exist."))
            : Result.Success(new CreateBranchResponse { Id = command.Id });
    }
}