
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public class DriverService : IDriverService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DriverService(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    // Create a new driver account and profile
    public async Task<DriverResponseDto> CreateAsync(
        CreateDriverDto request)
    {
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var licenseNumber = request.LicenseNumber
            .Trim().ToUpperInvariant();

        // Validate input
        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(licenseNumber) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 8 ||
            request.TaxiCompanyId <= 0)
        {
            throw new ArgumentException(
                "Invalid driver information.");
        }

        // Check duplicate email
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email.ToLower() == email);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        // Check duplicate license
        var licenseExists = await _db.Drivers
            .AnyAsync(d =>
                d.LicenseNumber.ToUpper() == licenseNumber);

        if (licenseExists)
        {
            throw new InvalidOperationException(
                "A driver with this license already exists.");
        }

        // Find taxi company
        var company = await _db.TaxiCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == request.TaxiCompanyId);

        if (company == null)
        {
            throw new KeyNotFoundException(
                "Taxi company not found.");
        }

        var now = DateTime.UtcNow;

        // Validate company and contract
        if (!company.IsActive ||
            company.ContractStartDate > now ||
            (company.ContractEndDate.HasValue &&
             company.ContractEndDate.Value < now))
        {
            throw new InvalidOperationException(
                "Taxi company is inactive or contract is not valid.");
        }

        // Find Driver role
        var role = await _db.Roles
            .SingleAsync(r => r.Name == "Driver");

        // Create user account
        var user = new User
        {
            FullName = fullName,
            Email = email,
            RoleId = role.Id,
            IsActive = true,
            MustChangePassword = true
        };

        // Secure password hashing
        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        // Create driver profile
        var driver = new Driver
        {
            User = user,
            TaxiCompanyId = company.Id,
            LicenseNumber = licenseNumber,
            IsActive = true,
            IsAvailable = false
        };

        // Save User and Driver together
        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync();

        return new DriverResponseDto
        {
            Id = driver.Id,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            LicenseNumber = driver.LicenseNumber,
            TaxiCompanyId = company.Id,
            TaxiCompanyName = company.Name,
            IsActive = driver.IsActive,
            IsAvailable = driver.IsAvailable
        };
    }

    // Get all drivers
    public async Task<List<DriverResponseDto>> GetAllAsync()
    {
        return await _db.Drivers
            .AsNoTracking()
            .OrderBy(d => d.Id)
            .Select(d => new DriverResponseDto
            {
                Id = d.Id,
                UserId = d.UserId,
                FullName = d.User.FullName,
                Email = d.User.Email,
                LicenseNumber = d.LicenseNumber,
                TaxiCompanyId = d.TaxiCompanyId,
                TaxiCompanyName = d.TaxiCompany.Name,
                IsActive = d.IsActive,
                IsAvailable = d.IsAvailable
            })
            .ToListAsync();
    }

    // Update driver availability
    public async Task<bool> SetAvailabilityAsync(
        int driverId,
        bool isAvailable)
    {
        var driver = await _db.Drivers
            .Include(d => d.User)
            .Include(d => d.TaxiCompany)
            .FirstOrDefaultAsync(d => d.Id == driverId);

        if (driver == null)
        {
            return false;
        }

        // Inactive drivers cannot become available
        if (isAvailable)
        {
            var now = DateTime.UtcNow;
            var company = driver.TaxiCompany;

            if (!driver.IsActive ||
                !driver.User.IsActive ||
                !company.IsActive ||
                company.ContractStartDate > now ||
                (company.ContractEndDate.HasValue &&
                 company.ContractEndDate.Value < now))
            {
                return false;
            }
        }

        driver.IsAvailable = isAvailable;

        await _db.SaveChangesAsync();

        return true;
    }
}
