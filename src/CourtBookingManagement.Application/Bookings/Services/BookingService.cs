using System.Security.Cryptography;
using System.Text.Json;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Models;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Application.Bookings.Exceptions;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Bookings;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Bookings.Security;

namespace CourtBookingManagement.Application.Bookings.Services;

public sealed class BookingService(
    IBookingRepository repository,
    IBookingPricingService bookingPricingService,
    IValidator<CreateBookingRequest> validator,
    IValidator<CalculateBookingPriceRequest> calculatePriceValidator,
    ICurrentUserService currentUserService,
    IBookingSlotLockService slotLockService,
    IBookingHoldCache holdCache,
    IOptions<BookingHoldOptions> holdOptions,
    ILogger<BookingService> logger) : IBookingService
{
    public async Task<Result<BookingDetailResponse>> GetByIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var booking = await repository.GetBookingDetailAsync(bookingId, cancellationToken);
        return booking is null
            ? Result.Failure<BookingDetailResponse>(new Error("Booking.NotFound", "Booking not found."))
            : Result.Success(booking);
    }

    public async Task<Result<BookingDetailResponse>> GetHoldAsync(
        Guid bookingId,
        string? holdToken,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        var holdTokenHash = BookingHoldToken.Hash(holdToken);
        if (customerId is null && holdTokenHash is null)
        {
            return Result.Failure<BookingDetailResponse>(
                new Error("Booking.NotFound", "Booking hold not found."));
        }

        var booking = await repository.GetBookingHoldAsync(
            bookingId,
            customerId,
            holdTokenHash,
            cancellationToken);
        return booking is null
            ? Result.Failure<BookingDetailResponse>(
                new Error("Booking.NotFound", "Booking hold not found."))
            : Result.Success(booking);
    }

    public async Task<Result<CancelBookingHoldResponse>> CancelHoldAsync(
        Guid bookingId,
        string? holdToken,
        CancellationToken cancellationToken)
    {
        var customerId = await GetCurrentCustomerIdAsync(cancellationToken);
        var holdTokenHash = BookingHoldToken.Hash(holdToken);
        if (customerId is null && holdTokenHash is null)
        {
            return Result.Failure<CancelBookingHoldResponse>(
                new Error("Booking.NotFound", "Booking hold not found."));
        }

        var cancelled = await repository.CancelBookingHoldAsync(
            bookingId,
            customerId,
            holdTokenHash,
            cancellationToken);
        if (cancelled is null)
        {
            return Result.Failure<CancelBookingHoldResponse>(
                new Error("Booking.StatusConflict", "The booking hold is no longer active or cannot be accessed."));
        }

        try
        {
            await holdCache.RemoveAsync(bookingId, cancelled.Resources, cancellationToken);
        }
        catch (BookingHoldCacheUnavailableException exception)
        {
            logger.LogWarning(
                exception,
                "Cancelled booking {BookingId} is committed in PostgreSQL but its Redis hold keys could not be removed.",
                bookingId);
        }

        return Result.Success(new CancelBookingHoldResponse(
            bookingId,
            BookingStatuses.ToApiValue(BookingStatuses.Cancelled)));
    }

    public async Task<Result<UpdateBookingStatusResponse>> UpdateStatusAsync(
        Guid bookingId,
        UpdateBookingStatusRequest request,
        CancellationToken cancellationToken)
    {
        var requestedStatus = BookingStatuses.Normalize(request.Status);
        if (!BookingStatuses.IsSupported(requestedStatus))
        {
            return Result.Failure<UpdateBookingStatusResponse>(
                new Error("Error.Validation", "The requested booking status is invalid."));
        }

        var current = await repository.GetBookingStatusAsync(bookingId, cancellationToken);
        if (current is null)
        {
            return Result.Failure<UpdateBookingStatusResponse>(
                new Error("Booking.NotFound", "Booking not found."));
        }

        var currentStatus = BookingStatuses.Normalize(current.Status);
        if (currentStatus == requestedStatus)
        {
            return Result.Success(new UpdateBookingStatusResponse
            {
                BookingId = current.BookingId,
                BookingCode = current.BookingCode,
                Status = BookingStatuses.ToApiValue(currentStatus),
                UpdatedAt = current.UpdatedAt
            });
        }

        if (!BookingStatusTransitions.CanTransition(currentStatus, requestedStatus))
        {
            return Result.Failure<UpdateBookingStatusResponse>(
                new Error("Booking.StatusConflict", $"Booking status cannot transition from {BookingStatuses.ToApiValue(currentStatus)} to {BookingStatuses.ToApiValue(requestedStatus)}."));
        }

        var updatedBy = currentUserService.IsAuthenticated ? currentUserService.UserId : null;
        var updated = await repository.UpdateBookingStatusAsync(
            bookingId,
            currentStatus,
            requestedStatus,
            updatedBy,
            cancellationToken);

        if (updated is null)
        {
            logger.LogWarning(
                "Booking status update lost an optimistic concurrency race. BookingId: {BookingId}, CurrentStatus: {CurrentStatus}, RequestedStatus: {RequestedStatus}, UpdatedBy: {UpdatedBy}",
                bookingId,
                currentStatus,
                requestedStatus,
                updatedBy);
            return Result.Failure<UpdateBookingStatusResponse>(
                new Error("Booking.StatusConflict", "The booking status was changed by another request. Refresh the booking and try again."));
        }

        logger.LogInformation(
            "Booking status updated. BookingId: {BookingId}, BookingCode: {BookingCode}, PreviousStatus: {PreviousStatus}, NewStatus: {NewStatus}, UpdatedBy: {UpdatedBy}",
            updated.BookingId,
            updated.BookingCode,
            currentStatus,
            updated.Status,
            updatedBy);

        return Result.Success(new UpdateBookingStatusResponse
            {
                BookingId = updated.BookingId,
                BookingCode = updated.BookingCode,
                Status = BookingStatuses.ToApiValue(updated.Status),
                UpdatedAt = updated.UpdatedAt
            });
    }

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

    public async Task<Result<CreateBookingResponse>> CreateAsync(
        CreateBookingRequest request,
        Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CreateBookingResponse>(new Error(
                "CourtStatus.Invalid",
                string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))));
        }

        // A logged-in user books under their own customer profile; guests are matched/created by phone number.
        Guid? knownCustomerId = null;
        if (currentUserService.UserId is Guid userId && userId != Guid.Empty)
        {
            knownCustomerId = await repository.GetCustomerIdByUserIdAsync(userId, cancellationToken);
        }

        if (knownCustomerId is null
            && (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.PhoneNumber)))
        {
            return Result.Failure<CreateBookingResponse>(new Error(
                "CourtStatus.Invalid",
                "Customer name and phone number are required for guest bookings."));
        }

        if (!await repository.ExistsBranchAsync(request.BranchId, cancellationToken))
        {
            return Result.Failure<CreateBookingResponse>(new Error("CourtStatus.NotFound", "Branch not found."));
        }

        var slots = request.Details
            .Select(detail => (detail.CourtId, detail.BookingDate, Start: TimeOnly.Parse(detail.StartTime), End: TimeOnly.Parse(detail.EndTime)))
            .ToArray();

        for (var i = 0; i < slots.Length; i++)
        {
            for (var j = i + 1; j < slots.Length; j++)
            {
                if (slots[i].CourtId == slots[j].CourtId
                    && slots[i].BookingDate == slots[j].BookingDate
                    && slots[i].Start < slots[j].End
                    && slots[i].End > slots[j].Start)
                {
                    return Result.Failure<CreateBookingResponse>(new Error("CourtStatus.Invalid", "Booking details contain overlapping time slots for the same court."));
                }
            }
        }

        foreach (var courtId in slots.Select(slot => slot.CourtId).Distinct())
        {
            if (!await repository.ExistsCourtAsync(courtId, cancellationToken)
                || !await repository.CourtBelongsToBranchAsync(courtId, request.BranchId, cancellationToken))
            {
                return Result.Failure<CreateBookingResponse>(new Error("CourtStatus.NotFound", "Court not found in the specified branch."));
            }
        }

        var resources = slots
            .Select(slot => new BookingSlotResource(slot.CourtId, slot.BookingDate, slot.Start, slot.End))
            .ToArray();
        var idempotencyRequestHash = idempotencyKey is null
            ? null
            : SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                Request = request,
                CustomerId = knownCustomerId
            }));
        var requiresOnlinePayment = string.Equals(
            request.PaymentMethod,
            PaymentMethod.Momo.ToString(),
            StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                request.PaymentMethod,
                PaymentMethod.Sepay.ToString(),
                StringComparison.OrdinalIgnoreCase);
        var holdDurationSeconds = requiresOnlinePayment
            ? holdOptions.Value.DurationSeconds
            : (int?)null;
        var (holdToken, holdTokenHash) = holdDurationSeconds is null
            ? (null, null)
            : BookingHoldToken.Create();

        if (idempotencyKey is Guid idempotencyKeyValue && idempotencyRequestHash is not null)
        {
            var existing = await repository.GetIdempotentBookingAsync(
                idempotencyKeyValue,
                idempotencyRequestHash,
                holdTokenHash,
                cancellationToken);
            if (existing?.KeyReused == true)
            {
                return Result.Failure<CreateBookingResponse>(new Error(
                    "Booking.IdempotencyKeyReused",
                    "The idempotency key was already used with a different request."));
            }

            if (existing?.Booking is CreateBookingResponse existingBooking)
            {
                existingBooking.HoldToken = existingBooking.ExpiresAt > existingBooking.ServerTime
                    ? holdToken
                    : null;
                return Result.Success(existingBooking);
            }
        }

        IBookingSlotLockHandle? lockHandle;
        try
        {
            lockHandle = await slotLockService.AcquireAsync(resources, cancellationToken);
        }
        catch (BookingLockServiceUnavailableException)
        {
            return Result.Failure<CreateBookingResponse>(new Error(
                "Booking.LockServiceUnavailable",
                "The booking lock service is temporarily unavailable."));
        }

        if (lockHandle is null)
        {
            if (idempotencyKey is Guid retryKey && idempotencyRequestHash is not null)
            {
                var existing = await repository.GetIdempotentBookingAsync(
                    retryKey,
                    idempotencyRequestHash,
                    holdTokenHash,
                    cancellationToken);
                if (existing?.KeyReused == true)
                {
                    return Result.Failure<CreateBookingResponse>(new Error(
                        "Booking.IdempotencyKeyReused",
                        "The idempotency key was already used with a different request."));
                }

                if (existing?.Booking is CreateBookingResponse existingBooking)
                {
                    existingBooking.HoldToken = existingBooking.ExpiresAt > existingBooking.ServerTime
                        ? holdToken
                        : null;
                    return Result.Success(existingBooking);
                }
            }

            return Result.Failure<CreateBookingResponse>(new Error(
                "Booking.SlotLocked",
                "The requested court slot is being reserved by another customer."));
        }

        await using (lockHandle)
        {
            var detailPrices = new List<decimal>(slots.Length);
            foreach (var slot in slots)
            {
                if (await repository.HasBookingOverlapAsync(slot.CourtId, slot.BookingDate, slot.Start, slot.End, cancellationToken))
                {
                    return Result.Failure<CreateBookingResponse>(new Error(
                        "Booking.SlotLocked",
                        "The court is not available for the requested time."));
                }

                try
                {
                    detailPrices.Add(await bookingPricingService.CalculatePriceAsync(
                        request.BranchId,
                        slot.BookingDate,
                        slot.Start,
                        slot.End,
                        cancellationToken));
                }
                catch (BookingPricingException exception)
                {
                    return Result.Failure<CreateBookingResponse>(
                        new Error("Booking.PricingConfiguration", exception.Message));
                }
            }

            CreateBookingResponse? booking;
            try
            {
                booking = await repository.CreateBookingAsync(
                    request,
                    detailPrices,
                    knownCustomerId, 
                    holdDurationSeconds,
                    holdTokenHash,
                    idempotencyKey,
                    idempotencyRequestHash,
                    cancellationToken);
            }
            catch (BookingIdempotencyKeyReusedException)
            {
                return Result.Failure<CreateBookingResponse>(new Error(
                    "Booking.IdempotencyKeyReused",
                    "The idempotency key was already used with a different request."));
            }
            if (booking is null)
            {
                return Result.Failure<CreateBookingResponse>(new Error(
                    "Booking.SlotLocked",
                    "The court is not available for the requested time."));
            }

            if (booking.ExpiresAt is DateTimeOffset expiresAt)
            {
                booking.HoldToken = holdToken;
                var timeToLive = expiresAt - booking.ServerTime;
                if (timeToLive > TimeSpan.Zero)
                {
                    try
                    {
                        await holdCache.SetAsync(
                            new BookingHoldCacheEntry(
                                booking.BookingId,
                                booking.BookingCode,
                                booking.Status,
                                expiresAt,
                                resources.Select(resource => resource.CourtId).Distinct().ToArray()),
                            resources,
                            timeToLive,
                            cancellationToken);
                    }
                    catch (BookingHoldCacheUnavailableException exception)
                    {
                        logger.LogWarning(
                            exception,
                            "Database booking hold {BookingId} is valid but its Redis cache entry could not be written.",
                            booking.BookingId);
                    }
                }
            }

            return Result.Success(booking);
        }
    }

    private async Task<Guid?> GetCurrentCustomerIdAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId || userId == Guid.Empty)
        {
            return null;
        }

        return await repository.GetCustomerIdByUserIdAsync(userId, cancellationToken);
    }

}