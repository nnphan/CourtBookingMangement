namespace CourtBookingManagement.Application.Roles.Models.Requests;

/// <summary>Fields used to replace role data and its complete permission set.</summary>
public sealed class UpdateRoleRequest
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyCollection<Guid> PermissionIds { get; set; } = Array.Empty<Guid>();
}
