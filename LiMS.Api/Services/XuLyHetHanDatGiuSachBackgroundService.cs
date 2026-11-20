using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LiMS.Api.Services;

/// <summary>
/// Worker định kỳ xử lý đặt giữ hết hạn và các thông báo nội bộ.
/// </summary>
public sealed class XuLyHetHanDatGiuSachBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<XuLyHetHanDatGiuSachBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan ChuKyXuLy =
        TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Dịch vụ xử lý đặt giữ hết hạn và thông báo đã khởi động.");

        using var timer = new PeriodicTimer(ChuKyXuLy);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Sử dụng chung một thời điểm UTC cho toàn bộ chu kỳ xử lý.
                var now = DateTime.UtcNow;

                await XuLyDatGiuHetHanAsync(now, stoppingToken);
                await XuLyOutboxAsync(now, stoppingToken);
                await XuLyNhacHanMuonSachAsync(now, stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // API đang tắt, kết thúc Worker bình thường.
        }

        logger.LogInformation(
            "Dịch vụ xử lý đặt giữ hết hạn và thông báo đã dừng.");
    }

    private async Task XuLyDatGiuHetHanAsync(
    DateTime now,
    CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var reservationQueue = scope.ServiceProvider
                .GetRequiredService<IReservationQueueService>();

            var processedCount =
                await reservationQueue.ProcessExpiredReservationsAsync(
                    now,
                    cancellationToken);

            if (processedCount > 0)
            {
                logger.LogInformation(
                    "Đã xử lý {Count} yêu cầu đặt giữ hết hạn.",
                    processedCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Lỗi khi xử lý đặt giữ sách hết hạn.");
        }
    }

    private async Task XuLyOutboxAsync(
    DateTime now,
    CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var processor = scope.ServiceProvider
                .GetRequiredService<NotificationOutboxProcessorService>();

            var processedCount =
                await processor.ProcessPendingAsync(
                    now,
                    cancellationToken);

            if (processedCount > 0)
            {
                logger.LogInformation(
                    "Đã chuyển {Count} sự kiện Outbox thành thông báo.",
                    processedCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Lỗi khi xử lý hàng đợi thông báo Outbox.");
        }
    }

    private async Task XuLyNhacHanMuonSachAsync(
    DateTime now,
    CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var scheduler = scope.ServiceProvider
                .GetRequiredService<LoanNotificationSchedulerService>();

            var createdCount =
                await scheduler.ProcessRemindersAsync(
                    now,
                    cancellationToken);

            if (createdCount > 0)
            {
                logger.LogInformation(
                    "Đã tạo {Count} thông báo nhắc hạn/quá hạn mượn sách.",
                    createdCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Lỗi khi tạo thông báo nhắc hạn và quá hạn mượn sách.");
        }
    }
}
