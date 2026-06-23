namespace Weather.Core.Models;

/// <summary>
/// The extra, link-followed data for a single grid cell that is cached
/// alongside (but separately from) the headline daily forecast: the hourly
/// forecast, the latest station observation, the cell's approximate centre
/// (used to rank neighbours by distance), and the id of the cell's nearest
/// reporting station. Serialised as one JSON blob per cell.
///
/// <para>
/// <see cref="StationId"/> is remembered so a routine observation refresh does
/// NOT have to re-list the cell's stations (<c>/gridpoints/.../stations</c>)
/// every time. That station list is effectively immutable, so once the nearest
/// station is known it is reused to fetch the latest observation directly by
/// id. The field is nullable both for backward compatibility with entries
/// written before it was tracked and for cells whose station could not be
/// resolved; in either case the next refresh falls back to re-listing stations.
/// </para>
/// </summary>
public sealed record CellExtras(
    IReadOnlyList<ForecastPeriod> Hourly,
    Observation? Observation,
    GeoCoordinate? Center,
    string? StationId = null);
