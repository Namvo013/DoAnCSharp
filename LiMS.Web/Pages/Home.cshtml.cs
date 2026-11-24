using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;

namespace LiMS.Web.Pages;

public class HomeModel(LibraryApiClient libraryApi) : PageModel
{
    public string StudentCode { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string FirstName { get; private set; } = "";
    public string TodayWeekday { get; private set; } = "";
    public string TodayLabel { get; private set; } = "";
    public IReadOnlyList<ApiBook> Books { get; private set; } = [];
    public int TotalBookCount { get; private set; }
    public IReadOnlyList<ApiLoan> ActiveLoans { get; private set; } = [];

    public string? GetAbsoluteBookCoverUrl(string? coverImageUrl) =>
        libraryApi.GetAbsoluteBookCoverUrl(coverImageUrl);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        StudentCode = HttpContext.Session.GetString("StudentCode") ?? "";
        if (string.IsNullOrEmpty(StudentCode))
            return RedirectToPage("/Index");

        FullName = HttpContext.Session.GetString("FullName") ?? "";
        FirstName = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "bạn";
        var vietnameseNow = VietnamTime.Convert(DateTimeOffset.UtcNow);
        TodayWeekday = vietnameseNow.ToString("dddd", new System.Globalization.CultureInfo("vi-VN"));
        TodayLabel = $"{vietnameseNow.Day} tháng {vietnameseNow.Month}, {vietnameseNow.Year}";

        var bookPage = await libraryApi.GetBooksPageAsync(
            null, null, null, "newest", 1, 4, cancellationToken);
        Books = bookPage.Items;
        TotalBookCount = bookPage.TotalCount;
        ActiveLoans = (await libraryApi.GetLoansAsync(StudentCode, cancellationToken))
            .Where(LoansModel.IsActive)
            .ToList();
        return Page();
    }

    public IActionResult OnGetQr()
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrWhiteSpace(studentCode))
            return Unauthorized();

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(studentCode, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        Response.Headers["Cache-Control"] = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(qrCode.GetGraphic(8), "image/png");
    }

    public string RemainingDays(DateTimeOffset dueDate)
    {
        var today = VietnamTime.Convert(DateTimeOffset.UtcNow).Date;
        var dueDateInVietnam = VietnamTime.Convert(dueDate).Date;
        var remaining = (dueDateInVietnam - today).Days;
        return remaining >= 0 ? $"Còn {remaining} ngày" : $"Quá hạn {Math.Abs(remaining)} ngày";
    }

    public static int LoanProgressPercentage(ApiLoan loan, DateTimeOffset now)
    {
        var borrowedAt = VietnamTime.Convert(loan.BorrowedDate).DateTime;
        var deadline = VietnamTime.Convert(loan.DueDate).Date.AddDays(1);
        var duration = (deadline - borrowedAt).TotalSeconds;
        if (duration <= 0)
            return 100;

        var elapsed = (VietnamTime.Convert(now).DateTime - borrowedAt).TotalSeconds;
        return (int)Math.Clamp(Math.Round(elapsed / duration * 100), 0, 100);
    }

    public int LoanProgressPercentage(ApiLoan loan) =>
        LoanProgressPercentage(loan, DateTimeOffset.UtcNow);
}
