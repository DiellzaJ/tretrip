using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using TreTrip.Api.Data;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;
using TreTrip.Api.Services;
using Xunit;

namespace TreTrip.Api.Tests;

public class PendingTripAssignmentIntegrationTests
{
    [Fact]
    public async Task OverlappingPendingTrips_ShouldAssignOnlyOne()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("tretrip_assignment_tests")
                .WithUsername("test_user")
                .WithPassword("test_password_only")
                .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var scheduler = new User
        {
            FullName = "Test Scheduler",
            Email = "scheduler@assignment.test",
            PasswordHash = "test-only",
            RoleId = 2
        };

        var driverUser = new User
        {
            FullName = "Test Driver",
            Email = "driver@assignment.test",
            PasswordHash = "test-only",
            RoleId = 3
        };

        var company = new TaxiCompany
        {
            Name = "Test Taxi Company",
            Phone = "123456",
            ContractStartDate = DateTime.UtcNow.AddDays(-1),
            ContractEndDate = DateTime.UtcNow.AddDays(30)
        };

        var driver = new Driver
        {
            User = driverUser,
            TaxiCompany = company,
            LicenseNumber = "TEST-LICENSE-002"
        };

        var vehicle = new Vehicle
        {
            TaxiCompany = company,
            PlateNumber = "TEST-002-AB",
            Model = "Test Vehicle"
        };

        // Both trips request the same departure time
        var departure = DateTime.UtcNow.AddDays(2);

        Trip CreatePendingTrip() => new()
        {
            Scheduler = scheduler,
            PickupAddress = "Prishtina Center",
            PickupLatitude = 42.6629,
            PickupLongitude = 21.1624,
            DestinationAddress = "Albi Mall",
            DestinationLatitude = 42.6382,
            DestinationLongitude = 21.1610,
            DistanceKm = 3.57m,
            EstimatedDurationMinutes = 20,
            Price = 5.36m,
            ScheduledAt = departure,
            Status = "Pending"
        };

        var tripA = CreatePendingTrip();
        var tripB = CreatePendingTrip();

        db.Users.AddRange(scheduler, driverUser);
        db.TaxiCompanies.Add(company);
        db.Drivers.Add(driver);
        db.Vehicles.Add(vehicle);
        db.Trips.AddRange(tripA, tripB);

        await db.SaveChangesAsync();

        // Use the real notification service
        var notificationService = new NotificationService(db);

        // Simulate real-time delivery
        var realtimeSender =
            Substitute.For<INotificationRealtimeSender>();

        realtimeSender
            .NotifyUserAsync(Arg.Any<int>())
            .Returns(Task.CompletedTask);

        var service = new PendingTripAssignmentService(
            db,
            notificationService,
            realtimeSender);

        // First assignment attempt
        var assignedCount =
            await service.AssignPendingTripsAsync();

        Assert.Equal(1, assignedCount);

        // Repeating assignment must not duplicate it
        var secondCount =
            await service.AssignPendingTripsAsync();

        Assert.Equal(0, secondCount);

        // Verify persisted state using fresh DbContext
        await using var verifyDb = new AppDbContext(options);

        var savedTrips = await verifyDb.Trips
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync();

        Assert.Equal(2, savedTrips.Count);

        var assigned = Assert.Single(
            savedTrips, t => t.Status == "Assigned");

        var pending = Assert.Single(
            savedTrips, t => t.Status == "Pending");

        Assert.Equal(driver.Id, assigned.DriverId);
        Assert.Equal(vehicle.Id, assigned.VehicleId);

        Assert.Null(pending.DriverId);
        Assert.Null(pending.VehicleId);

        // The assigned trip must generate 2 notifications
        var notifications = await verifyDb.Notifications
            .AsNoTracking()
            .Where(n => n.TripId == assigned.Id)
            .ToListAsync();

        Assert.Equal(2, notifications.Count);

        Assert.Contains(notifications,
            n => n.UserId == scheduler.Id);

        Assert.Contains(notifications,
            n => n.UserId == driverUser.Id);

        // No notifications should exist for Pending trip
        Assert.False(await verifyDb.Notifications
            .AnyAsync(n => n.TripId == pending.Id));

        await realtimeSender.Received(1)
            .NotifyUserAsync(scheduler.Id);

        await realtimeSender.Received(1)
            .NotifyUserAsync(driverUser.Id);
    }
}