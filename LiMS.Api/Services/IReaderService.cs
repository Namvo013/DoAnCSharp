using LiMS.Api.Contracts;

namespace LiMS.Api.Services;

/// <summary>Các thao tác tự phục vụ được giới hạn trong dữ liệu của độc giả hiện tại.</summary>
public interface IReaderService
{
    Task<ReservationResponse?> GetReservationAsync(int accountId, long reservationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProposalResponse>> GetProposalsAsync(int accountId, CancellationToken cancellationToken);
    Task<ProposalResponse?> GetProposalAsync(int accountId, int proposalId, CancellationToken cancellationToken);
    Task<ReaderProfileData> UpdateProfileAsync(
        int accountId,
        UpdateReaderProfileRequest request,
        CancellationToken cancellationToken);
    Task<ReservationResponse> CreateReservationAsync(int accountId, CreateReservationRequest request, CancellationToken cancellationToken);
    Task CancelReservationAsync(int accountId, long reservationId, CancellationToken cancellationToken);
    Task<ProposalResponse> CreateProposalAsync(int accountId, CreateProposalRequest request, CancellationToken cancellationToken);
    Task<LoanResponse> RenewAsync(int accountId, long loanId, CancellationToken cancellationToken);
}
