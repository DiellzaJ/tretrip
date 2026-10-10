using TreTrip.Api.DTOs;

namespace TreTrip.Api.Interfaces;

public enum CancelTripResult
{
    Cancelled,
    NotFound,
    InvalidStatus
}

public interface ITripService
{
    Task<TripResponseDto> CreateAsync(
        int schedulerId,
        CreateTripDto request);

    Task<List<TripResponseDto>> GetForSchedulerAsync(
        int schedulerId);

    Task<TripResponseDto?> GetByIdAsync(
        int tripId,
        int schedulerId);

    Task<CancelTripResult> CancelAsync(
        int tripId,
        int schedulerId);
}