using TreTrip.Api.DTOs;

namespace TreTrip.Api.Interfaces;

public interface IVehicleService
{
    Task<VehicleResponseDto> CreateAsync(
        CreateVehicleDto request);

    Task<List<VehicleResponseDto>> GetAllAsync();

    Task<bool> SetAvailabilityAsync(
        int vehicleId,
        bool isAvailable);
}