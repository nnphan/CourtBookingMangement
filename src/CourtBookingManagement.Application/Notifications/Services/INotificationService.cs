using CourtBookingManagement.Application.Notifications.Models.Requests;
using CourtBookingManagement.Application.Notifications.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Notifications.Services;

public interface INotificationService
{
    Task<Result<NotificationPagedResponse>> GetNotificationsAsync(
        NotificationSearchRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken);
}