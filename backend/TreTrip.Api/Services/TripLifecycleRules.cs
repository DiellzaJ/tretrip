namespace TreTrip.Api.Services;

internal static class TripLifecycleRules
{
    public static bool CanArrive(
        string status,
        DateTime? startedAt)
    {
        return status == "Assigned" &&
               !startedAt.HasValue;
    }

    public static bool CanStart(
        string status,
        DateTime? startedAt)
    {
        return (status == "Assigned" ||
                status == "Arrived") &&
               !startedAt.HasValue;
    }

    public static bool CanComplete(
        string status,
        DateTime? startedAt,
        DateTime? completedAt)
    {
        return status == "InProgress" &&
               startedAt.HasValue &&
               !completedAt.HasValue;
    }

    public static bool CanCancel(
        string status,
        DateTime? startedAt)
    {
        return (status == "Pending" ||
                status == "Assigned") &&
               !startedAt.HasValue;
    }
}