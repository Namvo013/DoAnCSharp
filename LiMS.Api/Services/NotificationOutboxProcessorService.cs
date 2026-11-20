using LiMS.Api.Data;
using LiMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Services;

/// <summary>
/// Đọc sự kiện từ Outbox và chuyển thành thông báo nội bộ cho độc giả.
/// </summary>
public sealed class NotificationOutboxProcessorService(
    LiMSDbContext db,
    ILogger<NotificationOutboxProcessorService> logger)
{
    public async Task<int> ProcessPendingAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var pendingIds = await db.NotificationOutboxes
            .AsNoTracking()
            .Where(item => item.ProcessedAt == null)
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.OutboxId)
            .Take(100)
            .ToListAsync(cancellationToken);

        var processedCount = 0;

        foreach (var outboxId in pendingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var transaction =
                    await db.Database.BeginTransactionAsync(cancellationToken);

                var item = await db.NotificationOutboxes
                    .SingleOrDefaultAsync(
                        x => x.OutboxId == outboxId,
                        cancellationToken);

                if (item is null || item.ProcessedAt is not null)
                    continue;

                var reservation = await db.Reservations
                    .AsNoTracking()
                    .Include(x => x.Book)
                    .SingleOrDefaultAsync(
                        x => x.ReservationId == item.ReservationId,
                        cancellationToken);

                // Nếu Reservation không còn tồn tại, bỏ qua sự kiện để tránh retry vô hạn.
                if (reservation is null)
                {
                    logger.LogWarning(
                        "Reservation {ReservationId} không tồn tại. Bỏ qua Outbox {OutboxId}.",
                        item.ReservationId,
                        item.OutboxId);

                    item.ProcessedAt = now;
                    item.LastError = $"Reservation {item.ReservationId} không tồn tại.";

                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    processedCount++;
                    continue;
                }

                var bookTitle = reservation.Book.Title;

                var (title, message, type) = item.EventType switch
                {
                    "ReservationReady" => (
                        "Sách đã sẵn sàng nhận",
                        $"Sách \"{bookTitle}\" đã sẵn sàng để nhận. " +
                        $"Hạn nhận: {reservation!.ExpireAt:dd/MM/yyyy HH:mm}.",
                        "ReservationReady"),

                    "ReservationExpired" => (
                        "Yêu cầu đặt giữ đã hết hạn",
                        $"Yêu cầu đặt giữ sách \"{bookTitle}\" đã hết hạn.",
                        "ReservationExpired"),

                    "ReservationCancelled" => (
                        "Yêu cầu đặt giữ đã được hủy",
                        $"Yêu cầu đặt giữ sách \"{bookTitle}\" đã được hủy.",
                        "ReservationCancelled"),

                    _ => throw new InvalidOperationException(
                        $"Loại sự kiện Outbox không được hỗ trợ: {item.EventType}")
                };

                var alreadyExists = await db.ReaderNotifications
                    .AnyAsync(
                        x => x.DeduplicationKey == item.DeduplicationKey,
                        cancellationToken);

                if (!alreadyExists)
                {
                    db.ReaderNotifications.Add(new ReaderNotification
                    {
                        ReaderId = item.ReaderId,
                        Title = title,
                        Message = message,
                        Type = type,
                        RelatedEntityType = "Reservation",
                        RelatedEntityId = item.ReservationId,
                        IsRead = false,
                        CreatedAt = now,
                        DeduplicationKey = item.DeduplicationKey
                    });
                }

                item.ProcessedAt = now;
                item.LastError = null;

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                processedCount++;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Làm sạch trạng thái EF sau khi transaction thất bại.
                db.ChangeTracker.Clear();

                var failedItem = await db.NotificationOutboxes
                    .SingleOrDefaultAsync(
                        x => x.OutboxId == outboxId,
                        cancellationToken);

                if (failedItem is not null &&
                    failedItem.ProcessedAt is null)
                {
                    failedItem.Attempts++;
                    failedItem.LastError =
                        exception.Message.Length > 1000
                            ? exception.Message[..1000]
                            : exception.Message;

                    await db.SaveChangesAsync(cancellationToken);
                }

                logger.LogError(
                    exception,
                    "Không thể xử lý Outbox {OutboxId}.",
                    outboxId);
            }
        }

        return processedCount;
    }
}
