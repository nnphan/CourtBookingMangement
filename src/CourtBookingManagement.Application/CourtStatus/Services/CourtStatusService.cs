using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.CourtStatus.Services;

public sealed class CourtStatusService(
    ICourtStatusRepository repository,
    IValidator<CreateBookingRequest> createBookingValidator,
    IValidator<UpdateBookingRequest> updateBookingValidator,
    IValidator<CreateCourtBlockRequest> blockValidator,
    IValidator<CreateEventRequest> eventValidator) : ICourtStatusService
{
    public async Task<Result<IReadOnlyList<BranchDto>>> GetBranchesAsync(CancellationToken ct) => Result.Success<IReadOnlyList<BranchDto>>(await repository.GetBranchesAsync(ct));

    public async Task<Result<CourtStatusResponse>> GetBoardAsync(Guid branchId, DateOnly date, CancellationToken ct) =>
        await repository.GetBoardAsync(branchId, date, ct) is { } board ? board : CourtStatusErrors.NotFound("Branch", branchId);

    public async Task<Result<BookingDetailResponse>> GetBookingAsync(Guid id, CancellationToken ct) =>
        await repository.GetBookingAsync(id, ct) is { } booking ? booking : CourtStatusErrors.NotFound("Booking", id);

    public async Task<Result<BookingDetailResponse>> CreateBookingAsync(CreateBookingRequest request, CancellationToken ct)
    {
        var validation = await createBookingValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return CourtStatusErrors.Invalid(string.Join("; ", validation.Errors.Select(x => x.ErrorMessage)));
        if (!await repository.BranchExistsAsync(request.BranchId, ct)) return CourtStatusErrors.NotFound("Branch", request.BranchId);
        if (!await repository.CourtExistsAsync(request.BranchId, request.CourtId, ct)) return CourtStatusErrors.NotFound("Court", request.CourtId);
        if (!await repository.CustomerExistsAsync(request.CustomerId, ct)) return CourtStatusErrors.NotFound("Customer", request.CustomerId);
        if (!await repository.IsWithinOperatingHoursAsync(request.BranchId, request.BookingDate, request.StartTime, request.EndTime, ct)) return CourtStatusErrors.Invalid("Booking must be within branch operating hours.");
        if (await repository.HasOverlappingBookingAsync(request.CourtId, request.BookingDate, request.StartTime, request.EndTime, null, ct)) return CourtStatusErrors.Conflict("The court is not available for the requested time.");
        var id = await repository.CreateBookingAsync(request.BranchId, request.CourtId, request.CustomerId, request.BookingDate, request.StartTime, request.EndTime, request.BookingType, request.Note, ct);
        return await GetBookingAsync(id, ct);
    }

    public async Task<Result<BookingDetailResponse>> UpdateBookingAsync(Guid id, UpdateBookingRequest request, CancellationToken ct)
    {
        var validation = await updateBookingValidator.ValidateAsync(request, ct);
        if (!validation.IsValid) return CourtStatusErrors.Invalid(string.Join("; ", validation.Errors.Select(x => x.ErrorMessage)));
        var current = await repository.GetBookingAsync(id, ct);
        if (current is null) return CourtStatusErrors.NotFound("Booking", id);
        var branchId = await repository.GetBookingBranchIdAsync(id, ct);
        if (branchId is null || !await repository.CourtExistsAsync(branchId.Value, request.CourtId, ct)) return CourtStatusErrors.NotFound("Court", request.CourtId);
        if (await repository.HasOverlappingBookingAsync(request.CourtId, request.BookingDate, request.StartTime, request.EndTime, id, ct)) return CourtStatusErrors.Conflict("The court is not available for the requested time.");
        await repository.UpdateBookingAsync(id, request.CourtId, request.BookingDate, request.StartTime, request.EndTime, request.BookingType, request.Note, ct);
        return await GetBookingAsync(id, ct);
    }

    public async Task<Result> CancelBookingAsync(Guid id, CancellationToken ct)
    {
        var current = await repository.GetBookingAsync(id, ct);
        if (current is null) return Result.Failure(CourtStatusErrors.NotFound("Booking", id));
        if (current.BookingStatus.Equals("completed", StringComparison.OrdinalIgnoreCase)) return Result.Failure(CourtStatusErrors.Invalid("Completed bookings cannot be cancelled."));
        await repository.CancelBookingAsync(id, ct); return Result.Success();
    }
}