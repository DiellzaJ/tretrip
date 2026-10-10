using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using TreTrip.Api.Data;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;
using TreTrip.Api.Services;
using Xunit;

namespace TreTrip.Api.Tests;

public class DriverAvailabilityIntegrationTests
{
    [Fact]
    public async Task AvailableDriver_ShouldReceivePendingTrip()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("tretrip_availability_tests")
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
            Email = "scheduler@availability.test",
            PasswordHash = "test-only",
            RoleId = 2
        };

        var driverUser = new User
        {
            FullName = "Test Driver",
            Email = "driver@availability.test",
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

        // Initially the driver is unavailable
        var driver = new Driver
        {
            User = driverUser,
            TaxiCompany = company,
            LicenseNumber = "TEST-LICENSE-003",
            IsAvailable = false
        };

        var vehicle = new Vehicle
        {
            TaxiCompany = company,
            PlateNumber = "TEST-003-AB",
            Model = "Test Vehicle",
            IsAvailable = true
        };

        var trip = new Trip
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
            ScheduledAt = DateTime.UtcNow.AddDays(2),
            Status = "Pending"
        };

        db.Users.AddRange(scheduler, driverUser);
        db.TaxiCompanies.Add(company);
        db.Drivers.Add(driver);
        db.Vehicles.Add(vehicle);
        db.Trips.Add(trip);

        await db.SaveChangesAsync();

        var realtimeSender =
            Substitute.For<INotificationRealtimeSender>();

        realtimeSender
            .NotifyUserAsync(Arg.Any<int>())
            .Returns(Task.CompletedTask);

        var service = new PendingTripAssignmentService(
            db,
            new NotificationService(db),
            realtimeSender);

        // Step 1: Driver unavailable
        var firstCount =
            await service.AssignPendingTripsAsync();

        Assert.Equal(0, firstCount);
        Assert.Equal("Pending", trip.Status);
        Assert.Null(trip.DriverId);

        // Step 2: Driver becomes available
        driver.IsAvailable = true;
        await db.SaveChangesAsync();

        // Step 3: Automatic assignment
        var secondCount =
            await service.AssignPendingTripsAsync();

        Assert.Equal(1, secondCount);

        // Step 4: Verify persisted state
        await using var verifyDb =
            new AppDbContext(options);

        var savedTrip = await verifyDb.Trips
            .AsNoTracking()
            .SingleAsync(t => t.Id == trip.Id);

        Assert.Equal("Assigned", savedTrip.Status);
        Assert.Equal(driver.Id, savedTrip.DriverId);
        Assert.Equal(vehicle.Id, savedTrip.VehicleId);

        // Step 5: Verify notifications
        var notifications = await verifyDb.Notifications
            .AsNoTracking()
            .Where(n => n.TripId == trip.Id)
            .ToListAsync();

        Assert.Equal(2, notifications.Count);

        Assert.Contains(notifications,
            n => n.UserId == scheduler.Id);

        Assert.Contains(notifications,
            n => n.UserId == driverUser.Id);

        // Step 6: Verify real-time events
        await realtimeSender.Received(1)
            .NotifyUserAsync(scheduler.Id);

        await realtimeSender.Received(1)
            .NotifyUserAsync(driverUser.Id);
    }
}