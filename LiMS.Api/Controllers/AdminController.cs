using System.Security.Claims;
using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiMS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Librarian")]
[Route("api/admin")]
public sealed class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("dashboard")]
    [EndpointSummary("Get library dashboard statistics")]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken cancellationToken) =>
        Ok(await adminService.GetDashboardAsync(cancellationToken));

    [HttpGet("books")]
    [EndpointSummary("List or search books for administration")]
    [ProducesResponseType<IReadOnlyList<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookResponse>>> Books(
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetBooksAsync(search, cancellationToken));

    [HttpGet("books/{id:int}")]
    [EndpointSummary("Get one book")]
    public async Task<ActionResult<BookResponse>> GetBook(int id, CancellationToken cancellationToken)
    {
        var book = await adminService.GetBookAsync(id, cancellationToken);
        return book is null ? NotFound() : Ok(book);
    }

    [HttpGet("books/{id:int}/copies")]
    [EndpointSummary("List physical copies of a book")]
    [ProducesResponseType<IReadOnlyList<BookCopyResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookCopyResponse>>> Copies(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetCopiesAsync(id, cancellationToken));

    [HttpPost("books")]
    [EndpointSummary("Create a book and its initial copies")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BookResponse>> CreateBook(
        CreateBookRequest request,
        CancellationToken cancellationToken)
    {
        var book = await adminService.CreateBookAsync(request, GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(GetBook), new { id = book.Id }, book);
    }

    [HttpPut("books/{id:int}")]
    [EndpointSummary("Update book metadata and visibility")]
    public async Task<ActionResult<BookResponse>> UpdateBook(
        int id,
        UpdateBookRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.UpdateBookAsync(id, request, GetActorId(), cancellationToken));

    [HttpPost("books/{id:int}/copies")]
    [EndpointSummary("Add physical copies to a book")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MessageResponse>> AddCopies(
        int id,
        CreateCopiesRequest request,
        CancellationToken cancellationToken)
    {
        await adminService.AddCopiesAsync(id, request, GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(Copies), new { id }, new MessageResponse("Đã thêm bản sao sách."));
    }

    [HttpPut("copies/{id:int}/status")]
    [EndpointSummary("Update a physical copy status")]
    public async Task<ActionResult<MessageResponse>> UpdateCopyStatus(
        int id,
        CopyStatusRequest request,
        CancellationToken cancellationToken)
    {
        await adminService.UpdateCopyStatusAsync(id, request, GetActorId(), cancellationToken);
        return Ok(new MessageResponse("Đã cập nhật trạng thái bản sao."));
    }

    [HttpGet("readers")]
    [EndpointSummary("Search reader accounts and cards")]
    [ProducesResponseType<IReadOnlyList<ReaderResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReaderResponse>>> Readers(
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetReadersAsync(search, cancellationToken));

    [HttpGet("readers/{id:int}")]
    [EndpointSummary("Get one reader")]
    public async Task<ActionResult<ReaderResponse>> GetReader(int id, CancellationToken cancellationToken)
    {
        var reader = await adminService.GetReaderAsync(id, cancellationToken);
        return reader is null ? NotFound() : Ok(reader);
    }

    [HttpPost("readers")]
    [EndpointSummary("Create a reader account and library card")]
    [ProducesResponseType<ReaderResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReaderResponse>> CreateReader(
        CreateReaderRequest request,
        CancellationToken cancellationToken)
    {
        var reader = await adminService.CreateReaderAsync(request, GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(GetReader), new { id = reader.ReaderId }, reader);
    }

    [HttpPut("readers/{id:int}")]
    [EndpointSummary("Update a reader profile and card")]
    public async Task<ActionResult<ReaderResponse>> UpdateReader(
        int id,
        UpdateReaderRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.UpdateReaderAsync(id, request, GetActorId(), cancellationToken));

    [HttpGet("loans")]
    [EndpointSummary("List circulation records")]
    [ProducesResponseType<IReadOnlyList<LoanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoanResponse>>> Loans(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetLoansAsync(status, cancellationToken));

    [HttpGet("loans/{id:long}")]
    [EndpointSummary("Get one circulation record")]
    public async Task<ActionResult<LoanResponse>> GetLoan(long id, CancellationToken cancellationToken)
    {
        var loan = await adminService.GetLoanAsync(id, cancellationToken);
        return loan is null ? NotFound() : Ok(loan);
    }

    [HttpPost("loans/checkout")]
    [EndpointSummary("Check out a copy by card and barcode")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LoanResponse>> Checkout(
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var loan = await adminService.CheckoutAsync(request, GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(GetLoan), new { id = loan.Id }, loan);
    }

    [HttpPost("loans/return")]
    [EndpointSummary("Return a copy and assess applicable fines")]
    public async Task<ActionResult<LoanResponse>> Return(
        ReturnRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.ReturnAsync(request, GetActorId(), cancellationToken));

    [HttpPost("loans/{id:long}/renew")]
    [EndpointSummary("Renew an eligible loan")]
    public async Task<ActionResult<LoanResponse>> Renew(long id, CancellationToken cancellationToken) =>
        Ok(await adminService.RenewAsync(id, GetActorId(), cancellationToken));

    [HttpGet("fines")]
    [EndpointSummary("List fines and payment status")]
    [ProducesResponseType<IReadOnlyList<FineResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FineResponse>>> Fines(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetFinesAsync(status, cancellationToken));

    [HttpPut("fines/{id:long}/collect")]
    [EndpointSummary("Collect an outstanding fine")]
    public async Task<ActionResult<FineResponse>> CollectFine(
        long id,
        CollectFineRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.CollectFineAsync(id, request, GetActorId(), cancellationToken));

    [HttpGet("proposals")]
    [EndpointSummary("List reader purchase proposals")]
    [ProducesResponseType<IReadOnlyList<ProposalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProposalResponse>>> Proposals(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetProposalsAsync(status, cancellationToken));

    [HttpPut("proposals/{id:int}/review")]
    [EndpointSummary("Approve or reject a purchase proposal")]
    public async Task<ActionResult<ProposalResponse>> ReviewProposal(
        int id,
        ReviewProposalRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.ReviewProposalAsync(id, request, GetActorId(), cancellationToken));

    [HttpGet("settings")]
    [EndpointSummary("List supported system settings")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<SystemConfigResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SystemConfigResponse>>> Settings(CancellationToken cancellationToken) =>
        Ok(await adminService.GetConfigsAsync(cancellationToken));

    [HttpPut("settings/{key}")]
    [EndpointSummary("Update a supported system setting")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SystemConfigResponse>> UpdateSetting(
        string key,
        UpdateSystemConfigRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.UpdateConfigAsync(key, request, GetActorId(), cancellationToken));

    [HttpGet("activity-logs")]
    [EndpointSummary("Read paginated administrative activity logs")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<ActivityLogResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActivityLogResponse>>> ActivityLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await adminService.GetActivityLogsAsync(page, pageSize, cancellationToken));

    private int GetActorId()
    {
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(accountId, out var id)
            ? id
            : throw new UnauthorizedAccessException("JWT account identifier is missing.");
    }
}
