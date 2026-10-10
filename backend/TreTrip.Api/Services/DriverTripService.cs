using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public class DriverTripService : IDriverTripService
{

    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;

    private const int EarlyStartMinutes = 15;

    public DriverTripService(
        AppDbContext db,
        INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    // Get trips assigned to the logged-in driver
    public async Task<List<TripResponseDto>> GetMyTripsAsync(
        int driverUserId)
    {
        var trips = await _db.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
                .ThenInclude(d => d!.User)
            .Include(t => t.Vehicle)
            .Include(t => t.TaxiCompany)
            .Where(t =>
                t.Driver != null &&
                t.Driver.UserId == driverUserId)
            .OrderByDescending(t => t.ScheduledAt)
            .ToListAsync();

        return trips.Select(t => new TripResponseDto
        {
            Id = t.Id,
            SchedulerId = t.SchedulerId,

            DriverId = t.DriverId,
            DriverName = t.Driver?.User.FullName,

            VehicleId = t.VehicleId,
            VehiclePlateNumber = t.Vehicle?.PlateNumber,

            TaxiCompanyId = t.TaxiCompanyId,
            TaxiCompanyName = t.TaxiCompany?.Name,

            PickupAddress = t.PickupAddress,
            PickupLatitude = t.PickupLatitude,
            PickupLongitude = t.PickupLongitude,

            DestinationAddress = t.DestinationAddress,
            DestinationLatitude = t.DestinationLatitude,
            DestinationLongitude = t.DestinationLongitude,

            DistanceKm = t.DistanceKm,
            EstimatedDurationMinutes =
                t.EstimatedDurationMinutes,
            Price = t.Price,

            ScheduledAt = t.ScheduledAt,
            StartedAt = t.StartedAt,
            CompletedAt = t.CompletedAt,
            CreatedAt = t.CreatedAt,

            Status = t.Status
        }).ToList();
    }

// Driver confirms arrival at pickup location
    public async Task<DriverTripActionResult> ArriveAsync(
        int tripId,
        int driverUserId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        // Coordinate with trip assignment and cancellation
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774411);");

        var trip = await _db.Trips
            .Include(t => t.Driver)
                .ThenInclude(d => d!.User)
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t =>
                t.Id == tripId &&
                t.Driver != null &&
                t.Driver.UserId == driverUserId);

        if (trip == null)
        {
            return DriverTripActionResult.NotFound;
        }

        // Only an assigned trip can be marked as arrived
        if (trip.Status != "Assigned" ||
            trip.StartedAt.HasValue)
        {
            return DriverTripActionResult.InvalidStatus;
        }

        // Avoid reporting arrival too far in advance
        if (DateTime.UtcNow <
            trip.ScheduledAt.AddMinutes(-30))
        {
            return DriverTripActionResult.TooEarly;
        }

        // Validate the driver account
        if (trip.Driver == null ||
            !trip.Driver.IsActive ||
            !trip.Driver.User.IsActive)
        {
            return DriverTripActionResult.InvalidStatus;
        }

        trip.Status = "Arrived";

        await _db.SaveChangesAsync();

        // Notify the employee who booked the taxi
        var driverName = trip.Driver.User.FullName;
        var plateNumber =
            trip.Vehicle?.PlateNumber ?? "Not available";

        var message =
            $"Your taxi has arrived! " +
            $"Driver: {driverName}. " +
            $"Vehicle: {plateNumber}. " +
            $"Pickup: {trip.PickupAddress}.";

        await _notificationService.CreateAsync(
            trip.SchedulerId,
            trip.Id,
            message
        );

        await transaction.CommitAsync();

        return DriverTripActionResult.Success;
    }

    // Start an assigned trip
    public async Task<DriverTripActionResult> StartAsync(
        int tripId,
        int driverUserId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        // Coordinate with automatic assignment
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774411);");

        var trip = await _db.Trips
            .Include(t => t.Driver)
                .ThenInclude(d => d!.User)
            .Include(t => t.Vehicle)
            .Include(t => t.TaxiCompany)
            .FirstOrDefaultAsync(t =>
                t.Id == tripId &&
                t.Driver != null &&
                t.Driver.UserId == driverUserId);

        if (trip == null)
        {
            return DriverTripActionResult.NotFound;
        }

        // Only assigned trips can be started
       if ((trip.Status != "Assigned" &&
        trip.Status != "Arrived") ||
        trip.StartedAt.HasValue)
        {
            return DriverTripActionResult.InvalidStatus;
        }

        var now = DateTime.UtcNow;

        // Allow starting at most 15 minutes early
        if (now < trip.ScheduledAt.AddMinutes(
            -EarlyStartMinutes))
        {
            return DriverTripActionResult.TooEarly;
        }

        // Validate driver, vehicle and company
        if (trip.Driver == null ||
            trip.Vehicle == null ||
            trip.TaxiCompany == null ||
            !trip.Driver.IsActive ||
            !trip.Driver.User.IsActive ||
            !trip.Driver.IsAvailable ||
            !trip.Vehicle.IsActive ||
            !trip.Vehicle.IsAvailable ||
            !trip.TaxiCompany.IsActive ||
            trip.TaxiCompany.ContractStartDate > now ||
            (trip.TaxiCompany.ContractEndDate.HasValue &&
             trip.TaxiCompany.ContractEndDate.Value < now))
        {
            return DriverTripActionResult.InvalidStatus;
        }

        // Prevent simultaneous active trips
        var hasActiveTrip = await _db.Trips
            .AnyAsync(t =>
                t.Id != tripId &&
                t.Status == "InProgress" &&
                (t.DriverId == trip.DriverId ||
                 t.VehicleId == trip.VehicleId));

        if (hasActiveTrip)
        {
            return DriverTripActionResult.InvalidStatus;
        }

        trip.Status = "InProgress";
        trip.StartedAt = now;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return DriverTripActionResult.Success;
    }

    // Complete an ongoing trip
    public async Task<DriverTripActionResult> CompleteAsync(
        int tripId,
        int driverUserId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774411);");

        var trip = await _db.Trips
            .FirstOrDefaultAsync(t =>
                t.Id == tripId &&
                t.Driver != null &&
                t.Driver.UserId == driverUserId);

        if (trip == null)
        {
            return DriverTripActionResult.NotFound;
        }

        // Only ongoing trips can be completed
        if (trip.Status != "InProgress" ||
            !trip.StartedAt.HasValue ||
            trip.CompletedAt.HasValue)
        {
            return DriverTripActionResult.InvalidStatus;
        }

        trip.Status = "Completed";
        trip.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return DriverTripActionResult.Success;
    }
}