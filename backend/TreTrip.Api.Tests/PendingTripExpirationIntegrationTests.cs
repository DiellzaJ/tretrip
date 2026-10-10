using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using TreTrip.Api.Data;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;
using TreTrip.Api.Services;
using Xunit;

namespace TreTrip.Api.Tests;

public class PendingTripExpirationIntegrationTests
{
    [Fact]
    public async Task ExpiredPendingTrip_ShouldExpireAndNotifyOnce()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("tretrip_expiration_tests")
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
            Email = "scheduler@expiration.test",
            PasswordHash = "test-only",
            RoleId = 2
        };

        // One expired trip, one future trip
        var expiredTrip = CreatePendingTrip(
            scheduler, DateTime.UtcNow.AddMinutes(-10));

        var futureTrip = CreatePendingTrip(
            scheduler, DateTime.UtcNow.AddDays(2));

        db.Users.Add(scheduler);
        db.Trips.AddRange(expiredTrip, futureTrip);
        await db.SaveChangesAsync();

        var realtimeSender =
            Substitute.For<INotificationRealtimeSender>();

        realtimeSender.NotifyUserAsync(Arg.Any<int>())
            .Returns(Task.CompletedTask);

        var service = new PendingTripAssignmentService(
            db,
            new NotificationService(db),
            realtimeSender);

        // First run: one trip should expire
        var firstCount =
            await service.ExpirePendingTripsAsync();

        Assert.Equal(1, firstCount);

        // Second run: no duplicate expiration
        var secondCount =
            await service.ExpirePendingTripsAsync();

        Assert.Equal(0, secondCount);

        // Verify persisted database state
        await using var verifyDb = new AppDbContext(options);

        var savedExpired = await verifyDb.Trips
            .AsNoTracking()
            .SingleAsync(t => t.Id == expiredTrip.Id);

        var savedFuture = await verifyDb.Trips
            .AsNoTracking()
            .SingleAsync(t => t.Id == futureTrip.Id);

        Assert.Equal("Expired", savedExpired.Status);
        Assert.Null(savedExpired.DriverId);

        Assert.Equal("Pending", savedFuture.Status);

        // Exactly one notification for expired trip
        var notifications = await verifyDb.Notifications
            .AsNoTracking()
            .ToListAsync();

        var notification = Assert.Single(notifications);

        Assert.Equal(expiredTrip.Id, notification.TripId);
        Assert.Equal(scheduler.Id, notification.UserId);
        Assert.Contains("has expired", notification.Message);
        Assert.False(notification.IsRead);

        // SignalR notification only once
        await realtimeSender.Received(1)
            .NotifyUserAsync(scheduler.Id);
    }

    private static Trip CreatePendingTrip(
        User scheduler,
        DateTime departure)
    {
        return new Trip
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
    }
}