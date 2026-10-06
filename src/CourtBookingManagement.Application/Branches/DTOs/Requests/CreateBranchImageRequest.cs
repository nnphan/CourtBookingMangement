namespace CourtBookingManagement.Application.Branches.DTOs.Requests;

public sealed class CreateBranchImageRequest
{
    public string ImageUrl { get; set; } = default!;

    public short SortOrder { get; set; }
}