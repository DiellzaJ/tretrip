
namespace TreTrip.Api.Models;

public class Driver
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int TaxiCompanyId { get; set; }
    public TaxiCompany TaxiCompany { get; set; } = null!;

    public string LicenseNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool IsAvailable { get; set; } = true;

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
