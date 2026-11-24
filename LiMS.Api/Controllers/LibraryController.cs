using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiMS.Api.Controllers;

/// <summary>Điểm cuối tra cứu thư mục và dữ liệu cá nhân của độc giả.</summary>
[ApiController]
[Route("api/library")]
public sealed class LibraryController(ILibraryQueryService libraryService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("health")]
    [EndpointSummary("Kiểm tra kết nối API và SQL Server")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken) =>
        Ok(new
        {
            status = "ok",
            database = await libraryService.CanConnectAsync(cancellationToken)
        });

    [AllowAnonymous]
    [HttpGet("books")]
    [EndpointSummary("Liệt kê sách đang hoạt động trong danh mục công khai")]
    [ProducesResponseType<IReadOnlyList<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookResponse>>> Books(CancellationToken cancellationToken) =>
        Ok(await libraryService.GetPublicBooksAsync(cancellationToken));

    [AllowAnonymous]
    [HttpGet("books/paged")]
    [EndpointSummary("Tìm kiếm, lọc và phân trang danh mục sách công khai")]
    [ProducesResponseType<PagedResponse<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<BookResponse>>> PagedBooks(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery, StringLength(100)] string? category,
        [FromQuery, RegularExpression("^(available|unavailable)?$")] string? availability,
        [FromQuery, RegularExpression("^(newest|title)?$")] string? sort,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 24,
        CancellationToken cancellationToken = default) =>
        Ok(await libraryService.GetPublicBooksPageAsync(
            search, category, availability, sort, page, pageSize, cancellationToken));

    [AllowAnonymous]
    [HttpGet("book-categories")]
    [EndpointSummary("Liệt kê thể loại đang có trong danh mục công khai")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> BookCategories(CancellationToken cancellationToken) =>
        Ok(await libraryService.GetPublicBookCategoriesAsync(cancellationToken));

    [AllowAnonymous]
    [HttpGet("books/{id:int}")]
    [EndpointSummary("Lấy thông tin một đầu sách đang hoạt động")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> GetPublicBook(int id, CancellationToken cancellationToken)
    {
        var book = await libraryService.GetPublicBookAsync(id, cancellationToken);
        return book is null ? NotFound() : Ok(book);
    }

    [AllowAnonymous]
    [HttpGet("books/{id:int}/cover")]
    [EndpointSummary("Lấy ảnh bìa của một đầu sách")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BookCover(int id, CancellationToken cancellationToken)
    {
        var cover = await libraryService.GetBookCoverAsync(id, cancellationToken);
        return cover is null ? NotFound() : File(cover.Data, cover.ContentType);
    }

    [Authorize]
    [HttpGet("users/{studentCode}")]
    [EndpointSummary("Lấy hồ sơ của độc giả hiện tại hoặc hồ sơ được cấp quyền")]
    public async Task<IActionResult> GetUser(string studentCode, CancellationToken cancellationToken)
    {
        var reader = await libraryService.GetReaderProfileAsync(studentCode, cancellationToken);
        if (reader is null)
            return NotFound();
        if (!IsAuthorizedFor(reader.AccountId))
            return Forbid();

        return Ok(new ReaderProfileResponse(
            reader.AccountId,
            reader.CardNo,
            reader.FullName,
            reader.Email,
            reader.Role,
            reader.Phone,
            reader.CardExpiryDate,
            reader.CardStatus));
    }

    [Authorize]
    [HttpGet("loans/{studentCode}")]
    [EndpointSummary("Liệt kê phiếu mượn mà tài khoản hiện tại được phép xem")]
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
    [EndpointSummary("Liệt kê phiếu đặt giữ mà tài khoản hiện tại được phép xem")]
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
    [EndpointSummary("Liệt kê thông báo của độc giả hiện tại")]
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
