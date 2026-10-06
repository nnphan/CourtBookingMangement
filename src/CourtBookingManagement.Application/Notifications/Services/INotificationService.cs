using CourtBookingManagement.Application.Notifications.Models.Requests;
using CourtBookingManagement.Application.Notifications.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Notifications.Services;

public interface INotificationService
{
    Task<Result<MarkNotificationReadResponse>> MarkAsReadAsync(
        Guid notificationId,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<Result<NotificationPagedResponse>> GetNotificationsAsync(
        NotificationSearchRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken);
}