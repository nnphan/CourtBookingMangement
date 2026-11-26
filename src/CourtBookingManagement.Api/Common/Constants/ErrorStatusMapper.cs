using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Api.Common.Constants;

public static class ErrorStatusMapper
{
    public static int ToStatusCode(string errorCode) => errorCode switch
    {
        "Auth.InvalidCredentials" or "Auth.RefreshTokenInvalid" or
        "Auth.RefreshTokenRevoked" or "Auth.RefreshTokenExpired" or
        "SePay.WebhookUnauthorized" => StatusCodes.Status401Unauthorized,
        "Auth.UserDisabled" or "Auth.EmailNotVerified" or "Auth.Unauthorized" or
        "PLAYER_MATCHING_DISABLED" => StatusCodes.Status403Forbidden,
        "NOTIFICATION.FORBIDDEN" => StatusCodes.Status403Forbidden,
        "NOTIFICATION.NOT_FOUND" => StatusCodes.Status404NotFound,
        "User.NotFound" or "Role.NotFound" or "Permission.NotFound" or "Branch.NotFound" or "Booking.NotFound" or
        "Payment.NotFound" or "PLAYER_MATCH.NOT_FOUND" or
        "PLAYER_MATCH.REQUEST_NOT_FOUND" or "PLAYER_MATCH.BOOKING_NOT_FOUND" or
        "PLAYER_MATCH.BRANCH_NOT_FOUND" or "PLAYER_MATCH.COURT_NOT_FOUND" or
        "PLAYER_MATCH.REQUEST_MISMATCH" or "PLAYER_MATCHING_PROFILE_REQUIRED" or
        "MATCH_ROOM_NOT_FOUND" or "JOIN_REQUEST_NOT_FOUND" => StatusCodes.Status404NotFound,
        "PLAYER_MATCH.INVALID_USER" => StatusCodes.Status401Unauthorized,
        "PLAYER_MATCH.NOT_HOST" or "MATCH_ROOM_NOT_OWNED" => StatusCodes.Status403Forbidden,
        "User.EmailAlreadyExists" or "AUTH_EMAIL_ALREADY_EXISTS" or "AUTH_PHONE_ALREADY_EXISTS" or
        "Permission.CodeAlreadyExists" or "Permission.InUse" or
        "Role.CodeAlreadyExists" or "Role.InUse" or "Role.FixedCatalog" or
        "PLAYER_MATCH.ALREADY_JOINED" or "PLAYER_MATCH.REQUEST_EXISTS" or "PLAYER_MATCH.DUPLICATE" or
        "PLAYER_MATCH.COURT_CONFLICT" or "PLAYER_MATCH.MATCH_FULL" or "PLAYER_MATCH.NOT_OPEN" or
        "Booking.StatusConflict" or "Booking.HoldExpired" or "Booking.IdempotencyKeyReused" or "Payment.StatusConflict" or
        "Booking.SlotLocked" =>
            StatusCodes.Status409Conflict,
        "PLAYER_MATCH.MATCH_EXPIRED" or "PLAYER_MATCH.REQUEST_STATE_CHANGED" or
        "PLAYER_MATCH.REQUEST_NOT_PENDING" or "PLAYER_MATCH.HOST_ALREADY_PARTICIPANT" or
        "PLAYER_MATCH.BOOKING_INVALID_STATUS" or "MATCH_ROOM_INVALID_STATUS_TRANSITION" or
        "MATCH_ROOM_BOOKING_INVALID" or "MATCH_ROOM_CAPACITY_INCONSISTENT" or
        "MATCH_ROOM_FULL" or "MATCH_ROOM_ALREADY_MEMBER" or "MATCH_ROOM_SCHEDULE_CONFLICT" or
        "JOIN_REQUEST_INVALID_STATUS" or
        "PLAYER_NOT_ELIGIBLE" => StatusCodes.Status409Conflict,
        "PLAYER_MATCH.INVALID_REQUEST_STATUS" or "Error.Validation" or "SEARCH_CRITERIA_INVALID" or
        "MATCH_ROOM_DRAFT_INCOMPLETE" or
        "PLAYER_MATCHING_PROFILE_INVALID" or "PREFERENCE_INVALID" or "PREFERENCE_WEIGHT_TOTAL_INVALID" or
        "PREFERENCE_BRANCH_INVALID" or
        "SePay.WebhookInvalidPayload" => StatusCodes.Status400BadRequest,
        "Booking.LockServiceUnavailable" => StatusCodes.Status503ServiceUnavailable,
        "Payment.MomoNotConfigured" or "Payment.SePayNotConfigured" or "Payment.ProviderUnavailable" =>
            StatusCodes.Status503ServiceUnavailable,
        "Payment.ProviderFailed" or "Payment.ProviderResponseInvalid" =>
            StatusCodes.Status502BadGateway,
        "Payment.InvalidAmount" => StatusCodes.Status400BadRequest,
        "Court.NotFound" or "Court.BranchNotFound" => StatusCodes.Status404NotFound,
        "Owner.NotFound" or "Owner.UserNotFound" => StatusCodes.Status404NotFound,
        "Owner.AlreadyExists" or "Owner.TaxIdAlreadyExists" or "Owner.HasActiveBranches" => StatusCodes.Status409Conflict,
        "Customer.NotFound" => StatusCodes.Status404NotFound,
        "Customer.ProfileAlreadyExists" => StatusCodes.Status409Conflict,
        "AVATAR_FILE_NOT_FOUND" => StatusCodes.Status404NotFound,
        "AVATAR_FILE_NOT_OWNED" => StatusCodes.Status403Forbidden,
        "AVATAR_FILE_INVALID_TYPE" or "AVATAR_FILE_TOO_LARGE" =>
            StatusCodes.Status400BadRequest,
        "AVATAR_FILE_NOT_ATTACHABLE" => StatusCodes.Status409Conflict,
        "PROFILE_NOT_FOUND" => StatusCodes.Status404NotFound,
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
        "Sales.OrderNotCompletable" or "Sales.ProductNotAvailable" or "Sales.InsufficientStock" or "Sales.InsufficientCash" or
        "Sales.QuantityLimitExceeded" or "Sales.RelatedEntityNotFound" =>
            StatusCodes.Status409Conflict,
        "Sales.InvalidRequest" or "Sales.InvalidPagination" or "Sales.InvalidFilter" =>
            StatusCodes.Status400BadRequest,
        "STAFF_INVALID_REQUEST" or "STAFF_INVALID_SORT" or "STAFF_INVITATION_INVALID" or
        "STAFF_INVITATION_EXPIRED" or "STAFF_INVITATION_ALREADY_USED" or
        "STAFF_INVITATION_REVOKED" or "STAFF_PASSWORD_INVALID" or
        "PASSWORD_POLICY_FAILED" or "PASSWORD_CONFIRMATION_MISMATCH" =>
            StatusCodes.Status400BadRequest,
        "INVITATION_RESEND_TOO_SOON" or "INVITATION_RATE_LIMITED" =>
            StatusCodes.Status429TooManyRequests,
        "STAFF_OWNER_NOT_FOUND" or "STAFF_BRANCH_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
        "STAFF_NOT_FOUND" => StatusCodes.Status404NotFound,
        "STAFF_ALREADY_EXISTS" or "STAFF_CODE_ALREADY_USED" or "STAFF_ALREADY_ACTIVE" =>
            StatusCodes.Status409Conflict,
        "STAFF_DELIVERY_NOT_CONFIGURED" => StatusCodes.Status503ServiceUnavailable,
        "STAFF_ROLE_NOT_CONFIGURED" => StatusCodes.Status500InternalServerError,
        "Error.Exception" => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };
}
