using LiMS.Api.Contracts;

namespace LiMS.Api.Services;

/// <summary>
/// Các thao tác quản lý thông báo nội bộ của độc giả.
/// </summary>
public interface INotificationService
{
    Task<IReadOnlyList<ReaderNotificationResponse>> GetNotificationsAsync(
        int accountId,
        CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(
        int accountId,
        CancellationToken cancellationToken);

    Task<bool> MarkAsReadAsync(
        int accountId,
        long notificationId,
        CancellationToken cancellationToken);

    Task<int> MarkAllAsReadAsync(
        int accountId,
        CancellationToken cancellationToken);
}
