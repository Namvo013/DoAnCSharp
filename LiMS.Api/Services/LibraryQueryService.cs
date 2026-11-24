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

    public async Task<PagedResponse<BookResponse>> GetPublicBooksPageAsync(
        string? search,
        string? category,
        string? availability,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var skip = GetPageSkip(page, pageSize);
        var query = db.Books.AsNoTracking().Where(book => book.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(book =>
                book.Title.Contains(term)
                || book.Author.Contains(term)
                || (book.Isbn != null && book.Isbn.Contains(term))
                || book.Category.Name.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(book => book.Category.Name == category);

        if (availability == "available")
            query = query.Where(book => book.Copies.Any(copy => copy.Status == RecordStatuses.Available));
        else if (availability == "unavailable")
            query = query.Where(book => !book.Copies.Any(copy => copy.Status == RecordStatuses.Available));

        var totalCount = await query.CountAsync(cancellationToken);
        var ordered = sort == "title"
            ? query.OrderBy(book => book.Title).ThenBy(book => book.BookId)
            : query.OrderByDescending(book => book.PublishYear).ThenBy(book => book.Title).ThenBy(book => book.BookId);
        var items = await ordered.Skip(skip).Take(pageSize)
            .Select(MapBook())
            .ToListAsync(cancellationToken);
        return new PagedResponse<BookResponse>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<string>> GetPublicBookCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Books.AsNoTracking()
            .Where(book => book.IsActive)
            .Select(book => book.Category.Name)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync(cancellationToken);

    public Task<BookResponse?> GetPublicBookAsync(int bookId, CancellationToken cancellationToken) =>
        db.Books.AsNoTracking()
            .Where(book => book.BookId == bookId && book.IsActive)
            .Select(MapBook())
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<BookCoverData?> GetBookCoverAsync(int bookId, CancellationToken cancellationToken)
    {
        var cover = await db.Books.AsNoTracking()
            .Where(book => book.BookId == bookId)
            .Select(book => new { book.CoverImageData, book.CoverImageContentType })
            .SingleOrDefaultAsync(cancellationToken);
        return cover?.CoverImageData is { Length: > 0 } data
            && cover.CoverImageContentType is { } contentType
                ? new BookCoverData(data, contentType)
                : null;
    }

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
                reader.Account.Role,
                reader.Account.Phone,
                reader.CardExpiryDate,
                reader.CardStatus);
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
                    loan.Copy.Book.Copies.Count(copy => copy.Status == RecordStatuses.OnLoan),
                    loan.Copy.Book.ShelfLocation,
                    loan.Copy.Book.Description,
                    loan.Copy.Book.CoverImageContentType == null
                        ? null
                        : "/api/library/books/" + loan.Copy.Book.BookId + "/cover")
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
                    item.Book.Copies.Count(copy => copy.Status == RecordStatuses.OnLoan),
                    item.Book.ShelfLocation,
                    item.Book.Description,
                    item.Book.CoverImageContentType == null
                        ? null
                        : "/api/library/books/" + item.Book.BookId + "/cover")
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

    public async Task<IReadOnlyList<ReaderNotificationResponse>>
    GetReaderNotificationsAsync(
        int readerId,
        CancellationToken cancellationToken)
    {
        // Chỉ lấy thông báo thuộc về độc giả đang được truy vấn.
        var notifications = await db.ReaderNotifications
            .AsNoTracking()
            .Where(item => item.ReaderId == readerId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                item.NotificationId,
                item.Title,
                item.Message,
                item.Type,
                item.CreatedAt,
                item.IsRead
            })
            .ToListAsync(cancellationToken);

        // Chuyển entity sang DTO trả về cho API.
        return notifications.Select(item =>
            new ReaderNotificationResponse(
                item.NotificationId,
                item.Title,
                item.Message,
                item.Type,
                ToUtcOffset(item.CreatedAt),
                item.IsRead))
            .ToArray();
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
            book.Copies.Count(copy => copy.Status == RecordStatuses.OnLoan),
            book.ShelfLocation,
            book.Description,
            book.CoverImageContentType == null
                ? null
                : "/api/library/books/" + book.BookId + "/cover");

    private static int GetPageSkip(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new ArgumentException("Page phải từ 1 trở lên, pageSize phải từ 1 đến 100.");

        return (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
    }

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
