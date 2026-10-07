namespace CourtBookingManagement.Application.UserRoles.Models.Responses;

public sealed class UserRoleResponse
{
    public Guid RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime AssignedAt { get; set; }

    public Guid? AssignedBy { get; set; }

    public string? AssignedByName { get; set; }

    public int PermissionCount { get; set; }
}
