
using System.Data;
using LiMS.Api.Data;
using LiMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Services;

public interface IReservationQueueService
{
    Task PromoteNextAsync(
        int bookId,
        DateTime now,
        CancellationToken cancellationToken);

    Task<int> ProcessExpiredReservationsAsync(
        DateTime now,
        CancellationToken cancellationToken);
}

public sealed class ReservationQueueService(LiMSDbContext db)
    : IReservationQueueService
{
    /// <summary>
    /// Đánh dấu các yêu cầu hết hạn và chuyển lượt cho độc giả tiếp theo.
    /// Phương thức này được gọi trong transaction của nghiệp vụ trả sách
    /// hoặc hủy đặt giữ.
    /// </summary>
    public async Task PromoteNextAsync(
        int bookId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Bước 1: Đánh dấu các yêu cầu Pending và ReadyForPickup đã hết hạn.
        var expiredReservations = await db.Reservations
            .Where(item => item.BookId == bookId
                && (item.Status == RecordStatuses.Pending
                    || item.Status == RecordStatuses.ReadyForPickup)
                && item.ExpireAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var expired in expiredReservations)
        {
            expired.Status = RecordStatuses.Expired;
        }

        // Bước 2: Không cấp lượt mới nếu vẫn còn người được ưu tiên nhận sách.
        var hasActivePickup = await db.Reservations.AnyAsync(
            item => item.BookId == bookId
                && item.Status == RecordStatuses.ReadyForPickup
                && item.ExpireAt > now,
            cancellationToken);

        if (hasActivePickup)
            return;

        // Bước 3: Tìm yêu cầu Pending hợp lệ có thời gian đặt sớm nhất.
        var next = await db.Reservations
            .Where(item => item.BookId == bookId
                && item.Status == RecordStatuses.Pending
                && item.ExpireAt > now)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (next is null)
            return;

        // Bước 4: Đọc thời hạn nhận sách từ cấu hình hệ thống.
        var pickupDaysText = await db.SystemConfigs
            .Where(config => config.ConfigKey == "ReservationPickupDays")
            .Select(config => config.ConfigValue)
            .SingleOrDefaultAsync(cancellationToken);

        var pickupDays =
            int.TryParse(pickupDaysText, out var value)
            && value is >= 1 and <= 30
                ? value
                : 3;

        // Bước 5: Chuyển yêu cầu tiếp theo sang trạng thái chờ nhận sách.
        next.Status = RecordStatuses.ReadyForPickup;
        next.ExpireAt = now.AddDays(pickupDays);
    }

    /// <summary>
    /// Quét và xử lý các yêu cầu đặt giữ đã hết hạn.
    /// Mỗi đầu sách được xử lý trong một transaction riêng.
    /// </summary>
    public async Task<int> ProcessExpiredReservationsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var processedCount = 0;

        // Bước 1: Đánh dấu các yêu cầu Pending đã hết hạn.
        var expiredPending = await db.Reservations
            .Where(item => item.Status == RecordStatuses.Pending
                && item.ExpireAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var reservation in expiredPending)
        {
            reservation.Status = RecordStatuses.Expired;
        }

        await db.SaveChangesAsync(cancellationToken);
        processedCount += expiredPending.Count;

        // Bước 2: Lấy danh sách đầu sách có lượt nhận sách đã hết hạn.
        var bookIds = await db.Reservations
            .Where(item => item.Status == RecordStatuses.ReadyForPickup
                && item.ExpireAt <= now)
            .Select(item => item.BookId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Bước 3: Xử lý từng đầu sách trong transaction riêng.
        foreach (var bookId in bookIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            // Kiểm tra lại vì có thể thao tác khác đã xử lý lượt này.
            var expiredReadyCount = await db.Reservations.CountAsync(
                item => item.BookId == bookId
                    && item.Status == RecordStatuses.ReadyForPickup
                    && item.ExpireAt <= now,
                cancellationToken);

            if (expiredReadyCount == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            // Chuyển lượt cho người tiếp theo trong hàng đợi.
            await PromoteNextAsync(bookId, now, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            processedCount += expiredReadyCount;
        }

        return processedCount;
    }
}
