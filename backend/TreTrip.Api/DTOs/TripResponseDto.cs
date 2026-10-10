namespace TreTrip.Api.DTOs;

public class TripResponseDto
{
    public int Id { get; set; }

    // Scheduler information
    public int SchedulerId { get; set; }

    // Assigned driver and vehicle
    public int? DriverId { get; set; }
    public string? DriverName { get; set; }

    public int? VehicleId { get; set; }
    public string? VehiclePlateNumber { get; set; }

    public int? TaxiCompanyId { get; set; }
    public string? TaxiCompanyName { get; set; }

    // Pickup
    public string PickupAddress { get; set; } = string.Empty;
    public double PickupLatitude { get; set; }
    public double PickupLongitude { get; set; }

    // Destination
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }

    // Trip estimates
    public decimal DistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public decimal Price { get; set; }

    // Dates
    public DateTime ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Trip status
    public string Status { get; set; } = string.Empty;
}