namespace CourtBookingManagement.Api.Common.Responses;

public sealed record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data,
    object? Metadata,
    string TraceId,
    DateTimeOffset Timestamp)
{
    public static ApiResponse<T> Create(
        T? data,
        string message,
        string traceId,
        object? metadata = null) =>
        new(true, message, data, metadata, traceId, DateTimeOffset.UtcNow);
}