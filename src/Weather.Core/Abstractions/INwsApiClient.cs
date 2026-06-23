using Weather.Core.Models;

namespace Weather.Core.Abstractions;

/// <summary>
/// Thin, testable wrapper over the National Weather Service REST API.
/// Implementations own HTTP, resilience and telemetry; they do NOT cache.
/// </summary>
public interface INwsApiClient
{
    /// <summary>
    /// Resolve a coordinate to its grid metadata via <c>/points/{lat},{lon}</c>.
    /// Returns <c>null</c> if NWS does not cover the location (e.g. outside the US).
    /// </summary>
    Task<PointMetadata?> GetPointMetadataAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the daily forecast for a grid cell via
    /// <c>/gridpoints/{office}/{x},{y}/forecast</c>, optionally as a conditional
    /// GET using a previously stored <paramref name="etag"/>. The result also
    /// carries the cell's approximate centre, parsed from the polygon geometry.
    /// </summary>
    Task<ForecastFetchResult> GetForecastAsync(
        GridPoint grid, string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the hourly forecast via
    /// <c>/gridpoints/{office}/{x},{y}/forecast/hourly</c>. Best-effort: returns
    /// an empty list if NWS is unavailable or the cell has no hourly data.
    /// </summary>
    Task<IReadOnlyList<ForecastPeriod>> GetHourlyForecastAsync(
        GridPoint grid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve the id of the cell's nearest reporting station via
    /// <c>/gridpoints/{office}/{x},{y}/stations</c> (stations are returned
    /// nearest-first). Best-effort: returns <c>null</c> when no station is
    /// listed or NWS is unavailable.
    ///
    /// <para>
    /// Exposed separately from <see cref="GetLatestObservationAsync(GridPoint, CancellationToken)"/>
    /// because the cell→station mapping is effectively immutable: callers can
    /// resolve it once, cache the id, and then refresh observations by id
    /// (see <see cref="GetLatestObservationAsync(GridPoint, string, CancellationToken)"/>)
    /// without re-listing stations on every refresh.
    /// </para>
    /// </summary>
    Task<string?> GetNearestStationIdAsync(
        GridPoint grid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the latest observation for an already-known station via
    /// <c>/stations/{id}/observations/latest</c>. This is the cheap refresh
    /// path: it makes a single request and does NOT re-list the cell's stations.
    /// Best-effort: returns <c>null</c> when no observation is available.
    /// </summary>
    /// <param name="grid">The owning grid cell (used for telemetry/logging context).</param>
    /// <param name="stationId">The station identifier (e.g. <c>"KPHF"</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<Observation?> GetLatestObservationAsync(
        GridPoint grid, string stationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience two-step fetch of the latest observation from the cell's
    /// nearest reporting station (<c>/gridpoints/.../stations</c> then
    /// <c>/stations/{id}/observations/latest</c>). Prefer caching the station id
    /// and calling <see cref="GetLatestObservationAsync(GridPoint, string, CancellationToken)"/>
    /// for repeated refreshes. Best-effort: returns <c>null</c> when no station
    /// or observation is available.
    /// </summary>
    Task<Observation?> GetLatestObservationAsync(
        GridPoint grid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch active alerts affecting a point via
    /// <c>/alerts/active?point={lat},{lon}</c>. Best-effort: returns an empty
    /// list when there are no alerts or NWS is unavailable.
    /// </summary>
    Task<IReadOnlyList<WeatherAlert>> GetActiveAlertsAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default);
}
