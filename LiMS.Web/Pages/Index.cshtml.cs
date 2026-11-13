using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LiMS.Web.Pages;

public class IndexModel(
    LibraryApiClient libraryApi,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = "";

    [BindProperty]
    [Required]
    [StringLength(50, MinimumLength = 6)]
    public string Password { get; set; } = "";

    [BindProperty]
    public bool RememberMe { get; set; }

    public IActionResult OnGet()
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Home");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var user = await libraryApi.LoginAsync(
                new LoginRequest(Username.Trim(), Password), cancellationToken);
            if (user is null)
            {
                ModelState.AddModelError(string.Empty,
                    "Thông tin đăng nhập không hợp lệ. Vui lòng kiểm tra lại tài khoản hoặc mật khẩu.");
                Password = "";
                ModelState.Remove(nameof(Password));
                return Page();
            }
            if (user.Role != "Borrower")
            {
                ModelState.AddModelError(string.Empty,
                    "Tài khoản thủ thư/quản trị phải đăng nhập bằng ứng dụng Admin.");
                Password = "";
                ModelState.Remove(nameof(Password));
                return Page();
            }
            if (string.IsNullOrWhiteSpace(user.AccessToken))
                throw new InvalidDataException("API đăng nhập không trả về mã truy cập.");

            if (user.ExpiresAt is not { } expiresAt || expiresAt <= DateTimeOffset.UtcNow)
                throw new InvalidDataException("Phiên đăng nhập do API cấp đã hết hạn hoặc không hợp lệ.");

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("LiMS:StudentCode", user.StudentCode),
                new Claim("LiMS:FullName", user.FullName),
                new Claim("LiMS:AccessToken", user.AccessToken),
                new Claim("LiMS:TokenExpiresAt", expiresAt.ToString("O"))
            };
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties
                {
                    IsPersistent = RememberMe,
                    ExpiresUtc = expiresAt
                });
            HttpContext.Session.SetString("StudentCode", user.StudentCode);
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("AccessToken", user.AccessToken);
            HttpContext.Session.SetString("TokenExpiresAt", expiresAt.ToString("O"));
            return RedirectToPage("/Home");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "LiMS API is unavailable during login.");
            ModelState.AddModelError(string.Empty,
                "Không thể kết nối máy chủ. Vui lòng thử lại sau.");
            Password = "";
            ModelState.Remove(nameof(Password));
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
