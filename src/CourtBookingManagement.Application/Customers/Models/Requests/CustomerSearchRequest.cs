namespace CourtBookingManagement.Application.Customers.Models.Requests;

public sealed class CustomerSearchRequest
{
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsGuest { get; set; }

    public Guid? MembershipLevelId { get; set; }

    public string SortBy { get; set; } = "latest";

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}
