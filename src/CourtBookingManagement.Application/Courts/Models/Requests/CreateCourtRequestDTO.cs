namespace CourtBookingManagement.Application.Courts.Models.Requests;

public sealed class CreateCourtRequestDTO
{
    public Guid BranchId { get; set; }

    public int CourtNumber { get; set; }

    public string? Name { get; set; }

    public Guid? CourtTypeId { get; set; }

    public string Status { get; set; } = "active";
}
