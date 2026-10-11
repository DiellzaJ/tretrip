using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AdminService(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    // Create a new employee with the Scheduler role
    public async Task<UserResponseDto> CreateEmployeeAsync(
        CreateEmployeeDto request)
    {
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 8)
        {
            throw new ArgumentException(
                "Invalid employee information.");
        }

        if (!email.EndsWith(
            "@tretek.io",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only Tretek employees with a @tretek.io email can be registered as Schedulers."
            );
        }

        // Check if the email is already registered
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email.ToLower() == email);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        // Find the Scheduler role
        var role = await _db.Roles
            .SingleAsync(r => r.Name == "Scheduler");

        // Create the user
        var user = new User
        {
            FullName = fullName,
            Email = email,
            RoleId = role.Id,
            IsActive = true,
            MustChangePassword = true
        };

        // Hash the password before saving
        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new UserResponseDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = role.Name,
            IsActive = user.IsActive,
            DeactivatedAt = user.DeactivatedAt
        };
    }

    // Get all registered users
    public async Task<List<UserResponseDto>> GetUsersAsync()
    {
        return await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role.Name,
                IsActive = u.IsActive,
                DeactivatedAt = u.DeactivatedAt
            })
            .ToListAsync();
    }

    // Activate or deactivate a user
    public async Task<bool> SetUserActiveStatusAsync(
        int userId,
        bool isActive)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || user.Role.Name == "Admin")
        {
            return false;
        }

        user.IsActive = isActive;
        user.DeactivatedAt = isActive
            ? null
            : DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return true;
    }
}
