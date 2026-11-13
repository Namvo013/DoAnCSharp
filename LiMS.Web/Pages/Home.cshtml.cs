using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class HomeModel(LibraryApiClient libraryApi) : PageModel
{
    public string StudentCode { get; private set; } = "";
    public string FirstName { get; private set; } = "";
    public string TodayWeekday { get; private set; } = "";
    public string TodayLabel { get; private set; } = "";
    public IReadOnlyList<ApiBook> Books { get; private set; } = [];
    public IReadOnlyList<ApiLoan> ActiveLoans { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        StudentCode = HttpContext.Session.GetString("StudentCode") ?? "";
        if (string.IsNullOrEmpty(StudentCode))
            return RedirectToPage("/Index");

        var fullName = HttpContext.Session.GetString("FullName") ?? "";
        FirstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "bạn";
        var vietnameseNow = VietnamTime.Convert(DateTimeOffset.UtcNow);
        TodayWeekday = vietnameseNow.ToString("dddd", new System.Globalization.CultureInfo("vi-VN"));
        TodayLabel = $"{vietnameseNow.Day} tháng {vietnameseNow.Month}, {vietnameseNow.Year}";

        Books = await libraryApi.GetBooksAsync(cancellationToken);
        ActiveLoans = (await libraryApi.GetLoansAsync(StudentCode, cancellationToken))
            .Where(LoansModel.IsActive)
            .ToList();
        return Page();
    }

    public string RemainingDays(DateTimeOffset dueDate)
    {
        var today = VietnamTime.Convert(DateTimeOffset.UtcNow).Date;
        var dueDateInVietnam = VietnamTime.Convert(dueDate).Date;
        var remaining = (dueDateInVietnam - today).Days;
        return remaining >= 0 ? $"Còn {remaining} ngày" : $"Quá hạn {Math.Abs(remaining)} ngày";
    }
}
