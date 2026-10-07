namespace CourtBookingManagement.Application.Customers.Models.Responses;

public sealed class CustomerDetailResponse
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsGuest { get; set; }

    public bool IsActive { get; set; }

    public int LoyaltyPointsBalance { get; set; }

    public Guid? MembershipLevelId { get; set; }

    public string? MembershipLevelCode { get; set; }

    public string? MembershipLevelName { get; set; }

    public decimal? DiscountPercentage { get; set; }

    public bool? IsEmailVerified { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
