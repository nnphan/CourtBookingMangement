using CourtBookingManagement.Application.CourtBookings.DTOs;

namespace CourtBookingManagement.Application.CourtBookings.Interfaces;

public interface ICourtBookingRepository
{
    Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken);
    Task<bool> CourtExistsAsync(Guid branchId, Guid courtId, CancellationToken cancellationToken);
    Task<bool> CourtExistsAsync(Guid courtId, CancellationToken cancellationToken);
    Task<bool> CourtIsBlockedAsync(Guid courtId, CancellationToken cancellationToken);
    Task<bool> IsWithinOperatingHoursAsync(Guid branchId, DateOnly date, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken);
    Task<bool> IsAvailableAsync(Guid courtId, DateOnly date, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken);
    Task<CreateCourtBookingResponse?> CreateGuestBookingAsync(CreateCourtBookingRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CreateCourtBookingResponse>> SearchAsync(string phoneNumber, CancellationToken cancellationToken);
    Task<bool> CancelAsync(string bookingCode, string phoneNumber, CancellationToken cancellationToken);
}