using TreTrip.Api.Services;
using Xunit;

namespace TreTrip.Api.Tests;

public class TripConflictCheckerTests
{
    private static readonly DateTime Start =
        new(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OverlappingTrips_ShouldReturnTrue()
    {
        var result = TripConflictChecker.Overlaps(
            Start,
            30,
            "Assigned",
            Start.AddMinutes(20),
            Start.AddMinutes(40),
            Start);

        Assert.True(result);
    }

    [Fact]
    public void TripInsideBuffer_ShouldReturnTrue()
    {
        var result = TripConflictChecker.Overlaps(
            Start,
            30,
            "Assigned",
            Start.AddMinutes(35),
            Start.AddMinutes(50),
            Start);

        Assert.True(result);
    }

    [Fact]
    public void TripAfterBuffer_ShouldReturnFalse()
    {
        var result = TripConflictChecker.Overlaps(
            Start,
            30,
            "Assigned",
            Start.AddMinutes(45),
            Start.AddMinutes(60),
            Start);

        Assert.False(result);
    }

    [Fact]
    public void TripEndingAtExistingStart_ShouldReturnFalse()
    {
        var result = TripConflictChecker.Overlaps(
            Start,
            30,
            "Assigned",
            Start.AddMinutes(-30),
            Start,
            Start);

        Assert.False(result);
    }

    [Theory]
    [InlineData("Arrived")]
    [InlineData("InProgress")]
    public void ActiveTrip_ShouldExtendOccupiedTime(
        string status)
    {
        var result = TripConflictChecker.Overlaps(
            Start,
            30,
            status,
            Start.AddMinutes(60),
            Start.AddMinutes(70),
            Start.AddMinutes(40));

        Assert.True(result);
    }
}