namespace CourtBookingManagement.Api.Common.Responses;

public sealed record ApiErrorResponse(
    bool Success,
    string ErrorCode,
    string Message,
    string TraceId,
    DateTimeOffset Timestamp,
    IReadOnlyCollection<ValidationError>? ValidationErrors = null);