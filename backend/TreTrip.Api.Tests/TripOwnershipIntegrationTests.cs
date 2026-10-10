using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Models;
using Xunit;

namespace TreTrip.Api.Tests;

public class TripOwnershipIntegrationTests
{
    [Fact]
    public async Task Users_CannotAccessOtherUsersTrips()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("tretrip_ownership_tests")
                .WithUsername("test_user")
                .WithPassword("test_password_only")
                .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var schedulerA = new User
        {
            FullName = "Scheduler A",
            Email = "schedulerA@ownership.test",
            PasswordHash = "test-only",
            RoleId = 2
        };

        var schedulerB = new User
        {
            FullName = "Scheduler B",
            Email = "schedulerB@ownership.test",
            PasswordHash = "test-only",
            RoleId = 2
        };

        var driverAUser = new User
        {
            FullName = "Driver A",
            Email = "driverA@ownership.test",
            PasswordHash = "test-only",
            RoleId = 3
        };

        var driverBUser = new User
        {
            FullName = "Driver B",
            Email = "driverB@ownership.test",
            PasswordHash = "test-only",
            RoleId = 3
        };

        var now = DateTime.UtcNow;

        var company = new TaxiCompany
        {
            Name = "Ownership Test Taxi",
            Phone = "123456",
            ContractStartDate = now.AddDays(-1),
            ContractEndDate = now.AddDays(30)
        };

        var driverA = new Driver
        {
            User = driverAUser,
            TaxiCompany = company,
            LicenseNumber = "OWN-LICENSE-001"
        };

        var driverB = new Driver
        {
            User = driverBUser,
            TaxiCompany = company,
            LicenseNumber = "OWN-LICENSE-002"
        };

        var vehicleA = new Vehicle
        {
            TaxiCompany = company,
            PlateNumber = "OWN-001",
            Model = "Vehicle A"
        };

        var vehicleB = new Vehicle
        {
            TaxiCompany = company,
            PlateNumber = "OWN-002",
            Model = "Vehicle B"
        };

        Trip CreateTrip(User scheduler, Driver driver,
            Vehicle vehicle) => new()
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
            EstimatedDurationMinutes = 20,
            Price = 5.36m,
            ScheduledAt = now.AddDays(2),
            Status = "Assigned"
        };

        var tripA = CreateTrip(
            schedulerA, driverA, vehicleA);

        var tripB = CreateTrip(
            schedulerB, driverB, vehicleB);

        db.Trips.AddRange(tripA, tripB);
        await db.SaveChangesAsync();

        using var factory =
            new OwnershipApiFactory(
                postgres.GetConnectionString());

        using var client = factory.CreateClient();

        var schedulerAToken =
            factory.CreateToken(schedulerA.Id, "Scheduler");

        var schedulerBToken =
            factory.CreateToken(schedulerB.Id, "Scheduler");

        var driverAToken =
            factory.CreateToken(driverAUser.Id, "Driver");

        var driverBToken =
            factory.CreateToken(driverBUser.Id, "Driver");

        async Task<HttpResponseMessage> SendAsAsync(
            HttpMethod method,
            string path,
            string token)
        {
            using var request =
                new HttpRequestMessage(method, path);

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", token);

            return await client.SendAsync(request);
        }

        // Scheduler A can see only Trip A
        using var schedulerAResponse = await SendAsAsync(
            HttpMethod.Get, "/api/trips", schedulerAToken);

        Assert.Equal(HttpStatusCode.OK,
            schedulerAResponse.StatusCode);

        var schedulerATrips = await schedulerAResponse.Content
            .ReadFromJsonAsync<List<TripResponseDto>>();

        Assert.NotNull(schedulerATrips);
        Assert.Equal(tripA.Id,
            Assert.Single(schedulerATrips).Id);

        // Scheduler B can see only Trip B
        using var schedulerBResponse = await SendAsAsync(
            HttpMethod.Get, "/api/trips", schedulerBToken);

        Assert.Equal(HttpStatusCode.OK,
            schedulerBResponse.StatusCode);

        var schedulerBTrips = await schedulerBResponse.Content
            .ReadFromJsonAsync<List<TripResponseDto>>();

        Assert.NotNull(schedulerBTrips);
        Assert.Equal(tripB.Id,
            Assert.Single(schedulerBTrips).Id);

        // Scheduler B cannot read Trip A
        using var foreignGet = await SendAsAsync(
            HttpMethod.Get,
            $"/api/trips/{tripA.Id}",
            schedulerBToken);

        Assert.Equal(HttpStatusCode.NotFound,
            foreignGet.StatusCode);

        // Scheduler B cannot cancel Trip A
        using var foreignCancel = await SendAsAsync(
            HttpMethod.Patch,
            $"/api/trips/{tripA.Id}/cancel",
            schedulerBToken);

        Assert.Equal(HttpStatusCode.NotFound,
            foreignCancel.StatusCode);

        // Driver A sees only Trip A
        using var driverAResponse = await SendAsAsync(
            HttpMethod.Get,
            "/api/driver/trips",
            driverAToken);

        Assert.Equal(HttpStatusCode.OK,
            driverAResponse.StatusCode);

        var driverATrips = await driverAResponse.Content
            .ReadFromJsonAsync<List<TripResponseDto>>();

        Assert.NotNull(driverATrips);
        Assert.Equal(tripA.Id,
            Assert.Single(driverATrips).Id);

        // Driver B sees only Trip B
        using var driverBResponse = await SendAsAsync(
            HttpMethod.Get,
            "/api/driver/trips",
            driverBToken);

        Assert.Equal(HttpStatusCode.OK,
            driverBResponse.StatusCode);

        var driverBTrips = await driverBResponse.Content
            .ReadFromJsonAsync<List<TripResponseDto>>();

        Assert.NotNull(driverBTrips);
        Assert.Equal(tripB.Id,
            Assert.Single(driverBTrips).Id);

        // Driver B cannot modify Driver A's trip
        foreach (var action in new[]
        {
            "arrived", "start", "complete"
        })
        {
            using var response = await SendAsAsync(
                HttpMethod.Patch,
                $"/api/driver/trips/{tripA.Id}/{action}",
                driverBToken);

            Assert.Equal(HttpStatusCode.NotFound,
                response.StatusCode);
        }

        // Verify no unauthorized changes were persisted
        await using var verifyDb =
            new AppDbContext(options);

        var savedTrip = await verifyDb.Trips
            .AsNoTracking()
            .SingleAsync(t => t.Id == tripA.Id);

        Assert.Equal("Assigned", savedTrip.Status);
        Assert.Equal(driverA.Id, savedTrip.DriverId);
        Assert.Equal(schedulerA.Id, savedTrip.SchedulerId);
        Assert.Null(savedTrip.StartedAt);
        Assert.Null(savedTrip.CompletedAt);

        Assert.False(await verifyDb.Notifications
            .AnyAsync(n => n.TripId == tripA.Id));
    }
}