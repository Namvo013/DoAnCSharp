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
        var bookId = book.BookId;
        var service = new ReservationQueueService(db);

        await service.PromoteNextAsync(bookId, now, CancellationToken.None);
        await db.SaveChangesAsync();

        var reservations = await db.Reservations.OrderBy(item => item.CreatedAt).ToListAsync();
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

        await new ReservationQueueService(db).PromoteNextAsync(book.BookId, now, CancellationToken.None);

        Assert.Equal(RecordStatuses.ReadyForPickup, reservation.Status);
        Assert.Equal(now.AddDays(3), reservation.ExpireAt);
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
