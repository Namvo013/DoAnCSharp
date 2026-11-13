using LiMS.Api.Contracts;
using LiMS.Api.Data;
using LiMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Services;

public sealed class LibraryQueryService(LiMSDbContext db) : ILibraryQueryService
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        db.Database.CanConnectAsync(cancellationToken);

    public async Task<IReadOnlyList<BookResponse>> GetPublicBooksAsync(CancellationToken cancellationToken) =>
        await db.Books.AsNoTracking()
            .Where(book => book.IsActive)
            .OrderBy(book => book.Title)
            .Select(MapBook())
            .ToListAsync(cancellationToken);

    public async Task<ReaderProfileData?> GetReaderProfileAsync(
        string studentCode,
        CancellationToken cancellationToken)
    {
        var reader = await db.Readers.AsNoTracking()
            .Include(item => item.Account)
            .OrderByDescending(item => item.CardNo == studentCode)
            .FirstOrDefaultAsync(
                item => item.CardNo == studentCode || item.Account.Username == studentCode,
                cancellationToken);
        return reader is null
            ? null
            : new ReaderProfileData(
                reader.ReaderId,
                reader.AccountId,
                reader.CardNo,
                reader.Account.FullName,
                reader.Account.Email,
                reader.Account.Role);
    }

    public async Task<IReadOnlyList<ReaderLoanResponse>> GetReaderLoansAsync(
        int readerId,
        CancellationToken cancellationToken)
    {
        var loans = await db.Loans.AsNoTracking()
            .Where(loan => loan.ReaderId == readerId)
            .OrderByDescending(loan => loan.BorrowDate)
            .Select(loan => new
            {
                loan.LoanId,
                UserId = loan.Reader.AccountId,
                BookId = loan.Copy.BookId,
                loan.BorrowDate,
                loan.DueDate,
                loan.ReturnDate,
                loan.Status,
                RenewalCount = (int)loan.RenewCount,
                Book = new BookResponse(
                    loan.Copy.Book.BookId,
                    loan.Copy.Book.Title,
                    loan.Copy.Book.Author,
                    loan.Copy.Book.Isbn,
                    loan.Copy.Book.Category.Name,
                    loan.Copy.Book.Publisher,
                    loan.Copy.Book.PublishYear,
                    loan.Copy.Book.Copies.Count,
                    loan.Copy.Book.Copies.Count(copy => copy.Status == RecordStatuses.Available),
                    loan.Copy.Book.ShelfLocation,
                    loan.Copy.Book.Description)
            })
            .ToListAsync(cancellationToken);

        return loans.Select(loan => new ReaderLoanResponse(
            loan.LoanId,
            loan.UserId,
            loan.BookId,
            ToUtcOffset(loan.BorrowDate),
            ToUtcOffset(loan.DueDate.ToDateTime(TimeOnly.MinValue)),
            loan.ReturnDate is null ? null : ToUtcOffset(loan.ReturnDate.Value),
            loan.Status,
            loan.RenewalCount,
            loan.Book)).ToArray();
    }

    public async Task<IReadOnlyList<ReaderReservationResponse>> GetReaderReservationsAsync(
        int readerId,
        CancellationToken cancellationToken)
    {
        var reservations = await db.Reservations.AsNoTracking()
            .Where(item => item.ReaderId == readerId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                item.ReservationId,
                item.BookId,
                item.CreatedAt,
                item.ExpireAt,
                item.Status,
                Book = new BookResponse(
                    item.Book.BookId,
                    item.Book.Title,
                    item.Book.Author,
                    item.Book.Isbn,
                    item.Book.Category.Name,
                    item.Book.Publisher,
                    item.Book.PublishYear,
                    item.Book.Copies.Count,
                    item.Book.Copies.Count(copy => copy.Status == RecordStatuses.Available),
                    item.Book.ShelfLocation,
                    item.Book.Description)
            })
            .ToListAsync(cancellationToken);

        return reservations.Select(item => new ReaderReservationResponse(
            item.ReservationId,
            item.BookId,
            ToUtcOffset(item.CreatedAt),
            ToUtcOffset(item.ExpireAt),
            item.Status,
            item.Book)).ToArray();
    }

    public async Task<IReadOnlyList<ReaderNotificationResponse>> GetReaderNotificationsAsync(
        int readerId,
        CancellationToken cancellationToken)
    {
        var emails = await db.EmailLogs.AsNoTracking()
            .Where(item => item.ReaderId == readerId)
            .OrderByDescending(item => item.SentAt)
            .Select(item => new { item.EmailId, item.EmailType, item.SentAt })
            .ToListAsync(cancellationToken);
        return emails.Select(item => new ReaderNotificationResponse(
            item.EmailId,
            item.EmailType,
            ToUtcOffset(item.SentAt),
            true)).ToArray();
    }

    private static System.Linq.Expressions.Expression<Func<Book, BookResponse>> MapBook() =>
        book => new BookResponse(
            book.BookId,
            book.Title,
            book.Author,
            book.Isbn,
            book.Category.Name,
            book.Publisher,
            book.PublishYear,
            book.Copies.Count,
            book.Copies.Count(copy => copy.Status == RecordStatuses.Available),
            book.ShelfLocation,
            book.Description);

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
