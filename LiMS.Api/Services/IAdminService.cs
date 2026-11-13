using LiMS.Api.Contracts;

namespace LiMS.Api.Services;

public interface IAdminService
{
    Task<IReadOnlyList<BookResponse>> GetBooksAsync(string? search, CancellationToken cancellationToken);
    Task<BookResponse?> GetBookAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookCopyResponse>> GetCopiesAsync(int bookId, CancellationToken cancellationToken);
    Task<BookResponse> CreateBookAsync(CreateBookRequest request, int actorId, CancellationToken cancellationToken);
    Task<BookResponse> UpdateBookAsync(int id, UpdateBookRequest request, int actorId, CancellationToken cancellationToken);
    Task AddCopiesAsync(int bookId, CreateCopiesRequest request, int actorId, CancellationToken cancellationToken);
    Task UpdateCopyStatusAsync(int copyId, CopyStatusRequest request, int actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReaderResponse>> GetReadersAsync(string? search, CancellationToken cancellationToken);
    Task<ReaderResponse?> GetReaderAsync(int readerId, CancellationToken cancellationToken);
    Task<ReaderResponse> CreateReaderAsync(CreateReaderRequest request, int actorId, CancellationToken cancellationToken);
    Task<ReaderResponse> UpdateReaderAsync(int readerId, UpdateReaderRequest request, int actorId, CancellationToken cancellationToken);
    Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanResponse>> GetLoansAsync(string? status, CancellationToken cancellationToken);
    Task<LoanResponse?> GetLoanAsync(long loanId, CancellationToken cancellationToken);
    Task<LoanResponse> CheckoutAsync(CheckoutRequest request, int actorId, CancellationToken cancellationToken);
    Task<LoanResponse> ReturnAsync(ReturnRequest request, int actorId, CancellationToken cancellationToken);
    Task<LoanResponse> RenewAsync(long loanId, int actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FineResponse>> GetFinesAsync(string? status, CancellationToken cancellationToken);
    Task<FineResponse> CollectFineAsync(long fineId, CollectFineRequest request, int actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProposalResponse>> GetProposalsAsync(string? status, CancellationToken cancellationToken);
    Task<ProposalResponse> ReviewProposalAsync(int proposalId, ReviewProposalRequest request, int actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SystemConfigResponse>> GetConfigsAsync(CancellationToken cancellationToken);
    Task<SystemConfigResponse> UpdateConfigAsync(string key, UpdateSystemConfigRequest request, int actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActivityLogResponse>> GetActivityLogsAsync(int page, int pageSize, CancellationToken cancellationToken);
}
