namespace CourtBookingManagement.Application.Permissions.Models.Responses;

public sealed class PermissionResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int RoleCount { get; set; }

    public DateTime CreatedAt { get; set; }
}