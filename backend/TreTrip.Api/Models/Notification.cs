
namespace TreTrip.Api.Models;

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int TripId { get; set; }
    public Trip Trip { get; set; } = null!;

    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
