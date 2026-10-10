
namespace TreTrip.Api.DTOs;

public class TaxiCompanyResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime ContractStartDate { get; set; }

    public DateTime? ContractEndDate { get; set; }
}
