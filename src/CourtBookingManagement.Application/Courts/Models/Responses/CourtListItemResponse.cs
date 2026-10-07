namespace CourtBookingManagement.Application.Courts.Models.Responses;

public sealed class CourtListItemResponse
{
    public Guid Id { get; set; }

    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public int CourtNumber { get; set; }

    public string? Name { get; set; }

    public Guid? CourtTypeId { get; set; }

    public string? CourtTypeCode { get; set; }

    public string? CourtTypeName { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
