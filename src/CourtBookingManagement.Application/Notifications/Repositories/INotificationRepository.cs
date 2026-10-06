using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Notifications.Models;
using CourtBookingManagement.Application.Notifications.Models.Requests;
using CourtBookingManagement.Application.Notifications.Models.Responses;

namespace CourtBookingManagement.Application.Notifications.Repositories;

public interface INotificationRepository
{
    Task<NotificationRecord?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken);

    Task<PagedResult<NotificationResponse>> GetNotificationsAsync(
        Guid userId,
        NotificationSearchRequest request,
        CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);
}