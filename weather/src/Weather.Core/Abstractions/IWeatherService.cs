using Weather.Core.Models;

namespace Weather.Core.Abstractions;

/// <summary>
/// Application-facing orchestration: combines metadata + forecast caches with
/// the NWS client and the background neighbourhood warmer.
/// </summary>
public interface IWeatherService
{
    /// <summary>
    /// Forecast for the cell containing <paramref name="coordinate"/>. Serves
    /// from cache when fresh; otherwise fetches and stores. Also schedules the
    /// surrounding cells to be warmed in the background. Returns <c>null</c> if
    /// NWS does not cover the location.
    /// </summary>
    Task<Forecast?> GetForecastAsync(GeoCoordinate coordinate, CancellationToken cancellationToken = default);

    /// <summary>
    /// The primary forecast plus any already-warm neighbouring cells. Neighbours
    /// are never fetched on the hot path — only returned if already cached.
    /// </summary>
    Task<NeighborhoodForecast?> GetNeighborhoodForecastAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache-aside forecast fetch for a known grid cell, WITHOUT triggering
    /// further warming. Used by the background warmer to avoid recursion.
    /// </summary>
    Task<Forecast?> GetForecastByGridAsync(GridPoint grid, CancellationToken cancellationToken = default);
}
