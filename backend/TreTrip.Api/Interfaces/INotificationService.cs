using TreTrip.Api.DTOs;

namespace TreTrip.Api.Interfaces;

public interface INotificationService
{
    // Create a notification for a user
    Task CreateAsync(
        int userId,
        int tripId,
        string message);

    // Get notifications belonging to the logged-in user
    Task<List<NotificationResponseDto>> GetForUserAsync(
        int userId);

    // Mark a notification as read
    Task<bool> MarkAsReadAsync(
        int notificationId,
        int userId);
}