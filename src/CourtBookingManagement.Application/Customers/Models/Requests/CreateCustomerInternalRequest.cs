namespace CourtBookingManagement.Application.Customers.Models.Requests;

public sealed class CreateCustomerInternalRequest
{
    public Guid? UserId { get; init; }

    public Guid? MembershipLevelId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string PhoneNumber { get; init; } = string.Empty;

    public bool IsGuest { get; init; }

    public int LoyaltyPointsBalance { get; init; }

    public bool IsActive { get; init; } = true;

    public Guid? CreatedBy { get; init; }
}
