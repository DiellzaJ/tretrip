
using TreTrip.Api.DTOs;

namespace TreTrip.Api.Interfaces;

public interface IAdminService
{
    Task<UserResponseDto> CreateEmployeeAsync(
        CreateEmployeeDto request);

    Task<List<UserResponseDto>> GetUsersAsync();

    Task<bool> SetUserActiveStatusAsync(
        int userId,
        bool isActive);
}
