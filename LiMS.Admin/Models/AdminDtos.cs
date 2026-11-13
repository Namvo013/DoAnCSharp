namespace LiMS.Admin.Models;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(
    int Id,
    string StudentCode,
    string FullName,
    string? Email,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAt);
public sealed record MessageResponse(string Message);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record BookResponse(
    int Id,
    string Title,
    string Author,
    string? Isbn,
    string Category,
    string? Publisher,
    short? PublishedYear,
    int TotalCopies,
    int AvailableCopies,
    string? ShelfLocation,
    string? Description);
public sealed record BookCopyResponse(int Id, int BookId, string Barcode, string Status);
public sealed record CreateBookRequest(
    string Category,
    string Title,
    string Author,
    string? Isbn,
    string? Publisher,
    short? PublishYear,
    string? ShelfLocation,
    string? Description,
    int InitialCopies);
public sealed record UpdateBookRequest(
    string Category,
    string Title,
    string Author,
    string? Isbn,
    string? Publisher,
    short? PublishYear,
    string? ShelfLocation,
    string? Description,
    bool IsActive);
public sealed record CreateCopiesRequest(int Quantity, string BarcodePrefix);
public sealed record CopyStatusRequest(string Status);
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
public sealed record CreateReaderRequest(
    string Username,
    string InitialPassword,
    string Email,
    string FullName,
    string? Phone,
    string CardNo,
    short BorrowLimit,
    int CardValidityMonths);
public sealed record UpdateReaderRequest(
    string FullName,
    string Email,
    string? Phone,
    string CardStatus,
    short BorrowLimit,
    DateOnly CardExpiryDate,
    bool IsActive);
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
public sealed record PopularBookResponse(int BookId, string Title, int LoanCount);
public sealed record CheckoutRequest(string ReaderCardNo, string Barcode, long? ReservationId);
public sealed record ReturnRequest(string Barcode, string? FineReason, decimal AdditionalFineAmount);
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
public sealed record CollectFineRequest(decimal Amount, string Reason);
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
public sealed record ReviewProposalRequest(string Decision, string? Note);
public sealed record SystemConfigResponse(string Key, string Value, string? Description, DateTimeOffset UpdatedAt);
public sealed record UpdateSystemConfigRequest(string Value, string? Description);
public sealed record ActivityLogResponse(
    long Id,
    string Username,
    string Action,
    string TargetTable,
    int? TargetId,
    string? Detail,
    DateTimeOffset CreatedAt);
public sealed record ApiProblem(string? Title, string? Detail);
