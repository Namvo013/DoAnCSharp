using LiMS.Api.Contracts;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LiMS.Tests;

public sealed class ReaderServiceWorkflowTests
{
    [Fact]
    public async Task GetReservationAsync_returns_only_reservation_owned_by_account()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync();
        var reservation = await database.AddReservationAsync(reader, book);

        var result = await database.CreateReaderService().GetReservationAsync(
            account.AccountId, reservation.ReservationId, CancellationToken.None);
        var otherAccountResult = await database.CreateReaderService().GetReservationAsync(
            account.AccountId + 1, reservation.ReservationId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(book.Title, result.BookTitle);
        Assert.Null(otherAccountResult);
    }

    [Fact]
    public async Task GetProposalAsync_returns_null_for_proposal_owned_by_another_account()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var other = await database.AddReaderAsync(username: "other-reader");
        var proposal = new PurchaseProposal
        {
            Reader = reader,
            Title = "Requested title",
            Author = "Writer",
            Reason = "Reason",
            Status = RecordStatuses.Pending
        };
        database.Context.PurchaseProposals.Add(proposal);
        await database.Context.SaveChangesAsync();

        var result = await database.CreateReaderService().GetProposalAsync(
            other.Account.AccountId, proposal.ProposalId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateProfileAsync_normalizes_profile_fields_and_writes_audit()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();

        var profile = await database.CreateReaderService().UpdateProfileAsync(
            account.AccountId,
            new UpdateReaderProfileRequest
            {
                FullName = "  Updated Name  ",
                Email = "  UPDATED@Example.test ",
                Phone = "  0900  "
            },
            CancellationToken.None);

        Assert.Equal("Updated Name", profile.FullName);
        Assert.Equal("updated@example.test", profile.Email);
        Assert.Equal("0900", profile.Phone);
        Assert.Equal("UpdateProfile", (await database.Context.ActivityLogs.SingleAsync()).Action);
        Assert.Equal(reader.ReaderId, profile.ReaderId);
    }

    [Fact]
    public async Task UpdateProfileAsync_converts_blank_phone_to_null()
    {
        await using var database = TestDatabase.Create();
        var (account, _) = await database.AddReaderAsync();

        var profile = await database.CreateReaderService().UpdateProfileAsync(
            account.AccountId,
            new UpdateReaderProfileRequest
            {
                FullName = "Reader",
                Email = "reader@example.test",
                Phone = "   "
            },
            CancellationToken.None);

        Assert.Null(profile.Phone);
    }

