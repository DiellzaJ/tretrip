
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using TreTrip.Api.Data;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;
using TreTrip.Api.Services;
using Xunit;

namespace TreTrip.Api.Tests;

public class DriverTripIntegrationTests
{
    [Fact]
    public async Task DriverTrip_ShouldFollowValidLifecycle()
    {
        // Start isolated PostgreSQL
        await using var postgres =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("tretrip_lifecycle_tests")
                .WithUsername("test_user")
                .WithPassword("test_password_only")
                .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        // Create test accounts
        var scheduler = new User
        {
            FullName = "Test Scheduler",
            Email = "scheduler@test.local",
            PasswordHash = "test-only",
            RoleId = 2
        };

        var driverUser = new User
        {
            FullName = "Test Driver",
            Email = "driver@test.local",
            PasswordHash = "test-only",
            RoleId = 3
        };

        var company = new TaxiCompany
        {
            Name = "Test Taxi",
            Phone = "123456",
            ContractStartDate = DateTime.UtcNow.AddDays(-1),
            ContractEndDate = DateTime.UtcNow.AddDays(30)
        };

        var driver = new Driver
        {
            User = driverUser,
            TaxiCompany = company,
            LicenseNumber = "TEST-LICENSE-001"
        };

        var vehicle = new Vehicle
        {
            TaxiCompany = company,
            PlateNumber = "TEST-001-AB",
            Model = "Test Vehicle"
        };

        var trip = new Trip
        {
            Scheduler = scheduler,
            Driver = driver,
            Vehicle = vehicle,
            TaxiCompany = company,
            PickupAddress = "Prishtina Center",
            PickupLatitude = 42.6629,
            PickupLongitude = 21.1624,
            DestinationAddress = "Albi Mall",
            DestinationLatitude = 42.6382,
            DestinationLongitude = 21.1610,
            DistanceKm = 3.57m,
            EstimatedDurationMinutes = 8,
            Price = 5.36m,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
            Status = "Assigned"
        };

        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var service = new DriverTripService(
            db,
            Substitute.For<INotificationService>(),
            Substitute.For<INotificationRealtimeSender>());

        // Cannot complete before starting
        var beforeStart = await service.CompleteAsync(
            trip.Id, driverUser.Id);

        Assert.Equal(
            DriverTripActionResult.InvalidStatus,
            beforeStart);

        // Another user cannot start this trip
        var wrongUser = await service.StartAsync(
            trip.Id, scheduler.Id);

        Assert.Equal(
            DriverTripActionResult.NotFound,
            wrongUser);

        // Driver starts the trip
        var started = await service.StartAsync(
            trip.Id, driverUser.Id);

        Assert.Equal(
            DriverTripActionResult.Success,
            started);

        // Starting twice must fail
        var duplicateStart = await service.StartAsync(
            trip.Id, driverUser.Id);

        Assert.Equal(
            DriverTripActionResult.InvalidStatus,
            duplicateStart);

        // Driver completes the trip
        var completed = await service.CompleteAsync(
            trip.Id, driverUser.Id);

        Assert.Equal(
            DriverTripActionResult.Success,
            completed);

        // Completing twice must fail
        var duplicateComplete = await service.CompleteAsync(
            trip.Id, driverUser.Id);

        Assert.Equal(
            DriverTripActionResult.InvalidStatus,
            duplicateComplete);

        // Verify persisted database state
        await using var verifyDb = new AppDbContext(options);

        var savedTrip = await verifyDb.Trips
            .AsNoTracking()
            .SingleAsync(t => t.Id == trip.Id);

        Assert.Equal("Completed", savedTrip.Status);
        Assert.NotNull(savedTrip.StartedAt);
        Assert.NotNull(savedTrip.CompletedAt);
    }
}
