using System.Security.Claims;
using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiMS.Api.Controllers;

/// <summary>Điểm cuối cho các thao tác tự phục vụ của độc giả đã đăng nhập.</summary>
[ApiController]
[Authorize(Roles = "Borrower")]
[Route("api/library")]
public sealed class ReaderController(IReaderService readerService) : ControllerBase
{
    [HttpPut("profile")]
    [EndpointSummary("Cập nhật thông tin liên hệ của độc giả hiện tại")]
    [ProducesResponseType<ReaderProfileResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReaderProfileResponse>> UpdateProfile(
        UpdateReaderProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await readerService.UpdateProfileAsync(
            GetAccountId(),
            request,
            cancellationToken);
        return Ok(new ReaderProfileResponse(
            profile.AccountId,
            profile.CardNo,
            profile.FullName,
            profile.Email,
            profile.Role,
            profile.Phone,
            profile.CardExpiryDate,
            profile.CardStatus));
    }

    [HttpGet("reservation/{id:long}")]
    [EndpointSummary("Lấy một phiếu đặt giữ của độc giả hiện tại")]
    public async Task<ActionResult<ReservationResponse>> GetReservation(
        long id,
        CancellationToken cancellationToken)
    {
        var reservation = await readerService.GetReservationAsync(GetAccountId(), id, cancellationToken);
        return reservation is null ? NotFound() : Ok(reservation);
    }

    [HttpPost("reservations")]
    [EndpointSummary("Đặt giữ một đầu sách hiện đã hết bản sao")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReservationResponse>> CreateReservation(
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await readerService.CreateReservationAsync(GetAccountId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, reservation);
    }

    [HttpDelete("reservations/{id:long}")]
    [EndpointSummary("Hủy phiếu đặt giữ của độc giả hiện tại")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelReservation(long id, CancellationToken cancellationToken)
    {
        await readerService.CancelReservationAsync(GetAccountId(), id, cancellationToken);
        return NoContent();
    }

    [HttpGet("proposals")]
    [EndpointSummary("Liệt kê đề xuất mua sách của độc giả hiện tại")]
    [ProducesResponseType<IReadOnlyList<ProposalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProposalResponse>>> Proposals(CancellationToken cancellationToken) =>
        Ok(await readerService.GetProposalsAsync(GetAccountId(), cancellationToken));

    [HttpGet("proposals/{id:int}")]
    [EndpointSummary("Lấy một đề xuất mua sách của độc giả hiện tại")]
    public async Task<ActionResult<ProposalResponse>> GetProposal(int id, CancellationToken cancellationToken)
    {
        var proposal = await readerService.GetProposalAsync(GetAccountId(), id, cancellationToken);
        return proposal is null ? NotFound() : Ok(proposal);
    }

    [HttpPost("proposals")]
    [EndpointSummary("Gửi đề xuất bổ sung sách")]
    [ProducesResponseType<ProposalResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProposalResponse>> CreateProposal(
        CreateProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await readerService.CreateProposalAsync(GetAccountId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetProposal), new { id = proposal.ProposalId }, proposal);
    }

    [HttpPost("loans/{id:long}/renew")]
    [EndpointSummary("Gia hạn phiếu mượn đủ điều kiện của độc giả hiện tại")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LoanResponse>> Renew(long id, CancellationToken cancellationToken) =>
        Ok(await readerService.RenewAsync(GetAccountId(), id, cancellationToken));

    private int GetAccountId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var accountId)
            ? accountId
            : throw new UnauthorizedAccessException("JWT account identifier is missing.");
    }
}
