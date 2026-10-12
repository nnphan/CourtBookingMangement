namespace CourtBookingManagement.Application.Branches.DTOs;

public sealed class BranchLookupResponse
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Name { get; set; } = null!;
}