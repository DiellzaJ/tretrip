
using TreTrip.Api.DTOs;

namespace TreTrip.Api.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
}
