using LiMS.Api.Contracts;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Tests;

public sealed class AdminServiceWorkflowTests
{
    [Fact]
    public async Task CreateBookAsync_creates_category_copies_and_audit_entry()
    {
        await using var database = TestDatabase.Create();
        var service = database.CreateAdminService();

        var book = await service.CreateBookAsync(new CreateBookRequest
        {
            Category = "  Computing  ",
            Title = "  LiMS internals  ",
            Author = "  A. Writer  ",
            InitialCopies = 2,
            Isbn = "  ISBN-1  "
        }, 9, CancellationToken.None);

        Assert.Equal("LiMS internals", book.Title);
        Assert.Equal("Computing", book.Category);
        Assert.Equal(2, book.TotalCopies);
        Assert.Equal(2, book.AvailableCopies);
        Assert.Equal("ISBN-1", book.Isbn);
        Assert.Equal(1, await database.Context.Categories.CountAsync());
        Assert.Equal(2, await database.Context.BookCopies.CountAsync());
        Assert.Equal("Create", (await database.Context.ActivityLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task UpdateBookAsync_updates_catalog_fields_and_active_state()
    {
        await using var database = TestDatabase.Create();
        var (_, original, _) = await database.AddBookAsync();
        var service = database.CreateAdminService();

        var updated = await service.UpdateBookAsync(original.BookId, new UpdateBookRequest
        {
            Category = "Updated category",
            Title = "Updated title",
            Author = "Updated author",
            Isbn = "  ",
            Publisher = "Publisher",
            PublishYear = 2025,
            ShelfLocation = "A-3",
            Description = "Updated description",
            IsActive = false
        }, 12, CancellationToken.None);

        Assert.Equal("Updated title", updated.Title);
        Assert.Equal("Updated category", updated.Category);
        Assert.Null(updated.Isbn);
        Assert.Equal("Publisher", updated.Publisher);
        Assert.False((await database.Context.Books.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task UpdateBookAsync_throws_not_found_for_missing_book()
    {
        await using var database = TestDatabase.Create();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            database.CreateAdminService().UpdateBookAsync(999, new UpdateBookRequest
            {
                Category = "History",
                Title = "Missing",
                Author = "Writer",
                IsActive = true
            }, 1, CancellationToken.None));
    }

    [Fact]
    public async Task AddCopiesAsync_assigns_next_barcode_and_records_audit()
    {
        await using var database = TestDatabase.Create();
        var (_, book, _) = await database.AddBookAsync();
        database.Context.BookCopies.Add(new BookCopy
        {
            BookId = book.BookId,
            Barcode = "LI-0003",
            Status = RecordStatuses.Available
        });
        await database.Context.SaveChangesAsync();

        await database.CreateAdminService().AddCopiesAsync(
            book.BookId,
            new CreateCopiesRequest { Quantity = 2, BarcodePrefix = "LI" },
            4,
            CancellationToken.None);

        var barcodes = await database.Context.BookCopies.OrderBy(item => item.Barcode)
            .Select(item => item.Barcode)
            .ToArrayAsync();
        Assert.Contains("LI-0004", barcodes);
        Assert.Contains("LI-0005", barcodes);
        Assert.Equal("CreateCopies", (await database.Context.ActivityLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task AddCopiesAsync_throws_not_found_without_creating_rows()
    {
        await using var database = TestDatabase.Create();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            database.CreateAdminService().AddCopiesAsync(
                700,
                new CreateCopiesRequest { Quantity = 1, BarcodePrefix = "LI" },
                4,
                CancellationToken.None));

        Assert.Empty(await database.Context.BookCopies.ToListAsync());
    }

    [Theory]
    [InlineData("Available")]
    [InlineData("Maintenance")]
    [InlineData("Lost")]
    public async Task UpdateCopyStatusAsync_accepts_supported_statuses(string status)
    {
        await using var database = TestDatabase.Create();
        var (_, _, copies) = await database.AddBookAsync();

        await database.CreateAdminService().UpdateCopyStatusAsync(
            copies[0].CopyId,
            new CopyStatusRequest { Status = status },
            3,
            CancellationToken.None);

        Assert.Equal(status, (await database.Context.BookCopies.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    public async Task UpdateCopyStatusAsync_rejects_unsupported_status(string status)
    {
        await using var database = TestDatabase.Create();
        var (_, _, copies) = await database.AddBookAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().UpdateCopyStatusAsync(
                copies[0].CopyId,
                new CopyStatusRequest { Status = status },
                3,
                CancellationToken.None));
        Assert.Equal(RecordStatuses.Available, (await database.Context.BookCopies.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateCopyStatusAsync_does_not_change_copy_while_on_loan()
    {
        await using var database = TestDatabase.Create();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().UpdateCopyStatusAsync(
                copies[0].CopyId,
                new CopyStatusRequest { Status = RecordStatuses.Lost },
                3,
                CancellationToken.None));
        Assert.Equal(RecordStatuses.OnLoan, (await database.Context.BookCopies.SingleAsync()).Status);
    }

    [Fact]
    public async Task CreateReaderAsync_normalizes_fields_hashes_password_and_audits()
    {
        await using var database = TestDatabase.Create();
        var service = database.CreateAdminService();

        var reader = await service.CreateReaderAsync(new CreateReaderRequest
        {
            Username = "  new.reader  ",
            InitialPassword = "reader-password",
            Email = "  Reader@Example.test ",
            FullName = "  New Reader  ",
            Phone = "  0900123456  ",
            CardNo = "  CARD-NEW  ",
            BorrowLimit = 3,
            CardValidityMonths = 24
        }, 5, CancellationToken.None);

        var account = await database.Context.Accounts.SingleAsync();
        Assert.Equal("new.reader", reader.Username);
        Assert.Equal("reader@example.test", reader.Email);
        Assert.Equal("New Reader", reader.FullName);
        Assert.Equal("CARD-NEW", reader.CardNo);
        Assert.Equal("0900123456", reader.Phone);
        Assert.Equal(AccountRoles.Borrower, account.Role);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<Account>().VerifyHashedPassword(account, account.PasswordHash, "reader-password"));
        Assert.Equal(1, await database.Context.ActivityLogs.CountAsync());
    }

    [Fact]
    public async Task UpdateReaderAsync_updates_account_and_card_fields()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(10);

        var result = await database.CreateAdminService().UpdateReaderAsync(reader.ReaderId, new UpdateReaderRequest
        {
            FullName = "  Updated Reader  ",
            Email = "  UPDATED@example.test ",
            Phone = " 5551234 ",
            CardStatus = RecordStatuses.Inactive,
            BorrowLimit = 2,
            CardExpiryDate = expiry,
            IsActive = false
        }, account.AccountId, CancellationToken.None);

        Assert.Equal("Updated Reader", result.FullName);
        Assert.Equal("updated@example.test", result.Email);
        Assert.Equal("5551234", result.Phone);
        Assert.Equal(RecordStatuses.Inactive, result.CardStatus);
        Assert.False(result.IsActive);
        Assert.Equal(expiry, result.CardExpiryDate);
    }

    [Theory]
    [InlineData("Expired")]
    [InlineData("Pending")]
    [InlineData("")]
    public async Task UpdateReaderAsync_rejects_non_card_status_values(string cardStatus)
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().UpdateReaderAsync(reader.ReaderId, new UpdateReaderRequest
            {
                FullName = "Updated Reader",
                Email = "updated@example.test",
                CardStatus = cardStatus,
                BorrowLimit = 5,
                CardExpiryDate = reader.CardExpiryDate,
                IsActive = true
            }, account.AccountId, CancellationToken.None));
        Assert.Equal(RecordStatuses.Active, (await database.Context.Readers.SingleAsync()).CardStatus);
    }

    [Fact]
    public async Task ResetReaderPasswordAsync_replaces_hash_and_writes_audit()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync();
        var previousHash = account.PasswordHash;

        await database.CreateAdminService().ResetReaderPasswordAsync(
            reader.ReaderId,
            new ResetReaderPasswordRequest { NewPassword = "replacement-password" },
            8,
            CancellationToken.None);

        var updated = await database.Context.Accounts.SingleAsync();
        Assert.NotEqual(previousHash, updated.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<Account>().VerifyHashedPassword(updated, updated.PasswordHash, "replacement-password"));
        Assert.Equal("ResetPassword", (await database.Context.ActivityLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task UploadBookCoverAsync_accepts_jpeg_signature_and_stores_detected_content_type()
    {
        await using var database = TestDatabase.Create();
        var (_, book, _) = await database.AddBookAsync();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "File", "cover.jpg");

        var result = await database.CreateAdminService().UploadBookCoverAsync(
            book.BookId, file, 2, CancellationToken.None);

        Assert.Equal($"/api/library/books/{book.BookId}/cover", result.CoverImageUrl);
        var saved = await database.Context.Books.SingleAsync();
        Assert.Equal("image/jpeg", saved.CoverImageContentType);
        Assert.Equal(bytes, saved.CoverImageData);
    }

    [Fact]
    public async Task UploadBookCoverAsync_rejects_non_image_bytes_without_overwriting_cover()
    {
        await using var database = TestDatabase.Create();
        var (_, book, _) = await database.AddBookAsync();
        var original = new byte[] { 9, 8, 7 };
        book.CoverImageData = original;
        book.CoverImageContentType = "image/png";
        await database.Context.SaveChangesAsync();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var file = new FormFile(stream, 0, stream.Length, "File", "not-image.bin");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().UploadBookCoverAsync(book.BookId, file, 2, CancellationToken.None));

        Assert.Equal(original, (await database.Context.Books.SingleAsync()).CoverImageData);
    }

    [Fact]
    public async Task UploadBookCoverAsync_rejects_empty_file()
    {
        await using var database = TestDatabase.Create();
        var (_, book, _) = await database.AddBookAsync();
        await using var stream = new MemoryStream();
        var file = new FormFile(stream, 0, 0, "File", "empty.jpg");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().UploadBookCoverAsync(book.BookId, file, 2, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateConfigAsync_canonicalizes_key_and_stores_valid_value()
    {
        await using var database = TestDatabase.Create();

        var result = await database.CreateAdminService().UpdateConfigAsync(
            "maxrenewals",
            new UpdateSystemConfigRequest { Value = " 2 ", Description = " Renewals " },
            6,
            CancellationToken.None);

        Assert.Equal("MaxRenewals", result.Key);
        Assert.Equal("2", result.Value);
        Assert.Equal("Renewals", result.Description);
        Assert.Equal("UpdateConfig", (await database.Context.ActivityLogs.SingleAsync()).Action);
    }

    [Theory]
    [InlineData("LoanDurationDays", "0")]
    [InlineData("MaxRenewals", "11")]
    [InlineData("ReservationPickupDays", "31")]
    [InlineData("MaxActiveLoans", "0")]
    [InlineData("FinePerDay", "-1")]
    public async Task UpdateConfigAsync_rejects_out_of_range_values(string key, string value)
    {
        await using var database = TestDatabase.Create();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().UpdateConfigAsync(
                key,
                new UpdateSystemConfigRequest { Value = value },
                6,
                CancellationToken.None));
        Assert.Empty(await database.Context.SystemConfigs.ToListAsync());
    }

    [Fact]
    public async Task UpdateConfigAsync_rejects_unsupported_key()
    {
        await using var database = TestDatabase.Create();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().UpdateConfigAsync(
                "Unsupported",
                new UpdateSystemConfigRequest { Value = "1" },
                6,
                CancellationToken.None));
    }

    [Fact]
    public async Task CheckoutAsync_creates_loan_and_marks_copy_on_loan()
    {
        await using var database = TestDatabase.Create();
        var (account, reader) = await database.AddReaderAsync(cardNo: "CARD-CHECKOUT");
        var (_, book, copies) = await database.AddBookAsync();
        var (_, librarian) = await database.AddLibrarianAsync();
        database.Context.SystemConfigs.Add(new SystemConfig { ConfigKey = "LoanDurationDays", ConfigValue = "21" });
        await database.Context.SaveChangesAsync();

        var loan = await database.CreateAdminService().CheckoutAsync(new CheckoutRequest
        {
            ReaderCardNo = reader.CardNo,
            Barcode = copies[0].Barcode
        }, librarian.AccountId, CancellationToken.None);

        Assert.Equal(reader.ReaderId, loan.ReaderId);
        Assert.Equal(book.BookId, loan.BookId);
        Assert.Equal("Borrowed", loan.Status);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(21), loan.DueDate);
        Assert.Equal(RecordStatuses.OnLoan, (await database.Context.BookCopies.SingleAsync()).Status);
        Assert.Equal("Checkout", (await database.Context.ActivityLogs.SingleAsync()).Action);
        Assert.NotEqual(account.AccountId, librarian.AccountId);
    }

    [Fact]
    public async Task CheckoutAsync_rejects_inactive_reader_before_creating_loan()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync(isActive: false);
        var (_, _, copies) = await database.AddBookAsync();
        var (_, librarian) = await database.AddLibrarianAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().CheckoutAsync(new CheckoutRequest
            {
                ReaderCardNo = reader.CardNo,
                Barcode = copies[0].Barcode
            }, librarian.AccountId, CancellationToken.None));
        Assert.Empty(await database.Context.Loans.ToListAsync());
    }

    [Fact]
    public async Task CheckoutAsync_rejects_expired_card()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync(
            cardNo: "CARD-EXPIRED",
            expiryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));
        var (_, _, copies) = await database.AddBookAsync();
        var (_, librarian) = await database.AddLibrarianAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().CheckoutAsync(new CheckoutRequest
            {
                ReaderCardNo = reader.CardNo,
                Barcode = copies[0].Barcode
            }, librarian.AccountId, CancellationToken.None));
    }

    [Fact]
    public async Task CheckoutAsync_rejects_copy_that_is_not_available()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.Maintenance);
        var (_, librarian) = await database.AddLibrarianAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().CheckoutAsync(new CheckoutRequest
            {
                ReaderCardNo = reader.CardNo,
                Barcode = copies[0].Barcode
            }, librarian.AccountId, CancellationToken.None));
    }

    [Fact]
    public async Task CheckoutAsync_rejects_when_reader_has_unpaid_fine()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyCount: 2);
        var (_, librarian) = await database.AddLibrarianAsync();
        var priorLoan = await database.AddLoanAsync(reader, copies[1], librarian);
        database.Context.Fines.Add(new Fine
        {
            Loan = priorLoan,
            Amount = 5000,
            Reason = "Overdue",
            Status = RecordStatuses.Pending
        });
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().CheckoutAsync(new CheckoutRequest
            {
                ReaderCardNo = reader.CardNo,
                Barcode = copies[0].Barcode
            }, librarian.AccountId, CancellationToken.None));
    }

    [Fact]
    public async Task CheckoutAsync_rejects_when_reader_has_reached_borrow_limit()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync(cardNo: "CARD-LIMIT", borrowLimit: 1);
        var (_, _, copies) = await database.AddBookAsync(copyCount: 2);
        var (_, librarian) = await database.AddLibrarianAsync();
        await database.AddLoanAsync(reader, copies[1], librarian);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().CheckoutAsync(new CheckoutRequest
            {
                ReaderCardNo = reader.CardNo,
                Barcode = copies[0].Barcode
            }, librarian.AccountId, CancellationToken.None));
    }

    [Fact]
    public async Task ReturnAsync_marks_loan_and_copy_returned_and_calculates_overdue_fine()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var (_, librarian) = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(
            reader, copies[0], librarian,
            dueDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2));
        database.Context.SystemConfigs.Add(new SystemConfig { ConfigKey = "FinePerDay", ConfigValue = "1500" });
        await database.Context.SaveChangesAsync();

        var returned = await database.CreateAdminService().ReturnAsync(
            new ReturnRequest { Barcode = copies[0].Barcode },
            librarian.AccountId,
            CancellationToken.None);

        Assert.Equal(RecordStatuses.Returned, returned.Status);
        Assert.NotNull(returned.ReturnDate);
        Assert.Equal(RecordStatuses.Available, (await database.Context.BookCopies.SingleAsync()).Status);
        var fine = await database.Context.Fines.SingleAsync();
        Assert.Equal(3000m, fine.Amount);
        Assert.Equal(RecordStatuses.Pending, fine.Status);
        Assert.Equal(loan.LoanId, fine.LoanId);
    }

    [Fact]
    public async Task ReturnAsync_requires_reason_for_additional_fine()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var (_, librarian) = await database.AddLibrarianAsync();
        await database.AddLoanAsync(reader, copies[0], librarian);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().ReturnAsync(
                new ReturnRequest { Barcode = copies[0].Barcode, AdditionalFineAmount = 2500 },
                librarian.AccountId,
                CancellationToken.None));
        Assert.Empty(await database.Context.Fines.ToListAsync());
    }

    [Fact]
    public async Task ReturnAsync_records_additional_fine_when_reason_is_provided()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var (_, librarian) = await database.AddLibrarianAsync();
        await database.AddLoanAsync(reader, copies[0], librarian);

        await database.CreateAdminService().ReturnAsync(
            new ReturnRequest
            {
                Barcode = copies[0].Barcode,
                AdditionalFineAmount = 2500,
                FineReason = "  Damaged cover  "
            },
            librarian.AccountId,
            CancellationToken.None);

        var fine = await database.Context.Fines.SingleAsync();
        Assert.Equal(2500m, fine.Amount);
        Assert.Equal("Damaged cover", fine.Reason);
    }

    [Fact]
    public async Task RenewAsync_extends_due_date_and_increments_count()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var (_, librarian) = await database.AddLibrarianAsync();
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        var loan = await database.AddLoanAsync(reader, copies[0], librarian, dueDate);
        database.Context.SystemConfigs.Add(new SystemConfig { ConfigKey = "LoanDurationDays", ConfigValue = "10" });
        await database.Context.SaveChangesAsync();

        var renewed = await database.CreateAdminService().RenewAsync(loan.LoanId, librarian.AccountId, CancellationToken.None);

        Assert.Equal(dueDate.AddDays(10), renewed.DueDate);
        Assert.Equal((short)1, renewed.RenewCount);
    }

    [Fact]
    public async Task RenewAsync_rejects_overdue_loan()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var (_, librarian) = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(
            reader, copies[0], librarian,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().RenewAsync(loan.LoanId, librarian.AccountId, CancellationToken.None));
        Assert.Equal((short)0, (await database.Context.Loans.SingleAsync()).RenewCount);
    }

    [Fact]
    public async Task RenewAsync_rejects_when_another_reader_has_reserved_book()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync(username: "reader-a");
        var (_, anotherReader) = await database.AddReaderAsync(username: "reader-b");
        var (_, book, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var (_, librarian) = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(reader, copies[0], librarian);
        await database.AddReservationAsync(anotherReader, book);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().RenewAsync(loan.LoanId, librarian.AccountId, CancellationToken.None));
    }

    [Fact]
    public async Task CollectFineAsync_marks_fine_paid_and_sets_collector()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var (_, librarian) = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(reader, copies[0], librarian);
        var fine = new Fine { Loan = loan, Amount = 7500, Reason = "Overdue", Status = RecordStatuses.Pending };
        database.Context.Fines.Add(fine);
        await database.Context.SaveChangesAsync();

        var result = await database.CreateAdminService().CollectFineAsync(
            fine.FineId,
            new CollectFineRequest { Amount = 7500, Reason = "  Cash  " },
            librarian.AccountId,
            CancellationToken.None);

        Assert.Equal(RecordStatuses.Paid, result.Status);
        Assert.NotNull(result.PaidAt);
        Assert.Equal(librarian.LibrarianId, (await database.Context.Fines.SingleAsync()).CollectedBy);
    }

    [Fact]
    public async Task CollectFineAsync_rejects_partial_payment()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyStatus: RecordStatuses.OnLoan);
        var (_, librarian) = await database.AddLibrarianAsync();
        var loan = await database.AddLoanAsync(reader, copies[0], librarian);
        var fine = new Fine { Loan = loan, Amount = 7500, Reason = "Overdue", Status = RecordStatuses.Pending };
        database.Context.Fines.Add(fine);
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().CollectFineAsync(
                fine.FineId,
                new CollectFineRequest { Amount = 1000, Reason = "Partial" },
                librarian.AccountId,
                CancellationToken.None));
        Assert.Equal(RecordStatuses.Pending, (await database.Context.Fines.SingleAsync()).Status);
    }

    [Fact]
    public async Task ReviewProposalAsync_records_decision_and_reviewer()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, librarian) = await database.AddLibrarianAsync();
        var proposal = new PurchaseProposal
        {
            Reader = reader,
            Title = "Proposed book",
            Author = "Proposed author",
            Reason = "Research",
            Status = RecordStatuses.Pending
        };
        database.Context.PurchaseProposals.Add(proposal);
        await database.Context.SaveChangesAsync();

        var reviewed = await database.CreateAdminService().ReviewProposalAsync(
            proposal.ProposalId,
            new ReviewProposalRequest { Decision = RecordStatuses.Approved, Note = "  Approved  " },
            librarian.AccountId,
            CancellationToken.None);

        Assert.Equal(RecordStatuses.Approved, reviewed.Status);
        Assert.Equal("Approved", reviewed.ReviewNote);
        Assert.Equal(librarian.LibrarianId, (await database.Context.PurchaseProposals.SingleAsync()).ReviewedBy);
    }

    [Fact]
    public async Task ReviewProposalAsync_rejects_already_reviewed_proposal()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, librarian) = await database.AddLibrarianAsync();
        var proposal = new PurchaseProposal
        {
            Reader = reader,
            Title = "Proposed book",
            Author = "Author",
            Reason = "Reason",
            Status = RecordStatuses.Approved
        };
        database.Context.PurchaseProposals.Add(proposal);
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            database.CreateAdminService().ReviewProposalAsync(
                proposal.ProposalId,
                new ReviewProposalRequest { Decision = RecordStatuses.Rejected },
                librarian.AccountId,
                CancellationToken.None));
    }
}
