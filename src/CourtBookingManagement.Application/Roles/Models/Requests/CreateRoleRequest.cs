namespace CourtBookingManagement.Application.Roles.Models.Requests;

/// <summary>Fields used to create a role and its initial complete permission set.</summary>
public sealed class CreateRoleRequest
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public IReadOnlyCollection<Guid> PermissionIds { get; set; } = Array.Empty<Guid>();
}
