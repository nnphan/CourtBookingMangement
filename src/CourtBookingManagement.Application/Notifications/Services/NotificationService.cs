using CourtBookingManagement.Application.Notifications.Models.Requests;
using CourtBookingManagement.Application.Notifications.Models.Responses;
using CourtBookingManagement.Application.Notifications.Repositories;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Notifications.Services;

public sealed class NotificationService(INotificationRepository repository) : INotificationService
{
    public async Task<Result<MarkNotificationReadResponse>> MarkAsReadAsync(
        Guid notificationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (notificationId == Guid.Empty)
        {
            return Result.Failure<MarkNotificationReadResponse>(
                new Error("NOTIFICATION.INVALID_ID", "A valid notification id is required."));
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<MarkNotificationReadResponse>(
                new Error("NOTIFICATION.INVALID_USER", "The current user is invalid."));
        }

        try
        {
            var notification = await repository.GetByIdAsync(notificationId, cancellationToken);
            if (notification is null)
            {
                return Result.Failure<MarkNotificationReadResponse>(
                    new Error("NOTIFICATION.NOT_FOUND", "Notification not found."));
            }

            if (notification.UserId != currentUserId)
            {
                return Result.Failure<MarkNotificationReadResponse>(
                    new Error("NOTIFICATION.FORBIDDEN", "You are not allowed to access this notification."));
            }

            if (!notification.IsRead)
            {
                await repository.MarkAsReadAsync(notificationId, cancellationToken);
                notification = await repository.GetByIdAsync(notificationId, cancellationToken);

                if (notification is null)
                {
                    return Result.Failure<MarkNotificationReadResponse>(
                        new Error("NOTIFICATION.NOT_FOUND", "Notification not found."));
                }
            }

            return Result.Success(new MarkNotificationReadResponse
            {
                NotificationId = notification.Id,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                Message = "Notification marked as read."
            });
        }
        catch (Exception exception)
        {
            return Result.Failure<MarkNotificationReadResponse>(Error.FromException(exception));
        }
    }

    public async Task<Result<NotificationPagedResponse>> GetNotificationsAsync(
        NotificationSearchRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<NotificationPagedResponse>(
                new Error("NOTIFICATION.INVALID_REQUEST", "Request cannot be null."));
        }

        if (currentUserId == Guid.Empty)
        {
            return Result.Failure<NotificationPagedResponse>(
                new Error("NOTIFICATION.INVALID_USER", "The current user is invalid."));
        }

        var normalizedRequest = new NotificationSearchRequest
        {
            IsRead = request.IsRead,
            Type = string.IsNullOrWhiteSpace(request.Type) ? null : request.Type.Trim().ToUpperInvariant(),
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 50)
        };

        try
        {
            var pagedNotifications = await repository.GetNotificationsAsync(currentUserId, normalizedRequest, cancellationToken);
            var unreadCount = await repository.GetUnreadCountAsync(currentUserId, cancellationToken);

            return Result.Success(new NotificationPagedResponse
            {
                Items = pagedNotifications.Items,
                TotalRecords = (int)Math.Min(pagedNotifications.TotalCount, int.MaxValue),
                TotalPages = pagedNotifications.TotalPages,
                Page = pagedNotifications.Page,
                PageSize = pagedNotifications.PageSize,
                UnreadCount = unreadCount
            });
        }
        catch (Exception exception)
        {
            return Result.Failure<NotificationPagedResponse>(Error.FromException(exception));
        }
    }
}