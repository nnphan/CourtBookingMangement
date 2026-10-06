namespace CourtBookingManagement.Application.Matching.Models.Responses;

public sealed class CreatePlayerMatchResponse
{
    public Guid Id { get; set; }

    public string Message { get; set; } = string.Empty;
}
