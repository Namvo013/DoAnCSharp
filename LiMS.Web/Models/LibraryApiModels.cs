namespace LiMS.Web.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record ApiUser(
    int Id,
    string StudentCode,
    string FullName,
    string? Email,
    string Role,
    string? AccessToken = null,
    DateTimeOffset? ExpiresAt = null,
    string? Phone = null,
    DateOnly? CardExpiryDate = null,
    string? CardStatus = null);

public sealed record ApiBook(
    int Id,
    string Title,
    string? Author,
    string? Isbn,
    string? Category,
    string? Publisher,
    int? PublishedYear,
    int TotalCopies,
    int AvailableCopies,
    int OnLoanCopies,
    string? ShelfLocation = null,
    string? Description = null,
    string? CoverImageUrl = null);

public sealed record ApiLoan(
    int Id,
    int UserId,
    int BookId,
    DateTimeOffset BorrowedDate,
    DateTimeOffset DueDate,
    DateTimeOffset? ReturnedDate,
    string Status,
    int RenewalCount,
    ApiBook? Book);

public sealed record ApiReservation(
    int Id,
    int BookId,
    DateTimeOffset CreatedDate,
    DateTimeOffset ExpiryDate,
    string Status,
    ApiBook? Book);

public sealed record ApiNotification(
    int Id,
    string Message,
    DateTimeOffset CreatedAt,
    bool IsRead);

public sealed record ChangePasswordRequest(
    string StudentCode,
    string CurrentPassword,
    string NewPassword);

public sealed record UpdateProfileRequest(string FullName, string Email, string? Phone);

public sealed record ApiMessage(string Message);

public sealed record CreatedReservation(
    long Id,
    int BookId,
    string BookTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpireAt,
    string Status);

public sealed record LoanRenewalResult(long Id, DateOnly DueDate, short RenewCount, string Status);

public sealed record CreatePurchaseProposalRequest(string Title, string Author, string Reason);

public sealed record PurchaseProposal(
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
