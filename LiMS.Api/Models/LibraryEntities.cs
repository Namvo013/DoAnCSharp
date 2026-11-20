namespace LiMS.Api.Models;

/// <summary>Danh sách vai trò được phép đăng nhập vào hệ thống.</summary>
public static class AccountRoles
{
    public const string Borrower = "Borrower";
    public const string Librarian = "Librarian";
    public const string Admin = "Admin";
}

/// <summary>Các trạng thái chuẩn dùng cho hồ sơ và nghiệp vụ thư viện.</summary>
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

/// <summary>Tài khoản xác thực và thông tin định danh chung.</summary>
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

/// <summary>Thẻ độc giả và quan hệ tới các phiếu mượn, đặt giữ.</summary>
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
    // Danh sách thông báo nội bộ của độc giả.
    public ICollection<ReaderNotification> Notifications { get; set; }
        = new List<ReaderNotification>();

    // Danh sách sự kiện đang chờ hệ thống xử lý thành thông báo.
    public ICollection<NotificationOutbox> NotificationOutboxes { get; set; }
        = new List<NotificationOutbox>();
}

/// <summary>Hồ sơ thủ thư chịu trách nhiệm ghi nhận nghiệp vụ tại quầy.</summary>
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

/// <summary>Nhật ký kiểm toán các thao tác trên dữ liệu thư viện.</summary>
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

/// <summary>Giá trị cấu hình cho các quy tắc nghiệp vụ của thư viện.</summary>
public sealed class SystemConfig
{
    public required string ConfigKey { get; set; }
    public required string ConfigValue { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Thể loại dùng để phân nhóm đầu sách.</summary>
public sealed class Category
{
    public int CategoryId { get; set; }
    public required string Name { get; set; }
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

/// <summary>Thông tin thư mục của một đầu sách.</summary>
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
    public byte[]? CoverImageData { get; set; }
    public string? CoverImageContentType { get; set; }
    public bool IsActive { get; set; } = true;
    public Category Category { get; set; } = null!;
    public ICollection<BookCopy> Copies { get; set; } = new List<BookCopy>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

/// <summary>Một bản sao vật lý có mã vạch và trạng thái riêng.</summary>
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

/// <summary>Yêu cầu của độc giả để giữ một đầu sách đang được mượn.</summary>
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

/// <summary>Phiếu mượn gắn với một bản sao và một độc giả.</summary>
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

/// <summary>Khoản phạt phát sinh từ phiếu mượn.</summary>
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

/// <summary>Đề xuất bổ sung đầu sách do độc giả gửi.</summary>
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

/// <summary>Thông tin ghi nhận các thông báo gửi tới độc giả.</summary>
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


/// <summary>
/// Thông báo nội bộ được lưu lại để độc giả xem trên giao diện Web.
/// </summary>
public sealed class ReaderNotification
{
    public long NotificationId { get; set; }

    // Độc giả nhận thông báo.
    public int ReaderId { get; set; }

    // Tiêu đề và nội dung hiển thị.
    public required string Title { get; set; }
    public required string Message { get; set; }

    // DueSoon, Overdue, ReservationReady,
    // ReservationExpired, ReservationCancelled.
    public required string Type { get; set; }

    // Loại và ID đối tượng liên quan: Loan hoặc Reservation.
    public string? RelatedEntityType { get; set; }
    public long? RelatedEntityId { get; set; }

    // Trạng thái đã đọc.
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    // Chống tạo thông báo trùng lặp.
    public required string DeduplicationKey { get; set; }

    public Reader Reader { get; set; } = null!;
}

/// <summary>
/// Hàng đợi sự kiện để xử lý thông báo sau khi nghiệp vụ chính hoàn tất.
/// </summary>
public sealed class NotificationOutbox
{
    public long OutboxId { get; set; }

    public int ReaderId { get; set; }

    // Ví dụ: ReservationReady, ReservationExpired,
    // ReservationCancelled.
    public required string EventType { get; set; }

    // ID phiếu đặt giữ phát sinh sự kiện.
    public long ReservationId { get; set; }

    // Khóa chống ghi sự kiện trùng.
    public required string DeduplicationKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // null nghĩa là chưa xử lý.
    public DateTime? ProcessedAt { get; set; }

    public int Attempts { get; set; } = 0;
    public string? LastError { get; set; }

    public Reader Reader { get; set; } = null!;
}