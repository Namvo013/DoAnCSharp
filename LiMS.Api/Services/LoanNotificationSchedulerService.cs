using System.Globalization;
using LiMS.Api.Data;
using LiMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Services;

/// <summary>
/// Tạo thông báo nhắc hạn và quá hạn cho các khoản mượn chưa trả.
/// </summary>
public sealed class LoanNotificationSchedulerService(
    LiMSDbContext db,
    ILogger<LoanNotificationSchedulerService> logger)
{
    public async Task<int> ProcessRemindersAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now);
        var threeDaysLater = today.AddDays(3);
        var oneDayLater = today.AddDays(1);

        var loans = await db.Loans
            .AsNoTracking()
            .Include(item => item.Copy)
                .ThenInclude(copy => copy.Book)
            .Where(item =>
                item.ReturnDate == null &&
                item.DueDate <= threeDaysLater)
            .ToListAsync(cancellationToken);

        var createdCount = 0;

        foreach (var loan in loans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? title = null;
            string? message = null;
            string? type = null;
            string? milestone = null;

            var bookTitle = loan.Copy.Book.Title;
            var dueDateText = loan.DueDate.ToString(
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture);

            if (loan.DueDate == threeDaysLater)
            {
                title = "Sắp đến hạn trả sách";
                message =
                    $"Sách \"{bookTitle}\" sẽ đến hạn trả sau 3 ngày " +
                    $"(ngày {dueDateText}). Vui lòng chuẩn bị trả sách đúng hạn.";
                type = "DueSoon";
                milestone = "before3";
            }
            else if (loan.DueDate == oneDayLater)
            {
                title = "Ngày mai đến hạn trả sách";
                message =
                    $"Sách \"{bookTitle}\" sẽ đến hạn trả vào ngày mai " +
                    $"({dueDateText}). Vui lòng trả sách đúng hạn.";
                type = "DueSoon";
                milestone = "before1";
            }
            else if (loan.DueDate < today)
            {
                var overdueDays = today.DayNumber - loan.DueDate.DayNumber;

                title = "Thông báo sách đã quá hạn";
                message =
                    $"Sách \"{bookTitle}\" đã quá hạn {overdueDays} ngày " +
                    $"(hạn trả {dueDateText}). Vui lòng hoàn trả sách sớm.";
                type = "Overdue";
                milestone = $"overdue:{today:yyyy-MM-dd}";
            }

            if (title is null || message is null ||
                type is null || milestone is null)
            {
                continue;
            }

            var dueDateKey = loan.DueDate.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            var deduplicationKey =
                $"loan:{loan.LoanId}:due:{dueDateKey}:{milestone}";

            var alreadyExists = await db.ReaderNotifications
                .AnyAsync(
                    item => item.DeduplicationKey == deduplicationKey,
                    cancellationToken);

            if (alreadyExists)
                continue;

            db.ReaderNotifications.Add(new ReaderNotification
            {
                ReaderId = loan.ReaderId,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityType = "Loan",
                RelatedEntityId = loan.LoanId,
                IsRead = false,
                CreatedAt = now,
                DeduplicationKey = deduplicationKey
            });

            createdCount++;
        }

        if (createdCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Đã tạo {Count} thông báo nhắc hạn và quá hạn mượn sách.",
                createdCount);
        }

        return createdCount;
    }
}
