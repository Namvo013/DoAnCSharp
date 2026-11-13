using System.ComponentModel.DataAnnotations;

namespace LiMS.Api.Contracts;

/// <summary>Credentials submitted to sign in.</summary>
public sealed record LoginRequest
{
    [Required, StringLength(100)]
    public required string Username { get; init; }

    [Required, StringLength(256)]
    public required string Password { get; init; }
}

/// <summary>Signed-in account profile and bearer token.</summary>
public sealed record LoginResponse(
    int Id,
    string StudentCode,
    string FullName,
    string? Email,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAt);

/// <summary>Values required to change the current account password.</summary>
public sealed record ChangePasswordRequest
{
    [Required, StringLength(256, MinimumLength = 6)]
    public required string CurrentPassword { get; init; }

    [Required, StringLength(256, MinimumLength = 8)]
    public required string NewPassword { get; init; }
}

/// <summary>A user-facing operation result.</summary>
public sealed record MessageResponse(string Message);

/// <summary>Book metadata and current physical-copy counts.</summary>
public sealed record BookResponse(
    int Id,
    string Title,
    string Author,
    string? Isbn,
    string Category,
    string? Publisher,
    int? PublishedYear,
    int TotalCopies,
    int AvailableCopies,
    string? ShelfLocation,
    string? Description);

/// <summary>Barcode, status, and parent book for one physical copy.</summary>
public sealed record BookCopyResponse(int Id, int BookId, string Barcode, string Status);

/// <summary>Book metadata and the number of copies to register.</summary>
public sealed record CreateBookRequest
{
    [Required, StringLength(100)]
    public required string Category { get; init; }

    [Required, StringLength(255)]
    public required string Title { get; init; }

    [Required, StringLength(150)]
    public required string Author { get; init; }

    [StringLength(20)]
    public string? Isbn { get; init; }

    [StringLength(100)]
    public string? Publisher { get; init; }

    [Range(1400, 2100)]
    public short? PublishYear { get; init; }

    [StringLength(50)]
    public string? ShelfLocation { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }

    [Range(0, 10000)]
    public int InitialCopies { get; init; }
}

/// <summary>Updated book metadata and visibility.</summary>
public sealed record UpdateBookRequest
{
    [Required, StringLength(100)]
    public required string Category { get; init; }

    [Required, StringLength(255)]
    public required string Title { get; init; }

    [Required, StringLength(150)]
    public required string Author { get; init; }

    [StringLength(20)]
    public string? Isbn { get; init; }

    [StringLength(100)]
    public string? Publisher { get; init; }

    [Range(1400, 2100)]
    public short? PublishYear { get; init; }

    [StringLength(50)]
    public string? ShelfLocation { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }

    public bool IsActive { get; init; }
}

/// <summary>Request to register a batch of physical book copies.</summary>
public sealed record CreateCopiesRequest
{
    [Range(1, 1000)]
    public int Quantity { get; init; }

    [Required, StringLength(45, MinimumLength = 1), RegularExpression("^[A-Za-z0-9._-]+$")]
    public required string BarcodePrefix { get; init; }
}

/// <summary>Requested status for a physical copy.</summary>
public sealed record CopyStatusRequest
{
    [Required, StringLength(20)]
    public required string Status { get; init; }
}

/// <summary>Information needed to create a reader account and library card.</summary>
public sealed record CreateReaderRequest
{
    [Required, StringLength(50)]
    public required string Username { get; init; }

    [Required, StringLength(256, MinimumLength = 8)]
    public required string InitialPassword { get; init; }

    [Required, EmailAddress, StringLength(100)]
    public required string Email { get; init; }

    [Required, StringLength(100)]
    public required string FullName { get; init; }

    [Phone, StringLength(20)]
    public string? Phone { get; init; }

    [Required, StringLength(20)]
    public required string CardNo { get; init; }

    [Range(1, 100)]
    public short BorrowLimit { get; init; } = 5;

    [Range(1, 120)]
    public int CardValidityMonths { get; init; } = 48;
}

/// <summary>Editable reader profile, card status, and borrowing limit.</summary>
public sealed record UpdateReaderRequest
{
    [Required, StringLength(100)]
    public required string FullName { get; init; }

    [Required, EmailAddress, StringLength(100)]
    public required string Email { get; init; }

    [Phone, StringLength(20)]
    public string? Phone { get; init; }

    [Required, StringLength(20)]
    public required string CardStatus { get; init; }

    [Range(1, 100)]
    public short BorrowLimit { get; init; }

