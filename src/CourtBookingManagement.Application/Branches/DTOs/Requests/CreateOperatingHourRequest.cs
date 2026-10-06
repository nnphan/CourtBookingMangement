namespace CourtBookingManagement.Application.Branches.DTOs.Requests;

public sealed class CreateOperatingHourRequest
{
    public TimeSpan OpenTime { get; set; }

    public TimeSpan CloseTime { get; set; }

    public bool IsClosed { get; set; }
}