using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class PurchaseProposalModel : PageModel
{
    public IActionResult OnGet()
    {
        return string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode"))
            ? RedirectToPage("/Index")
            : Page();
    }
}
