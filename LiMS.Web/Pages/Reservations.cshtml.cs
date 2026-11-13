using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class ReservationsModel(LibraryApiClient libraryApi) : PageModel
{
    public IReadOnlyList<ApiReservation> Reservations { get; private set; } = [];
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
        Reservations = await libraryApi.GetReservationsAsync(studentCode, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(long reservationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        try
        {
            await libraryApi.CancelReservationAsync(reservationId, cancellationToken);
            TempData["SuccessMessage"] = "Hủy đặt giữ thành công.";
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
}
