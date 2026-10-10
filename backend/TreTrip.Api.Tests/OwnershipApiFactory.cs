using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TreTrip.Api.Models;
using TreTrip.Api.Services;

namespace TreTrip.Api.Tests;

public sealed class OwnershipApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    private const string JwtKey =
        "TreTripOwnershipTestsOnlySecretKey1234567890";

    public OwnershipApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    private Dictionary<string, string?> Settings => new()
    {
        ["Jwt:Key"] = JwtKey,
        ["Jwt:Issuer"] = "tretrip-ownership-tests",
        ["Jwt:Audience"] = "tretrip-ownership-tests",
        ["ConnectionStrings:DefaultConnection"] =
            _connectionString
    };

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(Settings);
        });

        builder.ConfigureTestServices(services =>
        {
            // Disable automatic assignment during this test
            var worker = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType ==
                    typeof(PendingTripAssignmentWorker));

            if (worker != null)
                services.Remove(worker);
        });
    }

    public string CreateToken(int userId, string role)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(Settings)
            .Build();

        var user = new User
        {
            Id = userId,
            FullName = "Test User",
            Email = $"user{userId}@ownership.test",
            Role = new Role { Name = role }
        };

        return new TokenService(configuration)
            .GenerateToken(user).Token;
    }
}