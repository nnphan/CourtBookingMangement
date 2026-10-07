namespace CourtBookingManagement.Application.Courts.Models.Requests;

public sealed class UpdateCourtRequest
{
    public int CourtNumber { get; set; }

    public string? Name { get; set; }

    public Guid? CourtTypeId { get; set; }

    public string Status { get; set; } = "active";

    public bool IsActive { get; set; } = true;
}
