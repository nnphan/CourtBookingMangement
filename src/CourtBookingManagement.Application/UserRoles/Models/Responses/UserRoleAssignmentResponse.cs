namespace CourtBookingManagement.Application.UserRoles.Models.Responses;

public sealed class UserRoleAssignmentResponse
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public List<RoleItemResponse> Roles { get; set; } = [];
}
