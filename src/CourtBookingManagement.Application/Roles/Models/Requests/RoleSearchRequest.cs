namespace CourtBookingManagement.Application.Roles.Models.Requests;

public sealed class RoleSearchRequest
{
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public string SortBy { get; set; } = "latest";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}