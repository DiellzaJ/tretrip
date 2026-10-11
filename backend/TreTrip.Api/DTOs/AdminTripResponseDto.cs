namespace TreTrip.Api.DTOs;

// Fields exposed only to users with the Admin role.
public class AdminTripResponseDto
{
    public int Id { get; set; }
    public int SchedulerId { get; set; }
    public string SchedulerName { get; set; } = string.Empty;
    public string SchedulerEmail { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public decimal Price { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
