using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TreTrip.Api.Data;
using Xunit;

namespace TreTrip.Api.Tests;

public class PostgreSqlIntegrationTests
{
    [Fact]
    public async Task Database_ShouldApplyMigrationsAndSeedRoles()
    {
        // Start an isolated PostgreSQL database
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("tretrip_tests")
            .WithUsername("test_user")
            .WithPassword("test_password_only")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var db = new AppDbContext(options);

        // Apply the real database migrations
        await db.Database.MigrateAsync();

        // Verify the connection and seeded roles
        Assert.True(await db.Database.CanConnectAsync());

        var roles = await db.Roles
            .Select(r => r.Name)
            .ToListAsync();

        Assert.Contains("Admin", roles);
        Assert.Contains("Scheduler", roles);
        Assert.Contains("Driver", roles);
        Assert.Equal(3, roles.Count);
    }
}