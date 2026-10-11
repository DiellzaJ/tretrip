using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreTrip.Api.Data;
using TreTrip.Api.Models;

namespace TreTrip.Api.Services;

public interface IAccountSecurityService
{
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    Task RequestPasswordResetAsync(string email);
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
}

public sealed class AccountSecurityService : IAccountSecurityService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IConfiguration _settings;
    private readonly ILogger<AccountSecurityService> _logger;
    private readonly IHostEnvironment _environment;

    public AccountSecurityService(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        IConfiguration settings,
        ILogger<AccountSecurityService> logger,
        IHostEnvironment environment)
    {
        _db = db;
        _hasher = hasher;
        _settings = settings;
        _logger = logger;
        _environment = environment;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        if (!ValidPassword(newPassword)) return false;
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null) return false;

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword)
            == PasswordVerificationResult.Failed)
            return false;

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, newPassword)
            != PasswordVerificationResult.Failed)
            return false;

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        user.PasswordVersion++;
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastFailedLoginAt = null;
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpiresAt = null;
        await _db.SaveChangesAsync();
        return true;
    }

    private bool AllowLocalTestMail() =>
        _environment.IsDevelopment() &&
        (_settings["Smtp:Host"] is "localhost" or "127.0.0.1") &&
        string.Equals(_settings["Smtp:AllowInsecureLocal"], "true", StringComparison.OrdinalIgnoreCase);

    public bool IsMailConfigured() =>
        !string.IsNullOrWhiteSpace(_settings["Smtp:Host"]) &&
        (AllowLocalTestMail() || (
            !string.IsNullOrWhiteSpace(_settings["Smtp:Username"]) &&
            !string.IsNullOrWhiteSpace(_settings["Smtp:Password"]))) &&
        !string.IsNullOrWhiteSpace(_settings["Smtp:FromEmail"]) &&
        !string.IsNullOrWhiteSpace(_settings["Security:FrontendUrl"]);

    public async Task RequestPasswordResetAsync(string email)
    {
        if (!IsMailConfigured())
            throw new InvalidOperationException("Password recovery email is not configured.");

        var normalized = email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == normalized && u.IsActive);
        if (user == null) return; // Do not reveal whether an account exists.

        // Throttle repeated requests per account as well as per IP (in the controller).
        if (user.PasswordResetExpiresAt.HasValue &&
            user.PasswordResetExpiresAt.Value > DateTime.UtcNow.AddMinutes(13))
            return;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetTokenHash = Sha256(token);
        user.PasswordResetExpiresAt = DateTime.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync();

        var baseUrl = _settings["Security:FrontendUrl"]!.TrimEnd('/');
        var link = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";
        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_settings["Smtp:FromEmail"]!, "TreTrip Security");
            message.To.Add(user.Email);
            message.Subject = "Reset your TreTrip password";
            message.Body = "A password reset was requested for your TreTrip account.\n\n" +
                $"Open this link within 15 minutes:\n{link}\n\n" +
                "If you did not request this change, ignore this email.";

            using var smtp = new SmtpClient(_settings["Smtp:Host"],
                int.TryParse(_settings["Smtp:Port"], out var port) ? port : 587)
            {
                EnableSsl = !AllowLocalTestMail(),
                UseDefaultCredentials = false,
                Credentials = AllowLocalTestMail() ? null :
                    new NetworkCredential(_settings["Smtp:Username"], _settings["Smtp:Password"])
            };
            await smtp.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Never log reset tokens or links; keep response identical for existing/non-existing accounts.
            _logger.LogError(ex, "Password reset email delivery failed.");
        }
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
    {
        if (!ValidPassword(newPassword) || string.IsNullOrWhiteSpace(token) || token.Length > 200)
            return false;

        var normalized = email.Trim().ToLowerInvariant();
        var tokenHash = Sha256(token);
        var now = DateTime.UtcNow;
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u =>
            u.Email == normalized && u.IsActive &&
            u.PasswordResetTokenHash == tokenHash &&
            u.PasswordResetExpiresAt.HasValue && u.PasswordResetExpiresAt.Value > now);
        if (user == null) return false;

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, newPassword)
            != PasswordVerificationResult.Failed)
            return false;

        var newHash = _hasher.HashPassword(user, newPassword);
        // Atomic consumption; the same reset token cannot be used twice.
        var updated = await _db.Users.Where(u =>
                u.Id == user.Id && u.IsActive &&
                u.PasswordResetTokenHash == tokenHash &&
                u.PasswordResetExpiresAt.HasValue && u.PasswordResetExpiresAt.Value > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.PasswordHash, newHash)
                .SetProperty(u => u.PasswordResetTokenHash, (string?)null)
                .SetProperty(u => u.PasswordResetExpiresAt, (DateTime?)null)
                .SetProperty(u => u.MustChangePassword, false)
                .SetProperty(u => u.PasswordVersion, u => u.PasswordVersion + 1)
                .SetProperty(u => u.FailedLoginAttempts, 0)
                .SetProperty(u => u.LockoutEnd, (DateTime?)null)
                .SetProperty(u => u.LastFailedLoginAt, (DateTime?)null));
        return updated == 1;
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool ValidPassword(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= 12 && password.Length <= 128;
}
