
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    [HttpGet("test")]
    public IActionResult TestAdminAccess()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        return Ok(new
        {
            Message = "Admin access granted",
            UserId = userId,
            Role = "Admin"
        });
    }
}
