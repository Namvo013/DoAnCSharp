using System.ComponentModel.DataAnnotations;
using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class PurchaseProposalModel(
    LibraryApiClient libraryApi,
    ILogger<PurchaseProposalModel> logger) : PageModel
{
    public IReadOnlyList<PurchaseProposal> Proposals { get; private set; } = [];
    public string? SuccessMessage { get; private set; }

    [BindProperty, Required(ErrorMessage = "Vui lòng nhập tên tài liệu."), StringLength(255)]
    public string Title { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Vui lòng nhập tác giả."), StringLength(100)]
    public string Author { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Vui lòng nêu lý do đề xuất."), StringLength(2000)]
    public string Reason { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        SuccessMessage = TempData["SuccessMessage"] as string;
        await LoadProposalsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        if (!ModelState.IsValid)
        {
            await LoadProposalsAsync(cancellationToken);
            return Page();
        }

        try
        {
            await libraryApi.CreatePurchaseProposalAsync(
                new CreatePurchaseProposalRequest(Title.Trim(), Author.Trim(), Reason.Trim()),
                cancellationToken);
            TempData["SuccessMessage"] = "Đã gửi đề xuất. Thư viện sẽ xem xét và cập nhật kết quả tại đây.";
            return RedirectToPage();
        }
        catch (LibraryApiClient.LibraryApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "LiMS API is unavailable while submitting a purchase proposal.");
            ModelState.AddModelError(string.Empty, "Không thể kết nối máy chủ để gửi đề xuất. Vui lòng thử lại.");
        }

        await LoadProposalsAsync(cancellationToken);
        return Page();
    }

    public static string StatusLabel(string status) => status switch
    {
        "Pending" => "Chờ thư viện duyệt",
        "Approved" => "Đã chấp thuận",
        "Rejected" => "Từ chối",
        _ => status
    };

    public static string StatusClass(string status) => status switch
    {
        "Approved" => "green",
        "Rejected" => "red",
        _ => "amber"
    };

    private async Task LoadProposalsAsync(CancellationToken cancellationToken)
    {
        Proposals = await libraryApi.GetPurchaseProposalsAsync(cancellationToken);
    }
}
