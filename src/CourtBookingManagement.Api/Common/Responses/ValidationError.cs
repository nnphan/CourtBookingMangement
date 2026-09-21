namespace CourtBookingManagement.Api.Common.Responses;

public sealed record ValidationError(
    string Field,
    string Message,
    string? Code = null);