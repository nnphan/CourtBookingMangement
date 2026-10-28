namespace CourtBookingManagement.Application.Roles.Models.Responses;

/// <summary>Role data, including its assigned permission catalog entries.</summary>
public sealed class RoleResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public int UserCount { get; set; }

    public int PermissionCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public IReadOnlyList<PermissionSummaryResponse> Permissions { get; set; } =
        Array.Empty<PermissionSummaryResponse>();
}