namespace CourtBookingManagement.Application.Permissions.Models.Requests;

/// <summary>Fields used to create a permission catalog entry.</summary>
public sealed class CreatePermissionRequest
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
