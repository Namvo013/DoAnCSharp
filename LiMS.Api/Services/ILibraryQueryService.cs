using LiMS.Api.Contracts;

namespace LiMS.Api.Services;

public sealed record BookCoverData(byte[] Data, string ContentType);

public sealed record ReaderProfileData(
    int ReaderId,
    int AccountId,
    string CardNo,
    string FullName,
    string Email,
    string Role);

public interface ILibraryQueryService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BookResponse>> GetPublicBooksAsync(CancellationToken cancellationToken);
    Task<BookCoverData?> GetBookCoverAsync(int bookId, CancellationToken cancellationToken);
    Task<ReaderProfileData?> GetReaderProfileAsync(string studentCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReaderLoanResponse>> GetReaderLoansAsync(int readerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReaderReservationResponse>> GetReaderReservationsAsync(int readerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReaderNotificationResponse>> GetReaderNotificationsAsync(int readerId, CancellationToken cancellationToken);
}
