using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Bookings.Services;

public sealed class BookingService(
    IBookingRepository repository,
    IValidator<CreateBookingRequest> validator) : IBookingService
{
    public async Task<Result<BookingDetailResponse>> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<BookingDetailResponse>(new Error(
                "CourtStatus.Invalid",
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        if (!await repository.ExistsBranchAsync(request.BranchId, cancellationToken))
        {
            return Result.Failure<BookingDetailResponse>(new Error("CourtStatus.NotFound", "Branch not found."));
        }

        if (!await repository.ExistsCourtAsync(request.CourtId, cancellationToken)
            || !await repository.CourtBelongsToBranchAsync(request.CourtId, request.BranchId, cancellationToken))
        {
            return Result.Failure<BookingDetailResponse>(new Error("CourtStatus.NotFound", "Court not found in the specified branch."));
        }

        if (await repository.HasBookingOverlapAsync(
                request.CourtId,
                request.BookingDate,
                TimeOnly.Parse(request.StartTime),
                TimeOnly.Parse(request.EndTime),
                cancellationToken))
        {
            return Result.Failure<BookingDetailResponse>(new Error("CourtStatus.Conflict", "The court is not available for the requested time."));
        }

        var booking = await repository.CreateBookingAsync(request, cancellationToken);
        return booking is null
            ? Result.Failure<BookingDetailResponse>(new Error("CourtStatus.Conflict", "The court is not available for the requested time."))
            : Result.Success(booking);
    }
}