namespace CourtBookingManagement.Application.Notifications.Models.Responses;

public sealed class NotificationResponse
{
    public Guid Id { get; set; }

    public Guid? MatchId { get; set; }

    public string NotificationType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}