namespace CourtBookingManagement.Application.Inventory.Models.Requests;

public sealed record ReorderLevelRequest
{
    public required decimal ReorderLevel { get; init; }
}
