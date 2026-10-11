using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TreTrip.Api.DTOs;
using TreTrip.Api.Services;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AccountSecurityController : ControllerBase
{
    private readonly IAccountSecurityService _service;

    public AccountSecurityController(IAccountSecurityService service) => _service = service;

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        if (!await _service.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword))
            return BadRequest(new { message = "Current password is incorrect, the new password was already used, or it does not meet the policy." });

        return Ok(new { message = "Password updated. Sign in again." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        try
        {
            await _service.RequestPasswordResetAsync(request.Email);
        }
        catch (InvalidOperationException)
        {
            return StatusCode(503, new { message = "Password recovery email is not configured. Contact your administrator." });
        }
        return Accepted(new { message = "If this account exists, a reset link will be emailed." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-recovery")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        if (!await _service.ResetPasswordAsync(request.Email, request.Token, request.NewPassword))
            return BadRequest(new { message = "Reset link is invalid or expired, or the new password does not meet the policy." });

        return Ok(new { message = "Password reset. Please sign in." });
    }
}
