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
/// already had warm. Used by the (optional) neighbourhood view.
/// </summary>
public sealed record NeighborhoodDto(
    double Latitude,
    double Longitude,
    ForecastDto Primary,
    IReadOnlyList<ForecastDto> Neighbors);
