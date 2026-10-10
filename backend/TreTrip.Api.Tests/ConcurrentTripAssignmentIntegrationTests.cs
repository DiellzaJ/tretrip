using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;
using TreTrip.Api.Services;
using Xunit;

namespace TreTrip.Api.Tests;

public class ConcurrentTripAssignmentIntegrationTests
{
    [Fact]
    public async Task ConcurrentBookings_ShouldNotDoubleBookDriver()
    {

        // Isolated PostgreSQL database
        await using var postgres =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("tretrip_concurrency_tests")
                .WithUsername("test_user")
                .WithPassword("test_password_only")
                .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var setupDb = new AppDbContext(options);
        await setupDb.Database.MigrateAsync();

        var now = DateTime.UtcNow;
        var departure = now.AddDays(2);

        // Two different schedulers
        var schedulerA = new User
        {
            FullName = "Scheduler A",
            Email = "schedulerA@concurrency.test",
            PasswordHash = "test-only",
            RoleId = 2
        };

        var schedulerB = new User
        {
            FullName = "Scheduler B",
            Email = "schedulerB@concurrency.test",
            PasswordHash = "test-only",
            RoleId = 2
        };

        var driverUser = new User
        {
            FullName = "Test Driver",
            Email = "driver@concurrency.test",
            PasswordHash = "test-only",
            RoleId = 3
        };

        var company = new TaxiCompany
        {
            Name = "Concurrency Test Taxi",
            Phone = "123456",
            ContractStartDate = now.AddDays(-1),
            ContractEndDate = now.AddDays(30)
        };

        // Only one available driver
        var driver = new Driver
        {
            User = driverUser,
            TaxiCompany = company,
            LicenseNumber = "CONCURRENT-LICENSE-001",
            IsAvailable = true
        };

        // Only one available vehicle
        var vehicle = new Vehicle
        {
            TaxiCompany = company,
            PlateNumber = "CONCURRENT-001",
            Model = "Test Vehicle",
            IsAvailable = true
        };

        setupDb.Users.AddRange(
            schedulerA, schedulerB, driverUser);

        setupDb.TaxiCompanies.Add(company);
        setupDb.Drivers.Add(driver);
        setupDb.Vehicles.Add(vehicle);

        await setupDb.SaveChangesAsync();

        // Hold the assignment lock before launching bookings.
        // Both booking requests must wait until it is released.
        await using var blockerDb = new AppDbContext(options);

        await using var blockerTransaction =
            await blockerDb.Database.BeginTransactionAsync();

        await blockerDb.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(774411);");

        // Coordinate simultaneous requests
        var readyCount = 0;

        var bothReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var startGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<TripResponseDto> BookAsync(int schedulerId)
        {
            // Every request gets its own DbContext
            await using var db = new AppDbContext(options);

            var realtimeSender =
                Substitute.For<INotificationRealtimeSender>();

            realtimeSender
                .NotifyUserAsync(Arg.Any<int>())
                .Returns(Task.CompletedTask);

            var service = new TripService(
                db,
                new NotificationService(db),
                realtimeSender);

            if (Interlocked.Increment(ref readyCount) == 2)
                bothReady.TrySetResult(true);

            // Wait until both requests are ready
            await startGate.Task;

            return await service.CreateAsync(
                schedulerId,
                new CreateTripDto
                {
                    PickupAddress = "Prishtina Center",
                    PickupLatitude = 42.6629,
                    PickupLongitude = 21.1624,

                    DestinationAddress = "Albi Mall",
                    DestinationLatitude = 42.6382,
                    DestinationLongitude = 21.1610,

                    ScheduledAt = departure
                });
        }

        // Launch two independent booking requests
        var firstTask = Task.Run(
            () => BookAsync(schedulerA.Id));

        var secondTask = Task.Run(
            () => BookAsync(schedulerB.Id));

        try
        {
            await bothReady.Task.WaitAsync(
                TimeSpan.FromSeconds(10));
        }
        finally
        {
            startGate.TrySetResult(true);
        }

        // Wait until both bookings are blocked by PostgreSQL.
        // The database is isolated inside its own container.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        long waitingCount = 0;

        try
        {
            do
            {
                waitingCount = await setupDb.Database
                    .SqlQueryRaw<long>(
                        """
                        SELECT COUNT(*)::bigint AS "Value"
                        FROM pg_locks
                        WHERE locktype = 'advisory'
                          AND NOT granted
                        """)
                    .SingleAsync();

                if (waitingCount >= 2)
                    break;

                await Task.Delay(50);
            }
            while (DateTime.UtcNow < deadline);
        }
        finally
        {
            // Release the lock so bookings can proceed.
            await blockerTransaction.CommitAsync();
        }

        // Wait for both bookings to finish
        var results = await Task.WhenAll(
            firstTask, secondTask);

        // Verify both requests waited for the lock
        Assert.True(
            waitingCount >= 2,
            $"Expected 2 waiting bookings, found {waitingCount}.");

        Assert.Equal(2, results.Length);


        Assert.Equal(2, results.Length);

        // Verify actual database state
        await using var verifyDb =
            new AppDbContext(options);

        var trips = await verifyDb.Trips
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync();

        Assert.Equal(2, trips.Count);

        // Exactly one trip should be assigned
        var assigned = Assert.Single(
            trips, t => t.Status == "Assigned");

        // The other must remain pending
        var pending = Assert.Single(
            trips, t => t.Status == "Pending");

        Assert.NotEqual(
            assigned.SchedulerId,
            pending.SchedulerId);

        Assert.Equal(driver.Id, assigned.DriverId);
        Assert.Equal(vehicle.Id, assigned.VehicleId);

        Assert.Null(pending.DriverId);
        Assert.Null(pending.VehicleId);

        // Only the assigned driver receives a notification
        var notifications = await verifyDb.Notifications
            .AsNoTracking()
            .ToListAsync();

        var notification = Assert.Single(notifications);

        Assert.Equal(assigned.Id, notification.TripId);
        Assert.Equal(driverUser.Id, notification.UserId);
    }
}