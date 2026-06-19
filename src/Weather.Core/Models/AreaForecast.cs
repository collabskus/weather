namespace Weather.Core.Models;

/// <summary>
/// The complete picture for a coordinate: the user's own cell, every
/// neighbouring cell ordered by distance from the user, the location metadata,
/// and any active alerts for the point. This is the payload behind the
/// dashboard's full "all tiles, all data" view.
/// </summary>
public sealed record AreaForecast(
    GeoCoordinate Query,
    PointMetadata Metadata,
    CellWeather Primary,
    IReadOnlyList<CellWeather> Neighbors,
    IReadOnlyList<WeatherAlert> Alerts);
