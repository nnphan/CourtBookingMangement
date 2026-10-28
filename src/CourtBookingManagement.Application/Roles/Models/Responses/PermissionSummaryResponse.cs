namespace CourtBookingManagement.Application.Roles.Models.Responses;

/// <summary>Permission details assigned to a role.</summary>
public sealed class PermissionSummaryResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
