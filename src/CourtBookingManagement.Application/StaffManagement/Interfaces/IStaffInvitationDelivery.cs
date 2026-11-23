namespace CourtBookingManagement.Application.StaffManagement.Interfaces;

public interface IStaffInvitationDelivery
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(string email, string token, CancellationToken cancellationToken);
}
