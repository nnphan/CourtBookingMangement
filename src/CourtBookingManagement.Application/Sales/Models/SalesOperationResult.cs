namespace CourtBookingManagement.Application.Sales.Models;

public sealed record SalesOperationResult(SalesOperationStatus Status, Guid? ResourceId = null);

public enum SalesOperationStatus
{
    Succeeded,
    NotFound,
    InvalidState,
    ProductNotConfigured,
    InsufficientStock,
    QuantityLimitExceeded,
    RelatedEntityNotFound
}
