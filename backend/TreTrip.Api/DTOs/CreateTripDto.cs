using System.ComponentModel.DataAnnotations;

namespace TreTrip.Api.DTOs;

public class CreateTripDto
{
    // Pickup information
    [Required]
    [StringLength(250, MinimumLength = 3)]
    public string PickupAddress { get; set; } = string.Empty;

    [Required]
    [Range(-90, 90)]
    public double? PickupLatitude { get; set; }

    [Required]
    [Range(-180, 180)]
    public double? PickupLongitude { get; set; }

    // Destination information
    [Required]
    [StringLength(250, MinimumLength = 3)]
    public string DestinationAddress { get; set; } = string.Empty;

    [Required]
    [Range(-90, 90)]
    public double? DestinationLatitude { get; set; }

    [Required]
    [Range(-180, 180)]
    public double? DestinationLongitude { get; set; }

    // Requested departure time
    [Required]
    public DateTime? ScheduledAt { get; set; }
}