namespace CourtBookingManagement.Application.Branches.DTOs.Responses;

public sealed class CreateBranchResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public bool IsActive { get; set; }
}