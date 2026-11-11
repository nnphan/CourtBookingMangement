namespace CourtBookingManagement.Application.Inventory.Models.Requests;

public sealed record StockMovementSearchRequest
{
    public Guid? ProductId { get; init; }

    public string? MovementType { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
