using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class RenewalModel(LibraryApiClient libraryApi) : PageModel
{
    public IReadOnlyList<ApiLoan> ActiveLoans { get; private set; } = [];
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        ActiveLoans = (await libraryApi.GetLoansAsync(studentCode, cancellationToken))
            .Where(LoansModel.IsActive)
            .ToList();
        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostRenewAsync(long loanId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        try
        {
            var renewal = await libraryApi.RenewLoanAsync(loanId, cancellationToken);
            TempData["SuccessMessage"] =
                $"Gia hạn thành công. Hạn trả mới: {renewal.DueDate:dd/MM/yyyy}.";
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
