namespace CourtBookingManagement.Application.Permissions.Models.Requests;

public sealed class PermissionSearchRequest
{
    public string? Keyword { get; set; }

    public Guid? RoleId { get; set; }

    public string SortBy { get; set; } = "code_asc";

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}