namespace CourtBookingManagement.Application.Customers.Models.Responses;

public sealed class CustomerListItemResponse
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsGuest { get; set; }

    public bool IsActive { get; set; }

    public int LoyaltyPointsBalance { get; set; }

    public string? MembershipLevelCode { get; set; }

    public string? MembershipLevelName { get; set; }

    public DateTime CreatedAt { get; set; }
}
