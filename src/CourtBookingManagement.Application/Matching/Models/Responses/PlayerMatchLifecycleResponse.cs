namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class PlayerMatchLifecycleResponse
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
