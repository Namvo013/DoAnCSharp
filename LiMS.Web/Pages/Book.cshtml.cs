using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public sealed class BookModel(LibraryApiClient libraryApi) : PageModel
{
    public ApiBook? Book { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        SuccessMessage = TempData["SuccessMessage"] as string;
        return await LoadBookAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostReserveAsync(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        try
        {
            var reservation = await libraryApi.CreateReservationAsync(id, cancellationToken);
            TempData["SuccessMessage"] =
                $"Đặt giữ thành công: {reservation.BookTitle}, mã phiếu {reservation.Id}.";
            return RedirectToPage("/Reservations");
        }
        catch (LibraryApiClient.LibraryApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Không thể kết nối máy chủ. Vui lòng thử lại sau.";
        }

        return await LoadBookAsync(id, cancellationToken);
    }

    private async Task<IActionResult> LoadBookAsync(int id, CancellationToken cancellationToken)
    {
        var books = await libraryApi.GetBooksAsync(cancellationToken);
        Book = books.SingleOrDefault(book => book.Id == id);
        return Book is null ? NotFound() : Page();
    }
}
