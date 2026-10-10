
namespace TreTrip.Api.Models;

public class TaxiCompany
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
