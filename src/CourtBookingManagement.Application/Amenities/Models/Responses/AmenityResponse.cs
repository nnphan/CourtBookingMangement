namespace CourtBookingManagement.Application.Amenities.Models.Responses;

public sealed class AmenityResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Icon { get; set; }
}