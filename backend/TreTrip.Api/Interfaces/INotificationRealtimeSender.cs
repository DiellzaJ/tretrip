namespace TreTrip.Api.Interfaces;

public interface INotificationRealtimeSender
{
    Task NotifyUserAsync(int userId);
}