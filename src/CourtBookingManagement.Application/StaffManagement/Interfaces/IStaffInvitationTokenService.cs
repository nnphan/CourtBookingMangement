namespace CourtBookingManagement.Application.StaffManagement.Interfaces;

public interface IStaffInvitationTokenService
{
    string GenerateToken();
    bool IsValidToken(string token);
    string HashToken(string token);
}
