namespace CourtBookingManagement.Application.Notifications.Models.Responses;

public sealed class MarkNotificationReadResponse
{
    public Guid NotificationId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public string Message { get; set; } = string.Empty;
}