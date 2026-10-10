
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Models;

namespace TreTrip.Api.Data;

public static class AdminSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration config)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<User>>();

        if (await db.Users.AnyAsync(u => u.Role.Name == "Admin"))
            return;

        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
            return;

        email = email.Trim().ToLowerInvariant();

        if (!email.EndsWith("@tretek.io",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Admin email must belong to tretek.io.");
        }

        if (password.Length < 12)
        {
            throw new InvalidOperationException(
                "Admin password must be at least 12 characters.");
        }

        var role = await db.Roles
            .SingleAsync(r => r.Name == "Admin");

        var admin = new User
        {
            FullName = "Tretek Manager",
            Email = email,
            RoleId = role.Id,
            IsActive = true
        };

        admin.PasswordHash = hasher.HashPassword(admin, password);

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
