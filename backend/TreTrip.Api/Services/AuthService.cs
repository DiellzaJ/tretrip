
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.DTOs;
using TreTrip.Api.Interfaces;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        AppDbContext db,
        ITokenService tokenService,
        IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user == null || !user.IsActive)
            return null;

        var now = DateTime.UtcNow;

        if (user.LockoutEnd.HasValue && user.LockoutEnd > now)
            return null;

        if (user.LockoutEnd.HasValue && user.LockoutEnd <= now)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password
        );

        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginAttempts++;
            user.LastFailedLoginAt = now;

            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = now.AddMinutes(15);
            }

            await _db.SaveChangesAsync();
            return null;
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastFailedLoginAt = null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password
            );
        }

        await _db.SaveChangesAsync();

        var (token, expiresAt) = _tokenService.GenerateToken(user);

        return new LoginResponseDto
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.Name
        };
    }
}
