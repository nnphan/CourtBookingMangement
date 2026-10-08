namespace CourtBookingManagement.Application.Branches.DTOs.Requests;

public sealed class CreateBranchRequest
{
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public string Address { get; set; } = default!;

    public string? City { get; set; }

    public string? District { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? PhoneNumber { get; set; }

    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    public bool SupportsInstantBooking { get; set; }

    public List<Guid> AmenityIds { get; set; } = [];

    public List<CreateBranchImageRequest> Images { get; set; } = [];

    public List<CreateOperatingHourRequest> OperatingHours { get; set; } = [];

    public List<CreateCourtRequest> Courts { get; set; } = [];

    public List<CreateBranchPricingRequest> BranchPricings { get; set; } = [];
}