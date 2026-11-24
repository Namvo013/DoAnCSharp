
using LiMS.Api.Data;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Tests;

public sealed class ReservationQueueServiceTests
{
    [Fact]
    public async Task PromoteNextAsync_expires_old_requests_and_promotes_oldest_pending_request()
    {
        await using var db = new LiMSDbContext(new DbContextOptionsBuilder<LiMSDbContext>()
            .UseInMemoryDatabase($"{nameof(ReservationQueueServiceTests)}-{Guid.NewGuid():N}")
            .Options);

        var category = new Category { Name = "Queue" };
        var book = new Book { Category = category, Title = "Queued book", Author = "Author" };
        var expiredReader = CreateReader("expired-reader", "CARD-E");
        var firstReader = CreateReader("first-reader", "CARD-1");
        var secondReader = CreateReader("second-reader", "CARD-2");
        var now = DateTime.UtcNow;

        db.Reservations.AddRange(
            new Reservation
            {
                Reader = expiredReader,
                Book = book,
                CreatedAt = now.AddMinutes(-30),
                ExpireAt = now.AddMinutes(-1),
                Status = RecordStatuses.Pending
            },
            new Reservation
            {
                Reader = firstReader,
                Book = book,
                CreatedAt = now.AddMinutes(-20),
                ExpireAt = now.AddDays(30),
                Status = RecordStatuses.Pending
            },
            new Reservation
            {
                Reader = secondReader,
                Book = book,
                CreatedAt = now.AddMinutes(-10),
                ExpireAt = now.AddDays(30),
                Status = RecordStatuses.Pending
            });

        db.SystemConfigs.Add(new SystemConfig
        {
            ConfigKey = "ReservationPickupDays",
            ConfigValue = "4"
        });

        await db.SaveChangesAsync();

        await new ReservationQueueService(db)
            .PromoteNextAsync(book.BookId, now, CancellationToken.None);
        await db.SaveChangesAsync();

        var reservations = await db.Reservations
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();

        Assert.Equal(RecordStatuses.Expired, reservations[0].Status);
        Assert.Equal(RecordStatuses.ReadyForPickup, reservations[1].Status);
        Assert.Equal(now.AddDays(4), reservations[1].ExpireAt);
        Assert.Equal(RecordStatuses.Pending, reservations[2].Status);
    }

    [Fact]
    public async Task PromoteNextAsync_uses_default_pickup_duration_for_invalid_configuration()
    {
        await using var db = new LiMSDbContext(new DbContextOptionsBuilder<LiMSDbContext>()
            .UseInMemoryDatabase($"{nameof(ReservationQueueServiceTests)}-fallback-{Guid.NewGuid():N}")
            .Options);

        var book = new Book
        {
            Category = new Category { Name = "Fallback" },
            Title = "Fallback book",
            Author = "Author"
        };

        var reservation = new Reservation
        {
            Reader = CreateReader("fallback-reader", "CARD-F"),
            Book = book,
            CreatedAt = DateTime.UtcNow,
            ExpireAt = DateTime.UtcNow.AddDays(30),
            Status = RecordStatuses.Pending
        };

        db.Reservations.Add(reservation);
        db.SystemConfigs.Add(new SystemConfig
        {
            ConfigKey = "ReservationPickupDays",
            ConfigValue = "not-a-number"
        });

        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;

        await new ReservationQueueService(db)
            .PromoteNextAsync(book.BookId, now, CancellationToken.None);

        Assert.Equal(RecordStatuses.ReadyForPickup, reservation.Status);
        Assert.Equal(now.AddDays(3), reservation.ExpireAt);
    }

