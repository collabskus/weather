using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Nws;

namespace Weather.Infrastructure.Services;

/// <summary>
/// The cache-aside orchestrator. It is the only type that knows the freshness
/// policy: long-TTL point metadata, short-TTL forecasts honouring NWS
/// Cache-Control, conditional GETs to refresh expiry cheaply, and serving a
/// stale-but-valid copy when NWS is unreachable. Cache hit/miss telemetry is
/// recorded here because only this layer knows the <em>semantic</em> outcome.
/// </summary>
internal sealed class WeatherService(
    IPointMetadataCache metadataCache,
    IForecastCache forecastCache,
    INwsApiClient nwsClient,
    INeighborhoodWarmer warmer,
    WeatherTelemetry telemetry,
    TimeProvider timeProvider,
    IOptions<NwsClientOptions> options,
    ILogger<WeatherService> logger) : IWeatherService
{
    private const string MetadataCacheName = "metadata";
    private const string ForecastCacheName = "forecast";

    private readonly NwsClientOptions _options = options.Value;

    public async Task<Forecast?> GetForecastAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var coord = coordinate.Rounded();
        var metadata = await ResolveMetadataAsync(coord, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var forecast = await GetForecastByGridCoreAsync(metadata.Grid, cancellationToken).ConfigureAwait(false);
        if (forecast is not null)
        {
            // Fire-and-forget: schedule the surrounding cells to be warmed.
            warmer.RequestWarming(metadata.Grid);
        }

        return forecast;
    }

    public async Task<NeighborhoodForecast?> GetNeighborhoodForecastAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var coord = coordinate.Rounded();
        var metadata = await ResolveMetadataAsync(coord, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var primary = await GetForecastByGridCoreAsync(metadata.Grid, cancellationToken).ConfigureAwait(false);
        if (primary is null)
        {
            return null;
        }

        warmer.RequestWarming(metadata.Grid);

        // Best-effort: include only neighbours that are ALREADY warm. Never
        // fetch on the hot path — that is what keeps this endpoint fast.
        var now = timeProvider.GetUtcNow();
        var neighbors = new List<Forecast>();
        foreach (var cell in GridNeighborhood.Surrounding(metadata.Grid))
        {
            var cached = await forecastCache.GetAsync(cell, cancellationToken).ConfigureAwait(false);
            if (cached is not null && cached.IsFresh(now))
            {
                neighbors.Add(cached.Forecast);
            }
        }

        return new NeighborhoodForecast(coord, primary, neighbors);
    }

    public Task<Forecast?> GetForecastByGridAsync(GridPoint grid, CancellationToken cancellationToken = default) =>
        // Public entry point used by the background warmer. Deliberately does
        // NOT schedule further warming, which would recurse forever.
        GetForecastByGridCoreAsync(grid, cancellationToken);

    private async Task<PointMetadata?> ResolveMetadataAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cached = await metadataCache.GetAsync(coordinate, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(MetadataCacheName);
            return cached.Metadata;
        }

        telemetry.RecordCacheMiss(MetadataCacheName);

        var fetched = await nwsClient.GetPointMetadataAsync(coordinate, cancellationToken).ConfigureAwait(false);
        if (fetched is null)
        {
            // Either NWS is unreachable or the location is genuinely uncovered.
            // The mapping effectively never changes, so a stale hit is safe.
            if (cached is not null)
            {
                logger.LogWarning("Serving stale point metadata for {Coordinate}; NWS resolve failed.", coordinate);
                return cached.Metadata;
            }

            return null;
        }

        await metadataCache
            .UpsertAsync(new CachedPointMetadata(fetched, now, now + _options.PointMetadataTtl), cancellationToken)
            .ConfigureAwait(false);
        return fetched;
    }

    private async Task<Forecast?> GetForecastByGridCoreAsync(GridPoint grid, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cached = await forecastCache.GetAsync(grid, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(ForecastCacheName);
            return cached.Forecast;
        }

        telemetry.RecordCacheMiss(ForecastCacheName);

        var result = await nwsClient
            .GetForecastAsync(grid, cached?.ETag, cancellationToken)
            .ConfigureAwait(false);

        switch (result.Outcome)
        {
            case NwsFetchOutcome.Success when result.Forecast is not null:
                var fresh = new CachedForecast(
                    result.Forecast, result.ETag, now, ComputeExpiry(now, result.MaxAge));
                await forecastCache.UpsertAsync(fresh, cancellationToken).ConfigureAwait(false);
                return result.Forecast;

            case NwsFetchOutcome.NotModified when cached is not null:
                // Cheapest path: payload unchanged, just extend the TTL.
                var renewed = cached with
                {
                    ETag = result.ETag ?? cached.ETag,
                    RetrievedAtUtc = now,
                    ExpiresAtUtc = ComputeExpiry(now, result.MaxAge),
                };
                await forecastCache.UpsertAsync(renewed, cancellationToken).ConfigureAwait(false);
                return cached.Forecast;

            case NwsFetchOutcome.Unavailable when cached is not null:
                logger.LogWarning("Serving stale forecast for grid {Grid}; NWS unavailable.", grid);
                return cached.Forecast;

            case NwsFetchOutcome.NotFound:
            case NwsFetchOutcome.Unavailable:
            case NwsFetchOutcome.NotModified:
            default:
                return null;
        }
    }

    private DateTimeOffset ComputeExpiry(DateTimeOffset now, TimeSpan? maxAge)
    {
        var ttl = maxAge ?? _options.DefaultForecastTtl;
        if (ttl <= TimeSpan.Zero)
        {
            ttl = _options.DefaultForecastTtl;
        }

        if (ttl > _options.MaxForecastTtl)
        {
            ttl = _options.MaxForecastTtl;
        }

        return now + ttl;
    }
}
