using LiMS.Api.Contracts;

namespace LiMS.Api.Services;

/// <summary>Dữ liệu ảnh bìa sách và định dạng nội dung.</summary>
public sealed record BookCoverData(byte[] Data, string ContentType);

/// <summary>Dữ liệu hồ sơ độc giả dùng cho các truy vấn đã phân quyền.</summary>
public sealed record ReaderProfileData(
    int ReaderId,
    int AccountId,
    string CardNo,
    string FullName,
    string Email,
    string Role,
    string? Phone,
    DateOnly CardExpiryDate,
    string CardStatus);

/// <summary>Các truy vấn công khai và truy vấn hồ sơ thư viện đã được phân quyền.</summary>
public interface ILibraryQueryService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BookResponse>> GetPublicBooksAsync(CancellationToken cancellationToken);
    Task<PagedResponse<BookResponse>> GetPublicBooksPageAsync(
        string? search,
        string? category,
        string? availability,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetPublicBookCategoriesAsync(CancellationToken cancellationToken);
    Task<BookResponse?> GetPublicBookAsync(int bookId, CancellationToken cancellationToken);
    Task<BookCoverData?> GetBookCoverAsync(int bookId, CancellationToken cancellationToken);
    Task<ReaderProfileData?> GetReaderProfileAsync(string studentCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReaderLoanResponse>> GetReaderLoansAsync(int readerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReaderReservationResponse>> GetReaderReservationsAsync(int readerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReaderNotificationResponse>> GetReaderNotificationsAsync(int readerId, CancellationToken cancellationToken);
}
