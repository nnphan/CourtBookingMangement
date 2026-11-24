using CourtBookingManagement.Application.Email;
using CourtBookingManagement.Application.StaffManagement.Models;

namespace CourtBookingManagement.Application.StaffManagement.Interfaces;

public interface IStaffInvitationEmailComposer
{
    EmailMessage Compose(StaffInvitationEmailData invitation);
}
