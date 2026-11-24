using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiMS.Api.Controllers;

/// <summary>Điểm cuối quản trị sách, độc giả, mượn trả và báo cáo thư viện.</summary>
[ApiController]
[Authorize(Roles = "Admin,Librarian")]
[Route("api/admin")]
public sealed class AdminController(
    IAdminService adminService,
    IDatabaseMaintenanceService databaseMaintenanceService) : ControllerBase
{
    [HttpGet("dashboard")]
    [EndpointSummary("Lấy số liệu tổng quan hoạt động thư viện")]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken cancellationToken) =>
        Ok(await adminService.GetDashboardAsync(cancellationToken));

    [HttpGet("books")]
    [EndpointSummary("Liệt kê hoặc tìm kiếm đầu sách để quản trị")]
    [ProducesResponseType<IReadOnlyList<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookResponse>>> Books(
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetBooksAsync(search, cancellationToken));

    [HttpGet("books/paged")]
    [EndpointSummary("Liệt kê hoặc tìm kiếm đầu sách quản trị theo trang")]
    [ProducesResponseType<PagedResponse<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<BookResponse>>> PagedBooks(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await adminService.GetBooksPageAsync(search, page, pageSize, cancellationToken));

    [HttpGet("books/{id:int}")]
    [EndpointSummary("Lấy thông tin một đầu sách")]
    public async Task<ActionResult<BookResponse>> GetBook(int id, CancellationToken cancellationToken)
    {
        var book = await adminService.GetBookAsync(id, cancellationToken);
        return book is null ? NotFound() : Ok(book);
    }

    [HttpGet("books/{id:int}/copies")]
    [EndpointSummary("Liệt kê các bản sao của một đầu sách")]
    [ProducesResponseType<IReadOnlyList<BookCopyResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookCopyResponse>>> Copies(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetCopiesAsync(id, cancellationToken));

    [HttpPost("books")]
    [EndpointSummary("Tạo đầu sách và các bản sao ban đầu")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BookResponse>> CreateBook(
        CreateBookRequest request,
        CancellationToken cancellationToken)
    {
        var book = await adminService.CreateBookAsync(request, GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(GetBook), new { id = book.Id }, book);
    }

    [HttpPut("books/{id:int}")]
    [EndpointSummary("Cập nhật thông tin và trạng thái hiển thị của đầu sách")]
    public async Task<ActionResult<BookResponse>> UpdateBook(
        int id,
        UpdateBookRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.UpdateBookAsync(id, request, GetActorId(), cancellationToken));

    [HttpPost("books/{id:int}/cover")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024 + 128 * 1024)]
    [EndpointSummary("Tải lên hoặc thay ảnh bìa đầu sách")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> UploadBookCover(
        int id,
        [FromForm] UploadBookCoverRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.UploadBookCoverAsync(
            id,
            request.File,
            GetActorId(),
            cancellationToken));

    [HttpPost("books/{id:int}/copies")]
    [EndpointSummary("Thêm bản sao cho một đầu sách")]
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
    [EndpointSummary("Cập nhật trạng thái bản sao sách")]
    public async Task<ActionResult<MessageResponse>> UpdateCopyStatus(
        int id,
        CopyStatusRequest request,
        CancellationToken cancellationToken)
    {
        await adminService.UpdateCopyStatusAsync(id, request, GetActorId(), cancellationToken);
        return Ok(new MessageResponse("Đã cập nhật trạng thái bản sao."));
    }

    [HttpGet("readers")]
    [EndpointSummary("Tìm kiếm và lọc tài khoản, thẻ độc giả")]
    [ProducesResponseType<IReadOnlyList<ReaderResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReaderResponse>>> Readers(
    [FromQuery] string? search,
    [FromQuery] string? cardStatus,
    CancellationToken cancellationToken) =>
    Ok(await adminService.GetReadersAsync(search, cardStatus, cancellationToken));

    [HttpGet("readers/paged")]
    [EndpointSummary("Tìm kiếm và phân trang hồ sơ độc giả")]
    [ProducesResponseType<PagedResponse<ReaderResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ReaderResponse>>> PagedReaders(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery] string? cardStatus,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await adminService.GetReadersPageAsync(search, cardStatus, page, pageSize, cancellationToken));

    [HttpGet("readers/{id:int}")]
    [EndpointSummary("Lấy thông tin một độc giả")]
    public async Task<ActionResult<ReaderResponse>> GetReader(int id, CancellationToken cancellationToken)
    {
        var reader = await adminService.GetReaderAsync(id, cancellationToken);
        return reader is null ? NotFound() : Ok(reader);
    }

    [HttpPost("readers")]
    [EndpointSummary("Tạo tài khoản và thẻ độc giả")]
    [ProducesResponseType<ReaderResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReaderResponse>> CreateReader(
        CreateReaderRequest request,
        CancellationToken cancellationToken)
    {
        var reader = await adminService.CreateReaderAsync(request, GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(GetReader), new { id = reader.ReaderId }, reader);
    }

    [HttpPut("readers/{id:int}")]
    [EndpointSummary("Cập nhật hồ sơ và thẻ độc giả")]
    public async Task<ActionResult<ReaderResponse>> UpdateReader(
        int id,
        UpdateReaderRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.UpdateReaderAsync(id, request, GetActorId(), cancellationToken));

    [HttpPut("readers/{id:int}/password")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Đặt mật khẩu mới cho tài khoản độc giả")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> ResetReaderPassword(
        int id,
        ResetReaderPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await adminService.ResetReaderPasswordAsync(id, request, GetActorId(), cancellationToken);
        return Ok(new MessageResponse("Đã đặt lại mật khẩu cho độc giả."));
    }

    [HttpGet("loans")]
    [EndpointSummary("Liệt kê các phiếu mượn trả")]
    [ProducesResponseType<IReadOnlyList<LoanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoanResponse>>> Loans(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetLoansAsync(status, cancellationToken));

    [HttpGet("loans/paged")]
    [EndpointSummary("Lọc và phân trang phiếu mượn trả")]
    [ProducesResponseType<PagedResponse<LoanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<LoanResponse>>> PagedLoans(
        [FromQuery] string? status,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await adminService.GetLoansPageAsync(status, page, pageSize, cancellationToken));

    [HttpGet("loans/{id:long}")]
    [EndpointSummary("Lấy thông tin một phiếu mượn trả")]
    public async Task<ActionResult<LoanResponse>> GetLoan(long id, CancellationToken cancellationToken)
    {
        var loan = await adminService.GetLoanAsync(id, cancellationToken);
        return loan is null ? NotFound() : Ok(loan);
    }

    [HttpPost("loans/checkout")]
    [EndpointSummary("Lập phiếu mượn bằng thẻ độc giả và mã vạch")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LoanResponse>> Checkout(
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var loan = await adminService.CheckoutAsync(request, GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(GetLoan), new { id = loan.Id }, loan);
    }

    [HttpPost("loans/return")]
    [EndpointSummary("Nhận trả bản sao và tính khoản phạt nếu có")]
    public async Task<ActionResult<LoanResponse>> Return(
        ReturnRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.ReturnAsync(request, GetActorId(), cancellationToken));

    [HttpPost("loans/{id:long}/renew")]
    [EndpointSummary("Gia hạn phiếu mượn đủ điều kiện")]
    public async Task<ActionResult<LoanResponse>> Renew(long id, CancellationToken cancellationToken) =>
        Ok(await adminService.RenewAsync(id, GetActorId(), cancellationToken));

    [HttpGet("fines")]
    [EndpointSummary("Liệt kê khoản phạt và trạng thái thanh toán")]
    [ProducesResponseType<IReadOnlyList<FineResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FineResponse>>> Fines(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetFinesAsync(status, cancellationToken));

    [HttpGet("fines/paged")]
    [EndpointSummary("Lọc và phân trang khoản phạt")]
    [ProducesResponseType<PagedResponse<FineResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FineResponse>>> PagedFines(
        [FromQuery] string? status,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await adminService.GetFinesPageAsync(status, page, pageSize, cancellationToken));

    [HttpPut("fines/{id:long}/collect")]
    [EndpointSummary("Thu khoản phạt còn nợ")]
    public async Task<ActionResult<FineResponse>> CollectFine(
        long id,
        CollectFineRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.CollectFineAsync(id, request, GetActorId(), cancellationToken));

    [HttpGet("proposals")]
    [EndpointSummary("Liệt kê đề xuất mua sách của độc giả")]
    [ProducesResponseType<IReadOnlyList<ProposalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProposalResponse>>> Proposals(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetProposalsAsync(status, cancellationToken));

    [HttpGet("proposals/paged")]
    [EndpointSummary("Lọc và phân trang đề xuất mua sách")]
    [ProducesResponseType<PagedResponse<ProposalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProposalResponse>>> PagedProposals(
        [FromQuery] string? status,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await adminService.GetProposalsPageAsync(status, page, pageSize, cancellationToken));

    [HttpPut("proposals/{id:int}/review")]
    [EndpointSummary("Duyệt hoặc từ chối đề xuất mua sách")]
    public async Task<ActionResult<ProposalResponse>> ReviewProposal(
        int id,
        ReviewProposalRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.ReviewProposalAsync(id, request, GetActorId(), cancellationToken));

    [HttpGet("settings")]
    [EndpointSummary("Liệt kê các thiết lập hệ thống được hỗ trợ")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<SystemConfigResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SystemConfigResponse>>> Settings(CancellationToken cancellationToken) =>
        Ok(await adminService.GetConfigsAsync(cancellationToken));

    [HttpPut("settings/{key}")]
    [EndpointSummary("Cập nhật một thiết lập hệ thống được hỗ trợ")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SystemConfigResponse>> UpdateSetting(
        string key,
        UpdateSystemConfigRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminService.UpdateConfigAsync(key, request, GetActorId(), cancellationToken));

    [HttpGet("activity-logs")]
    [EndpointSummary("Đọc nhật ký hoạt động quản trị theo trang")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<IReadOnlyList<ActivityLogResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActivityLogResponse>>> ActivityLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await adminService.GetActivityLogsAsync(page, pageSize, cancellationToken));

    [HttpGet("activity-logs/paged")]
    [EndpointSummary("Lấy nhật ký hoạt động quản trị theo trang và tổng số")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<PagedResponse<ActivityLogResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ActivityLogResponse>>> PagedActivityLogs(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await adminService.GetActivityLogsPageAsync(page, pageSize, cancellationToken));

    [HttpGet("database-backups")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Liệt kê các bản sao lưu cơ sở dữ liệu SQL Server")]
    [ProducesResponseType<DatabaseBackupStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DatabaseBackupStatusResponse>> DatabaseBackups(
        CancellationToken cancellationToken) =>
        Ok(await databaseMaintenanceService.GetBackupStatusAsync(cancellationToken));

    [HttpGet("database-backups/{fileName}")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Lấy thông tin một tệp sao lưu cơ sở dữ liệu")]
    [ProducesResponseType<DatabaseBackupFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatabaseBackupFileResponse>> GetDatabaseBackup(
        string fileName,
        CancellationToken cancellationToken) =>
        Ok(await databaseMaintenanceService.GetBackupAsync(fileName, cancellationToken));

    [HttpPost("database-backups")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Tạo và xác minh bản sao lưu cơ sở dữ liệu LiMS")]
    [ProducesResponseType<DatabaseBackupFileResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DatabaseBackupFileResponse>> CreateDatabaseBackup(
        CancellationToken cancellationToken)
    {
        var backup = await databaseMaintenanceService.CreateBackupAsync(GetActorId(), cancellationToken);
        return CreatedAtAction(nameof(GetDatabaseBackup), new { fileName = backup.FileName }, backup);
    }

    [HttpPost("database-backups/{fileName}/restore")]
    [Authorize(Roles = "Admin")]
    [EndpointSummary("Phục hồi cơ sở dữ liệu LiMS từ bản sao lưu đã xác minh")]
    [ProducesResponseType<DatabaseRestoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DatabaseRestoreResponse>> RestoreDatabaseBackup(
        string fileName,
        RestoreDatabaseBackupRequest request,
        CancellationToken cancellationToken) =>
        Ok(await databaseMaintenanceService.RestoreBackupAsync(
            fileName,
            request.ConfirmDatabaseName,
            GetActorId(),
            cancellationToken));

    private int GetActorId()
    {
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(accountId, out var id)
            ? id
            : throw new UnauthorizedAccessException("JWT account identifier is missing.");
    }
}
