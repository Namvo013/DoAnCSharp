namespace LiMS.Api.Models;

public static class AccountRoles
{
    public const string Borrower = "Borrower";
    public const string Librarian = "Librarian";
    public const string Admin = "Admin";
}

public static class RecordStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Available = "Available";
    public const string OnLoan = "OnLoan";
    public const string Maintenance = "Maintenance";
    public const string Unavailable = "Unavailable";
    public const string Lost = "Lost";
    public const string Pending = "Pending";
    public const string ReadyForPickup = "ReadyForPickup";
    public const string Fulfilled = "Fulfilled";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string Returned = "Returned";
    public const string Overdue = "Overdue";
    public const string Paid = "Paid";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public sealed class Account
{
    public int AccountId { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Reader? Reader { get; set; }
    public Librarian? Librarian { get; set; }
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
}

public sealed class Reader
{
    public int ReaderId { get; set; }
    public int AccountId { get; set; }
    public required string CardNo { get; set; }
    public DateOnly CardIssueDate { get; set; }
    public DateOnly CardExpiryDate { get; set; }
    public required string CardStatus { get; set; }
    public short BorrowLimit { get; set; }
    public Account Account { get; set; } = null!;
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<PurchaseProposal> PurchaseProposals { get; set; } = new List<PurchaseProposal>();
    public ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();
}

public sealed class Librarian
{
    public int LibrarianId { get; set; }
    public int AccountId { get; set; }
    public required string StaffCode { get; set; }
    public Account Account { get; set; } = null!;
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Fine> CollectedFines { get; set; } = new List<Fine>();
    public ICollection<PurchaseProposal> ReviewedProposals { get; set; } = new List<PurchaseProposal>();
}

public sealed class ActivityLog
{
    public long LogId { get; set; }
    public int AccountId { get; set; }
    public required string Action { get; set; }
    public required string TargetTable { get; set; }
    public int? TargetId { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Account Account { get; set; } = null!;
}

public sealed class SystemConfig
{
    public required string ConfigKey { get; set; }
    public required string ConfigValue { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Category
{
    public int CategoryId { get; set; }
    public required string Name { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

public sealed class Book
{
    public int BookId { get; set; }
    public int CategoryId { get; set; }
    public required string Title { get; set; }
    public required string Author { get; set; }
    public string? Isbn { get; set; }
    public string? Publisher { get; set; }
    public short? PublishYear { get; set; }
    public string? ShelfLocation { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Category Category { get; set; } = null!;
    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

public sealed class BookCopy
{
    public int CopyId { get; set; }
    public int BookId { get; set; }
    public required string Barcode { get; set; }
    public required string Status { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Book Book { get; set; } = null!;
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}

public sealed class Reservation
{
    public long ReservationId { get; set; }
    public int ReaderId { get; set; }
    public int BookId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpireAt { get; set; }
    public required string Status { get; set; }
    public Reader Reader { get; set; } = null!;
    public Book Book { get; set; } = null!;
    public Loan? Loan { get; set; }
    public ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();
}

public sealed class Loan
{
    public long LoanId { get; set; }
    public int ReaderId { get; set; }
    public int CopyId { get; set; }
    public int LibrarianId { get; set; }
    public long? ReservationId { get; set; }
    public DateTime BorrowDate { get; set; } = DateTime.UtcNow;
    public DateOnly DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public short RenewCount { get; set; }
    public required string Status { get; set; }
    public Reader Reader { get; set; } = null!;
    public BookCopy Copy { get; set; } = null!;
    public Librarian Librarian { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
    public ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();
}

public sealed class Fine
{
    public long FineId { get; set; }
    public long LoanId { get; set; }
    public int? CollectedBy { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public required string Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public Loan Loan { get; set; } = null!;
    public Librarian? Collector { get; set; }
}

public sealed class PurchaseProposal
{
    public int ProposalId { get; set; }
    public int ReaderId { get; set; }
    public int? ReviewedBy { get; set; }
    public required string Title { get; set; }
    public required string Author { get; set; }
    public required string Reason { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public Reader Reader { get; set; } = null!;
    public Librarian? Reviewer { get; set; }
}

public sealed class EmailLog
{
    public long EmailId { get; set; }
    public int ReaderId { get; set; }
    public long? LoanId { get; set; }
    public long? ReservationId { get; set; }
    public required string EmailType { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public required string SendStatus { get; set; }
    public Reader Reader { get; set; } = null!;
    public Loan? Loan { get; set; }
    public Reservation? Reservation { get; set; }
}
