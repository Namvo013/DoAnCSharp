using LiMS.Api.Contracts;
using LiMS.Api.Data;
using LiMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Services;

public sealed class ReaderService(
    LiMSDbContext db,
    IReservationQueueService reservationQueue) : IReaderService
{
    public async Task<ReservationResponse?> GetReservationAsync(
        int accountId,
        long reservationId,
        CancellationToken cancellationToken)
    {
        var reservation = await db.Reservations.AsNoTracking()
            .Include(item => item.Book)
            .SingleOrDefaultAsync(
                item => item.ReservationId == reservationId && item.Reader.AccountId == accountId,
                cancellationToken);
        return reservation is null
            ? null
            : new ReservationResponse(
                reservation.ReservationId,
                reservation.BookId,
                reservation.Book.Title,
                ToUtcOffset(reservation.CreatedAt),
                ToUtcOffset(reservation.ExpireAt),
                reservation.Status);
    }

    public async Task<IReadOnlyList<ProposalResponse>> GetProposalsAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var proposals = await db.PurchaseProposals.AsNoTracking()
            .Include(item => item.Reader).ThenInclude(reader => reader.Account)
            .Include(item => item.Reviewer).ThenInclude(reviewer => reviewer!.Account)
            .Where(item => item.Reader.AccountId == accountId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return proposals.Select(item => new ProposalResponse(
            item.ProposalId,
            item.Reader.Account.FullName,
            item.Reader.CardNo,
            item.Title,
            item.Author,
            item.Reason,
            item.Status,
            ToUtcOffset(item.CreatedAt),
            item.Reviewer?.Account.FullName,
            item.ReviewedAt is null ? null : ToUtcOffset(item.ReviewedAt.Value),
            item.ReviewNote)).ToArray();
    }

    public async Task<ProposalResponse?> GetProposalAsync(
        int accountId,
        int proposalId,
        CancellationToken cancellationToken)
    {
        var proposal = await db.PurchaseProposals.AsNoTracking()
            .Include(item => item.Reader).ThenInclude(reader => reader.Account)
            .Include(item => item.Reviewer).ThenInclude(reviewer => reviewer!.Account)
            .SingleOrDefaultAsync(
                item => item.ProposalId == proposalId && item.Reader.AccountId == accountId,
                cancellationToken);
        return proposal is null
            ? null
            : new ProposalResponse(
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
    }

    public async Task<ReservationResponse> CreateReservationAsync(
        int accountId,
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var now = DateTime.UtcNow;
        var reader = await db.Readers.Include(item => item.Account).SingleOrDefaultAsync(
            item => item.AccountId == accountId
                && item.Account.IsActive
                && item.CardStatus == RecordStatuses.Active,
            cancellationToken)
            ?? throw new BusinessRuleException("Tài khoản chưa có thẻ độc giả đang hoạt động.");
        var book = await db.Books.SingleOrDefaultAsync(
            item => item.BookId == request.BookId && item.IsActive,
            cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đầu sách.");
        if (await db.BookCopies.AnyAsync(
                copy => copy.BookId == book.BookId && copy.Status == RecordStatuses.Available,
                cancellationToken))
            throw new BusinessRuleException("Sách đang có bản sao sẵn sàng; vui lòng đến quầy mượn.");
        if (!await db.BookCopies.AnyAsync(
                copy => copy.BookId == book.BookId && copy.Status == RecordStatuses.OnLoan,
                cancellationToken))
            throw new BusinessRuleException("Hiện không có bản sao đang lưu thông để đặt giữ.");

        var alreadyReserved = await db.Reservations.AnyAsync(
            item => item.ReaderId == reader.ReaderId
                && item.BookId == book.BookId
                && item.ExpireAt > now
                && (item.Status == RecordStatuses.Pending || item.Status == RecordStatuses.ReadyForPickup),
            cancellationToken);
        if (alreadyReserved)
            throw new BusinessRuleException("Bạn đã có yêu cầu đặt giữ còn hiệu lực cho đầu sách này.");

        var reservation = new Reservation
        {
            ReaderId = reader.ReaderId,
            BookId = book.BookId,
            CreatedAt = now,
            ExpireAt = now.AddDays(30),
            Status = RecordStatuses.Pending
        };
        db.Reservations.Add(reservation);
        AddAudit(accountId, "CreateReservation", "RESERVATION", null, book.Title);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ReservationResponse(
            reservation.ReservationId,
            book.BookId,
            book.Title,
            ToUtcOffset(reservation.CreatedAt),
            ToUtcOffset(reservation.ExpireAt),
            reservation.Status);
    }

    public async Task CancelReservationAsync(
        int accountId,
        long reservationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var reservation = await db.Reservations.Include(item => item.Reader)
            .Include(item => item.Book)
            .SingleOrDefaultAsync(
                item => item.ReservationId == reservationId && item.Reader.AccountId == accountId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu đặt giữ.");
        if (reservation.Status != RecordStatuses.Pending
            && reservation.Status != RecordStatuses.ReadyForPickup)
            throw new BusinessRuleException("Yêu cầu đặt giữ này không thể hủy.");

        var wasReady = reservation.Status == RecordStatuses.ReadyForPickup;
        reservation.Status = RecordStatuses.Cancelled;
        if (wasReady)
            await reservationQueue.PromoteNextAsync(reservation.BookId, DateTime.UtcNow, cancellationToken);
        AddAudit(accountId, "CancelReservation", "RESERVATION", (int)reservationId, reservation.Book.Title);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProposalResponse> CreateProposalAsync(
        int accountId,
        CreateProposalRequest request,
        CancellationToken cancellationToken)
    {
        var reader = await db.Readers.Include(item => item.Account).SingleOrDefaultAsync(
            item => item.AccountId == accountId
                && item.Account.IsActive
                && item.CardStatus == RecordStatuses.Active,
            cancellationToken)
            ?? throw new BusinessRuleException("Tài khoản chưa có thẻ độc giả đang hoạt động.");
        var proposal = new PurchaseProposal
        {
            ReaderId = reader.ReaderId,
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Reason = request.Reason.Trim(),
            Status = RecordStatuses.Pending
        };
        db.PurchaseProposals.Add(proposal);
        AddAudit(accountId, "CreateProposal", "PURCHASE_PROPOSAL", null, proposal.Title);
        await db.SaveChangesAsync(cancellationToken);
        return new ProposalResponse(
            proposal.ProposalId,
            reader.Account.FullName,
            reader.CardNo,
            proposal.Title,
            proposal.Author,
            proposal.Reason,
            proposal.Status,
            ToUtcOffset(proposal.CreatedAt),
            null,
            null,
            null);
    }

    public async Task<LoanResponse> RenewAsync(
        int accountId,
        long loanId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var loan = await db.Loans
            .Include(item => item.Reader).ThenInclude(reader => reader.Account)
            .Include(item => item.Copy).ThenInclude(copy => copy.Book)
            .Include(item => item.Librarian).ThenInclude(librarian => librarian.Account)
            .SingleOrDefaultAsync(
                item => item.LoanId == loanId && item.Reader.AccountId == accountId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu mượn của tài khoản hiện tại.");
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
        AddAudit(accountId, "Renew", "LOAN", (int)loanId, $"Gia hạn lần {loan.RenewCount}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LoanResponse(
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
    }

    private async Task<int> GetIntConfigAsync(string key, int fallback, CancellationToken cancellationToken)
    {
        var value = await db.SystemConfigs.Where(config => config.ConfigKey == key)
            .Select(config => config.ConfigValue)
            .SingleOrDefaultAsync(cancellationToken);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
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

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
