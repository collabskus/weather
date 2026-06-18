namespace Weather.Core.Models;

/// <summary>
/// The slow-changing metadata returned by <c>/points/{lat},{lon}</c>: which
/// grid cell a coordinate maps to, plus the URLs and human-readable location.
/// This mapping effectively never changes, so it is cached for a long time.
/// </summary>
public sealed record PointMetadata(
    GeoCoordinate Query,
    GridPoint Grid,
    string ForecastUrl,
    string ForecastHourlyUrl,
    string? City,
    string? State,
    string? TimeZone,
    string? RadarStation);
