
namespace TreTrip.Api.DTOs;

public class DriverResponseDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string LicenseNumber { get; set; } = string.Empty;

    public int TaxiCompanyId { get; set; }

    public string TaxiCompanyName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool IsAvailable { get; set; }
}
