using System.ComponentModel.DataAnnotations;

namespace TreTrip.Api.DTOs;

public class CreateVehicleDto
{
    [Required]
    [StringLength(20, MinimumLength = 3)]
    public string PlateNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Model { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TaxiCompanyId { get; set; }
}