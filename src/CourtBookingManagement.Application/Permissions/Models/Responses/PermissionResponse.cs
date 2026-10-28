namespace CourtBookingManagement.Application.Permissions.Models.Responses;

/// <summary>Permission catalog data returned by the API.</summary>
public sealed class PermissionResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int RoleCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}