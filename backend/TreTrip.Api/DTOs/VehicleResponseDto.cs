namespace TreTrip.Api.DTOs;

public class VehicleResponseDto
{
    public int Id { get; set; }

    public string PlateNumber { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int TaxiCompanyId { get; set; }

    public string TaxiCompanyName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool IsAvailable { get; set; }
}