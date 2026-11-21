using System.ComponentModel.DataAnnotations;

namespace LiMS.Api.Contracts;

/// <summary>Phản hồi thống nhất cho các lỗi API; dữ liệu thành công giữ nguyên để tương thích client.</summary>
public sealed record ApiResponse<T>(
    bool Success,
    string Code,
    string Message,
    T? Data = default,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static ApiResponse<T> Failure(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(false, code, message, default, errors);
}

/// <summary>Thông tin đăng nhập được gửi để xác thực.</summary>
public sealed record LoginRequest
{
    [Required, StringLength(100)]
    public required string Username { get; init; }

    [Required, StringLength(256)]
    public required string Password { get; init; }
}

/// <summary>Hồ sơ tài khoản đã đăng nhập và mã truy cập.</summary>
public sealed record LoginResponse(
    int Id,
    string StudentCode,
    string FullName,
    string? Email,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAt);

/// <summary>Thông tin cần thiết để đổi mật khẩu tài khoản hiện tại.</summary>
public sealed record ChangePasswordRequest
{
    [Required, StringLength(256, MinimumLength = 6)]
    public required string CurrentPassword { get; init; }

    [Required, StringLength(256, MinimumLength = 8)]
    public required string NewPassword { get; init; }
}

/// <summary>Kết quả thao tác hiển thị cho người dùng.</summary>
public sealed record MessageResponse(string Message);

/// <summary>Thông tin đầu sách và số lượng bản sao hiện có.</summary>
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
    int OnLoanCopies,
    string? ShelfLocation,
    string? Description,
    string? CoverImageUrl);

/// <summary>Mã vạch, trạng thái và đầu sách của một bản sao.</summary>
public sealed record BookCopyResponse(int Id, int BookId, string Barcode, string Status);

/// <summary>Tệp ảnh bìa được tải lên cho một đầu sách.</summary>
public sealed record UploadBookCoverRequest
{
    [Required]
    public required IFormFile File { get; init; }
}

/// <summary>Thông tin đầu sách và số bản sao cần ghi nhận.</summary>
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

/// <summary>Thông tin đầu sách và trạng thái hiển thị sau cập nhật.</summary>
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

/// <summary>Yêu cầu ghi nhận một lô bản sao sách.</summary>
public sealed record CreateCopiesRequest
{
    [Range(1, 1000)]
    public int Quantity { get; init; }

    [Required, StringLength(45, MinimumLength = 1), RegularExpression("^[A-Za-z0-9._-]+$")]
    public required string BarcodePrefix { get; init; }
}

/// <summary>Trạng thái cần đặt cho một bản sao sách.</summary>
public sealed record CopyStatusRequest
{
    [Required, StringLength(20)]
    public required string Status { get; init; }
}

/// <summary>Thông tin cần thiết để tạo tài khoản và thẻ độc giả.</summary>
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

/// <summary>Hồ sơ độc giả, trạng thái thẻ và giới hạn mượn có thể chỉnh sửa.</summary>
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

/// <summary>Mật khẩu mới do quản trị viên đặt cho tài khoản độc giả.</summary>
public sealed record ResetReaderPasswordRequest
{
    [Required, StringLength(256, MinimumLength = 8)]
    public required string NewPassword { get; init; }
}

/// <summary>Thông tin tài khoản và thẻ thư viện của độc giả.</summary>
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

/// <summary>Hồ sơ tài khoản và trạng thái thẻ của độc giả hiện tại.</summary>
public sealed record ReaderProfileResponse(
    int Id,
    string StudentCode,
    string FullName,
    string Email,
    string Role,
    string? Phone,
    DateOnly CardExpiryDate,
    string CardStatus);

/// <summary>Thông tin hồ sơ mà độc giả được phép tự cập nhật.</summary>
public sealed record UpdateReaderProfileRequest
{
    [Required, StringLength(100)]
    public required string FullName { get; init; }

    [Required, EmailAddress, StringLength(100)]
    public required string Email { get; init; }

    [Phone, StringLength(20)]
    public string? Phone { get; init; }
}

/// <summary>Mã vạch sách và thẻ độc giả dùng để lập phiếu mượn.</summary>
public sealed record CheckoutRequest
{
    [Required, StringLength(50)]
    public required string ReaderCardNo { get; init; }

    [Required, StringLength(50)]
    public required string Barcode { get; init; }

    public long? ReservationId { get; init; }
}

/// <summary>Mã vạch và thông tin phạt bổ sung khi nhận trả sách.</summary>
public sealed record ReturnRequest
{
    [Required, StringLength(50)]
    public required string Barcode { get; init; }

    [StringLength(255)]
    public string? FineReason { get; init; }

    [Range(0, 100000000)]
    public decimal AdditionalFineAmount { get; init; }
}

/// <summary>Mã phiếu mượn cần gia hạn.</summary>
public sealed record RenewLoanRequest
{
    public long LoanId { get; init; }
}

/// <summary>Thông tin phiếu mượn trả về cho client API.</summary>
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

