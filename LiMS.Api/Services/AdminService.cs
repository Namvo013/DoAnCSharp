using System.Data;
using LiMS.Api.Contracts;
using LiMS.Api.Data;
using LiMS.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Services;

public sealed class AdminService(
    LiMSDbContext db,
    IPasswordHasher<Account> passwordHasher,
    IReservationQueueService reservationQueue) : IAdminService
{
    private static readonly HashSet<string> AllowedCopyStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Available", "Maintenance", "Lost" };

    private static readonly Dictionary<string, (string Value, string Description)> DefaultConfigs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["LoanDurationDays"] = ("14", "Số ngày mượn một lần."),
            ["MaxRenewals"] = ("1", "Số lần gia hạn tối đa cho mỗi phiếu mượn."),
            ["ReservationPickupDays"] = ("3", "Số ngày giữ sách sau khi có sách."),
            ["FinePerDay"] = ("5000", "Mức phạt quá hạn mỗi ngày (VND)."),
            ["MaxActiveLoans"] = ("5", "Giới hạn phiếu mượn đang mở khi không có giới hạn riêng trên thẻ.")
        };

    public async Task<IReadOnlyList<BookResponse>> GetBooksAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = db.Books.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(book =>
                book.Title.Contains(term)
                || book.Author.Contains(term)
                || (book.Isbn != null && book.Isbn.Contains(term))
                || book.Category.Name.Contains(term));
        }

        return await query.OrderBy(book => book.Title)
            .Select(book => new BookResponse(
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
                book.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<BookResponse?> GetBookAsync(int id, CancellationToken cancellationToken) =>
        await db.Books.AsNoTracking()
            .Where(book => book.BookId == id)
            .Select(book => new BookResponse(
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
                book.Description))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BookCopyResponse>> GetCopiesAsync(
        int bookId,
        CancellationToken cancellationToken) =>
        await db.BookCopies.AsNoTracking()
            .Where(copy => copy.BookId == bookId)
            .OrderBy(copy => copy.Barcode)
            .Select(copy => new BookCopyResponse(copy.CopyId, copy.BookId, copy.Barcode, copy.Status))
            .ToListAsync(cancellationToken);

    public async Task<BookResponse> CreateBookAsync(
        CreateBookRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var categoryName = request.Category.Trim();
        var category = await db.Categories.SingleOrDefaultAsync(
            item => item.Name == categoryName,
            cancellationToken);
        if (category is null)
        {
            category = new Category { Name = categoryName };
            db.Categories.Add(category);
        }

        var book = new Book
        {
            Category = category,
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Isbn = NormalizeOptional(request.Isbn),
            Publisher = NormalizeOptional(request.Publisher),
            PublishYear = request.PublishYear,
            ShelfLocation = NormalizeOptional(request.ShelfLocation),
            Description = NormalizeOptional(request.Description)
        };
        db.Books.Add(book);
        await db.SaveChangesAsync(cancellationToken);

        for (var index = 1; index <= request.InitialCopies; index++)
        {
            db.BookCopies.Add(new BookCopy
            {
                BookId = book.BookId,
                Barcode = $"B{book.BookId:D6}-{index:D4}",
                Status = RecordStatuses.Available
            });
        }

        AddAudit(actorId, "Create", "BOOK", book.BookId, book.Title);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetBookAsync(book.BookId, cancellationToken))!;
    }

    public async Task<BookResponse> UpdateBookAsync(
        int id,
        UpdateBookRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        var book = await db.Books.Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.BookId == id, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đầu sách.");
        var categoryName = request.Category.Trim();
        var category = await db.Categories.SingleOrDefaultAsync(
            item => item.Name == categoryName,
            cancellationToken);
        if (category is null)
        {
            category = new Category { Name = categoryName };
            db.Categories.Add(category);
        }

        book.Category = category;
        book.Title = request.Title.Trim();
        book.Author = request.Author.Trim();
        book.Isbn = NormalizeOptional(request.Isbn);
        book.Publisher = NormalizeOptional(request.Publisher);
        book.PublishYear = request.PublishYear;
        book.ShelfLocation = NormalizeOptional(request.ShelfLocation);
        book.Description = NormalizeOptional(request.Description);
        book.IsActive = request.IsActive;
        AddAudit(actorId, "Update", "BOOK", id, book.Title);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetBookAsync(id, cancellationToken))!;
    }

    public async Task AddCopiesAsync(
        int bookId,
        CreateCopiesRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        if (!await db.Books.AnyAsync(book => book.BookId == bookId, cancellationToken))
            throw new KeyNotFoundException("Không tìm thấy đầu sách.");

        var prefix = request.BarcodePrefix.Trim();
        var existingBarcodes = await db.BookCopies
            .Where(copy => copy.Barcode.StartsWith(prefix + "-"))
            .Select(copy => copy.Barcode)
            .ToListAsync(cancellationToken);
        var firstIndex = existingBarcodes
            .Select(barcode => int.TryParse(barcode[(prefix.Length + 1)..], out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        var copies = Enumerable.Range(firstIndex, request.Quantity)
            .Select(index => new BookCopy
            {
                BookId = bookId,
                Barcode = $"{prefix}-{index:D4}",
                Status = RecordStatuses.Available
            })
            .ToArray();
        db.BookCopies.AddRange(copies);
        AddAudit(actorId, "CreateCopies", "BOOK", bookId, $"Số bản sao: {request.Quantity}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateCopyStatusAsync(
        int copyId,
        CopyStatusRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        if (!AllowedCopyStatuses.Contains(request.Status))
            throw new ArgumentException("Trạng thái bản sao không hợp lệ.");

        var copy = await db.BookCopies.SingleOrDefaultAsync(item => item.CopyId == copyId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy bản sao sách.");
        if (copy.Status == RecordStatuses.OnLoan)
            throw new BusinessRuleException("Không thể đổi trạng thái bản sao đang được mượn.");

        copy.Status = request.Status;
        AddAudit(actorId, "UpdateCopyStatus", "BOOK_COPY", copyId, request.Status);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReaderResponse>> GetReadersAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = db.Readers.AsNoTracking().Include(reader => reader.Account).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(reader =>
                reader.CardNo.Contains(term)
                || reader.Account.Username.Contains(term)
                || reader.Account.FullName.Contains(term)
                || reader.Account.Email.Contains(term));
        }

        var readers = await query.OrderBy(reader => reader.Account.FullName).ToListAsync(cancellationToken);
        return readers.Select(MapReader).ToArray();
    }

    public async Task<ReaderResponse?> GetReaderAsync(int readerId, CancellationToken cancellationToken)
    {
        var reader = await db.Readers.AsNoTracking().Include(item => item.Account)
            .SingleOrDefaultAsync(item => item.ReaderId == readerId, cancellationToken);
        return reader is null ? null : MapReader(reader);
    }

    public async Task<ReaderResponse> CreateReaderAsync(
        CreateReaderRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        var account = new Account
        {
            Username = request.Username.Trim(),
            PasswordHash = string.Empty,
            Email = request.Email.Trim().ToLowerInvariant(),
            FullName = request.FullName.Trim(),
            Phone = NormalizeOptional(request.Phone),
            Role = AccountRoles.Borrower
        };
        account.PasswordHash = passwordHasher.HashPassword(account, request.InitialPassword);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reader = new Reader
        {
            Account = account,
            CardNo = request.CardNo.Trim(),
            CardIssueDate = today,
            CardExpiryDate = today.AddMonths(request.CardValidityMonths),
            CardStatus = RecordStatuses.Active,
            BorrowLimit = request.BorrowLimit
        };
        db.Readers.Add(reader);
        await db.SaveChangesAsync(cancellationToken);
        AddAudit(actorId, "Create", "READER", reader.ReaderId, reader.CardNo);
        await db.SaveChangesAsync(cancellationToken);
        return MapReader(reader);
    }

    public async Task<ReaderResponse> UpdateReaderAsync(
        int readerId,
        UpdateReaderRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        var reader = await db.Readers.Include(item => item.Account)
            .SingleOrDefaultAsync(item => item.ReaderId == readerId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy độc giả.");
        if (!string.Equals(request.CardStatus, RecordStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.CardStatus, RecordStatuses.Inactive, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Trạng thái thẻ chỉ được là Active hoặc Inactive.");

        reader.Account.FullName = request.FullName.Trim();
        reader.Account.Email = request.Email.Trim().ToLowerInvariant();
        reader.Account.Phone = NormalizeOptional(request.Phone);
        reader.Account.IsActive = request.IsActive;
        reader.CardStatus = request.CardStatus;
        reader.BorrowLimit = request.BorrowLimit;
        reader.CardExpiryDate = request.CardExpiryDate;
        AddAudit(actorId, "Update", "READER", readerId, reader.CardNo);
        await db.SaveChangesAsync(cancellationToken);
        return MapReader(reader);
    }

    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counts = await db.Loans.AsNoTracking()
            .GroupBy(loan => loan.Copy.BookId)
            .Select(group => new { BookId = group.Key, LoanCount = group.Count() })
            .OrderByDescending(item => item.LoanCount)
            .Take(5)
            .ToListAsync(cancellationToken);
        var bookIds = counts.Select(item => item.BookId).ToArray();
        var titles = await db.Books.AsNoTracking()
            .Where(book => bookIds.Contains(book.BookId))
            .Select(book => new { book.BookId, book.Title })
            .ToDictionaryAsync(book => book.BookId, book => book.Title, cancellationToken);
        var popular = counts
            .Select(item => new PopularBookResponse(item.BookId, titles[item.BookId], item.LoanCount))
            .ToArray();

        return new DashboardResponse(
            await db.Books.CountAsync(book => book.IsActive, cancellationToken),
            await db.BookCopies.CountAsync(cancellationToken),
            await db.BookCopies.CountAsync(copy => copy.Status == RecordStatuses.Available, cancellationToken),
            await db.Readers.CountAsync(reader => reader.Account.IsActive && reader.CardStatus == RecordStatuses.Active, cancellationToken),
            await db.Loans.CountAsync(loan => loan.ReturnDate == null, cancellationToken),
            await db.Loans.CountAsync(loan => loan.ReturnDate == null && loan.DueDate < today, cancellationToken),
            await db.Reservations.CountAsync(reservation =>
                reservation.Status == RecordStatuses.Pending || reservation.Status == RecordStatuses.ReadyForPickup,
                cancellationToken),
            await db.PurchaseProposals.CountAsync(proposal => proposal.Status == RecordStatuses.Pending, cancellationToken),
            await db.Fines.Where(fine => fine.Status != RecordStatuses.Paid).SumAsync(fine => (decimal?)fine.Amount, cancellationToken) ?? 0,
            await db.Fines.Where(fine => fine.Status == RecordStatuses.Paid).SumAsync(fine => (decimal?)fine.Amount, cancellationToken) ?? 0,
            popular);
    }

    public async Task<IReadOnlyList<LoanResponse>> GetLoansAsync(string? status, CancellationToken cancellationToken)
    {
        var query = LoanQuery();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(loan => loan.Status == status);

        var loans = await query.OrderByDescending(loan => loan.BorrowDate)
            .Take(1000)
            .ToListAsync(cancellationToken);
        return loans.Select(MapLoan).ToArray();
    }

    public async Task<LoanResponse?> GetLoanAsync(long loanId, CancellationToken cancellationToken)
    {
        var loan = await LoanQuery().SingleOrDefaultAsync(item => item.LoanId == loanId, cancellationToken);
        return loan is null ? null : MapLoan(loan);
    }

    public async Task<LoanResponse> CheckoutAsync(
        CheckoutRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reader = await db.Readers.Include(item => item.Account)
            .SingleOrDefaultAsync(item => item.CardNo == request.ReaderCardNo, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy thẻ độc giả.");
        if (!reader.Account.IsActive
            || reader.CardStatus != RecordStatuses.Active
            || reader.CardExpiryDate < today)
            throw new BusinessRuleException("Thẻ độc giả không còn hiệu lực.");

        var openLoans = await db.Loans.CountAsync(
            loan => loan.ReaderId == reader.ReaderId && loan.ReturnDate == null,
            cancellationToken);
        var globalMaximumLoans = await GetIntConfigAsync("MaxActiveLoans", 5, cancellationToken);
        var maximumLoans = reader.BorrowLimit > 0
            ? Math.Min(reader.BorrowLimit, globalMaximumLoans)
            : globalMaximumLoans;
        if (openLoans >= maximumLoans)
            throw new BusinessRuleException("Độc giả đã đạt giới hạn sách đang mượn.");

        var outstandingFines = await db.Fines.AnyAsync(
            fine => fine.Loan.ReaderId == reader.ReaderId && fine.Status != RecordStatuses.Paid,
            cancellationToken);
        if (outstandingFines)
            throw new BusinessRuleException("Độc giả còn khoản phạt chưa thanh toán.");

        var copy = await db.BookCopies.Include(item => item.Book)
            .SingleOrDefaultAsync(item => item.Barcode == request.Barcode, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy mã vạch sách.");
        if (copy.Status != RecordStatuses.Available || !copy.Book.IsActive)
            throw new BusinessRuleException("Bản sao sách hiện không sẵn sàng cho mượn.");

        Reservation? reservation = null;
        if (request.ReservationId.HasValue)
        {
            reservation = await db.Reservations.SingleOrDefaultAsync(
                item => item.ReservationId == request.ReservationId.Value
                    && item.ReaderId == reader.ReaderId
                    && item.BookId == copy.BookId
                    && (item.Status == RecordStatuses.Pending || item.Status == RecordStatuses.ReadyForPickup),
                cancellationToken)
                ?? throw new BusinessRuleException("Phiếu đặt giữ không hợp lệ cho độc giả hoặc đầu sách này.");
        }
        else
        {
            reservation = await db.Reservations
                .Where(item => item.ReaderId == reader.ReaderId
                    && item.BookId == copy.BookId
                    && item.ExpireAt > DateTime.UtcNow
                    && (item.Status == RecordStatuses.Pending || item.Status == RecordStatuses.ReadyForPickup))
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var earlierReservationExists = await db.Reservations.AnyAsync(
            item => item.BookId == copy.BookId
                && item.ReaderId != reader.ReaderId
                && (item.Status == RecordStatuses.Pending || item.Status == RecordStatuses.ReadyForPickup)
                && item.ExpireAt > DateTime.UtcNow
                && (reservation == null || item.CreatedAt < reservation.CreatedAt),
            cancellationToken);
        if (earlierReservationExists)
            throw new BusinessRuleException("Đầu sách đang được giữ cho độc giả khác theo thứ tự đặt trước.");

        var librarianId = await GetLibrarianIdAsync(actorId, cancellationToken);
        var duration = await GetIntConfigAsync("LoanDurationDays", 14, cancellationToken);
        copy.Status = RecordStatuses.OnLoan;
        if (reservation is not null)
            reservation.Status = "Fulfilled";

        var loan = new Loan
        {
            ReaderId = reader.ReaderId,
            CopyId = copy.CopyId,
            LibrarianId = librarianId,
            ReservationId = reservation?.ReservationId,
            BorrowDate = DateTime.UtcNow,
            DueDate = today.AddDays(duration),
            Status = "Borrowed"
        };
        db.Loans.Add(loan);
        AddAudit(actorId, "Checkout", "LOAN", null, $"Barcode: {copy.Barcode}; thẻ: {reader.CardNo}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (await GetLoanAsync(loan.LoanId, cancellationToken))!;
    }

    public async Task<LoanResponse> ReturnAsync(
        ReturnRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var loan = await LoanQuery()
            .SingleOrDefaultAsync(item => item.Copy.Barcode == request.Barcode && item.ReturnDate == null, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu mượn đang mở cho mã vạch này.");

        var now = DateTime.UtcNow;
        loan.ReturnDate = now;
        loan.Status = RecordStatuses.Returned;
        loan.Copy.Status = RecordStatuses.Available;
        await reservationQueue.PromoteNextAsync(loan.Copy.BookId, now, cancellationToken);
        var daysLate = Math.Max(0, DateOnly.FromDateTime(now).DayNumber - loan.DueDate.DayNumber);
        if (daysLate > 0)
        {
            var perDay = await GetDecimalConfigAsync("FinePerDay", 5000m, cancellationToken);
            db.Fines.Add(new Fine
            {
                LoanId = loan.LoanId,
                Amount = daysLate * perDay,
                Reason = $"Trả sách trễ {daysLate} ngày.",
                Status = RecordStatuses.Pending
            });
        }

        if (request.AdditionalFineAmount > 0)
        {
            if (string.IsNullOrWhiteSpace(request.FineReason))
                throw new ArgumentException("Cần nhập lý do cho khoản phạt bổ sung.");

            db.Fines.Add(new Fine
            {
                LoanId = loan.LoanId,
                Amount = request.AdditionalFineAmount,
                Reason = request.FineReason.Trim(),
                Status = RecordStatuses.Pending
            });
        }

        AddAudit(actorId, "Return", "LOAN", (int)loan.LoanId, $"Barcode: {request.Barcode}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetLoanAsync(loan.LoanId, cancellationToken))!;
    }

    public async Task<LoanResponse> RenewAsync(
        long loanId,
        int actorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var loan = await LoanQuery().SingleOrDefaultAsync(item => item.LoanId == loanId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu mượn.");
        if (loan.ReturnDate is not null)
            throw new BusinessRuleException("Không thể gia hạn phiếu mượn đã trả.");
        if (!loan.Reader.Account.IsActive || loan.Reader.CardStatus != RecordStatuses.Active)
            throw new BusinessRuleException("Thẻ độc giả không còn hiệu lực.");

        var maxRenewals = await GetIntConfigAsync("MaxRenewals", 1, cancellationToken);
        if (loan.RenewCount >= maxRenewals)
            throw new BusinessRuleException("Phiếu mượn đã đạt số lần gia hạn tối đa.");

        var anotherReaderHasReservation = await db.Reservations.AnyAsync(
            reservation => reservation.BookId == loan.Copy.BookId
                && reservation.ReaderId != loan.ReaderId
                && (reservation.Status == RecordStatuses.Pending || reservation.Status == RecordStatuses.ReadyForPickup)
                && reservation.ExpireAt > DateTime.UtcNow,
            cancellationToken);
        if (anotherReaderHasReservation)
            throw new BusinessRuleException("Không thể gia hạn vì đầu sách đang được độc giả khác đặt giữ.");
        if (loan.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new BusinessRuleException("Không thể gia hạn phiếu mượn đã quá hạn.");

        var duration = await GetIntConfigAsync("LoanDurationDays", 14, cancellationToken);
        loan.DueDate = loan.DueDate.AddDays(duration);
        loan.RenewCount++;
        loan.Status = "Borrowed";
        AddAudit(actorId, "Renew", "LOAN", (int)loanId, $"Gia hạn lần {loan.RenewCount}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapLoan(loan);
    }

    public async Task<IReadOnlyList<FineResponse>> GetFinesAsync(string? status, CancellationToken cancellationToken)
    {
        var query = FineQuery();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(fine => fine.Status == status);
        var fines = await query.OrderByDescending(fine => fine.FineId)
            .Take(1000)
            .ToListAsync(cancellationToken);
        return fines.Select(MapFine).ToArray();
    }

    public async Task<FineResponse> CollectFineAsync(
        long fineId,
        CollectFineRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var fine = await FineQuery().SingleOrDefaultAsync(item => item.FineId == fineId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy khoản phạt.");
        if (fine.Status == RecordStatuses.Paid)
            throw new BusinessRuleException("Khoản phạt này đã được thanh toán.");
        if (request.Amount > 0 && request.Amount != fine.Amount)
            throw new BusinessRuleException("Khoản phạt chỉ hỗ trợ thanh toán toàn bộ số tiền còn lại.");

        fine.Status = RecordStatuses.Paid;
        fine.PaidAt = DateTime.UtcNow;
        fine.CollectedBy = await GetLibrarianIdAsync(actorId, cancellationToken);
        AddAudit(actorId, "CollectFine", "FINE", (int)fineId, $"{fine.Amount} VND: {request.Reason.Trim()}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapFine(fine);
    }

    public async Task<IReadOnlyList<ProposalResponse>> GetProposalsAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var query = ProposalQuery();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(proposal => proposal.Status == status);
        var proposals = await query.OrderByDescending(proposal => proposal.CreatedAt)
            .Take(1000)
            .ToListAsync(cancellationToken);
        return proposals.Select(MapProposal).ToArray();
    }

    public async Task<ProposalResponse> ReviewProposalAsync(
        int proposalId,
        ReviewProposalRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var proposal = await ProposalQuery().SingleOrDefaultAsync(item => item.ProposalId == proposalId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đề xuất.");
        if (proposal.Status != RecordStatuses.Pending)
            throw new BusinessRuleException("Đề xuất này đã được xử lý.");

        proposal.Status = request.Decision;
        proposal.ReviewedBy = await GetLibrarianIdAsync(actorId, cancellationToken);
        proposal.ReviewedAt = DateTime.UtcNow;
        proposal.ReviewNote = NormalizeOptional(request.Note);
        AddAudit(actorId, "ReviewProposal", "PURCHASE_PROPOSAL", proposalId, request.Decision);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapProposal(proposal);
    }

    public async Task<IReadOnlyList<SystemConfigResponse>> GetConfigsAsync(CancellationToken cancellationToken)
    {
        var configs = await db.SystemConfigs.AsNoTracking()
            .OrderBy(config => config.ConfigKey)
            .ToListAsync(cancellationToken);
        return configs.Select(MapConfig).ToArray();
    }

    public async Task<SystemConfigResponse> UpdateConfigAsync(
        string key,
        UpdateSystemConfigRequest request,
        int actorId,
        CancellationToken cancellationToken)
    {
        if (!DefaultConfigs.TryGetValue(key, out var defaultConfig))
            throw new ArgumentException("Khóa cấu hình không được hỗ trợ.");
        var canonicalKey = DefaultConfigs.Keys.First(
            supportedKey => string.Equals(supportedKey, key, StringComparison.OrdinalIgnoreCase));
        ValidateConfigValue(canonicalKey, request.Value);

        var config = await db.SystemConfigs.SingleOrDefaultAsync(
            item => item.ConfigKey == canonicalKey,
            cancellationToken);
        if (config is null)
        {
            config = new SystemConfig { ConfigKey = canonicalKey, ConfigValue = request.Value.Trim() };
            db.SystemConfigs.Add(config);
        }
        else
        {
            config.ConfigValue = request.Value.Trim();
        }

        config.Description = NormalizeOptional(request.Description) ?? defaultConfig.Description;
        config.UpdatedAt = DateTime.UtcNow;
        AddAudit(actorId, "UpdateConfig", "SYSTEM_CONFIG", null, canonicalKey);
        await db.SaveChangesAsync(cancellationToken);
        return MapConfig(config);
    }

    public async Task<IReadOnlyList<ActivityLogResponse>> GetActivityLogsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 200)
            throw new ArgumentException("Page phải từ 1 trở lên, pageSize phải từ 1 đến 200.");

        var logs = await db.ActivityLogs.AsNoTracking()
            .Include(log => log.Account)
            .OrderByDescending(log => log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return logs.Select(log => new ActivityLogResponse(
                log.LogId,
                log.Account.Username,
                log.Action,
                log.TargetTable,
                log.TargetId,
                log.Detail,
                ToUtcOffset(log.CreatedAt)))
            .ToArray();
    }

    private IQueryable<Loan> LoanQuery() =>
        db.Loans.Include(loan => loan.Reader).ThenInclude(reader => reader.Account)
            .Include(loan => loan.Copy).ThenInclude(copy => copy.Book)
            .Include(loan => loan.Librarian).ThenInclude(librarian => librarian.Account);

    private IQueryable<Fine> FineQuery() =>
        db.Fines.Include(fine => fine.Loan).ThenInclude(loan => loan.Copy).ThenInclude(copy => copy.Book)
            .Include(fine => fine.Loan).ThenInclude(loan => loan.Reader).ThenInclude(reader => reader.Account);

    private IQueryable<PurchaseProposal> ProposalQuery() =>
        db.PurchaseProposals.Include(proposal => proposal.Reader).ThenInclude(reader => reader.Account)
            .Include(proposal => proposal.Reviewer).ThenInclude(reviewer => reviewer!.Account);

    private async Task<int> GetLibrarianIdAsync(int accountId, CancellationToken cancellationToken) =>
        await db.Librarians.Where(librarian => librarian.AccountId == accountId)
            .Select(librarian => (int?)librarian.LibrarianId)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException("Tài khoản chưa được cấu hình hồ sơ thủ thư.");

    private async Task<int> GetIntConfigAsync(string key, int fallback, CancellationToken cancellationToken)
    {
        var value = await db.SystemConfigs.Where(config => config.ConfigKey == key)
            .Select(config => config.ConfigValue)
            .SingleOrDefaultAsync(cancellationToken);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private async Task<decimal> GetDecimalConfigAsync(string key, decimal fallback, CancellationToken cancellationToken)
    {
        var value = await db.SystemConfigs.Where(config => config.ConfigKey == key)
            .Select(config => config.ConfigValue)
            .SingleOrDefaultAsync(cancellationToken);
        return decimal.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private void AddAudit(int accountId, string action, string table, int? targetId, string? detail) =>
        db.ActivityLogs.Add(new ActivityLog
        {
            AccountId = accountId,
            Action = action,
            TargetTable = table,
            TargetId = targetId,
            Detail = detail
        });

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ReaderResponse MapReader(Reader reader) =>
        new(
            reader.ReaderId,
            reader.AccountId,
            reader.Account.Username,
            reader.Account.FullName,
            reader.Account.Email,
            reader.Account.Phone,
            reader.CardNo,
            reader.CardIssueDate,
            reader.CardExpiryDate,
            reader.CardStatus,
            reader.BorrowLimit,
            reader.Account.IsActive,
            reader.Account.Role);

    private static LoanResponse MapLoan(Loan loan) =>
        new(
            loan.LoanId,
            loan.ReaderId,
            loan.Copy.BookId,
            loan.CopyId,
            loan.Copy.Barcode,
            loan.Copy.Book.Title,
            loan.Reader.Account.FullName,
            loan.Reader.CardNo,
            loan.Librarian.Account.FullName,
            loan.ReservationId,
            ToUtcOffset(loan.BorrowDate),
            loan.DueDate,
            loan.ReturnDate is null ? null : ToUtcOffset(loan.ReturnDate.Value),
            loan.RenewCount,
            loan.Status);

    private static FineResponse MapFine(Fine fine) =>
        new(
            fine.FineId,
            fine.LoanId,
            fine.Loan.Copy.Book.Title,
            fine.Loan.Reader.Account.FullName,
            fine.Loan.Reader.CardNo,
            fine.Amount,
            fine.Reason,
            fine.Status,
            fine.PaidAt is null ? null : ToUtcOffset(fine.PaidAt.Value));

    private static ProposalResponse MapProposal(PurchaseProposal proposal) =>
        new(
            proposal.ProposalId,
            proposal.Reader.Account.FullName,
            proposal.Reader.CardNo,
            proposal.Title,
            proposal.Author,
            proposal.Reason,
            proposal.Status,
            ToUtcOffset(proposal.CreatedAt),
            proposal.Reviewer?.Account.FullName,
            proposal.ReviewedAt is null ? null : ToUtcOffset(proposal.ReviewedAt.Value),
            proposal.ReviewNote);

    private static SystemConfigResponse MapConfig(SystemConfig config) =>
        new(config.ConfigKey, config.ConfigValue, config.Description, ToUtcOffset(config.UpdatedAt));

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static void ValidateConfigValue(string key, string value)
    {
        var valid = key switch
        {
            "LoanDurationDays" => int.TryParse(value, out var days) && days is >= 1 and <= 365,
            "MaxRenewals" => int.TryParse(value, out var renewals) && renewals is >= 0 and <= 10,
            "ReservationPickupDays" => int.TryParse(value, out var pickupDays) && pickupDays is >= 1 and <= 30,
            "MaxActiveLoans" => int.TryParse(value, out var maxLoans) && maxLoans is >= 1 and <= 100,
            "FinePerDay" => decimal.TryParse(value, out var fine) && fine is >= 0 and <= 1000000,
            _ => false
        };
        if (!valid)
            throw new ArgumentException($"Giá trị cấu hình {key} không hợp lệ.");
    }
}