    [Fact]
    public async Task UpdateProfileAsync_rejects_email_already_used_by_another_account()
    {
        await using var database = TestDatabase.Create();
        var (account, _) = await database.AddReaderAsync();
        await database.AddReaderAsync(username: "other", cardNo: "CARD-OTHER");
        var duplicateEmail = (await database.Context.Accounts.OrderBy(item => item.AccountId).LastAsync()).Email;

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().UpdateProfileAsync(
                account.AccountId,
                new UpdateReaderProfileRequest
                {
                    FullName = "Reader",
                    Email = duplicateEmail,
                    Phone = null
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateProfileAsync_throws_not_found_when_account_has_no_reader_profile()
    {
        await using var database = TestDatabase.Create();
        var staff = await database.AddLibrarianAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            database.CreateReaderService().UpdateProfileAsync(
                staff.Account.AccountId,
                new UpdateReaderProfileRequest
                {
                    FullName = "Staff",
                    Email = "staff@example.test"
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateProposalAsync_stores_trimmed_request_for_active_reader()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();

        var result = await database.CreateReaderService().CreateProposalAsync(
            account.AccountId,
            new CreateProposalRequest
            {
                Title = "  New title  ",
                Author = "  New author  ",
                Reason = "  Needed for class  "
            },
            CancellationToken.None);

        Assert.Equal(reader.CardNo, result.ReaderCardNo);
        Assert.Equal("New title", result.Title);
        Assert.Equal("New author", result.Author);
        Assert.Equal("Needed for class", result.Reason);
        Assert.Equal(RecordStatuses.Pending, result.Status);
    }

    [Fact]
    public async Task CreateProposalAsync_rejects_inactive_reader_account()
    {
        await using var database = TestDatabase.Create();
        var (account, _) = await database.AddReaderAsync(isActive: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().CreateProposalAsync(
                account.AccountId,
                new CreateProposalRequest { Title = "Title", Author = "Author", Reason = "Reason" },
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateProposalAsync_rejects_inactive_card()
    {
        await using var database = TestDatabase.Create();
        var (account, _) = await database.AddReaderAsync(cardStatus: RecordStatuses.Inactive);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().CreateProposalAsync(
                account.AccountId,
                new CreateProposalRequest { Title = "Title", Author = "Author", Reason = "Reason" },
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateReservationAsync_creates_pending_reservation_for_unavailable_book()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);

        var result = await database.CreateReaderService().CreateReservationAsync(
            account.AccountId,
            new CreateReservationRequest { BookId = book.BookId },
            CancellationToken.None);

        Assert.Equal(book.BookId, result.BookId);
        Assert.Equal(RecordStatuses.Pending, result.Status);
        Assert.Equal(reader.ReaderId, (await database.Context.Reservations.SingleAsync()).ReaderId);
    }

    [Fact]
    public async Task CreateReservationAsync_rejects_when_a_copy_is_available()
    {
        await using var database = TestDatabase.Create();
        var (account, _) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync(copyStatus: RecordStatuses.Available);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().CreateReservationAsync(
                account.AccountId,
                new CreateReservationRequest { BookId = book.BookId },
                CancellationToken.None));
        Assert.Empty(await database.Context.Reservations.ToListAsync());
    }

    [Fact]
    public async Task CreateReservationAsync_rejects_when_book_has_no_copies_on_loan()
    {
        await using var database = TestDatabase.Create();
        var (account, _) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync(copyCount: 0);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().CreateReservationAsync(
                account.AccountId,
                new CreateReservationRequest { BookId = book.BookId },
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateReservationAsync_rejects_duplicate_active_reservation()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        await database.AddReservationAsync(reader, book);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().CreateReservationAsync(
                account.AccountId,
                new CreateReservationRequest { BookId = book.BookId },
                CancellationToken.None));
        Assert.Single(await database.Context.Reservations.ToListAsync());
    }

    [Fact]
    public async Task CreateReservationAsync_rejects_inactive_reader()
    {
        await using var database = TestDatabase.Create();
        var (account, _) = await database.AddReaderAsync(cardStatus: RecordStatuses.Inactive);
        var (_, book, _) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().CreateReservationAsync(
                account.AccountId,
                new CreateReservationRequest { BookId = book.BookId },
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateReservationAsync_returns_not_found_for_missing_book()
    {
        await using var database = TestDatabase.Create();
        var (account, _) = await database.AddReaderAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            database.CreateReaderService().CreateReservationAsync(
                account.AccountId,
                new CreateReservationRequest { BookId = 999 },
                CancellationToken.None));
    }

    [Fact]
    public async Task CancelReservationAsync_cancels_pending_reservation_owned_by_reader()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var reservation = await database.AddReservationAsync(reader, book);

        await database.CreateReaderService().CancelReservationAsync(
            account.AccountId, reservation.ReservationId, CancellationToken.None);

        Assert.Equal(RecordStatuses.Cancelled, (await database.Context.Reservations.SingleAsync()).Status);
        Assert.Equal("CancelReservation", (await database.Context.ActivityLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task CancelReservationAsync_rejects_reservation_owned_by_another_reader()
    {
        await using var database = TestDatabase.Create();
        var (_, owner) = await database.AddReaderAsync(username: "owner");
        var (otherAccount, _) = await database.AddReaderAsync(username: "other-reader");
        var (_, book, _) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var reservation = await database.AddReservationAsync(owner, book);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            database.CreateReaderService().CancelReservationAsync(
                otherAccount.AccountId, reservation.ReservationId, CancellationToken.None));
        Assert.Equal(RecordStatuses.Pending, (await database.Context.Reservations.SingleAsync()).Status);
    }

    [Fact]
    public async Task CancelReservationAsync_rejects_completed_reservation()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var (_, book, _) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var reservation = await database.AddReservationAsync(reader, book, RecordStatuses.Fulfilled);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().CancelReservationAsync(
                account.AccountId, reservation.ReservationId, CancellationToken.None));
    }

    [Fact]
    public async Task CancelReservationAsync_promotes_next_reader_when_ready_request_is_cancelled()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync(username: "ready-reader");
        var (_, nextReader) = await database.AddReaderAsync(username: "next-reader");
        var (_, book, _) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var ready = await database.AddReservationAsync(reader, book, RecordStatuses.ReadyForPickup);
        await database.AddReservationAsync(nextReader, book, RecordStatuses.Pending);
        var queue = new Mock<IReservationQueueService>();

        await database.CreateReaderService(queue.Object).CancelReservationAsync(
            account.AccountId, ready.ReservationId, CancellationToken.None);

        Assert.Equal(RecordStatuses.Cancelled, ready.Status);
        queue.Verify(
            item => item.PromoteNextAsync(book.BookId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RenewAsync_extends_reader_loan_within_configured_limit()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var staff = await database.AddLibrarianAsync();
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);
        var loan = await database.AddLoanAsync(reader, copies[0], staff.Librarian, dueDate);
        database.Context.SystemConfigs.AddRange(
            new SystemConfig { ConfigKey = "MaxRenewals", ConfigValue = "2" },
            new SystemConfig { ConfigKey = "LoanDurationDays", ConfigValue = "12" });
        await database.Context.SaveChangesAsync();

        var result = await database.CreateReaderService().RenewAsync(
            account.AccountId, loan.LoanId, CancellationToken.None);

        Assert.Equal(dueDate.AddDays(12), result.DueDate);
        Assert.Equal((short)1, result.RenewCount);
        Assert.Equal(loan.LoanId, result.Id);
    }

    [Fact]
    public async Task RenewAsync_does_not_allow_reader_to_renew_another_readers_loan()
    {
        await using var database = TestDatabase.Create();
        var (owner, reader) = await database.AddReaderAsync(username: "loan-owner");
        var (other, _) = await database.AddReaderAsync(username: "not-owner");
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var staff = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(reader, copies[0], staff.Librarian);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            database.CreateReaderService().RenewAsync(other.AccountId, loan.LoanId, CancellationToken.None));
        Assert.Equal((short)0, (await database.Context.Loans.SingleAsync()).RenewCount);
        Assert.NotEqual(owner.AccountId, other.AccountId);
    }

    [Fact]
    public async Task RenewAsync_rejects_already_returned_loan()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.Available);
        var staff = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(
            reader, copies[0], staff.Librarian, returnDate: DateTime.UtcNow.AddHours(-1));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().RenewAsync(account.AccountId, loan.LoanId, CancellationToken.None));
    }

    [Fact]
    public async Task RenewAsync_rejects_reader_with_inactive_card()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync(cardStatus: RecordStatuses.Inactive);
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var staff = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(reader, copies[0], staff.Librarian);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().RenewAsync(account.AccountId, loan.LoanId, CancellationToken.None));
    }

    [Fact]
    public async Task RenewAsync_rejects_when_renewal_limit_has_been_reached()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var staff = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(reader, copies[0], staff.Librarian, renewCount: 1);
        database.Context.SystemConfigs.Add(new SystemConfig { ConfigKey = "MaxRenewals", ConfigValue = "1" });
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().RenewAsync(account.AccountId, loan.LoanId, CancellationToken.None));
    }

    [Fact]
    public async Task RenewAsync_rejects_when_another_reader_has_active_reservation()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync(username: "borrower");
        var (_, anotherReader) = await database.AddReaderAsync(username: "reserver");
        var (_, book, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var staff = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(reader, copies[0], staff.Librarian);
        await database.AddReservationAsync(anotherReader, book);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateReaderService().RenewAsync(account.AccountId, loan.LoanId, CancellationToken.None));
    }
}
