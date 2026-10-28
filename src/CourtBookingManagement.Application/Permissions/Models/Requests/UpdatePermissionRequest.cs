namespace CourtBookingManagement.Application.Permissions.Models.Requests;

/// <summary>Editable display fields for an existing permission; its code is immutable.</summary>
public sealed class UpdatePermissionRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
