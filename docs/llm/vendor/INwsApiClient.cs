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
    /// Fetch the latest observation from the cell's nearest reporting station
    /// (<c>/gridpoints/.../stations</c> then <c>/stations/{id}/observations/latest</c>).
    /// Best-effort: returns <c>null</c> when no station or observation is available.
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
