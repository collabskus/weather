using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Core.Telemetry;

namespace Weather.Infrastructure.Services;

/// <summary>
/// Consumes origin cells from the <see cref="INeighborhoodWarmer"/> queue and
/// warms the surrounding grid cells in the background, off the request path.
/// Three guards keep upstream load sane:
/// <list type="bullet">
///   <item>a global <see cref="RateLimiter"/> paces all outbound warming,</item>
///   <item>an in-flight set deduplicates concurrent work for the same cell,</item>
///   <item>cells that are already fresh are skipped before any network call.</item>
/// </list>
/// Each cell is fetched in its own DI scope so the scoped
/// <see cref="IWeatherService"/> never becomes a captive dependency.
/// </summary>
internal sealed class NeighborhoodWarmingBackgroundService(
    INeighborhoodWarmer warmer,
    IServiceScopeFactory scopeFactory,
    IForecastCache forecastCache,
    RateLimiter rateLimiter,
    WeatherTelemetry telemetry,
    TimeProvider timeProvider,
    ILogger<NeighborhoodWarmingBackgroundService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<GridPoint, byte> _inFlight = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Neighbourhood warming service started.");

        try
        {
            await foreach (var origin in warmer.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await WarmNeighboursAsync(origin, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to warm the neighbourhood around {Origin}.", origin);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        logger.LogInformation("Neighbourhood warming service stopping.");
    }

    private async Task WarmNeighboursAsync(GridPoint origin, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var cell in GridNeighborhood.Surrounding(origin))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Deduplicate: if this cell is already being warmed, skip it.
            if (!_inFlight.TryAdd(cell, 0))
            {
                continue;
            }

            try
            {
                // Skip cells that are already fresh — no network needed.
                var cached = await forecastCache.GetAsync(cell, cancellationToken).ConfigureAwait(false);
                if (cached is not null && cached.IsFresh(now))
                {
                    continue;
                }

                using var lease = await rateLimiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
                if (!lease.IsAcquired)
                {
                    logger.LogDebug("Rate limiter rejected warming for {Cell}; skipping.", cell);
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var weatherService = scope.ServiceProvider.GetRequiredService<IWeatherService>();
                var forecast = await weatherService
                    .GetForecastByGridAsync(cell, cancellationToken)
                    .ConfigureAwait(false);

                if (forecast is not null)
                {
                    telemetry.RecordNeighborWarmed(cell.GridId);
                }
            }
            finally
            {
                _inFlight.TryRemove(cell, out _);
            }
        }
    }
}
