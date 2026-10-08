namespace CourtBookingManagement.Application.Branches.DTOs.Requests;

public sealed class CreateCourtRequest
{
    public int CourtNumber { get; set; }

    public string Name { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}