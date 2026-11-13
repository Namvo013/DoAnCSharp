using System.ComponentModel.DataAnnotations;
using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class ProfileModel(
    LibraryApiClient libraryApi,
    ILogger<ProfileModel> logger) : PageModel
{
    public ApiUser? Profile { get; private set; }
    public string? SuccessMessage { get; private set; }

    [BindProperty, Required]
    public string CurrentPassword { get; set; } = "";

    [BindProperty, Required, MinLength(8)]
    public string NewPassword { get; set; } = "";

    [BindProperty, Required, MinLength(8)]
    public string ConfirmPassword { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        Profile = await libraryApi.GetUserAsync(studentCode, cancellationToken)
            ?? throw new InvalidDataException("API không tìm thấy hồ sơ của tài khoản hiện tại.");
        return Page();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        if (NewPassword != ConfirmPassword)
            ModelState.AddModelError(nameof(ConfirmPassword), "Mật khẩu xác nhận không khớp.");
        if (!ModelState.IsValid)
        {
            Profile = await libraryApi.GetUserAsync(studentCode, cancellationToken);
            return Page();
        }

        try
        {
            var result = await libraryApi.ChangePasswordAsync(
                new ChangePasswordRequest(studentCode, CurrentPassword, NewPassword),
                cancellationToken);
            if (!result.Succeeded)
                ModelState.AddModelError(string.Empty, result.Message);
            else
                SuccessMessage = result.Message;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "LiMS API is unavailable while changing a password.");
            ModelState.AddModelError(string.Empty, "Không thể kết nối máy chủ để đổi mật khẩu.");
        }

        Profile = await libraryApi.GetUserAsync(studentCode, cancellationToken);
        return Page();
    }
}
