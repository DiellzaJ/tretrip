
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(
        INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    // Get the logged-in user's ID from JWT
    private int? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return int.TryParse(userId, out var id)
            ? id
            : null;
    }

    // Get notifications for the logged-in user
    [HttpGet]
    public async Task<ActionResult<List<NotificationResponseDto>>>
        GetMyNotifications()
    {
        var userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var notifications = await _notificationService
            .GetForUserAsync(userId.Value);

        return Ok(notifications);
    }

    // Mark a notification as read
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetCurrentUserId();

        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var updated = await _notificationService
            .MarkAsReadAsync(id, userId.Value);

        if (!updated)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        return Ok(new
        {
            message = "Notification marked as read.",
            notificationId = id,
            isRead = true
        });
    }
}
