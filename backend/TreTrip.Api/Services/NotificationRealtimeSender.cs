
using Microsoft.AspNetCore.SignalR;
using TreTrip.Api.Hubs;
using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Services;

public class NotificationRealtimeSender
    : INotificationRealtimeSender
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationRealtimeSender> _logger;

    public NotificationRealtimeSender(
        IHubContext<NotificationHub> hub,
        ILogger<NotificationRealtimeSender> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyUserAsync(int userId)
    {
        try
        {
            await _hub.Clients
                .User(userId.ToString())
                .SendAsync("NotificationsUpdated");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not deliver real-time notification to user {UserId}",
                userId);
        }
    }
}
