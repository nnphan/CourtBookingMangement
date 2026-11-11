using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Api.Common.Constants;

public static class ErrorStatusMapper
{
    public static int ToStatusCode(string errorCode) => errorCode switch
    {
        "Auth.InvalidCredentials" or "Auth.RefreshTokenInvalid" or
        "Auth.RefreshTokenRevoked" or "Auth.RefreshTokenExpired" => StatusCodes.Status401Unauthorized,
        "Auth.UserDisabled" or "Auth.EmailNotVerified" or "Auth.Unauthorized" => StatusCodes.Status403Forbidden,
        "NOTIFICATION.FORBIDDEN" => StatusCodes.Status403Forbidden,
        "NOTIFICATION.NOT_FOUND" => StatusCodes.Status404NotFound,
        "User.NotFound" or "Role.NotFound" or "Permission.NotFound" or "Branch.NotFound" or "Booking.NotFound" or "PLAYER_MATCH.NOT_FOUND" or
        "PLAYER_MATCH.REQUEST_NOT_FOUND" or "PLAYER_MATCH.BOOKING_NOT_FOUND" or
        "PLAYER_MATCH.BRANCH_NOT_FOUND" or "PLAYER_MATCH.COURT_NOT_FOUND" or
        "PLAYER_MATCH.REQUEST_MISMATCH" => StatusCodes.Status404NotFound,
        "PLAYER_MATCH.INVALID_USER" => StatusCodes.Status401Unauthorized,
        "PLAYER_MATCH.NOT_HOST" => StatusCodes.Status403Forbidden,
        "User.EmailAlreadyExists" or "AUTH_EMAIL_ALREADY_EXISTS" or "AUTH_PHONE_ALREADY_EXISTS" or
        "Permission.CodeAlreadyExists" or "Permission.InUse" or
        "Role.CodeAlreadyExists" or "Role.InUse" or
        "PLAYER_MATCH.ALREADY_JOINED" or "PLAYER_MATCH.REQUEST_EXISTS" or "PLAYER_MATCH.DUPLICATE" or
        "PLAYER_MATCH.COURT_CONFLICT" or "PLAYER_MATCH.MATCH_FULL" or "PLAYER_MATCH.NOT_OPEN" or
        "Booking.StatusConflict" or
        "PLAYER_MATCH.MATCH_EXPIRED" or "PLAYER_MATCH.REQUEST_STATE_CHANGED" or
        "PLAYER_MATCH.REQUEST_NOT_PENDING" or "PLAYER_MATCH.HOST_ALREADY_PARTICIPANT" or
        "PLAYER_MATCH.BOOKING_INVALID_STATUS" => StatusCodes.Status409Conflict,
        "PLAYER_MATCH.INVALID_REQUEST_STATUS" or "Error.Validation" => StatusCodes.Status400BadRequest,
        "Court.NotFound" or "Court.BranchNotFound" => StatusCodes.Status404NotFound,
        "Owner.NotFound" or "Owner.UserNotFound" => StatusCodes.Status404NotFound,
        "Owner.AlreadyExists" or "Owner.TaxIdAlreadyExists" or "Owner.HasActiveBranches" => StatusCodes.Status409Conflict,
        "Customer.NotFound" => StatusCodes.Status404NotFound,
        "Customer.ProfileAlreadyExists" => StatusCodes.Status409Conflict,
        "Auth.CustomerRoleNotConfigured" or "Auth.DefaultMembershipNotConfigured" => StatusCodes.Status500InternalServerError,
        "Court.DuplicateName" or "Court.DuplicateNumber" => StatusCodes.Status409Conflict,
        "CourtBooking.NotFound" => StatusCodes.Status404NotFound,
        "CourtBooking.Conflict" => StatusCodes.Status409Conflict,
        "CourtBooking.Blocked" => StatusCodes.Status400BadRequest,
        "CourtBooking.Invalid" => StatusCodes.Status400BadRequest,
        "Catalog.CategoryNotFound" or "Catalog.ProductNotFound" or "Catalog.BranchNotFound" =>
            StatusCodes.Status404NotFound,
        "Catalog.CategoryCodeExists" or "Catalog.ProductSkuExists" or "Catalog.ProductBarcodeExists" or
        "Catalog.ProductInactive" or "Catalog.CategoryInactive" => StatusCodes.Status409Conflict,
        "Catalog.InvalidRequest" or "Catalog.InvalidPagination" or "Catalog.InvalidSort" =>
            StatusCodes.Status400BadRequest,
        "Inventory.ReceiptNotFound" => StatusCodes.Status404NotFound,
        "Inventory.ReceiptNotDraft" or "Inventory.ProductNotConfigured" or "Inventory.InsufficientStock" or
        "Inventory.QuantityLimitExceeded" =>
            StatusCodes.Status409Conflict,
        "Inventory.InvalidRequest" or "Inventory.InvalidPagination" or "Inventory.InvalidFilter" =>
            StatusCodes.Status400BadRequest,
        "Sales.OrderNotFound" or "Sales.InvoiceNotFound" =>
            StatusCodes.Status404NotFound,
        "Sales.OrderNotCompletable" or "Sales.ProductNotAvailable" or "Sales.InsufficientStock" or
        "Sales.QuantityLimitExceeded" or "Sales.RelatedEntityNotFound" =>
            StatusCodes.Status409Conflict,
        "Sales.InvalidRequest" or "Sales.InvalidPagination" or "Sales.InvalidFilter" =>
            StatusCodes.Status400BadRequest,
        "Error.Exception" => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };
}