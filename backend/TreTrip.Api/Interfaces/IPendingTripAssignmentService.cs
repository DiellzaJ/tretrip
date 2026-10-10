namespace TreTrip.Api.Interfaces;

public interface IPendingTripAssignmentService
{
    Task<int> AssignPendingTripsAsync(
        CancellationToken cancellationToken = default);
}