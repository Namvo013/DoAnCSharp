using System.Security.Claims;
using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiMS.Api.Controllers;

[ApiController]
[Route("api/library")]
public sealed class LibraryController(ILibraryQueryService libraryService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("health")]
    [EndpointSummary("Check API and SQL Server connectivity")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken) =>
        Ok(new
        {
            status = "ok",
            database = await libraryService.CanConnectAsync(cancellationToken)
        });

    [AllowAnonymous]
    [HttpGet("books")]
    [EndpointSummary("List active books in the public catalog")]
    [ProducesResponseType<IReadOnlyList<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookResponse>>> Books(CancellationToken cancellationToken) =>
        Ok(await libraryService.GetPublicBooksAsync(cancellationToken));

    [Authorize]
    [HttpGet("users/{studentCode}")]
    [EndpointSummary("Get the current reader profile or an authorized reader profile")]
    public async Task<IActionResult> GetUser(string studentCode, CancellationToken cancellationToken)
    {
        var reader = await libraryService.GetReaderProfileAsync(studentCode, cancellationToken);
        if (reader is null)
            return NotFound();
        if (!IsAuthorizedFor(reader.AccountId))
            return Forbid();

        return Ok(new
        {
            Id = reader.AccountId,
            StudentCode = reader.CardNo,
            reader.FullName,
            reader.Email,
            reader.Role
        });
    }

    [Authorize]
    [HttpGet("loans/{studentCode}")]
    [EndpointSummary("List loans visible to the current account")]
    [ProducesResponseType<IReadOnlyList<ReaderLoanResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Loans(string studentCode, CancellationToken cancellationToken)
    {
        var reader = await libraryService.GetReaderProfileAsync(studentCode, cancellationToken);
        if (reader is null)
            return NotFound();
        if (!IsAuthorizedFor(reader.AccountId))
            return Forbid();

        return Ok(await libraryService.GetReaderLoansAsync(reader.ReaderId, cancellationToken));
    }

    [Authorize]
    [HttpGet("reservations/{studentCode}")]
    [EndpointSummary("List reservations visible to the current account")]
    [ProducesResponseType<IReadOnlyList<ReaderReservationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reservations(string studentCode, CancellationToken cancellationToken)
    {
        var reader = await libraryService.GetReaderProfileAsync(studentCode, cancellationToken);
        if (reader is null)
            return NotFound();
        if (!IsAuthorizedFor(reader.AccountId))
            return Forbid();

        return Ok(await libraryService.GetReaderReservationsAsync(reader.ReaderId, cancellationToken));
    }

    [Authorize]
    [HttpGet("notifications/{studentCode}")]
    [EndpointSummary("List email log entries for the current reader")]
    [ProducesResponseType<IReadOnlyList<ReaderNotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Notifications(string studentCode, CancellationToken cancellationToken)
    {
        var reader = await libraryService.GetReaderProfileAsync(studentCode, cancellationToken);
        if (reader is null)
            return NotFound();
        if (!IsAuthorizedFor(reader.AccountId))
            return Forbid();

        return Ok(await libraryService.GetReaderNotificationsAsync(reader.ReaderId, cancellationToken));
    }

    private bool IsAuthorizedFor(int accountId) =>
        User.IsInRole("Admin")
        || User.IsInRole("Librarian")
        || int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentAccountId)
            && currentAccountId == accountId;
}
