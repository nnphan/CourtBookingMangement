namespace CourtBookingManagement.Application.Notifications.Models;

public sealed class NotificationRecord
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
}