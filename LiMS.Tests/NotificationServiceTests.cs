using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Tests;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task GetNotificationsAsync_scopes_results_to_account_and_orders_newest_first()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync(username: "recipient");
        var (_, otherReader) = await database.AddReaderAsync(username: "other-recipient");
        await AddNotificationAsync(database, reader, "older", DateTime.UtcNow.AddMinutes(-2));
        await AddNotificationAsync(database, reader, "newer", DateTime.UtcNow.AddMinutes(-1));
        await AddNotificationAsync(database, otherReader, "private", DateTime.UtcNow);

        var notifications = await new NotificationService(database.Context)
            .GetNotificationsAsync(account.AccountId, CancellationToken.None);

        Assert.Equal(new[] { "newer", "older" }, notifications.Select(item => item.Title));
        Assert.DoesNotContain(notifications, item => item.Title == "private");
    }

    [Fact]
    public async Task GetUnreadCountAsync_counts_only_unread_notifications_for_account()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync(username: "recipient");
        var (_, otherReader) = await database.AddReaderAsync(username: "other-recipient");
        await AddNotificationAsync(database, reader, "unread-one", DateTime.UtcNow, isRead: false);
        await AddNotificationAsync(database, reader, "read", DateTime.UtcNow, isRead: true);
        await AddNotificationAsync(database, otherReader, "other-unread", DateTime.UtcNow, isRead: false);

        var count = await new NotificationService(database.Context)
            .GetUnreadCountAsync(account.AccountId, CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MarkAsReadAsync_sets_read_state_and_timestamp()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var notification = await AddNotificationAsync(database, reader, "ready", DateTime.UtcNow, isRead: false);

        var result = await new NotificationService(database.Context)
            .MarkAsReadAsync(account.AccountId, notification.NotificationId, CancellationToken.None);

        Assert.True(result);
        var updated = await database.Context.ReaderNotifications.SingleAsync();
        Assert.True(updated.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task MarkAsReadAsync_is_idempotent_for_already_read_notification()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var readAt = DateTime.UtcNow.AddHours(-1);
        var notification = await AddNotificationAsync(
            database, reader, "already-read", DateTime.UtcNow, isRead: true, readAt: readAt);

        var result = await new NotificationService(database.Context)
            .MarkAsReadAsync(account.AccountId, notification.NotificationId, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(readAt, (await database.Context.ReaderNotifications.SingleAsync()).ReadAt);
    }

    [Fact]
    public async Task MarkAsReadAsync_does_not_allow_another_account_to_read_notification()
    {
        await using var database = TestDatabase.Create();
        var (_, owner) = await database.AddReaderAsync(username: "owner");
        var (otherAccount, _) = await database.AddReaderAsync(username: "not-owner");
        var notification = await AddNotificationAsync(database, owner, "private", DateTime.UtcNow, isRead: false);

        var result = await new NotificationService(database.Context)
            .MarkAsReadAsync(otherAccount.AccountId, notification.NotificationId, CancellationToken.None);

        Assert.False(result);
        Assert.False((await database.Context.ReaderNotifications.SingleAsync()).IsRead);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_marks_only_unread_notifications_for_account()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync(username: "recipient");
        var (_, otherReader) = await database.AddReaderAsync(username: "other-recipient");
        await AddNotificationAsync(database, reader, "unread-one", DateTime.UtcNow, isRead: false);
        await AddNotificationAsync(database, reader, "unread-two", DateTime.UtcNow, isRead: false);
        await AddNotificationAsync(database, reader, "read", DateTime.UtcNow, isRead: true);
        await AddNotificationAsync(database, otherReader, "other", DateTime.UtcNow, isRead: false);

        var count = await new NotificationService(database.Context)
            .MarkAllAsReadAsync(account.AccountId, CancellationToken.None);

        Assert.Equal(2, count);
        var unread = await database.Context.ReaderNotifications
            .Where(item => item.ReaderId == reader.ReaderId)
            .Where(item => !item.IsRead)
            .CountAsync();
        Assert.Equal(0, unread);
        var otherUnread = await database.Context.ReaderNotifications
            .Where(item => item.ReaderId == otherReader.ReaderId)
            .CountAsync(item => !item.IsRead);
        Assert.Equal(1, otherUnread);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_returns_zero_when_account_has_no_unread_items()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        await AddNotificationAsync(database, reader, "read", DateTime.UtcNow, isRead: true);

        var count = await new NotificationService(database.Context)
            .MarkAllAsReadAsync(account.AccountId, CancellationToken.None);

        Assert.Equal(0, count);
    }

    private static async Task<ReaderNotification> AddNotificationAsync(
        TestDatabase database,
        Reader reader,
        string title,
        DateTime createdAt,
        bool isRead = false,
        DateTime? readAt = null)
    {
        var notification = new ReaderNotification
        {
            Reader = reader,
            Title = title,
            Message = $"Message for {title}",
            Type = "UnitTest",
            IsRead = isRead,
            CreatedAt = createdAt,
            ReadAt = readAt,
            DeduplicationKey = $"notification:{Guid.NewGuid():N}"
        };
        database.Context.ReaderNotifications.Add(notification);
        await database.Context.SaveChangesAsync();
        return notification;
    }
}
