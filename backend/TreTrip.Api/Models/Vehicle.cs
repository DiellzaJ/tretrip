
namespace TreTrip.Api.Models;

public class Vehicle
{
    public int Id { get; set; }

    public int TaxiCompanyId { get; set; }
    public TaxiCompany TaxiCompany { get; set; } = null!;

    public string PlateNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool IsAvailable { get; set; } = true;

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
