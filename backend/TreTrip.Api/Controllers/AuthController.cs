
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);

        if (result == null)
        {
            return Unauthorized(new
            {
                message = "Invalid credentials or account unavailable."
            });
        }

        return Ok(result);
    }
}
