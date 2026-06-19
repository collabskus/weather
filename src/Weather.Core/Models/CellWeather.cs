namespace Weather.Core.Models;

/// <summary>
/// Everything known about a single NWS grid cell ("tile"): the daily forecast,
/// the hourly forecast, the latest observation, the cell's approximate centre,
/// and — when assembled as part of a neighbourhood — how far that cell is from
/// the user. This is what the area endpoint returns for the primary cell and
/// for every neighbour.
/// </summary>
public sealed record CellWeather(
    GridPoint Grid,
    GeoCoordinate? Center,
    Forecast Daily,
    IReadOnlyList<ForecastPeriod> Hourly,
    Observation? Observation,
    double? DistanceMeters);
