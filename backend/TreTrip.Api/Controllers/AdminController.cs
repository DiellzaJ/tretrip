
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    // Test Admin authorization
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

    // Create an employee
    [HttpPost("employees")]
    public async Task<ActionResult<UserResponseDto>>
        CreateEmployee([FromBody] CreateEmployeeDto request)
    {
        try
        {
            var employee = await _adminService
                .CreateEmployeeAsync(request);

            return StatusCode(201, employee);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
            when (ex.Message ==
                  "A user with this email already exists.")
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Get all users
    [HttpGet("users")]
    public async Task<ActionResult<List<UserResponseDto>>>
        GetUsers()
    {
        var users = await _adminService.GetUsersAsync();

        return Ok(users);
    }

    // Activate or deactivate a user
    [HttpPatch("users/{userId:int}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        int userId,
        [FromBody] UpdateUserStatusDto request)
    {
        var success = await _adminService
            .SetUserActiveStatusAsync(
                userId,
                request.IsActive!.Value
            );

        if (!success)
        {
            return NotFound(new
            {
                message = "User not found or cannot be modified."
            });
        }

        return Ok(new
        {
            message = "User status updated successfully.",
            userId,
            isActive = request.IsActive.Value
        });
    }
}
