
namespace TreTrip.Api.Models;

public class Trip
{
    public int Id { get; set; }

    public int SchedulerId { get; set; }
    public User Scheduler { get; set; } = null!;

    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public int? TaxiCompanyId { get; set; }
    public TaxiCompany? TaxiCompany { get; set; }

    public string PickupAddress { get; set; } = string.Empty;
    public double PickupLatitude { get; set; }
    public double PickupLongitude { get; set; }

    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }

    public decimal DistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public decimal Price { get; set; }

    public DateTime ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Pending, Assigned, InProgress, Completed, Cancelled
    public string Status { get; set; } = "Pending";

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
