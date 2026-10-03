using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.CourtStatus;

internal static class CourtStatusErrors
{
    public static Error NotFound(string resource, Guid id) => new("CourtStatus.NotFound", $"{resource} '{id}' was not found.");
    public static Error Invalid(string message) => new("CourtStatus.Invalid", message);
    public static Error Conflict(string message) => new("CourtStatus.Conflict", message);
}