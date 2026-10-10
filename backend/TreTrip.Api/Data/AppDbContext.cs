
using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Models;

namespace TreTrip.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TaxiCompany> TaxiCompanies => Set<TaxiCompany>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique fields
        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name).IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.PlateNumber).IsUnique();

        modelBuilder.Entity<Driver>()
            .HasIndex(d => d.LicenseNumber).IsUnique();

        // Role -> Users (1:N)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // User -> Driver (1:0..1)
        modelBuilder.Entity<Driver>()
            .HasOne(d => d.User)
            .WithOne(u => u.DriverProfile)
            .HasForeignKey<Driver>(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // TaxiCompany -> Drivers
        modelBuilder.Entity<Driver>()
            .HasOne(d => d.TaxiCompany)
            .WithMany(c => c.Drivers)
            .HasForeignKey(d => d.TaxiCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // TaxiCompany -> Vehicles
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.TaxiCompany)
            .WithMany(c => c.Vehicles)
            .HasForeignKey(v => v.TaxiCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // User -> Trips (Scheduler)
        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Scheduler)
            .WithMany(u => u.ScheduledTrips)
            .HasForeignKey(t => t.SchedulerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Driver -> Trips
        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Driver)
            .WithMany(d => d.Trips)
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Vehicle -> Trips
        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Vehicle)
            .WithMany(v => v.Trips)
            .HasForeignKey(t => t.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // TaxiCompany -> Trips
        modelBuilder.Entity<Trip>()
            .HasOne(t => t.TaxiCompany)
            .WithMany(c => c.Trips)
            .HasForeignKey(t => t.TaxiCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // User -> Notifications
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Trip -> Notifications
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Trip)
            .WithMany(t => t.Notifications)
            .HasForeignKey(n => n.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        // Decimal precision
        modelBuilder.Entity<Trip>()
            .Property(t => t.Price)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Trip>()
            .Property(t => t.DistanceKm)
            .HasPrecision(10, 2);

        // Seed default roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Scheduler" },
            new Role { Id = 3, Name = "Driver" }
        );
    }
}
