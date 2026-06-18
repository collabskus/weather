namespace Weather.Core.Models;

/// <summary>
/// The forecast for the user's own grid cell (<see cref="Primary"/>) plus any
/// adjacent cells that were already warm in the cache. Neighbours are returned
/// best-effort: an empty list simply means nothing was pre-warmed yet.
/// </summary>
public sealed record NeighborhoodForecast(
    GeoCoordinate Query,
    Forecast Primary,
    IReadOnlyList<Forecast> Neighbors);
