namespace CourtBookingManagement.Application.Notifications.Models.Responses;

public sealed class NotificationPagedResponse
{
    public IReadOnlyList<NotificationResponse> Items { get; init; } = Array.Empty<NotificationResponse>();

    public int TotalRecords { get; init; }

    public int TotalPages { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int UnreadCount { get; init; }
}