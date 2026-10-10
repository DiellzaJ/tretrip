
using System.ComponentModel.DataAnnotations;

namespace TreTrip.Api.DTOs;

public class UpdateDriverAvailabilityDto
{
    [Required]
    public bool? IsAvailable { get; set; }
}
