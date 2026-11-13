using System.Security.Claims;
using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiMS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Borrower")]
[Route("api/library")]
public sealed class ReaderController(IReaderService readerService) : ControllerBase
{
    [HttpGet("reservation/{id:long}")]
    [EndpointSummary("Get one reservation belonging to the current reader")]
    public async Task<ActionResult<ReservationResponse>> GetReservation(
        long id,
        CancellationToken cancellationToken)
    {
        var reservation = await readerService.GetReservationAsync(GetAccountId(), id, cancellationToken);
        return reservation is null ? NotFound() : Ok(reservation);
    }

    [HttpPost("reservations")]
    [EndpointSummary("Reserve an unavailable book")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReservationResponse>> CreateReservation(
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await readerService.CreateReservationAsync(GetAccountId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, reservation);
    }

    [HttpDelete("reservations/{id:long}")]
    [EndpointSummary("Cancel a reservation belonging to the current reader")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelReservation(long id, CancellationToken cancellationToken)
    {
        await readerService.CancelReservationAsync(GetAccountId(), id, cancellationToken);
        return NoContent();
    }

    [HttpGet("proposals")]
    [EndpointSummary("List the current reader's purchase proposals")]
    [ProducesResponseType<IReadOnlyList<ProposalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProposalResponse>>> Proposals(CancellationToken cancellationToken) =>
        Ok(await readerService.GetProposalsAsync(GetAccountId(), cancellationToken));

    [HttpGet("proposals/{id:int}")]
    [EndpointSummary("Get one purchase proposal belonging to the current reader")]
    public async Task<ActionResult<ProposalResponse>> GetProposal(int id, CancellationToken cancellationToken)
    {
        var proposal = await readerService.GetProposalAsync(GetAccountId(), id, cancellationToken);
        return proposal is null ? NotFound() : Ok(proposal);
    }

    [HttpPost("proposals")]
    [EndpointSummary("Submit a book purchase proposal")]
    [ProducesResponseType<ProposalResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProposalResponse>> CreateProposal(
        CreateProposalRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await readerService.CreateProposalAsync(GetAccountId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetProposal), new { id = proposal.ProposalId }, proposal);
    }

    [HttpPost("loans/{id:long}/renew")]
    [EndpointSummary("Renew an eligible loan belonging to the current reader")]
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
