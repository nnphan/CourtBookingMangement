namespace CourtBookingManagement.Application.Courts.Models.Requests;

public sealed class CourtSearchRequest
{
    public string? Keyword { get; set; }

    public Guid? BranchId { get; set; }

    public bool? IsActive { get; set; }

    public string SortBy { get; set; } = "latest";

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}
