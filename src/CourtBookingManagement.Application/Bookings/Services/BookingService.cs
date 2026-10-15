using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Bookings.Services;

public sealed class BookingService(
    IBookingRepository repository,
    IBookingPricingService bookingPricingService,
    IValidator<CreateBookingRequest> validator,
    IValidator<CalculateBookingPriceRequest> calculatePriceValidator,
    ICurrentUserService currentUserService) : IBookingService
{
    public async Task<Result<IReadOnlyList<MyBookingResponse>>> GetMyBookingsAsync(
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<MyBookingResponse>>(
                new Error("Auth.InvalidCredentials", "The authenticated user could not be identified."));
        }

        var customerId = await repository.GetCustomerIdByUserIdAsync(userId, cancellationToken);
        if (customerId is null)
        {
            return Result.Success<IReadOnlyList<MyBookingResponse>>(Array.Empty<MyBookingResponse>());
        }

        var bookings = await repository.GetMyBookingsAsync(customerId.Value, cancellationToken);
        return Result.Success(bookings);
    }

    public async Task<Result<CalculateBookingPriceResponse>> CalculatePriceAsync(
        CalculateBookingPriceRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await calculatePriceValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CalculateBookingPriceResponse>(new Error(
                "Error.Validation",
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        if (!await repository.ExistsBranchAsync(request.BranchId, cancellationToken))
        {
            return Result.Failure<CalculateBookingPriceResponse>(new Error("Branch.NotFound", "Branch not found."));
        }

        var durationHours = (decimal)(request.EndTime - request.StartTime).TotalHours;
        decimal totalAmount;
        try
        {
            totalAmount = await bookingPricingService.CalculatePriceAsync(
                request.BranchId,
                request.BookingDate,
                request.StartTime,
                request.EndTime,
                cancellationToken);
        }
        catch (BookingPricingException exception)
        {
            return Result.Failure<CalculateBookingPriceResponse>(new Error("Booking.PricingConfiguration", exception.Message));
        }

        return Result.Success(new CalculateBookingPriceResponse
        {
            BranchId = request.BranchId,
            BookingDate = request.BookingDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            DurationHours = durationHours,
            SubtotalAmount = totalAmount,
            PaidAmount = 0m,
            TotalAmount = totalAmount
        });
    }

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

        decimal calculatedPrice;
        try
        {
            calculatedPrice = await bookingPricingService.CalculatePriceAsync(
                request.BranchId,
                request.BookingDate,
                TimeOnly.Parse(request.StartTime),
                TimeOnly.Parse(request.EndTime),
                cancellationToken);
        }
        catch (BookingPricingException exception)
        {
            return Result.Failure<BookingDetailResponse>(new Error("Booking.PricingConfiguration", exception.Message));
        }

        var booking = await repository.CreateBookingAsync(request, calculatedPrice, cancellationToken);
        return booking is null
            ? Result.Failure<BookingDetailResponse>(new Error("CourtStatus.Conflict", "The court is not available for the requested time."))
            : Result.Success(booking);
    }
}