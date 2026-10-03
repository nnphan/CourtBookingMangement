using CourtBookingManagement.Application.CourtBookings.DTOs;
using CourtBookingManagement.Application.CourtBookings.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.CourtBookings.Services;

public sealed class CourtBookingService(
    ICourtBookingRepository repository,
    IValidator<CreateCourtBookingRequest> createValidator,
    IValidator<CheckCourtAvailabilityRequest> availabilityValidator,
    IValidator<SearchCourtBookingsRequest> searchValidator,
    IValidator<CancelCourtBookingRequest> cancelValidator) : ICourtBookingService
{
    public async Task<Result<CreateCourtBookingResponse>> CreateAsync(CreateCourtBookingRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Invalid(validation);
        if (!await repository.BranchExistsAsync(request.BranchId, cancellationToken)) return Failure("CourtBooking.NotFound", "Branch not found");
        if (!await repository.CourtExistsAsync(request.BranchId, request.CourtId, cancellationToken)) return Failure("CourtBooking.NotFound", "Court not found");
        if (await repository.CourtIsBlockedAsync(request.CourtId, cancellationToken)) return Failure("CourtBooking.Blocked", "Court is blocked");
        if (!await repository.IsWithinOperatingHoursAsync(request.BranchId, request.BookingDate, request.StartTime, request.EndTime, cancellationToken)) return Failure("CourtBooking.Invalid", "Booking time is outside operating hours");
        if (!await repository.IsAvailableAsync(request.CourtId, request.BookingDate, request.StartTime, request.EndTime, cancellationToken)) return Failure("CourtBooking.Conflict", "Court is already booked in selected time range");

        var booking = await repository.CreateGuestBookingAsync(request, cancellationToken);
        return booking is null ? Failure("CourtBooking.Invalid", "Booking could not be created") : Result.Success(booking);
    }

    public async Task<Result<CheckCourtAvailabilityResponse>> CheckAvailabilityAsync(CheckCourtAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var validation = await availabilityValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Invalid<CheckCourtAvailabilityResponse>(validation);
        if (!await repository.CourtExistsAsync(request.CourtId, cancellationToken)) return Failure<CheckCourtAvailabilityResponse>("CourtBooking.NotFound", "Court not found");
        if (await repository.CourtIsBlockedAsync(request.CourtId, cancellationToken)) return Failure<CheckCourtAvailabilityResponse>("CourtBooking.Blocked", "Court is blocked");
        return Result.Success(new CheckCourtAvailabilityResponse(await repository.IsAvailableAsync(request.CourtId, request.BookingDate, request.StartTime, request.EndTime, cancellationToken)));
    }

    public async Task<Result<IReadOnlyList<CreateCourtBookingResponse>>> SearchAsync(SearchCourtBookingsRequest request, CancellationToken cancellationToken)
    {
        var validation = await searchValidator.ValidateAsync(request, cancellationToken);
        return !validation.IsValid ? Invalid<IReadOnlyList<CreateCourtBookingResponse>>(validation) : Result.Success(await repository.SearchAsync(request.PhoneNumber, cancellationToken));
    }

    public async Task<Result> CancelAsync(CancelCourtBookingRequest request, CancellationToken cancellationToken)
    {
        var validation = await cancelValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result.Failure(Invalid(validation));
        return await repository.CancelAsync(request.BookingCode, request.PhoneNumber, cancellationToken)
            ? Result.Success()
            : Result.Failure(new Error("CourtBooking.NotFound", "Booking not found"));
    }

    private static Error Invalid(FluentValidation.Results.ValidationResult validation) => new("Error.Validation", string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
    private static Result<T> Invalid<T>(FluentValidation.Results.ValidationResult validation) => Result.Failure<T>(Invalid(validation));
    private static Result<T> Failure<T>(string code, string message) => Result.Failure<T>(new Error(code, message));
    private static Result<CreateCourtBookingResponse> Failure(string code, string message) => Failure<CreateCourtBookingResponse>(code, message);
}