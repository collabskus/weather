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
    /// Fetch the forecast for a grid cell via
    /// <c>/gridpoints/{office}/{x},{y}/forecast</c>, optionally as a conditional
    /// GET using a previously stored <paramref name="etag"/>.
    /// </summary>
    Task<ForecastFetchResult> GetForecastAsync(
        GridPoint grid, string? etag = null, CancellationToken cancellationToken = default);
}
