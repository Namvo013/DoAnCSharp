using System.Security.Claims;
using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiMS.Api.Controllers;

/// <summary>Điểm cuối xác thực và quản lý thông tin đăng nhập.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [EndpointSummary("Xác thực tài khoản và cấp mã truy cập JWT")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result is null
            ? Unauthorized(ApiResponse<object?>.Failure(
                "INVALID_CREDENTIALS",
                "Tài khoản hoặc mật khẩu không chính xác."))
            : Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    [EndpointSummary("Đổi mật khẩu của tài khoản hiện đã xác thực")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var accountIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountIdValue, out var accountId))
            return Unauthorized();

        var changed = await authService.ChangePasswordAsync(accountId, request, cancellationToken);
        return changed
            ? Ok(new MessageResponse("Đổi mật khẩu thành công."))
            : BadRequest(ApiResponse<object?>.Failure(
                "CURRENT_PASSWORD_INVALID",
                "Mật khẩu hiện tại không chính xác."));
    }
}
