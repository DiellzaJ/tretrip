using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TreTrip.Api.Models;
using TreTrip.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace TreTrip.Api.Tests;

public sealed class AuthorizationApiFactory
    : WebApplicationFactory<Program>
{
    private const string TestJwtKey =
        "TreTripIntegrationTestsOnlySecretKey1234567890";

    private static readonly Dictionary<string, string?> TestSettings =
        new()
        {
            ["Jwt:Key"] = TestJwtKey,
            ["Jwt:Issuer"] = "tretrip-integration-tests",
            ["Jwt:Audience"] = "tretrip-integration-tests",
            ["ConnectionStrings:DefaultConnection"] =
                "Host=127.0.0.1;Port=1;Database=unused_test_db;" +
                "Username=unused;Password=unused"
        };

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // Prevent development Admin seeding
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(TestSettings);
        });

        builder.ConfigureTestServices(services =>
        {
            // Disable the background database worker
            // for these authorization-only tests.
            var worker = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType ==
                    typeof(PendingTripAssignmentWorker));

            if (worker != null)
                services.Remove(worker);

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Events.OnTokenValidated =
                        _ => Task.CompletedTask;
                });

        });
    }

    public string CreateToken(string role)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(TestSettings)
            .Build();

        var user = new User
        {
            Id = 42,
            FullName = "Authorization Test User",
            Email = "authorization@test.local",
            Role = new Role { Name = role }
        };

        var tokenService = new TokenService(configuration);

        return tokenService.GenerateToken(user).Token;
    }
}