using LiMS.Api.Contracts;
using LiMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Services;

/// <summary>
/// Xử lý việc đọc và cập nhật trạng thái thông báo của độc giả.
/// </summary>
public sealed class NotificationService(LiMSDbContext db)
    : INotificationService
{
    public async Task<IReadOnlyList<ReaderNotificationResponse>>
        GetNotificationsAsync(
            int accountId,
            CancellationToken cancellationToken)
    {
        // Chỉ lấy thông báo thuộc tài khoản hiện tại.
        var notifications = await db.ReaderNotifications
            .AsNoTracking()
            .Where(item => item.Reader.AccountId == accountId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                item.NotificationId,
                item.Title,
                item.Message,
                item.Type,
                item.CreatedAt,
                item.IsRead
            })
            .ToListAsync(cancellationToken);

        return notifications.Select(item =>
            new ReaderNotificationResponse(
                item.NotificationId,
                item.Title,
                item.Message,
                item.Type,
                ToUtcOffset(item.CreatedAt),
                item.IsRead))
            .ToArray();
    }

    public Task<int> GetUnreadCountAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        return db.ReaderNotifications.CountAsync(
            item => item.Reader.AccountId == accountId
                && !item.IsRead,
            cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(
        int accountId,
        long notificationId,
        CancellationToken cancellationToken)
    {
        // Không cho phép tài khoản khác cập nhật thông báo này.
        var notification = await db.ReaderNotifications
            .SingleOrDefaultAsync(
                item => item.NotificationId == notificationId
                    && item.Reader.AccountId == accountId,
                cancellationToken);

        if (notification is null)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var notifications = await db.ReaderNotifications
            .Where(item => item.Reader.AccountId == accountId
                && !item.IsRead)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return notifications.Count;
    }

    // Chuẩn hóa DateTime thành UTC trước khi chuyển sang DateTimeOffset.
    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        var utcValue = DateTime.SpecifyKind(
            value,
            DateTimeKind.Utc);

        return new DateTimeOffset(utcValue);
    }
}
