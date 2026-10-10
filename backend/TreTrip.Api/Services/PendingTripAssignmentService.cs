using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Services;

public class PendingTripAssignmentService
    : IPendingTripAssignmentService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly INotificationRealtimeSender _realtimeSender;

    public PendingTripAssignmentService(
        AppDbContext db,
        INotificationService notificationService,
        INotificationRealtimeSender realtimeSender)
    {
        _db = db;
        _notificationService = notificationService;
        _realtimeSender = realtimeSender;
    }
    public async Task<int> ExpirePendingTripsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);

        // Use the same lock as trip assignment.
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774411);",
            cancellationToken);

        var now = DateTime.UtcNow;

        // Find Pending trips whose departure time has passed.
        var expiredTrips = await _db.Trips
            .Where(t =>
                t.Status == "Pending" &&
                t.ScheduledAt <= now)
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

        if (expiredTrips.Count == 0)
            return 0;

        // Mark expired trips.
        foreach (var trip in expiredTrips)
        {
            trip.Status = "Expired";
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Create notifications for affected schedulers.
        foreach (var trip in expiredTrips)
        {
            var message =
                $"Your trip #{trip.Id} scheduled for " +
                $"{trip.ScheduledAt:yyyy-MM-dd HH:mm} UTC " +
                "has expired because no driver was assigned.";

            await _notificationService.CreateAsync(
                trip.SchedulerId,
                trip.Id,
                message);
        }

        // Persist the changes before sending SignalR events.
        await transaction.CommitAsync(cancellationToken);

        // Notify affected schedulers in real time.
        var schedulerIds = expiredTrips
            .Select(t => t.SchedulerId)
            .Distinct();

        foreach (var schedulerId in schedulerIds)
        {
            await _realtimeSender.NotifyUserAsync(
                schedulerId);
        }

        return expiredTrips.Count;
    }

    public async Task<int> AssignPendingTripsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);

        // Coordinate with trip creation and cancellation.
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774411);",
            cancellationToken);

        var now = DateTime.UtcNow;

        // Old, expired Pending trips are not assigned.
        var pendingTrips = await _db.Trips
            .Where(t =>
                t.Status == "Pending" &&
                t.ScheduledAt > now &&
                t.Scheduler.IsActive &&
                t.Scheduler.Role.Name == "Scheduler")
            .OrderBy(t => t.ScheduledAt)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        if (pendingTrips.Count == 0)
            return 0;

        var drivers = await _db.Drivers
            .AsNoTracking()
            .Where(d =>
                d.IsActive &&
                d.IsAvailable &&
                d.User.IsActive &&
                d.User.Role.Name == "Driver" &&
                d.TaxiCompany.IsActive)
            .OrderBy(d => d.Id)
            .Select(d => new
            {
                d.Id,
                d.UserId,
                DriverName = d.User.FullName,
                d.TaxiCompanyId,
                d.TaxiCompany.ContractStartDate,
                d.TaxiCompany.ContractEndDate
            })
            .ToListAsync(cancellationToken);

        var vehicles = await _db.Vehicles
            .AsNoTracking()
            .Where(v =>
                v.IsActive &&
                v.IsAvailable &&
                v.TaxiCompany.IsActive)
            .OrderBy(v => v.Id)
            .Select(v => new
            {
                v.Id,
                v.PlateNumber,
                v.TaxiCompanyId,
                v.TaxiCompany.ContractStartDate,
                v.TaxiCompany.ContractEndDate
            })
            .ToListAsync(cancellationToken);

        var occupiedTrips = await _db.Trips
            .AsNoTracking()
            .Where(t =>
                t.Status == "Assigned" ||
                t.Status == "Arrived" ||
                t.Status == "InProgress")
            .ToListAsync(cancellationToken);

        var assignments = new List<(
            int TripId,
            int SchedulerId,
            int DriverUserId,
            string DriverName,
            string Plate,
            string Pickup,
            string Destination,
            DateTime ScheduledAt)>();

        foreach (var trip in pendingTrips)
        {
            var tripEnd = trip.ScheduledAt.AddMinutes(
                trip.EstimatedDurationMinutes +
                TripConflictChecker.BufferMinutes);

            var conflicts = occupiedTrips
                .Where(t => TripConflictChecker.Overlaps(
                    t.ScheduledAt,
                    t.EstimatedDurationMinutes,
                    t.Status,
                    trip.ScheduledAt,
                    tripEnd,
                    now))
                .ToList();

            foreach (var driver in drivers)
            {
                // Company contract must cover departure.
                if (driver.ContractStartDate > trip.ScheduledAt ||
                    (driver.ContractEndDate.HasValue &&
                     driver.ContractEndDate.Value < trip.ScheduledAt))
                    continue;

                if (conflicts.Any(t => t.DriverId == driver.Id))
                    continue;

                var vehicle = vehicles.FirstOrDefault(v =>
                    v.TaxiCompanyId == driver.TaxiCompanyId &&
                    v.ContractStartDate <= trip.ScheduledAt &&
                    (!v.ContractEndDate.HasValue ||
                     v.ContractEndDate.Value >= trip.ScheduledAt) &&
                    !conflicts.Any(t => t.VehicleId == v.Id));

                if (vehicle == null)
                    continue;

                trip.DriverId = driver.Id;
                trip.VehicleId = vehicle.Id;
                trip.TaxiCompanyId = driver.TaxiCompanyId;
                trip.Status = "Assigned";

                // Prevent another Pending trip in this batch
                // from using an overlapping slot.
                occupiedTrips.Add(trip);

                assignments.Add((
                    trip.Id,
                    trip.SchedulerId,
                    driver.UserId,
                    driver.DriverName,
                    vehicle.PlateNumber,
                    trip.PickupAddress,
                    trip.DestinationAddress,
                    trip.ScheduledAt));

                break;
            }
        }

        if (assignments.Count == 0)
            return 0;

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var assignment in assignments)
        {
            var driverMessage =
                $"New trip assigned! " +
                $"Pickup: {assignment.Pickup}. " +
                $"Destination: {assignment.Destination}. " +
                $"Departure (UTC): " +
                $"{assignment.ScheduledAt:yyyy-MM-dd HH:mm}.";

            await _notificationService.CreateAsync(
                assignment.DriverUserId,
                assignment.TripId,
                driverMessage);

            var schedulerMessage =
                $"A driver has been assigned to your trip! " +
                $"Driver: {assignment.DriverName}. " +
                $"Vehicle: {assignment.Plate}.";

            await _notificationService.CreateAsync(
                assignment.SchedulerId,
                assignment.TripId,
                schedulerMessage);
        }

        // Commit before sending real-time events.
        await transaction.CommitAsync(cancellationToken);

        var userIds = assignments
            .SelectMany(a => new[]
            {
                a.DriverUserId,
                a.SchedulerId
            })
            .Distinct();

        foreach (var userId in userIds)
        {
            await _realtimeSender.NotifyUserAsync(userId);
        }

        return assignments.Count;
    }
}