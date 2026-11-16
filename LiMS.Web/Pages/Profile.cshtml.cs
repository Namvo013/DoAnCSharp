using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class ProfileModel(
    LibraryApiClient libraryApi,
    ILogger<ProfileModel> logger) : PageModel
{
    public ApiUser? Profile { get; private set; }
    public string StudentCode { get; private set; } = "";
    public string? SuccessMessage { get; private set; }

    [BindProperty, Required, StringLength(100)]
    public string FullName { get; set; } = "";

    [BindProperty, Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = "";

    [BindProperty, Phone, StringLength(20)]
    public string? Phone { get; set; }

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

        StudentCode = studentCode;
        SuccessMessage = TempData["SuccessMessage"] as string;
        Profile = await libraryApi.GetUserAsync(studentCode, cancellationToken)
            ?? throw new InvalidDataException("API không tìm thấy hồ sơ của tài khoản hiện tại.");
        SetProfileFields(Profile);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateProfileAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        StudentCode = studentCode;
        RemoveModelStateEntries(nameof(CurrentPassword), nameof(NewPassword), nameof(ConfirmPassword));
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var profile = await libraryApi.UpdateProfileAsync(
                new UpdateProfileRequest(FullName.Trim(), Email.Trim(), Phone?.Trim()),
                cancellationToken);
            HttpContext.Session.SetString("FullName", profile.FullName);
            await RefreshNameClaimAsync(profile.FullName);
            TempData["SuccessMessage"] = "Đã cập nhật thông tin cá nhân.";
            return RedirectToPage();
        }
        catch (LibraryApiClient.LibraryApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "LiMS API is unavailable while updating a reader profile.");
            ModelState.AddModelError(string.Empty, "Không thể kết nối máy chủ để cập nhật hồ sơ.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(CancellationToken cancellationToken)
    {
        var studentCode = HttpContext.Session.GetString("StudentCode");
        if (string.IsNullOrEmpty(studentCode))
            return RedirectToPage("/Index");

        StudentCode = studentCode;
        FullName = HttpContext.Session.GetString("FullName") ?? "";
        RemoveModelStateEntries(nameof(FullName), nameof(Email), nameof(Phone));
        if (NewPassword != ConfirmPassword)
            ModelState.AddModelError(nameof(ConfirmPassword), "Mật khẩu xác nhận không khớp.");
        if (!ModelState.IsValid)
        {
            await LoadProfileForDisplayAsync(studentCode, cancellationToken);
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

        await LoadProfileForDisplayAsync(studentCode, cancellationToken);
        return Page();
    }

    private void RemoveModelStateEntries(params string[] fields)
    {
        foreach (var field in fields)
            ModelState.Remove(field);
    }

    private void SetProfileFields(ApiUser profile)
    {
        FullName = profile.FullName;
        Email = profile.Email ?? "";
        Phone = profile.Phone;
    }

    private async Task RefreshNameClaimAsync(string fullName)
    {
        var authentication = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        if (authentication.Principal?.Identity is not ClaimsIdentity identity)
            return;

        var currentName = identity.FindFirst("LiMS:FullName");
        if (currentName is not null)
            identity.RemoveClaim(currentName);
        identity.AddClaim(new Claim("LiMS:FullName", fullName));
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authentication.Principal,
            authentication.Properties);
    }

    private async Task LoadProfileForDisplayAsync(string studentCode, CancellationToken cancellationToken)
    {
        try
        {
            Profile = await libraryApi.GetUserAsync(studentCode, cancellationToken);
            if (Profile is not null)
                SetProfileFields(Profile);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "LiMS API is unavailable while loading the reader profile.");
            ModelState.AddModelError(string.Empty, "Không thể tải thông tin cá nhân. Vui lòng thử lại.");
        }
    }
}
