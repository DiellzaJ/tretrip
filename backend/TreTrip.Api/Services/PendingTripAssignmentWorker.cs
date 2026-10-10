using TreTrip.Api.Interfaces;

namespace TreTrip.Api.Services;

public sealed class PendingTripAssignmentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingTripAssignmentWorker> _logger;

    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromSeconds(30);

    public PendingTripAssignmentWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingTripAssignmentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Pending Trip Assignment Worker started.");

        // Check once when the application starts.
        await ProcessPendingTripsAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessPendingTripsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }

    private async Task ProcessPendingTripsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();


            var assignmentService =
                scope.ServiceProvider.GetRequiredService<
                    IPendingTripAssignmentService>();

            // First expire outdated Pending trips.
            var expiredCount =
                await assignmentService.ExpirePendingTripsAsync(
                    cancellationToken);

            if (expiredCount > 0)
            {
                _logger.LogInformation(
                    "Automatically expired {Count} pending trips.",
                    expiredCount);
            }

            // Then assign remaining eligible Pending trips.
            var assignedCount =
                await assignmentService.AssignPendingTripsAsync(
                    cancellationToken);


            if (assignedCount > 0)
            {
                _logger.LogInformation(
                    "Automatically assigned {Count} pending trips.",
                    assignedCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Application is stopping.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while processing pending trip assignments.");
        }
    }
}