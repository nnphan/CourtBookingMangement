using System.Text.RegularExpressions;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Application.Bookings.Services;

public sealed class BookingService(
    IBookingRepository repository,
    IBookingPricingService bookingPricingService,
    IValidator<CreateBookingRequest> validator,
    IValidator<CalculateBookingPriceRequest> calculatePriceValidator,
    ICurrentUserService currentUserService,
    ILogger<BookingService> logger) : IBookingService
{
    private const string BookingConflictCode = "BOOKING_CONFLICT";
    private static readonly Regex GuestPhoneNumberPattern = new("^0[0-9]{9,10}$", RegexOptions.Compiled);

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
        catch (BookingPricingException)
        {
            return Result.Failure<CalculateBookingPriceResponse>(
                new Error("BOOKING_PRICING_NOT_FOUND", "Pricing is not configured for the requested period."));
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

    public async Task<Result<BookingResponse>> CreateAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailure(validation);
        }

        if (currentUserService.UserId is not Guid userId || userId == Guid.Empty)
        {
            return Result.Failure<BookingResponse>(
                new Error("Auth.InvalidCredentials", "The authenticated user could not be identified."));
        }

        var customerId = await repository.GetCustomerIdByUserIdAsync(userId, cancellationToken);
        if (customerId is null)
        {
            return Result.Failure<BookingResponse>(
                new Error("Customer.NotFound", "An active customer profile is required to create a booking."));
        }

        return await CreateAggregateAsync(request, customerId, null, null, userId, true, cancellationToken);
    }

    public async Task<Result<BookingResponse>> CreateForStaffAsync(
        CreateStaffBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid staffUserId || staffUserId == Guid.Empty)
        {
            return Result.Failure<BookingResponse>(
                new Error("Auth.InvalidCredentials", "The authenticated staff user could not be identified."));
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailure(validation);
        }

        if (request.CustomerId is Guid suppliedCustomerId)
        {
            if (suppliedCustomerId == Guid.Empty)
            {
                return Result.Failure<BookingResponse>(new Error("Error.Validation", "CustomerId must not be empty."));
            }

            if (!await repository.CustomerExistsAsync(suppliedCustomerId, cancellationToken))
            {
                return Result.Failure<BookingResponse>(new Error("Customer.NotFound", "Customer not found."));
            }

            if (!string.IsNullOrWhiteSpace(request.CustomerName) || !string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return Result.Failure<BookingResponse>(new Error(
                    "Error.Validation",
                    "Provide either customerId or guest contact details, not both."));
            }

            return await CreateAggregateAsync(request, suppliedCustomerId, null, null, staffUserId, false, cancellationToken);
        }

        var guestName = request.CustomerName?.Trim();
        var guestPhoneNumber = request.PhoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(guestName) || guestName.Length > 150
            || string.IsNullOrWhiteSpace(guestPhoneNumber) || !GuestPhoneNumberPattern.IsMatch(guestPhoneNumber))
        {
            return Result.Failure<BookingResponse>(new Error(
                "Error.Validation",
                "Provide an active customerId or a guest name and valid Vietnam phone number."));
        }

        return await CreateAggregateAsync(request, null, guestName, guestPhoneNumber, staffUserId, false, cancellationToken);
    }

    private async Task<Result<BookingResponse>> CreateAggregateAsync(
        CreateBookingRequest request,
        Guid? customerId,
        string? guestName,
        string? guestPhoneNumber,
        Guid? actorId,
        bool requireCustomerOwnership,
        CancellationToken cancellationToken)
    {
        var branch = await repository.GetBranchBookingDataAsync(request.BranchId, cancellationToken);
        if (branch is null)
        {
            return Result.Failure<BookingResponse>(new Error("BOOKING_BRANCH_NOT_FOUND", "Branch not found."));
        }

        if (!branch.IsActive || branch.IsDeleted)
        {
            return Result.Failure<BookingResponse>(new Error("BOOKING_BRANCH_INACTIVE", "The selected branch is not available for booking."));
        }

        var details = request.Details!.ToArray();
        if (details.Any(detail => detail.BookingDate < branch.BusinessDate))
        {
            return Result.Failure<BookingResponse>(
                new Error("Error.Validation", "Booking dates must be today or a future date in the branch timezone."));
        }

        if (details.Any(detail => !branch.OperatingHours.Any(hours =>
                !hours.IsClosed
                && detail.StartTime >= hours.OpenTime
                && detail.EndTime <= hours.CloseTime)))
        {
            return Result.Failure<BookingResponse>(new Error(
                "BOOKING_OUTSIDE_OPERATING_HOURS",
                "One or more requested periods fall outside the branch operating hours."));
        }

        var distinctCourtIds = details.Select(detail => detail.CourtId).Distinct().ToArray();
        var courts = await repository.GetCourtsAsync(distinctCourtIds, cancellationToken);
        if (distinctCourtIds.Except(courts.Select(court => court.Id)).Any())
        {
            return Result.Failure<BookingResponse>(new Error("BOOKING_COURT_NOT_FOUND", "One or more courts were not found."));
        }

        if (courts.Any(court => court.BranchId != request.BranchId))
        {
            return Result.Failure<BookingResponse>(new Error("BOOKING_COURT_WRONG_BRANCH", "Every court must belong to the selected branch."));
        }

        if (courts.Any(court => !court.IsActive || court.IsDeleted
            || !string.Equals(court.Status, "active", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<BookingResponse>(new Error("BOOKING_COURT_INACTIVE", "One or more courts are not available for booking."));
        }

        if (await repository.HasBookingConflictsAsync(details, cancellationToken))
        {
            return BookingConflict();
        }

        var bookingType = BookingTypes.All.Single(value =>
            string.Equals(value, request.BookingType, StringComparison.OrdinalIgnoreCase));
        var paymentMethod = PaymentMethods.All.Single(value =>
            string.Equals(value, request.PaymentMethod, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<decimal> prices;
        try
        {
            prices = await bookingPricingService.CalculatePricesAsync(
                request.BranchId,
                details.Select(detail => new BookingPricingRequestDetail(detail.BookingDate, detail.StartTime, detail.EndTime)).ToArray(),
                cancellationToken);
        }
        catch (BookingPricingException)
        {
            return Result.Failure<BookingResponse>(
                new Error("BOOKING_PRICING_NOT_FOUND", "Pricing is not configured for one or more requested periods."));
        }

        var subtotal = prices.Sum();
        try
        {
            var booking = await repository.CreateBookingAsync(
                request,
                customerId,
                guestName,
                guestPhoneNumber,
                actorId,
                requireCustomerOwnership,
                bookingType,
                paymentMethod,
                prices,
                cancellationToken);
            if (booking is null)
            {
                return BookingConflict();
            }

            logger.LogInformation(
                "Booking aggregate created. BookingId: {BookingId}, BookingCode: {BookingCode}, CustomerId: {CustomerId}, BranchId: {BranchId}, CurrentUserId: {CurrentUserId}, RequestedDetailCount: {RequestedDetailCount}, DistinctCourtCount: {DistinctCourtCount}, BookingType: {BookingType}, PaymentMethod: {PaymentMethod}, SubtotalAmount: {SubtotalAmount}",
                booking.Id,
                booking.BookingCode,
                booking.CustomerId,
                booking.BranchId,
                actorId,
                details.Length,
                distinctCourtIds.Length,
                bookingType,
                paymentMethod,
                subtotal);
            return Result.Success(booking);
        }
        catch (BookingConflictException)
        {
            logger.LogWarning(
                "Booking aggregate conflicted during persistence. BranchId: {BranchId}, RequestedDetailCount: {RequestedDetailCount}, DistinctCourtCount: {DistinctCourtCount}",
                request.BranchId,
                details.Length,
                distinctCourtIds.Length);
            return BookingConflict();
        }
    }

    private static Result<BookingResponse> ValidationFailure(FluentValidation.Results.ValidationResult validation)
    {
        var errorCode = validation.Errors
            .Select(error => error.ErrorCode)
            .FirstOrDefault(code => code.StartsWith("BOOKING_", StringComparison.Ordinal))
            ?? "Error.Validation";
        return Result.Failure<BookingResponse>(new Error(
            errorCode,
            string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
    }

    private static Result<BookingResponse> BookingConflict() =>
        Result.Failure<BookingResponse>(new Error(
            BookingConflictCode,
            "One or more selected courts are no longer available for the requested time."));
}
