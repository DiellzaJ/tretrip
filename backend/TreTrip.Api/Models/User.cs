
namespace TreTrip.Api.Models;

public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAt { get; set; }

    public ICollection<Trip> ScheduledTrips { get; set; } = new List<Trip>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public Driver? DriverProfile { get; set; }

    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastFailedLoginAt { get; set; }
}
