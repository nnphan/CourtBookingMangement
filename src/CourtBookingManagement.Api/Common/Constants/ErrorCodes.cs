namespace CourtBookingManagement.Api.Common.Constants;

public static class ErrorCodes
{
    public const string Validation = "COMMON_001";
    public const string BadRequest = "COMMON_002";
    public const string Unauthorized = "AUTH_001";
    public const string Forbidden = "AUTH_002";
    public const string NotFound = "COMMON_003";
    public const string Conflict = "COMMON_004";
    public const string Unexpected = "COMMON_500";

    public const string InvalidCredentials = "AUTH_001";
    public const string AuthorizationDenied = "AUTH_002";
    public const string CourtNotFound = "COURT_001";
    public const string CourtUnavailable = "COURT_002";
    public const string BookingNotFound = "BOOKING_001";
    public const string BookingSlotUnavailable = "BOOKING_002";
    public const string UserNotFound = "USER_001";
    public const string UserEmailAlreadyExists = "USER_002";
    public const string PaymentFailed = "PAYMENT_001";
    public const string PaymentAlreadyProcessed = "PAYMENT_002";
    public const string RecommendationUnavailable = "MATCHING_001";
    public const string NotificationDeliveryFailed = "NOTIFICATION_001";
}