/// <summary>Tóm tắt phiếu mượn hiển thị cho độc giả sở hữu phiếu.</summary>
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

/// <summary>Số tiền phạt và thông tin thu phạt.</summary>
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

/// <summary>Yêu cầu thanh toán toàn bộ khoản phạt còn nợ.</summary>
public sealed record CollectFineRequest
{
    [Range(0, 100000000)]
    public decimal Amount { get; init; }

    [Required, StringLength(255)]
    public required string Reason { get; init; }
}

/// <summary>Quyết định và ghi chú tùy chọn cho đề xuất mua sách.</summary>
public sealed record ReviewProposalRequest
{
    [Required]
    [RegularExpression("^(Approved|Rejected)$")]
    public required string Decision { get; init; }

    [StringLength(1000)]
    public string? Note { get; init; }
}

/// <summary>Đề xuất mua sách của độc giả và trạng thái xét duyệt.</summary>
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

/// <summary>Các số liệu tổng hợp phục vụ trang tổng quan quản trị.</summary>
public sealed record DashboardResponse(
    int TotalBooks,
    int TotalCopies,
    int AvailableCopies,
    int ActiveReaders,
    int ActiveLoans,
    int OverdueLoans,
    int PendingReservations,
    int PendingProposals,
    int ApprovedProposals,
    int RejectedProposals,
    decimal UnpaidFines,
    decimal CollectedFines,
    IReadOnlyList<PopularBookResponse> PopularBooks,
    IReadOnlyList<MonthlyCirculationReportResponse> MonthlyReports);

/// <summary>Số lượt mượn của một đầu sách phổ biến.</summary>
public sealed record PopularBookResponse(int BookId, string Title, int LoanCount);

/// <summary>Tổng lượt mượn, trả và tiền phạt đã thu theo tháng.</summary>
public sealed record MonthlyCirculationReportResponse(
    string Month,
    int BorrowCount,
    int ReturnCount,
    decimal CollectedFines);

/// <summary>Thư mục sao lưu SQL Server và danh sách tệp hiện có.</summary>
public sealed record DatabaseBackupStatusResponse(
    string DatabaseName,
    string BackupDirectory,
    IReadOnlyList<DatabaseBackupFileResponse> Backups);

/// <summary>Thông tin một tệp sao lưu cơ sở dữ liệu LiMS.</summary>
public sealed record DatabaseBackupFileResponse(
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt);

/// <summary>Kết quả phục hồi cơ sở dữ liệu LiMS từ bản sao lưu.</summary>
public sealed record DatabaseRestoreResponse(
    string DatabaseName,
    string RestoredFileName,
    string SafetyBackupFileName,
    DateTimeOffset CompletedAt);

/// <summary>Xác nhận phục hồi bằng cách nhập chính xác tên cơ sở dữ liệu.</summary>
public sealed record RestoreDatabaseBackupRequest
{
    [Required, MaxLength(128)]
    public required string ConfirmDatabaseName { get; init; }
}

/// <summary>Quy tắc nghiệp vụ được lưu trong cấu hình hệ thống.</summary>
public sealed record SystemConfigResponse(string Key, string Value, string? Description, DateTimeOffset UpdatedAt);

/// <summary>Giá trị và mô tả mới cho một thiết lập nghiệp vụ được hỗ trợ.</summary>
public sealed record UpdateSystemConfigRequest
{
    [Required, StringLength(1000)]
    public required string Value { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }
}

/// <summary>Thao tác quản trị được ghi nhận vào nhật ký.</summary>
public sealed record ActivityLogResponse(
    long Id,
    string Username,
    string Action,
    string TargetTable,
    int? TargetId,
    string? Detail,
    DateTimeOffset CreatedAt);

/// <summary>Thông tin đặt giữ và đầu sách liên quan.</summary>
public sealed record ReservationResponse(
    long Id,
    int BookId,
    string BookTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpireAt,
    string Status);

/// <summary>Tóm tắt phiếu đặt giữ của độc giả hiện tại.</summary>
public sealed record ReaderReservationResponse(
    long Id,
    int BookId,
    DateTimeOffset CreatedDate,
    DateTimeOffset ExpiryDate,
    string Status,
    BookResponse? Book);

/// <summary>
/// Thông tin một thông báo nội bộ dành cho độc giả.
/// </summary>
public sealed record ReaderNotificationResponse(
    long Id,
    string Title,
    string Message,
    string Type,
    DateTimeOffset CreatedAt,
    bool IsRead);

/// <summary>Mã đầu sách cần đặt giữ.</summary>
public sealed record CreateReservationRequest
{
    [Range(1, int.MaxValue)]
    public int BookId { get; init; }
}

/// <summary>Đề xuất bổ sung đầu sách do độc giả gửi.</summary>
public sealed record CreateProposalRequest
{
    [Required, StringLength(255)]
    public required string Title { get; init; }

    [Required, StringLength(100)]
    public required string Author { get; init; }

    [Required, StringLength(2000)]
    public required string Reason { get; init; }
}
