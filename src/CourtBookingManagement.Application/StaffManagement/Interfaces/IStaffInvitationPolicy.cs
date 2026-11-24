namespace CourtBookingManagement.Application.StaffManagement.Interfaces;

public interface IStaffInvitationPolicy
{
    int ExpirationHours { get; }
    int ResendCooldownMinutes { get; }
    int MaxResendsPerWindow { get; }
    int ResendWindowMinutes { get; }
}
