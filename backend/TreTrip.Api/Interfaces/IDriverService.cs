
using TreTrip.Api.DTOs;

namespace TreTrip.Api.Interfaces;

public interface IDriverService
{
    Task<DriverResponseDto> CreateAsync(
        CreateDriverDto request);

    Task<List<DriverResponseDto>> GetAllAsync();

    Task<bool> SetAvailabilityAsync(
        int driverId,
        bool isAvailable);
}
