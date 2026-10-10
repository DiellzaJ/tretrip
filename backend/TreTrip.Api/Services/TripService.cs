using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public class TripService : ITripService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly INotificationRealtimeSender _realtimeSender;
    private const int BufferMinutes = 15;

    public TripService(
        AppDbContext db,
        INotificationService notificationService,
        INotificationRealtimeSender realtimeSender)
    {
        _db = db;
        _notificationService = notificationService;
        _realtimeSender = realtimeSender;
    }

    public async Task<TripResponseDto> CreateAsync(
        int schedulerId,
        CreateTripDto request)
    {
        // Validate scheduler account
        var scheduler = await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == schedulerId);

        if (scheduler == null ||
            !scheduler.IsActive ||
            scheduler.Role.Name != "Scheduler")
        {
            throw new UnauthorizedAccessException(
                "Scheduler account is not authorized.");
        }

        // Validate departure time
        if (!request.ScheduledAt.HasValue ||
            request.ScheduledAt.Value.Kind != DateTimeKind.Utc ||
            request.ScheduledAt.Value <= DateTime.UtcNow)
        {
            throw new ArgumentException(
                "ScheduledAt must be a future UTC date.");
        }

        var scheduledAt = request.ScheduledAt.Value;

        var pickup = request.PickupAddress?.Trim() ?? "";
        var destination = request.DestinationAddress?.Trim() ?? "";

        if (pickup.Length < 3 || destination.Length < 3 ||
            !request.PickupLatitude.HasValue ||
            !request.PickupLongitude.HasValue ||
            !request.DestinationLatitude.HasValue ||
            !request.DestinationLongitude.HasValue)
        {
            throw new ArgumentException(
                "Invalid pickup or destination information.");
        }

        var lat1 = request.PickupLatitude.Value;
        var lon1 = request.PickupLongitude.Value;
        var lat2 = request.DestinationLatitude.Value;
        var lon2 = request.DestinationLongitude.Value;

        if (!double.IsFinite(lat1) ||
            !double.IsFinite(lon1) ||
            !double.IsFinite(lat2) ||
            !double.IsFinite(lon2) ||
            Math.Abs(lat1) > 90 ||
            Math.Abs(lat2) > 90 ||
            Math.Abs(lon1) > 180 ||
            Math.Abs(lon2) > 180)
        {
            throw new ArgumentException(
                "Invalid geographic coordinates.");
        }

        // Approximate distance using Haversine
        var straightDistance = CalculateDistanceKm(
            lat1, lon1, lat2, lon2);

        if (straightDistance < 0.05)
        {
            throw new ArgumentException(
                "Pickup and destination are too close.");
        }

        // Demo estimates, not actual road routing
        var distanceKm = Math.Round(
            (decimal)(straightDistance * 1.3), 2);

        var durationMinutes = Math.Max(
            5,
            (int)Math.Ceiling(
                (double)distanceKm / 30.0 * 60.0));

        // Demo pricing: EUR 2.50 + EUR 0.80/km
        var price = Math.Round(
            2.50m + distanceKm * 0.80m, 2);

        var tripEnd = scheduledAt.AddMinutes(
            durationMinutes + BufferMinutes);

        // Serialize trip assignment transactions
        // to avoid simultaneous double assignment.
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774411);");

        // Find eligible drivers
        var drivers = await _db.Drivers
            .AsNoTracking()
            .Where(d =>
                d.IsActive &&
                d.IsAvailable &&
                d.User.IsActive &&
                d.User.Role.Name == "Driver" &&
                d.TaxiCompany.IsActive &&
                d.TaxiCompany.ContractStartDate <= scheduledAt &&
                (!d.TaxiCompany.ContractEndDate.HasValue ||
                 d.TaxiCompany.ContractEndDate.Value >= scheduledAt))
            .OrderBy(d => d.Id)
            .Select(d => new
            {
                d.Id,
                d.TaxiCompanyId
            })
            .ToListAsync();

        // Find eligible vehicles
        var vehicles = await _db.Vehicles
            .AsNoTracking()
            .Where(v =>
                v.IsActive &&
                v.IsAvailable &&
                v.TaxiCompany.IsActive &&
                v.TaxiCompany.ContractStartDate <= scheduledAt &&
                (!v.TaxiCompany.ContractEndDate.HasValue ||
                 v.TaxiCompany.ContractEndDate.Value >= scheduledAt))
            .OrderBy(v => v.Id)
            .Select(v => new
            {
                v.Id,
                v.TaxiCompanyId
            })
            .ToListAsync();

        // Existing assigned and ongoing trips
        var existingTrips = await _db.Trips
            .AsNoTracking()
            .Where(t =>
                t.Status == "Assigned" ||
                t.Status == "Arrived" ||
                t.Status == "InProgress")
            .Select(t => new
            {
                t.DriverId,
                t.VehicleId,
                t.ScheduledAt,
                t.EstimatedDurationMinutes,
                t.Status
            })
            .ToListAsync();
        // Find trips that conflict with the requested time
        var now = DateTime.UtcNow;

        var conflictingTrips = existingTrips
            .Where(t =>
            {
                var occupiedFrom = t.ScheduledAt;

                var occupiedUntil =
                    t.ScheduledAt.AddMinutes(
                        t.EstimatedDurationMinutes +
                        BufferMinutes);

                // Trips already active may take longer
                // than their original estimated duration.
                if (t.Status == "Arrived" ||
                    t.Status == "InProgress")
                {
                    if (now < occupiedFrom)
                        occupiedFrom = now;

                    var estimatedRemainingEnd =
                        now.AddMinutes(
                            t.EstimatedDurationMinutes +
                            BufferMinutes);

                    if (estimatedRemainingEnd > occupiedUntil)
                        occupiedUntil = estimatedRemainingEnd;
                }

                return occupiedFrom < tripEnd &&
                       occupiedUntil > scheduledAt;
            })
            .ToList();


        int? assignedDriverId = null;
        int? assignedVehicleId = null;
        int? assignedCompanyId = null;

        foreach (var driver in drivers)
        {

            var driverBusy = conflictingTrips.Any(t =>
                t.DriverId == driver.Id);


            if (driverBusy)
                continue;


            var vehicle = vehicles.FirstOrDefault(v =>
                v.TaxiCompanyId == driver.TaxiCompanyId &&
                !conflictingTrips.Any(t =>
                    t.VehicleId == v.Id));


            if (vehicle == null)
                continue;

            assignedDriverId = driver.Id;
            assignedVehicleId = vehicle.Id;
            assignedCompanyId = driver.TaxiCompanyId;

            break;
        }

        // Create reservation
        var trip = new Trip
        {
            SchedulerId = schedulerId,

            DriverId = assignedDriverId,
            VehicleId = assignedVehicleId,
            TaxiCompanyId = assignedCompanyId,

            PickupAddress = pickup,
            PickupLatitude = lat1,
            PickupLongitude = lon1,

            DestinationAddress = destination,
            DestinationLatitude = lat2,
            DestinationLongitude = lon2,

            DistanceKm = distanceKm,
            EstimatedDurationMinutes = durationMinutes,
            Price = price,

            ScheduledAt = scheduledAt,
            CreatedAt = DateTime.UtcNow,

            Status = assignedDriverId.HasValue
                ? "Assigned"
                : "Pending"
        };

        _db.Trips.Add(trip);
        await _db.SaveChangesAsync();

        // Notify the driver when a trip is assigned
        int? driverUserIdToNotify = null;
        if (trip.DriverId.HasValue)
        {
            var driverUserId = await _db.Drivers
                .Where(d => d.Id == trip.DriverId.Value)
                .Select(d => d.UserId)
                .SingleAsync();

            driverUserIdToNotify = driverUserId;

            var message =
                $"New trip assigned! " +
                $"Pickup: {trip.PickupAddress}. " +
                $"Destination: {trip.DestinationAddress}. " +
                $"Departure (UTC): {trip.ScheduledAt:yyyy-MM-dd HH:mm}.";

            await _notificationService.CreateAsync(
                driverUserId,
                trip.Id,
                message
            );
        }

        await transaction.CommitAsync();

        // Send real-time event only after successful commit
        if (driverUserIdToNotify.HasValue)
        {
            await _realtimeSender.NotifyUserAsync(
                driverUserIdToNotify.Value);
        }

        var result = await GetByIdAsync(trip.Id, schedulerId);

        return result ?? throw new InvalidOperationException(
            "Created trip could not be retrieved.");
    }

    // All trips created by a scheduler
    public async Task<List<TripResponseDto>>
        GetForSchedulerAsync(int schedulerId)
    {
        var trips = await TripDetails()
            .Where(t => t.SchedulerId == schedulerId)
            .OrderByDescending(t => t.ScheduledAt)
            .ToListAsync();

        return trips.Select(ToResponse).ToList();
    }

    // Single trip, owned by the scheduler
    public async Task<TripResponseDto?> GetByIdAsync(
        int tripId,
        int schedulerId)
    {
        var trip = await TripDetails()
            .FirstOrDefaultAsync(t =>
                t.Id == tripId &&
                t.SchedulerId == schedulerId);

        return trip == null ? null : ToResponse(trip);
    }

    private IQueryable<Trip> TripDetails()
    {
        return _db.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
                .ThenInclude(d => d!.User)
            .Include(t => t.Vehicle)
            .Include(t => t.TaxiCompany);
    }

    private static TripResponseDto ToResponse(Trip trip)
    {
        return new TripResponseDto
        {
            Id = trip.Id,
            SchedulerId = trip.SchedulerId,

            DriverId = trip.DriverId,
            DriverName = trip.Driver?.User.FullName,

            VehicleId = trip.VehicleId,
            VehiclePlateNumber = trip.Vehicle?.PlateNumber,

            TaxiCompanyId = trip.TaxiCompanyId,
            TaxiCompanyName = trip.TaxiCompany?.Name,

            PickupAddress = trip.PickupAddress,
            PickupLatitude = trip.PickupLatitude,
            PickupLongitude = trip.PickupLongitude,

            DestinationAddress = trip.DestinationAddress,
            DestinationLatitude = trip.DestinationLatitude,
            DestinationLongitude = trip.DestinationLongitude,

            DistanceKm = trip.DistanceKm,
            EstimatedDurationMinutes =
                trip.EstimatedDurationMinutes,
            Price = trip.Price,

            ScheduledAt = trip.ScheduledAt,
            StartedAt = trip.StartedAt,
            CompletedAt = trip.CompletedAt,
            CreatedAt = trip.CreatedAt,

            Status = trip.Status
        };
    }

    // Cancel a trip owned by the Scheduler
    public async Task<CancelTripResult> CancelAsync(
        int tripId,
        int schedulerId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        // Use the same lock as automatic assignment
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774411);");

        var trip = await _db.Trips
            .FirstOrDefaultAsync(t =>
                t.Id == tripId &&
                t.SchedulerId == schedulerId);

        if (trip == null)
        {
            return CancelTripResult.NotFound;
        }

        if ((trip.Status != "Pending" &&
            trip.Status != "Assigned") ||
            trip.StartedAt.HasValue)
        {
            return CancelTripResult.InvalidStatus;
        }

        trip.Status = "Cancelled";

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return CancelTripResult.Cancelled;
    }

    // Haversine: straight-line distance
    private static double CalculateDistanceKm(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        const double earthRadiusKm = 6371.0;

        static double ToRadians(double degrees)
            => degrees * Math.PI / 180.0;

        var deltaLat = ToRadians(lat2 - lat1);
        var deltaLon = ToRadians(lon2 - lon1);

        var a =
            Math.Pow(Math.Sin(deltaLat / 2), 2) +
            Math.Cos(ToRadians(lat1)) *
            Math.Cos(ToRadians(lat2)) *
            Math.Pow(Math.Sin(deltaLon / 2), 2);

        a = Math.Clamp(a, 0.0, 1.0);

        return 2 * earthRadiusKm *
               Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}