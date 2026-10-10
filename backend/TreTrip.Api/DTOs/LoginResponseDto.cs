
namespace TreTrip.Api.DTOs;

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
