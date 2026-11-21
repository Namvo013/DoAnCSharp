using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiMS.Tests;

public sealed class NotificationSchedulerTests
{
    [Fact]
    public async Task ProcessRemindersAsync_creates_three_day_and_one_day_reminders()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync(username: "reminder-reader");
        var (_, _, copies) = await database.AddBookAsync("Reminder title", copyCount: 2);
        var staff = await database.AddLibrarianAsync();
        var now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
        await database.AddLoanAsync(reader, copies[0], staff.Librarian, DateOnly.FromDateTime(now).AddDays(3));
        await database.AddLoanAsync(reader, copies[1], staff.Librarian, DateOnly.FromDateTime(now).AddDays(1));
        var scheduler = new LoanNotificationSchedulerService(
            database.Context, NullLogger<LoanNotificationSchedulerService>.Instance);

        var count = await scheduler.ProcessRemindersAsync(now, CancellationToken.None);

        Assert.Equal(2, count);
        var notifications = await database.Context.ReaderNotifications.OrderBy(item => item.Type).ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, item => Assert.Equal("DueSoon", item.Type));
        Assert.Contains(notifications, item => item.DeduplicationKey.EndsWith(":before3", StringComparison.Ordinal));
        Assert.Contains(notifications, item => item.DeduplicationKey.EndsWith(":before1", StringComparison.Ordinal));
        Assert.All(notifications, item => Assert.Contains("Reminder title", item.Message));
        Assert.All(notifications, item => Assert.False(item.IsRead));
    }

    [Fact]
    public async Task ProcessRemindersAsync_creates_overdue_notification_with_day_count()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync("Overdue title");
        var staff = await database.AddLibrarianAsync();
        var now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
        await database.AddLoanAsync(reader, copies[0], staff.Librarian, new DateOnly(2026, 11, 18));
        var scheduler = new LoanNotificationSchedulerService(
            database.Context, NullLogger<LoanNotificationSchedulerService>.Instance);

        var count = await scheduler.ProcessRemindersAsync(now, CancellationToken.None);

        Assert.Equal(1, count);
        var notification = await database.Context.ReaderNotifications.SingleAsync();
        Assert.Equal("Overdue", notification.Type);
        Assert.Contains("3 ngày", notification.Message);
        Assert.Equal($"loan:1:due:2026-11-18:overdue:2026-11-21", notification.DeduplicationKey);
    }

    [Fact]
    public async Task ProcessRemindersAsync_does_not_notify_for_returned_or_non_milestone_loans()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyCount: 3);
        var staff = await database.AddLibrarianAsync();
        var today = new DateOnly(2026, 11, 21);
        var now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
        await database.AddLoanAsync(reader, copies[0], staff.Librarian, today);
        await database.AddLoanAsync(reader, copies[1], staff.Librarian, today.AddDays(2));
        await database.AddLoanAsync(
            reader, copies[2], staff.Librarian, today.AddDays(-1), DateTime.UtcNow.AddHours(-1));

        var count = await new LoanNotificationSchedulerService(
                database.Context, NullLogger<LoanNotificationSchedulerService>.Instance)
            .ProcessRemindersAsync(now, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(await database.Context.ReaderNotifications.ToListAsync());
    }

    [Fact]
    public async Task ProcessRemindersAsync_is_idempotent_for_same_due_date_milestone()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync();
        var staff = await database.AddLibrarianAsync();
        var now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
        await database.AddLoanAsync(reader, copies[0], staff.Librarian, DateOnly.FromDateTime(now).AddDays(1));
        var scheduler = new LoanNotificationSchedulerService(
            database.Context, NullLogger<LoanNotificationSchedulerService>.Instance);

        var firstCount = await scheduler.ProcessRemindersAsync(now, CancellationToken.None);
        var secondCount = await scheduler.ProcessRemindersAsync(now.AddHours(1), CancellationToken.None);

        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);
        Assert.Single(await database.Context.ReaderNotifications.ToListAsync());
    }

    [Fact]
    public async Task ProcessRemindersAsync_allows_one_overdue_reminder_per_day()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync();
        var staff = await database.AddLibrarianAsync();
        await database.AddLoanAsync(reader, copies[0], staff.Librarian, new DateOnly(2026, 11, 18));
        var scheduler = new LoanNotificationSchedulerService(
            database.Context, NullLogger<LoanNotificationSchedulerService>.Instance);

        var dayOne = await scheduler.ProcessRemindersAsync(
            new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        var dayTwo = await scheduler.ProcessRemindersAsync(
            new DateTime(2026, 11, 22, 9, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal(1, dayOne);
        Assert.Equal(1, dayTwo);
        Assert.Equal(2, await database.Context.ReaderNotifications.CountAsync());
    }

    [Fact]
    public async Task ProcessPendingAsync_converts_supported_reservation_event_to_notification()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync("Ready book");
        var reservation = await database.AddReservationAsync(reader, book, RecordStatuses.ReadyForPickup);
        var outbox = await AddOutboxAsync(database, reader, reservation, "ReservationReady");
        var now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
        var processor = CreateProcessor(database);

        var count = await processor.ProcessPendingAsync(now, CancellationToken.None);

        Assert.Equal(1, count);
        var notification = await database.Context.ReaderNotifications.SingleAsync();
        Assert.Equal("Sách đã sẵn sàng nhận", notification.Title);
        Assert.Equal("ReservationReady", notification.Type);
        Assert.Contains("Ready book", notification.Message);
        Assert.Equal(reservation.ReservationId, notification.RelatedEntityId);
        Assert.Equal(outbox.DeduplicationKey, notification.DeduplicationKey);
        Assert.Equal(now, (await database.Context.NotificationOutboxes.SingleAsync()).ProcessedAt);
    }

    [Theory]
    [InlineData("ReservationExpired", "Yêu cầu đặt giữ đã hết hạn")]
    [InlineData("ReservationCancelled", "Yêu cầu đặt giữ đã được hủy")]
    public async Task ProcessPendingAsync_maps_supported_events_to_specific_titles(string eventType, string expectedTitle)
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync("Event book");
        var reservation = await database.AddReservationAsync(reader, book);
        await AddOutboxAsync(database, reader, reservation, eventType);

        var count = await CreateProcessor(database).ProcessPendingAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal(expectedTitle, (await database.Context.ReaderNotifications.SingleAsync()).Title);
    }

    [Fact]
    public async Task ProcessPendingAsync_marks_missing_reservation_processed_to_avoid_retry_loop()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var outbox = new NotificationOutbox
        {
            ReaderId = reader.ReaderId,
            ReservationId = 999,
            EventType = "ReservationReady",
            DeduplicationKey = "missing-reservation-event",
            CreatedAt = DateTime.UtcNow
        };
        database.Context.NotificationOutboxes.Add(outbox);
        await database.Context.SaveChangesAsync();

        var count = await CreateProcessor(database).ProcessPendingAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, count);
        var saved = await database.Context.NotificationOutboxes.SingleAsync();
        Assert.NotNull(saved.ProcessedAt);
        Assert.Equal(0, saved.Attempts);
        Assert.Empty(await database.Context.ReaderNotifications.ToListAsync());
    }

    [Fact]
    public async Task ProcessPendingAsync_is_idempotent_when_notification_already_exists()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync();
        var reservation = await database.AddReservationAsync(reader, book);
        var outbox = await AddOutboxAsync(database, reader, reservation, "ReservationCancelled");
        database.Context.ReaderNotifications.Add(new ReaderNotification
        {
            Reader = reader,
            Title = "Existing",
            Message = "Already processed elsewhere",
            Type = "ReservationCancelled",
            RelatedEntityType = "Reservation",
            RelatedEntityId = reservation.ReservationId,
            DeduplicationKey = outbox.DeduplicationKey
        });
        await database.Context.SaveChangesAsync();

        var count = await CreateProcessor(database).ProcessPendingAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Single(await database.Context.ReaderNotifications.ToListAsync());
        Assert.NotNull((await database.Context.NotificationOutboxes.SingleAsync()).ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_increments_attempts_for_unsupported_event()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync();
        var reservation = await database.AddReservationAsync(reader, book);
        var outbox = await AddOutboxAsync(database, reader, reservation, "UnsupportedEvent");

        var count = await CreateProcessor(database).ProcessPendingAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0, count);
        var saved = await database.Context.NotificationOutboxes.SingleAsync();
        Assert.Equal(1, saved.Attempts);
        Assert.Contains("UnsupportedEvent", saved.LastError);
        Assert.Null(saved.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_truncates_error_text_to_database_limit()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync();
        var reservation = await database.AddReservationAsync(reader, book);
        await AddOutboxAsync(database, reader, reservation, new string('X', 1200));

        await CreateProcessor(database).ProcessPendingAsync(DateTime.UtcNow, CancellationToken.None);

        var error = (await database.Context.NotificationOutboxes.SingleAsync()).LastError;
        Assert.NotNull(error);
        Assert.Equal(1000, error.Length);
    }

    [Fact]
    public async Task ProcessPendingAsync_limits_each_batch_to_one_hundred_items()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        for (var index = 1; index <= 105; index++)
        {
            database.Context.NotificationOutboxes.Add(new NotificationOutbox
            {
                ReaderId = reader.ReaderId,
                ReservationId = index + 1000,
                EventType = "ReservationReady",
                DeduplicationKey = $"batch-event:{index}",
                CreatedAt = DateTime.UtcNow.AddSeconds(index)
            });
        }
        await database.Context.SaveChangesAsync();

        var count = await CreateProcessor(database).ProcessPendingAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(100, count);
        Assert.Equal(100, await database.Context.NotificationOutboxes.CountAsync(item => item.ProcessedAt != null));
        Assert.Equal(5, await database.Context.NotificationOutboxes.CountAsync(item => item.ProcessedAt == null));
    }

    private static NotificationOutboxProcessorService CreateProcessor(TestDatabase database) =>
        new(database.Context, NullLogger<NotificationOutboxProcessorService>.Instance);

    private static async Task<NotificationOutbox> AddOutboxAsync(
        TestDatabase database,
        Reader reader,
        Reservation reservation,
        string eventType)
    {
        var item = new NotificationOutbox
        {
            ReaderId = reader.ReaderId,
            ReservationId = reservation.ReservationId,
            EventType = eventType,
            DeduplicationKey = $"reservation:{reservation.ReservationId}:{eventType}",
            CreatedAt = DateTime.UtcNow
        };
        database.Context.NotificationOutboxes.Add(item);
        await database.Context.SaveChangesAsync();
        return item;
    }
}
