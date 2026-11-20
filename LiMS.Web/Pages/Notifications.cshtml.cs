using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class NotificationsModel(LibraryApiClient libraryApi) : PageModel
{
    // Danh sách thông báo của độc giả.
    public IReadOnlyList<ApiNotification> Notifications { get; private set; } = [];

    // Tổng số thông báo chưa đọc.
    public int UnreadCount { get; private set; }

    // Thông báo kết quả thao tác.
    [TempData]
    public string? StatusMessage { get; set; }

    // Hiển thị danh sách thông báo.
    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");

        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        Notifications = await libraryApi.GetNotificationsAsync(cancellationToken);
        UnreadCount = await libraryApi.GetUnreadNotificationCountAsync(cancellationToken);

        return Page();
    }

    // Đánh dấu một thông báo là đã đọc.
    public async Task<IActionResult> OnPostMarkAsReadAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        await libraryApi.MarkNotificationAsReadAsync(id, cancellationToken);

        StatusMessage = "Đã đánh dấu thông báo là đã đọc.";

        return RedirectToPage();
    }

    // Đánh dấu tất cả thông báo là đã đọc.
    public async Task<IActionResult> OnPostMarkAllAsReadAsync(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        var updatedCount = await libraryApi.MarkAllNotificationsAsReadAsync(cancellationToken);

        StatusMessage = $"Đã đánh dấu {updatedCount} thông báo là đã đọc.";

        return RedirectToPage();
    }
}