    [Fact]
    public async Task ProcessExpiredReservationsAsync_expires_pending_and_creates_outbox_event()
    {
        await using var database = TestDatabase.Create();

        var (_, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync("Expired pending book");

        var now = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

        var reservation = await database.AddReservationAsync(
            reader,
            book,
            RecordStatuses.Pending,
            now.AddDays(-2),
            now.AddMinutes(-1));

        var count = await new ReservationQueueService(database.Context)
            .ProcessExpiredReservationsAsync(now, CancellationToken.None);

        Assert.Equal(1, count);

        var savedReservation = await database.Context.Reservations
            .SingleAsync(item => item.ReservationId == reservation.ReservationId);

        Assert.Equal(RecordStatuses.Expired, savedReservation.Status);

        var outbox = await database.Context.NotificationOutboxes.SingleAsync();

        Assert.Equal(reader.ReaderId, outbox.ReaderId);
        Assert.Equal(reservation.ReservationId, outbox.ReservationId);
        Assert.Equal("ReservationExpired", outbox.EventType);
        Assert.Equal(
            $"reservation:{reservation.ReservationId}:ReservationExpired",
            outbox.DeduplicationKey);
        Assert.Equal(now, outbox.CreatedAt);
        Assert.Equal(0, outbox.Attempts);
    }

    [Fact]
    public async Task ProcessExpiredReservationsAsync_expires_ready_and_promotes_oldest_pending()
    {
        await using var database = TestDatabase.Create();

        var (_, expiredReader) = await database.AddReaderAsync("expired-ready-reader");
        var (_, firstReader) = await database.AddReaderAsync("first-pending-reader");
        var (_, secondReader) = await database.AddReaderAsync("second-pending-reader");
        var (_, book, _) = await database.AddBookAsync("Queue promotion book");

        var now = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

        var expiredReady = await database.AddReservationAsync(
            expiredReader,
            book,
            RecordStatuses.ReadyForPickup,
            now.AddDays(-3),
            now.AddMinutes(-1));

        var firstPending = await database.AddReservationAsync(
            firstReader,
            book,
            RecordStatuses.Pending,
            now.AddMinutes(-20),
            now.AddDays(5));

        var secondPending = await database.AddReservationAsync(
            secondReader,
            book,
            RecordStatuses.Pending,
            now.AddMinutes(-10),
            now.AddDays(5));

        database.Context.SystemConfigs.Add(new SystemConfig
        {
            ConfigKey = "ReservationPickupDays",
            ConfigValue = "4"
        });

        await database.Context.SaveChangesAsync();

        var count = await new ReservationQueueService(database.Context)
            .ProcessExpiredReservationsAsync(now, CancellationToken.None);

        Assert.Equal(1, count);

        await database.Context.Entry(expiredReady).ReloadAsync();
        await database.Context.Entry(firstPending).ReloadAsync();
        await database.Context.Entry(secondPending).ReloadAsync();

        Assert.Equal(RecordStatuses.Expired, expiredReady.Status);
        Assert.Equal(RecordStatuses.ReadyForPickup, firstPending.Status);
        Assert.Equal(now.AddDays(4), firstPending.ExpireAt);
        Assert.Equal(RecordStatuses.Pending, secondPending.Status);

        var outboxes = await database.Context.NotificationOutboxes
            .OrderBy(item => item.EventType)
            .ToListAsync();

        Assert.Equal(2, outboxes.Count);

        Assert.Contains(outboxes, item =>
            item.ReservationId == expiredReady.ReservationId
            && item.EventType == "ReservationExpired");

        Assert.Contains(outboxes, item =>
            item.ReservationId == firstPending.ReservationId
            && item.EventType == "ReservationReady");
    }

    [Fact]
    public async Task PromoteNextAsync_does_not_promote_when_active_pickup_exists()
    {
        await using var database = TestDatabase.Create();

        var (_, activeReader) = await database.AddReaderAsync("active-reader");
        var (_, pendingReader) = await database.AddReaderAsync("waiting-reader");
        var (_, book, _) = await database.AddBookAsync("Active pickup book");

        var now = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

        var activeReservation = await database.AddReservationAsync(
            activeReader,
            book,
            RecordStatuses.ReadyForPickup,
            now.AddDays(-1),
            now.AddDays(2));

        var pendingReservation = await database.AddReservationAsync(
            pendingReader,
            book,
            RecordStatuses.Pending,
            now,
            now.AddDays(5));

        await new ReservationQueueService(database.Context)
            .PromoteNextAsync(book.BookId, now, CancellationToken.None);

        await database.Context.Entry(activeReservation).ReloadAsync();
        await database.Context.Entry(pendingReservation).ReloadAsync();

        Assert.Equal(RecordStatuses.ReadyForPickup, activeReservation.Status);
        Assert.Equal(RecordStatuses.Pending, pendingReservation.Status);
        Assert.Empty(await database.Context.NotificationOutboxes.ToListAsync());
    }

    [Fact]
    public async Task ProcessExpiredReservationsAsync_does_not_create_ready_event_when_queue_is_empty()
    {
        await using var database = TestDatabase.Create();

        var (_, reader) = await database.AddReaderAsync("last-expired-reader");
        var (_, book, _) = await database.AddBookAsync("Empty queue book");

        var now = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

        var expiredReservation = await database.AddReservationAsync(
            reader,
            book,
            RecordStatuses.ReadyForPickup,
            now.AddDays(-2),
            now.AddMinutes(-1));

        var count = await new ReservationQueueService(database.Context)
            .ProcessExpiredReservationsAsync(now, CancellationToken.None);

        Assert.Equal(1, count);

        await database.Context.Entry(expiredReservation).ReloadAsync();

        Assert.Equal(RecordStatuses.Expired, expiredReservation.Status);

        var outboxes = await database.Context.NotificationOutboxes.ToListAsync();

        Assert.Single(outboxes);
        Assert.Equal("ReservationExpired", outboxes[0].EventType);
        Assert.DoesNotContain(outboxes, item => item.EventType == "ReservationReady");
    }

    [Fact]
    public async Task ProcessExpiredReservationsAsync_is_idempotent_for_expired_pending_requests()
    {
        await using var database = TestDatabase.Create();

        var (_, reader) = await database.AddReaderAsync("idempotent-reader");
        var (_, book, _) = await database.AddBookAsync("Idempotent queue book");

        var now = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

        var reservation = await database.AddReservationAsync(
            reader,
            book,
            RecordStatuses.Pending,
            now.AddDays(-2),
            now.AddMinutes(-1));

        var service = new ReservationQueueService(database.Context);

        var firstCount = await service.ProcessExpiredReservationsAsync(
            now, CancellationToken.None);

        var secondCount = await service.ProcessExpiredReservationsAsync(
            now.AddMinutes(1), CancellationToken.None);

        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);

        await database.Context.Entry(reservation).ReloadAsync();

        Assert.Equal(RecordStatuses.Expired, reservation.Status);

        var outboxes = await database.Context.NotificationOutboxes.ToListAsync();

        Assert.Single(outboxes);
        Assert.Equal(
            $"reservation:{reservation.ReservationId}:ReservationExpired",
            outboxes[0].DeduplicationKey);
    }

    private static Reader CreateReader(string username, string cardNo) => new()
    {
        Account = new Account
        {
            Username = username,
            PasswordHash = "test-hash",
            Email = $"{username}@example.test",
            FullName = username,
            Role = AccountRoles.Borrower
        },
        CardNo = cardNo,
        CardIssueDate = new DateOnly(2026, 1, 1),
        CardExpiryDate = new DateOnly(2027, 1, 1),
        CardStatus = RecordStatuses.Active,
        BorrowLimit = 5
    };
}
