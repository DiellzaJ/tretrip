using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    // Create notification
    public async Task CreateAsync(
        int userId,
        int tripId,
        string message)
    {
        if (userId <= 0 || tripId <= 0 ||
            string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "Invalid notification information.");
        }

        var notification = new Notification
        {
            UserId = userId,
            TripId = tripId,
            Message = message.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);

        await _db.SaveChangesAsync();
    }

    // Get notifications for logged-in user
    public async Task<List<NotificationResponseDto>>
        GetForUserAsync(int userId)
    {
        return await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(100)
            .Select(n => new NotificationResponseDto
            {
                Id = n.Id,
                TripId = n.TripId,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    // Mark notification as read
    public async Task<bool> MarkAsReadAsync(
        int notificationId,
        int userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n =>
                n.Id == notificationId &&
                n.UserId == userId);

        if (notification == null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync();
        }

        return true;
    }
}