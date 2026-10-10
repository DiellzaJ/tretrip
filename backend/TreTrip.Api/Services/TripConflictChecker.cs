namespace TreTrip.Api.Services;

internal static class TripConflictChecker
{
    public const int BufferMinutes = 15;

    public static bool Overlaps(
        DateTime existingScheduledAt,
        int existingDurationMinutes,
        string existingStatus,
        DateTime requestedStart,
        DateTime requestedEnd,
        DateTime now)
    {
        var occupiedFrom = existingScheduledAt;

        var occupiedUntil = existingScheduledAt.AddMinutes(
            existingDurationMinutes + BufferMinutes);

        if (existingStatus == "Arrived" ||
            existingStatus == "InProgress")
        {
            if (now < occupiedFrom)
                occupiedFrom = now;

            var estimatedRemainingEnd = now.AddMinutes(
                existingDurationMinutes + BufferMinutes);

            if (estimatedRemainingEnd > occupiedUntil)
                occupiedUntil = estimatedRemainingEnd;
        }

        return occupiedFrom < requestedEnd &&
               occupiedUntil > requestedStart;
    }
}