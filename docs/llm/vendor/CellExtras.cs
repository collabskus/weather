namespace Weather.Core.Models;

/// <summary>
/// The extra, link-followed data for a single grid cell that is cached
/// alongside (but separately from) the headline daily forecast: the hourly
/// forecast, the latest station observation, and the cell's approximate centre
/// (used to rank neighbours by distance). Serialised as one JSON blob per cell.
/// </summary>
public sealed record CellExtras(
    IReadOnlyList<ForecastPeriod> Hourly,
    Observation? Observation,
    GeoCoordinate? Center);
