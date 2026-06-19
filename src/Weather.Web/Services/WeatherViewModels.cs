namespace Weather.Web.Services;

/// <summary>
/// Client-side view of a single grid cell's forecast, deserialized from the
/// Weather API's JSON. These records are intentionally separate from the API's
/// own contract types: the Blazor app only depends on the wire shape, not on
/// any server assembly.
/// </summary>
public sealed record ForecastDto(
    string GridId,
    int GridX,
    int GridY,
    DateTimeOffset GeneratedAt,
    DateTimeOffset UpdateTime,
    IReadOnlyList<ForecastPeriodDto> Periods);

/// <summary>One named forecast period as returned by the API.</summary>
public sealed record ForecastPeriodDto(
    int Number,
    string Name,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool IsDaytime,
    int Temperature,
    string TemperatureUnit,
    int? ProbabilityOfPrecipitation,
    string WindSpeed,
    string WindDirection,
    string ShortForecast,
    string DetailedForecast,
    string Icon);

/// <summary>
/// A neighbourhood response: the primary cell plus any adjacent cells the API
/// already had warm. Used by the (legacy) neighbourhood view.
/// </summary>
public sealed record NeighborhoodDto(
    double Latitude,
    double Longitude,
    ForecastDto Primary,
    IReadOnlyList<ForecastDto> Neighbors);

/// <summary>
/// The full "all tiles, all data" view: the user's own cell, every neighbouring
/// cell ordered by distance, location metadata, and any active alerts.
/// </summary>
public sealed record AreaDto(
    double Latitude,
    double Longitude,
    string? City,
    string? State,
    string? TimeZone,
    string? RadarStation,
    CellDto Primary,
    IReadOnlyList<CellDto> Neighbors,
    IReadOnlyList<AlertDto> Alerts);

/// <summary>One grid cell with all of its data: daily, hourly and observation.</summary>
public sealed record CellDto(
    string GridId,
    int GridX,
    int GridY,
    double? CenterLatitude,
    double? CenterLongitude,
    double? DistanceMeters,
    DateTimeOffset GeneratedAt,
    DateTimeOffset UpdateTime,
    IReadOnlyList<ForecastPeriodDto> Periods,
    IReadOnlyList<ForecastPeriodDto> Hourly,
    ObservationDto? Observation);

/// <summary>The latest observation for a cell, in US units.</summary>
public sealed record ObservationDto(
    string? StationId,
    string? StationName,
    DateTimeOffset? Timestamp,
    string? TextDescription,
    string? Icon,
    int? TemperatureF,
    int? DewpointF,
    int? RelativeHumidity,
    int? WindSpeedMph,
    int? WindGustMph,
    string? WindDirection,
    double? PressureInHg,
    double? VisibilityMiles);

/// <summary>One active alert affecting the area.</summary>
public sealed record AlertDto(
    string Id,
    string Event,
    string? Severity,
    string? Certainty,
    string? Urgency,
    string? Headline,
    string? Description,
    string? Instruction,
    string? AreaDescription,
    string? SenderName,
    DateTimeOffset? Effective,
    DateTimeOffset? Onset,
    DateTimeOffset? Expires,
    DateTimeOffset? Ends);
