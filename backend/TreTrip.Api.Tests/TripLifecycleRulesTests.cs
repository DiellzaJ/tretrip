using TreTrip.Api.Services;
using Xunit;

namespace TreTrip.Api.Tests;

public class TripLifecycleRulesTests
{
    private static readonly DateTime StartedAt =
        new(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Assigned", true)]
    [InlineData("Pending", false)]
    [InlineData("Arrived", false)]
    [InlineData("InProgress", false)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    public void CanArrive_ShouldValidateStatus(
        string status, bool expected)
    {
        var result = TripLifecycleRules.CanArrive(
            status, null);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanArrive_ShouldRejectStartedTrip()
    {
        Assert.False(TripLifecycleRules.CanArrive(
            "Assigned", StartedAt));
    }

    [Theory]
    [InlineData("Assigned", true)]
    [InlineData("Arrived", true)]
    [InlineData("Pending", false)]
    [InlineData("InProgress", false)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    public void CanStart_ShouldValidateStatus(
        string status, bool expected)
    {
        var result = TripLifecycleRules.CanStart(
            status, null);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanStart_ShouldRejectDuplicateStart()
    {
        Assert.False(TripLifecycleRules.CanStart(
            "InProgress", StartedAt));
    }

    [Fact]
    public void CanComplete_ShouldAllowInProgress()
    {
        Assert.True(TripLifecycleRules.CanComplete(
            "InProgress", StartedAt, null));
    }

    [Fact]
    public void CanComplete_ShouldRejectNotStarted()
    {
        Assert.False(TripLifecycleRules.CanComplete(
            "Assigned", null, null));
    }

    [Fact]
    public void CanComplete_ShouldRejectDuplicateCompletion()
    {
        Assert.False(TripLifecycleRules.CanComplete(
            "Completed", StartedAt, DateTime.UtcNow));
    }

    [Fact]
    public void CanComplete_ShouldRequireStartedAt()
    {
        Assert.False(TripLifecycleRules.CanComplete(
            "InProgress", null, null));
    }

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("Assigned", true)]
    [InlineData("Arrived", false)]
    [InlineData("InProgress", false)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    [InlineData("Expired", false)]
    public void CanCancel_ShouldValidateStatus(
        string status, bool expected)
    {
        var result = TripLifecycleRules.CanCancel(
            status, null);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanCancel_ShouldRejectStartedTrip()
    {
        Assert.False(TripLifecycleRules.CanCancel(
            "Assigned", StartedAt));
    }
}