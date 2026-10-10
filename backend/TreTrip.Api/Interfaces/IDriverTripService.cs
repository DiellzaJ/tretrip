using TreTrip.Api.DTOs;

namespace TreTrip.Api.Interfaces;

public enum DriverTripActionResult
{
    Success,
    NotFound,
    InvalidStatus,
    TooEarly
}

public interface IDriverTripService
{
    // Get trips assigned to the logged-in driver
    Task<List<TripResponseDto>> GetMyTripsAsync(
        int driverUserId);

    // Start an assigned trip
    Task<DriverTripActionResult> StartAsync(
        int tripId,
        int driverUserId);

    // Complete an ongoing trip
    Task<DriverTripActionResult> CompleteAsync(
        int tripId,
        int driverUserId);
}