
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LiMS.Api.Services;

/// <summary>
/// Tự động kiểm tra và xử lý các yêu cầu đặt giữ sách đã hết hạn.
/// </summary>
public sealed class XuLyHetHanDatGiuSachBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<XuLyHetHanDatGiuSachBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan ChuKyXuLy = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Dịch vụ tự động xử lý hết hạn đặt giữ sách đã khởi động.");

        using var timer = new PeriodicTimer(ChuKyXuLy);

        // Chờ một phút trước lần quét đầu tiên để API khởi động ổn định.
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await XuLyDatGiuHetHanAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // API đang tắt, kết thúc Worker bình thường.
        }

        logger.LogInformation(
            "Dịch vụ tự động xử lý hết hạn đặt giữ sách đã dừng.");
    }

    private async Task XuLyDatGiuHetHanAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            // Tạo scope riêng để sử dụng các dịch vụ Scoped như DbContext.
            await using var scope = scopeFactory.CreateAsyncScope();

            var reservationQueue = scope.ServiceProvider
                .GetRequiredService<IReservationQueueService>();

            var now = DateTime.UtcNow;

            var processedCount =
                await reservationQueue.ProcessExpiredReservationsAsync(
                    now,
                    cancellationToken);

            if (processedCount > 0)
            {
                logger.LogInformation(
                    "Đã tự động xử lý {Count} yêu cầu đặt giữ sách hết hạn.",
                    processedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Không ghi nhận việc API dừng như một lỗi xử lý.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Lỗi khi tự động xử lý các yêu cầu đặt giữ sách hết hạn.");
        }
    }
}
