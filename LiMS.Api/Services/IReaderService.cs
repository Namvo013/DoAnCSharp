using LiMS.Api.Contracts;

namespace LiMS.Api.Services;

public interface IReaderService
{
    Task<ReservationResponse?> GetReservationAsync(int accountId, long reservationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProposalResponse>> GetProposalsAsync(int accountId, CancellationToken cancellationToken);
    Task<ProposalResponse?> GetProposalAsync(int accountId, int proposalId, CancellationToken cancellationToken);
    Task<ReservationResponse> CreateReservationAsync(int accountId, CreateReservationRequest request, CancellationToken cancellationToken);
    Task CancelReservationAsync(int accountId, long reservationId, CancellationToken cancellationToken);
    Task<ProposalResponse> CreateProposalAsync(int accountId, CreateProposalRequest request, CancellationToken cancellationToken);
    Task<LoanResponse> RenewAsync(int accountId, long loanId, CancellationToken cancellationToken);
}
