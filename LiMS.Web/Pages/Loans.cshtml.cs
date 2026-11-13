using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class LoansModel(LibraryApiClient libraryApi) : PageModel
{
    public IReadOnlyList<ApiLoan> Loans { get; private set; } = [];
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
        Loans = await libraryApi.GetLoansAsync(studentCode, cancellationToken);
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

    public static bool IsActive(ApiLoan loan) =>
        loan.ReturnedDate is null && loan.Status is "Borrowed" or "Active" or "Đang mượn";

    public static bool IsOverdue(ApiLoan loan) =>
        IsActive(loan) && VietnamTime.Convert(loan.DueDate).Date < VietnamTime.Convert(DateTimeOffset.UtcNow).Date;

    public static bool CanRenew(ApiLoan loan) =>
        IsActive(loan) && !IsOverdue(loan) && loan.RenewalCount < 1;

    public static string DisplayStatus(ApiLoan loan)
    {
        if (loan.ReturnedDate is not null || loan.Status is "Returned" or "Đã trả")
            return "Đã trả";
        return IsOverdue(loan) ? "Quá hạn" : "Đang mượn";
    }

    public static string FormatDate(DateTimeOffset date) =>
        VietnamTime.Convert(date).ToString("dd/MM/yyyy");
}
