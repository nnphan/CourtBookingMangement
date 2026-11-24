using CourtBookingManagement.Application.StaffManagement.Interfaces;
using Microsoft.Extensions.Options;

namespace CourtBookingManagement.Infrastructure.Email;

public sealed class StaffInvitationPolicy(IOptions<StaffInvitationOptions> options)
    : IStaffInvitationPolicy
{
    public int ExpirationHours => options.Value.ExpirationHours;
    public int ResendCooldownMinutes => options.Value.ResendCooldownMinutes;
    public int MaxResendsPerWindow => options.Value.MaxResendsPerWindow;
    public int ResendWindowMinutes => options.Value.ResendWindowMinutes;
}
