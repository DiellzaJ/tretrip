
using System.ComponentModel.DataAnnotations;

namespace TreTrip.Api.DTOs;

public class CreateDriverDto
{
    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LicenseNumber { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TaxiCompanyId { get; set; }
}
