using System.ComponentModel.DataAnnotations;

namespace TreTrip.Api.DTOs;

public class UpdateVehicleAvailabilityDto
{
    [Required]
    public bool? IsAvailable { get; set; }
}