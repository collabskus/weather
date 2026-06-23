using System.Collections.Concurrent;
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
///
/// <para>
/// <b>Stampede protection.</b> The forecast fetch for a single grid cell is run
/// through an <see cref="IRequestCoalescer{TKey}"/> keyed by the cell. When a
/// cell is cold (or has just expired) and many requests arrive at once — the
/// "refresh-spam from many browsers" case — only the first request calls NWS;
/// the rest await that one in-flight fetch and share its result. This is what
/// guarantees that load on NWS is bounded by the number of <em>distinct cold
/// cells</em>, not by the number of users.
/// </para>
/// </summary>
internal sealed class WeatherService(
    IPointMetadataCache metadataCache,
    IForecastCache forecastCache,
    ICellExtrasCache extrasCache,
    IAlertCache alertCache,
    INwsApiClient nwsClient,
    INeighborhoodWarmer warmer,
    IRequestCoalescer<GridPoint> forecastCoalescer,
    IRequestCoalescer<string> alertCoalescer,
    WeatherTelemetry telemetry,
    TimeProvider timeProvider,
    IOptions<NwsClientOptions> options,
    ILogger<WeatherService> logger) : IWeatherService
{
    private const string MetadataCacheName = "metadata";
    private const string ForecastCacheName = "forecast";
    private const string ExtrasCacheName = "extras";
    private const string AlertsCacheName = "alerts";

    // Nominal NWS cell size (~2.5 km) used only as a fallback ordering metric
    // when a cell's true polygon centre is not currently known.
    private const double NominalCellMeters = 2500d;

    // Keep the hot-path fan-out polite to NWS even on a cold area request.
    private const int NeighborFetchConcurrency = 4;

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

        var (forecast, _) = await GetForecastByGridCoreAsync(metadata.Grid, cancellationToken).ConfigureAwait(false);
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

        var (primary, _) = await GetForecastByGridCoreAsync(metadata.Grid, cancellationToken).ConfigureAwait(false);
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

    public async Task<AreaForecast?> GetAreaForecastAsync(
        GeoCoordinate coordinate, int radius = 1, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var coord = coordinate.Rounded();
        var metadata = await ResolveMetadataAsync(coord, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var primary = await AssembleCellAsync(metadata.Grid, cancellationToken).ConfigureAwait(false);
        if (primary is null)
        {
            return null;
        }

        // Schedule background warming of the daily forecasts for the ring.
        warmer.RequestWarming(metadata.Grid);

        // Alerts are point-based and apply to the whole area; resolve them
        // cache-aside (short TTL) so repeated views and browser refreshes do
        // NOT re-hit /alerts/active every time.
        var alerts = await ResolveAlertsAsync(coord, cancellationToken).ConfigureAwait(false);

        // Assemble every neighbouring cell, cache-aside, with bounded
        // concurrency so a cold area can never stampede NWS.
        var cells = GridNeighborhood.Surrounding(metadata.Grid, radius);
        var assembled = new ConcurrentBag<CellWeather>();

        await Parallel.ForEachAsync(
            cells,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = NeighborFetchConcurrency,
                CancellationToken = cancellationToken,
            },
            async (cell, token) =>
            {
                var assembledCell = await AssembleCellAsync(cell, token).ConfigureAwait(false);
                if (assembledCell is not null)
                {
                    assembled.Add(assembledCell);
                }
            }).ConfigureAwait(false);

        var neighbors = assembled
            .OrderBy(c => OrderKeyMeters(coord, metadata.Grid, c))
            .Select(c => c with { DistanceMeters = DisplayDistanceMeters(coord, c) })
            .ToList();

        return new AreaForecast(coord, metadata, primary, neighbors, alerts);
    }

    public async Task<Forecast?> GetForecastByGridAsync(GridPoint grid, CancellationToken cancellationToken = default)
    {
        // Public entry point used by the background warmer. Deliberately does
        // NOT schedule further warming, which would recurse forever.
        var (forecast, _) = await GetForecastByGridCoreAsync(grid, cancellationToken).ConfigureAwait(false);
        return forecast;
    }

    /// <summary>
    /// Build the full bundle for one cell: the headline daily forecast
    /// (cache-aside, with conditional GETs) plus the cached "extras" (hourly +
    /// latest observation + centre). Returns <c>null</c> only when the daily
    /// forecast itself is unavailable.
    /// </summary>
    private async Task<CellWeather?> AssembleCellAsync(GridPoint grid, CancellationToken cancellationToken)
    {
        var (daily, centerFromFetch) = await GetForecastByGridCoreAsync(grid, cancellationToken).ConfigureAwait(false);
        if (daily is null)
        {
            return null;
        }

        var extras = await ResolveExtrasAsync(grid, centerFromFetch, cancellationToken).ConfigureAwait(false);
        var center = centerFromFetch ?? extras.Center;

        return new CellWeather(grid, center, daily, extras.Hourly, extras.Observation, DistanceMeters: null);
    }

    private async Task<CellExtras> ResolveExtrasAsync(
        GridPoint grid, GeoCoordinate? centerHint, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cached = await extrasCache.GetAsync(grid, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(ExtrasCacheName);

            // Backfill the centre if we only just learned it from a fresh daily fetch.
            if (centerHint is not null && cached.Extras.Center is null)
            {
                var upgraded = cached.Extras with { Center = centerHint };
                await extrasCache
                    .UpsertAsync(grid, cached with { Extras = upgraded }, cancellationToken)
                    .ConfigureAwait(false);
                return upgraded;
            }

            return cached.Extras;
        }

        telemetry.RecordCacheMiss(ExtrasCacheName);

        var hourly = await nwsClient.GetHourlyForecastAsync(grid, cancellationToken).ConfigureAwait(false) ?? [];
        var observation = await nwsClient.GetLatestObservationAsync(grid, cancellationToken).ConfigureAwait(false);

        var center = centerHint ?? cached?.Extras.Center;
        var extras = new CellExtras(hourly, observation, center);

        await extrasCache
            .UpsertAsync(grid, new CachedCellExtras(extras, now, now + _options.DefaultForecastTtl), cancellationToken)
            .ConfigureAwait(false);

        return extras;
    }

    /// <summary>
    /// Cache-aside fetch of active alerts for a coordinate, protected by the
    /// single-flight coalescer (keyed by the coordinate's cache key) so a burst
    /// of concurrent misses collapses into one upstream <c>/alerts/active</c>
    /// call. A fresh cache entry is served straight from SQLite; on miss/expiry
    /// the result is fetched once and stored with a short TTL. Caching an empty
    /// list is deliberate and correct: "no active alerts" is a valid answer
    /// worth remembering for the TTL, and it is what stops repeated views and
    /// browser refreshes from re-hitting the alerts endpoint every time.
    /// </summary>
    private async Task<IReadOnlyList<WeatherAlert>> ResolveAlertsAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var cached = await alertCache.GetAsync(coordinate, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(AlertsCacheName);
            return cached.Alerts;
        }

        telemetry.RecordCacheMiss(AlertsCacheName);

        var key = coordinate.Rounded().ToCacheKey();

        return await alertCoalescer.RunAsync(
            key,
            async token =>
            {
                // Re-check the cache inside the flight: a concurrent leader for
                // the same coordinate may have just filled it.
                var inner = timeProvider.GetUtcNow();
                var rechecked = await alertCache.GetAsync(coordinate, token).ConfigureAwait(false);
                if (rechecked is not null && rechecked.IsFresh(inner))
                {
                    return rechecked.Alerts;
                }

                // The client is best-effort: it returns an empty list both when
                // there are genuinely no alerts and when NWS is unreachable.
                // Either way an empty list is a safe thing to cache for the
                // short TTL; the next refresh after expiry will try again.
                var fetched = await nwsClient.GetActiveAlertsAsync(coordinate, token).ConfigureAwait(false) ?? [];

                await alertCache
                    .UpsertAsync(
                        coordinate,
                        new CachedAlerts(fetched, inner, inner + _options.AlertsTtl),
                        token)
                    .ConfigureAwait(false);

                return fetched;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static double OrderKeyMeters(GeoCoordinate query, GridPoint origin, CellWeather cell) =>
        cell.Center is { } center
            ? query.DistanceMetersTo(center)
            : GridApproxMeters(origin, cell.Grid);

    private static double? DisplayDistanceMeters(GeoCoordinate query, CellWeather cell) =>
        cell.Center is { } center ? query.DistanceMetersTo(center) : null;

    private static double GridApproxMeters(GridPoint origin, GridPoint cell)
    {
        double dx = cell.GridX - origin.GridX;
        double dy = cell.GridY - origin.GridY;
        return Math.Sqrt((dx * dx) + (dy * dy)) * NominalCellMeters;
    }

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

    /// <summary>
    /// Cache-aside fetch of the headline daily forecast for a cell, protected by
    /// the single-flight coalescer so concurrent misses for the same cell
    /// collapse into one upstream call. Also returns the cell's approximate
    /// centre when it came from a fresh fetch (it is not stored in the daily
    /// cache, so a pure cache hit yields a null centre and the caller falls back
    /// to the extras-cached centre or a grid estimate).
    /// </summary>
    private Task<(Forecast? Forecast, GeoCoordinate? Center)> GetForecastByGridCoreAsync(
        GridPoint grid, CancellationToken cancellationToken)
    {
        // A flag the factory flips when THIS call is the one that actually ran
        // the fetch (the coalescer "leader"). Everyone else is a follower whose
        // upstream call was avoided.
        var ranFactory = false;

        return RunCoalescedAsync();

        async Task<(Forecast?, GeoCoordinate?)> RunCoalescedAsync()
        {
            var result = await forecastCoalescer.RunAsync(
                grid,
                async flightToken =>
                {
                    ranFactory = true;
                    return await FetchForecastCoreAsync(grid, flightToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            if (ranFactory)
            {
                telemetry.RecordCoalesceLeader(ForecastCacheName);
            }
            else
            {
                telemetry.RecordCoalesceFollower(ForecastCacheName);
            }

            return result;
        }
    }

    /// <summary>
    /// The actual cache-aside body, run by at most one caller per cell at a
    /// time (the coalescer guarantees this). Re-reads the cache first, so a
    /// follower that waited for an earlier flight — and then started a fresh one
    /// — still sees the freshly-filled cache and never calls NWS.
    /// </summary>
    private async Task<(Forecast? Forecast, GeoCoordinate? Center)> FetchForecastCoreAsync(
        GridPoint grid, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cached = await forecastCache.GetAsync(grid, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(ForecastCacheName);
            return (cached.Forecast, null);
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
                return (result.Forecast, result.Center);

            case NwsFetchOutcome.NotModified when cached is not null:
                // Cheapest path: payload unchanged, just extend the TTL.
                var renewed = cached with
                {
                    ETag = result.ETag ?? cached.ETag,
                    RetrievedAtUtc = now,
                    ExpiresAtUtc = ComputeExpiry(now, result.MaxAge),
                };
                await forecastCache.UpsertAsync(renewed, cancellationToken).ConfigureAwait(false);
                return (cached.Forecast, null);

            case NwsFetchOutcome.Unavailable when cached is not null:
                logger.LogWarning("Serving stale forecast for grid {Grid}; NWS unavailable.", grid);
                return (cached.Forecast, null);

            case NwsFetchOutcome.NotFound:
            case NwsFetchOutcome.Unavailable:
            case NwsFetchOutcome.NotModified:
            default:
                return (null, null);
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
