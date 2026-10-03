using CourtBookingManagement.Application.CourtStatus.DTOs;

namespace CourtBookingManagement.Application.CourtStatus.Interfaces;

public interface ICourtStatusRepository
{
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken);
    Task<CourtStatusResponse?> GetBoardAsync(Guid branchId, DateOnly date, CancellationToken cancellationToken);
    Task<BookingDetailResponse?> GetBookingAsync(Guid id, CancellationToken cancellationToken);
    Task<Guid?> GetBookingBranchIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> BranchExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CourtExistsAsync(Guid branchId, Guid courtId, CancellationToken cancellationToken);
    Task<bool> CustomerExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> HasOverlappingBookingAsync(Guid courtId, DateOnly date, TimeOnly start, TimeOnly end, Guid? excludingBookingId, CancellationToken cancellationToken);
    Task<bool> IsWithinOperatingHoursAsync(Guid branchId, DateOnly date, TimeOnly start, TimeOnly end, CancellationToken cancellationToken);
    Task<Guid> CreateBookingAsync(Guid branchId, Guid courtId, Guid customerId, DateOnly date, TimeOnly start, TimeOnly end, string type, string? note, CancellationToken cancellationToken);
    Task UpdateBookingAsync(Guid id, Guid courtId, DateOnly date, TimeOnly start, TimeOnly end, string type, string? note, CancellationToken cancellationToken);
    Task CancelBookingAsync(Guid id, CancellationToken cancellationToken);
}