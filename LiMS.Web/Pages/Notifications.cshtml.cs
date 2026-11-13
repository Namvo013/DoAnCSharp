using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class NotificationsModel(LibraryApiClient libraryApi) : PageModel
{
    public IReadOnlyList<ApiNotification> Notifications { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        Notifications = await libraryApi.GetNotificationsAsync(studentCode, cancellationToken);
        return Page();
    }
}