    public DateOnly CardExpiryDate { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Reader account and library-card details.</summary>
public sealed record ReaderResponse(
    int ReaderId,
    int AccountId,
    string Username,
    string FullName,
    string Email,
    string? Phone,
    string CardNo,
    DateOnly CardIssueDate,
    DateOnly CardExpiryDate,
    string CardStatus,
    short BorrowLimit,
    bool IsActive,
    string Role);

/// <summary>Book barcode and reader card used for checkout.</summary>
public sealed record CheckoutRequest
{
    [Required, StringLength(50)]
    public required string ReaderCardNo { get; init; }

    [Required, StringLength(50)]
    public required string Barcode { get; init; }

    public long? ReservationId { get; init; }
}

/// <summary>Barcode and optional fine details recorded on return.</summary>
public sealed record ReturnRequest
{
    [Required, StringLength(50)]
    public required string Barcode { get; init; }

    [StringLength(255)]
    public string? FineReason { get; init; }

    [Range(0, 100000000)]
    public decimal AdditionalFineAmount { get; init; }
}

/// <summary>Identifies a loan that should be renewed.</summary>
public sealed record RenewLoanRequest
{
    public long LoanId { get; init; }
}

/// <summary>Loan information returned to API clients.</summary>
public sealed record LoanResponse(
    long Id,
    int ReaderId,
    int BookId,
    int CopyId,
    string Barcode,
    string BookTitle,
    string ReaderName,
    string ReaderCardNo,
    string LibrarianName,
    long? ReservationId,
    DateTimeOffset BorrowDate,
    DateOnly DueDate,
    DateTimeOffset? ReturnDate,
    short RenewCount,
    string Status);

/// <summary>Loan summary shown to the reader who owns the record.</summary>
public sealed record ReaderLoanResponse(
    long Id,
    int UserId,
    int BookId,
    DateTimeOffset BorrowedDate,
    DateTimeOffset DueDate,
    DateTimeOffset? ReturnedDate,
    string Status,
    int RenewalCount,
    BookResponse? Book);

/// <summary>Fine amount and collection details.</summary>
public sealed record FineResponse(
    long Id,
    long LoanId,
    string BookTitle,
    string ReaderName,
    string ReaderCardNo,
    decimal Amount,
    string Reason,
    string Status,
    DateTimeOffset? PaidAt);

/// <summary>Full or implicit payment of an outstanding fine.</summary>
public sealed record CollectFineRequest
{
    [Range(0, 100000000)]
    public decimal Amount { get; init; }

    [Required, StringLength(255)]
    public required string Reason { get; init; }
}

/// <summary>Decision and optional note for a purchase proposal.</summary>
public sealed record ReviewProposalRequest
{
    [Required]
    [RegularExpression("^(Approved|Rejected)$")]
    public required string Decision { get; init; }

    [StringLength(1000)]
    public string? Note { get; init; }
}

/// <summary>Reader purchase proposal and its review status.</summary>
public sealed record ProposalResponse(
    int ProposalId,
    string ReaderName,
    string ReaderCardNo,
    string Title,
    string Author,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt,
    string? ReviewerName,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);

/// <summary>Aggregate counts and circulation statistics for the admin dashboard.</summary>
public sealed record DashboardResponse(
    int TotalBooks,
    int TotalCopies,
    int AvailableCopies,
    int ActiveReaders,
    int ActiveLoans,
    int OverdueLoans,
    int PendingReservations,
    int PendingProposals,
    decimal UnpaidFines,
    decimal CollectedFines,
    IReadOnlyList<PopularBookResponse> PopularBooks);

/// <summary>Loan total for one popular book.</summary>
public sealed record PopularBookResponse(int BookId, string Title, int LoanCount);

/// <summary>Business rule stored in system configuration.</summary>
public sealed record SystemConfigResponse(string Key, string Value, string? Description, DateTimeOffset UpdatedAt);

/// <summary>Replacement value and description for a supported business setting.</summary>
public sealed record UpdateSystemConfigRequest
{
    [Required, StringLength(1000)]
    public required string Value { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }
}

/// <summary>Audited administrative action.</summary>
public sealed record ActivityLogResponse(
    long Id,
    string Username,
    string Action,
    string TargetTable,
    int? TargetId,
    string? Detail,
    DateTimeOffset CreatedAt);

/// <summary>Reader reservation and associated book.</summary>
public sealed record ReservationResponse(
    long Id,
    int BookId,
    string BookTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpireAt,
    string Status);

/// <summary>Reservation summary shown to the reader who owns the record.</summary>
public sealed record ReaderReservationResponse(
    long Id,
    int BookId,
    DateTimeOffset CreatedDate,
    DateTimeOffset ExpiryDate,
    string Status,
    BookResponse? Book);

/// <summary>Notification history returned to the current reader.</summary>
public sealed record ReaderNotificationResponse(
    long Id,
    string Message,
    DateTimeOffset CreatedAt,
    bool IsRead);

/// <summary>Book identifier requested for reservation.</summary>
public sealed record CreateReservationRequest
{
    [Range(1, int.MaxValue)]
    public int BookId { get; init; }
}

/// <summary>Reader-submitted request to acquire a book for the library.</summary>
public sealed record CreateProposalRequest
{
    [Required, StringLength(255)]
    public required string Title { get; init; }

    [Required, StringLength(100)]
    public required string Author { get; init; }

    [Required, StringLength(2000)]
    public required string Reason { get; init; }
}
