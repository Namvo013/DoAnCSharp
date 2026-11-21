using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class ReservationsModel(LibraryApiClient libraryApi) : PageModel
{
    public IReadOnlyList<ApiReservation> Reservations { get; private set; } = [];
    public string? ResultType { get; private set; }
    public string? ResultBookTitle { get; private set; }
    public string? ResultId { get; private set; }
    public string? ResultStatus { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        ResultType = TempData["ReservationResultType"] as string;
        ResultBookTitle = TempData["ReservationResultBookTitle"] as string;
        ResultId = TempData["ReservationResultId"] as string;
        ResultStatus = TempData["ReservationResultStatus"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
        if (ResultType is "created" or "cancelled")
            return Page();

        Reservations = await libraryApi.GetReservationsAsync(studentCode, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(long reservationId, CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        try
        {
            var reservations = await libraryApi.GetReservationsAsync(studentCode, cancellationToken);
            var reservation = reservations.FirstOrDefault(item => item.Id == reservationId);
            await libraryApi.CancelReservationAsync(reservationId, cancellationToken);
            TempData["ReservationResultType"] = "cancelled";
            TempData["ReservationResultBookTitle"] = reservation?.Book?.Title ?? "Tài liệu đã đặt giữ";
            TempData["ReservationResultId"] = reservationId.ToString();
            TempData["ReservationResultStatus"] = "Đã hủy";
        }
        catch (LibraryApiClient.LibraryApiException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (HttpRequestException)
        {
            TempData["ErrorMessage"] = "Không thể kết nối máy chủ. Vui lòng thử lại sau.";
        }

        return RedirectToPage();
    }

    public string GetStatusText(string status) => status switch
    {
        "Pending" => "Đang chờ",
        "ReadyForPickup" => "Sẵn sàng nhận",
        "Fulfilled" => "Đã nhận sách",
        "Cancelled" => "Đã hủy",
        "Expired" => "Hết hiệu lực",
        _ => status
    };
}
