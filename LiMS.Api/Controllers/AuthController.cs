using System.Security.Claims;
using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [EndpointSummary("Authenticate an account and issue a JWT")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result is null
            ? Unauthorized(new ProblemDetails { Title = "Thông tin đăng nhập không hợp lệ." })
            : Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    [EndpointSummary("Change the password of the authenticated account")]
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
            : BadRequest(new ProblemDetails { Title = "Mật khẩu hiện tại không đúng." });
    }
}
