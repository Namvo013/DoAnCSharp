using LiMS.Api.Data;
using LiMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Services;

public interface IReservationQueueService
{
    Task PromoteNextAsync(int bookId, DateTime now, CancellationToken cancellationToken);
}

public sealed class ReservationQueueService(LiMSDbContext db) : IReservationQueueService
{
    public async Task PromoteNextAsync(int bookId, DateTime now, CancellationToken cancellationToken)
    {
        var expiredReservations = await db.Reservations
            .Where(item => item.BookId == bookId
                && (item.Status == RecordStatuses.Pending || item.Status == RecordStatuses.ReadyForPickup)
                && item.ExpireAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var expired in expiredReservations)
            expired.Status = RecordStatuses.Expired;

        var next = await db.Reservations
            .Where(item => item.BookId == bookId
                && item.Status == RecordStatuses.Pending
                && item.ExpireAt > now)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (next is null)
            return;

        var pickupDaysText = await db.SystemConfigs
            .Where(config => config.ConfigKey == "ReservationPickupDays")
            .Select(config => config.ConfigValue)
            .SingleOrDefaultAsync(cancellationToken);
        var pickupDays = int.TryParse(pickupDaysText, out var value) && value is >= 1 and <= 30
            ? value
            : 3;
        next.Status = RecordStatuses.ReadyForPickup;
        next.ExpireAt = now.AddDays(pickupDays);
    }
}
