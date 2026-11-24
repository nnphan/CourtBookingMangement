using CourtBookingManagement.Application.StaffManagement.Models;

namespace CourtBookingManagement.Application.StaffManagement.Interfaces;

public interface IStaffInvitationRepository
{
    Task<StaffInvitationState?> GetStateByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);
}
