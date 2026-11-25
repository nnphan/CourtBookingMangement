using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Models;

namespace CourtBookingManagement.Application.Bookings.Interfaces;

public interface IBookingRepository
{
    Task<IReadOnlyList<BookingForPlayerMatchDto>> GetPlayerMatchBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BranchPricingDto>> GetActivePricingsAsync(Guid branchId, CancellationToken cancellationToken);
    Task<bool> ExistsBranchAsync(Guid branchId, CancellationToken cancellationToken);
    Task<bool> ExistsCourtAsync(Guid courtId, CancellationToken cancellationToken);
    Task<bool> CourtBelongsToBranchAsync(Guid courtId, Guid branchId, CancellationToken cancellationToken);
    Task<bool> HasBookingOverlapAsync(Guid courtId, DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken);
    Task<CreateBookingResponse?> CreateBookingAsync(
        CreateBookingRequest request,
        IReadOnlyList<decimal> detailPrices,
        Guid? knownCustomerId,
        int? holdDurationSeconds,
        byte[]? holdTokenHash,
        Guid? idempotencyKey,
        byte[]? idempotencyRequestHash,
        Guid? selectionSubjectId,
        CancellationToken cancellationToken);
    Task<IdempotentBookingLookup?> GetIdempotentBookingAsync(
        Guid idempotencyKey,
        byte[] requestHash,
        byte[]? replacementHoldTokenHash,
        CancellationToken cancellationToken);
    Task<BookingDetailResponse?> GetBookingHoldAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken);
    Task<ExpiredBookingHold?> CancelBookingHoldAsync(
        Guid bookingId,
        Guid? customerId,
        byte[]? holdTokenHash,
        CancellationToken cancellationToken);
    Task<BookingDetailResponse?> GetBookingDetailAsync(Guid bookingId, CancellationToken cancellationToken);
    Task<BookingStatusProjection?> GetBookingStatusAsync(Guid bookingId, CancellationToken cancellationToken);
    Task<UpdateBookingStatusResponse?> UpdateBookingStatusAsync(
        Guid bookingId,
        string currentStatus,
        string newStatus,
        Guid? updatedBy,
        CancellationToken cancellationToken);
    Task<Guid?> GetCustomerIdByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MyBookingResponse>> GetMyBookingsAsync(Guid customerId, CancellationToken cancellationToken);
}

public sealed class BookingStatusProjection
{
    public Guid BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}
