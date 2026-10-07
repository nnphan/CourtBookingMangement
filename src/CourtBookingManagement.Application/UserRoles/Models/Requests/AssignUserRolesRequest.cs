namespace CourtBookingManagement.Application.UserRoles.Models.Requests;

public sealed class AssignUserRolesRequest
{
    public List<Guid> RoleIds { get; set; } = [];
}
