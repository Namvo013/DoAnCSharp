using System.ComponentModel.DataAnnotations;
using LiMS.Api.Contracts;

namespace LiMS.Tests;

public sealed class RequestValidationTests
{
    [Fact]
    public void ChangePasswordRequest_enforces_new_password_minimum_length()
    {
        var request = new ChangePasswordRequest { CurrentPassword = "current", NewPassword = "short" };

        AssertInvalid(request, nameof(ChangePasswordRequest.NewPassword));
    }

    [Fact]
    public void CreateBookRequest_requires_category_title_and_author()
    {
        var request = new CreateBookRequest
        {
            Category = "",
            Title = "",
            Author = "",
            InitialCopies = 0
        };

        AssertInvalid(request, nameof(CreateBookRequest.Category));
        AssertInvalid(request, nameof(CreateBookRequest.Title));
        AssertInvalid(request, nameof(CreateBookRequest.Author));
    }

    [Theory]
    [InlineData((short)1399)]
    [InlineData((short)2101)]
    public void CreateBookRequest_rejects_year_outside_supported_range(short year)
    {
        var request = ValidCreateBook() with { PublishYear = year };

        AssertInvalid(request, nameof(CreateBookRequest.PublishYear));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void CreateBookRequest_rejects_initial_copy_count_outside_range(int count)
    {
        var request = ValidCreateBook() with { InitialCopies = count };

        AssertInvalid(request, nameof(CreateBookRequest.InitialCopies));
    }

    [Theory]
    [InlineData(0, "BC")]
    [InlineData(1001, "BC")]
    [InlineData(1, "bad prefix")]
    public void CreateCopiesRequest_rejects_invalid_quantity_or_barcode_prefix(int quantity, string prefix)
    {
        var request = new CreateCopiesRequest { Quantity = quantity, BarcodePrefix = prefix };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void CreateCopiesRequest_accepts_supported_prefix_characters()
    {
        var request = new CreateCopiesRequest { Quantity = 1000, BarcodePrefix = "BC_1.2-A" };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("reader@")]
    public void CreateReaderRequest_rejects_invalid_email(string email)
    {
        var request = ValidCreateReader() with { Email = email };

        AssertInvalid(request, nameof(CreateReaderRequest.Email));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void CreateReaderRequest_requires_minimum_password_length(string password)
    {
        var request = ValidCreateReader() with { InitialPassword = password };

        AssertInvalid(request, nameof(CreateReaderRequest.InitialPassword));
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)101)]
    public void CreateReaderRequest_rejects_borrow_limit_outside_range(short borrowLimit)
    {
        var request = ValidCreateReader() with { BorrowLimit = borrowLimit };

        AssertInvalid(request, nameof(CreateReaderRequest.BorrowLimit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void CreateReaderRequest_rejects_card_validity_outside_range(int months)
    {
        var request = ValidCreateReader() with { CardValidityMonths = months };

        AssertInvalid(request, nameof(CreateReaderRequest.CardValidityMonths));
    }

    [Theory]
    [InlineData("Expired")]
    [InlineData("Borrowed")]
    [InlineData("")]
    public void ReviewProposalRequest_accepts_only_review_decisions(string decision)
    {
        var request = new ReviewProposalRequest { Decision = decision };

        AssertInvalid(request, nameof(ReviewProposalRequest.Decision));
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public void ReviewProposalRequest_accepts_supported_decisions(string decision)
    {
        var request = new ReviewProposalRequest { Decision = decision };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000001)]
    public void ReturnRequest_rejects_additional_fine_outside_range(decimal amount)
    {
        var request = new ReturnRequest { Barcode = "BC-1", AdditionalFineAmount = amount };

        AssertInvalid(request, nameof(ReturnRequest.AdditionalFineAmount));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100000001)]
    public void CollectFineRequest_rejects_amount_outside_range(decimal amount)
    {
        var request = new CollectFineRequest { Amount = amount, Reason = "Cash" };

        AssertInvalid(request, nameof(CollectFineRequest.Amount));
    }

    [Fact]
    public void RestoreDatabaseBackupRequest_requires_database_confirmation()
    {
        var request = new RestoreDatabaseBackupRequest { ConfirmDatabaseName = "" };

        AssertInvalid(request, nameof(RestoreDatabaseBackupRequest.ConfirmDatabaseName));
    }

    [Fact]
    public void CreateReservationRequest_requires_positive_book_id()
    {
        var request = new CreateReservationRequest { BookId = 0 };

        AssertInvalid(request, nameof(CreateReservationRequest.BookId));
    }

    [Fact]
    public void CreateProposalRequest_rejects_missing_title_author_and_reason()
    {
        var request = new CreateProposalRequest { Title = "", Author = "", Reason = "" };

        AssertInvalid(request, nameof(CreateProposalRequest.Title));
        AssertInvalid(request, nameof(CreateProposalRequest.Author));
        AssertInvalid(request, nameof(CreateProposalRequest.Reason));
    }

    [Fact]
    public void UpdateReaderProfileRequest_rejects_invalid_email()
    {
        var request = new UpdateReaderProfileRequest { FullName = "Reader", Email = "not-an-email" };

        AssertInvalid(request, nameof(UpdateReaderProfileRequest.Email));
    }

    [Fact]
    public void CopyStatusRequest_requires_a_status()
    {
        var request = new CopyStatusRequest { Status = "" };

        AssertInvalid(request, nameof(CopyStatusRequest.Status));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void ResetReaderPasswordRequest_requires_eight_characters(string password)
    {
        var request = new ResetReaderPasswordRequest { NewPassword = password };

        AssertInvalid(request, nameof(ResetReaderPasswordRequest.NewPassword));
    }

    [Fact]
    public void UpdateReaderRequest_validates_email_and_borrow_limit()
    {
        var request = new UpdateReaderRequest
        {
            FullName = "Reader",
            Email = "invalid-email",
            CardStatus = "Active",
            BorrowLimit = 0,
            CardExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true
        };

        AssertInvalid(request, nameof(UpdateReaderRequest.Email));
        AssertInvalid(request, nameof(UpdateReaderRequest.BorrowLimit));
    }

    [Fact]
    public void UpdateBookRequest_requires_core_catalog_fields()
    {
        var request = new UpdateBookRequest
        {
            Category = "",
            Title = "",
            Author = "",
            IsActive = true
        };

        AssertInvalid(request, nameof(UpdateBookRequest.Category));
        AssertInvalid(request, nameof(UpdateBookRequest.Title));
        AssertInvalid(request, nameof(UpdateBookRequest.Author));
    }

    [Theory]
    [InlineData(100000001)]
    public void CollectFineRequest_rejects_amount_above_supported_limit(decimal amount)
    {
        var request = new CollectFineRequest { Amount = amount, Reason = "Payment" };

        AssertInvalid(request, nameof(CollectFineRequest.Amount));
    }

    [Fact]
    public void CollectFineRequest_requires_reason()
    {
        var request = new CollectFineRequest { Amount = 0, Reason = "" };

        AssertInvalid(request, nameof(CollectFineRequest.Reason));
    }

    [Fact]
    public void UpdateSystemConfigRequest_requires_value()
    {
        var request = new UpdateSystemConfigRequest { Value = "" };

        AssertInvalid(request, nameof(UpdateSystemConfigRequest.Value));
    }

    [Fact]
    public void CreateProposalRequest_rejects_reason_over_maximum_length()
    {
        var request = new CreateProposalRequest
        {
            Title = "Title",
            Author = "Author",
            Reason = new string('r', 2001)
        };

        AssertInvalid(request, nameof(CreateProposalRequest.Reason));
    }

    [Fact]
    public void CheckoutRequest_requires_reader_card_and_copy_barcode()
    {
        var request = new CheckoutRequest { ReaderCardNo = "", Barcode = "" };

        AssertInvalid(request, nameof(CheckoutRequest.ReaderCardNo));
        AssertInvalid(request, nameof(CheckoutRequest.Barcode));
    }

    [Fact]
    public void UploadBookCoverRequest_requires_a_file()
    {
        var request = new UploadBookCoverRequest { File = null! };

        AssertInvalid(request, nameof(UploadBookCoverRequest.File));
    }

    private static CreateBookRequest ValidCreateBook() => new()
    {
        Category = "History",
        Title = "Book",
        Author = "Author",
        InitialCopies = 1
    };

    private static CreateReaderRequest ValidCreateReader() => new()
    {
        Username = "reader",
        InitialPassword = "long-password",
        Email = "reader@example.test",
        FullName = "Reader",
        CardNo = "CARD-1",
        BorrowLimit = 5,
        CardValidityMonths = 48
    };

    private static void AssertInvalid(object request, string memberName) =>
        Assert.Contains(Validate(request), result => result.MemberNames.Contains(memberName));

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
