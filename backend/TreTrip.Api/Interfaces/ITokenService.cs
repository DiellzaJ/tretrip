
using TreTrip.Api.Models;

namespace TreTrip.Api.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
}
