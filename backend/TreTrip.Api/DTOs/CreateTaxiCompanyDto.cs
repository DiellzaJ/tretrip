
using System.ComponentModel.DataAnnotations;

namespace TreTrip.Api.DTOs;

public class CreateTaxiCompanyDto
{
    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public DateTime? ContractStartDate { get; set; }

    public DateTime? ContractEndDate { get; set; }
}
