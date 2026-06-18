namespace Weather.Infrastructure.Nws;

// ---------------------------------------------------------------------------
// Wire DTOs for the National Weather Service API.
//
// These mirror only the fields the application consumes. Everything is
// nullable because the upstream API is outside our control: a defensive parse
// that tolerates missing fields is safer than one that throws on a surprise.
// Property names map to the camelCase JSON via WeatherJson.Options.
// ---------------------------------------------------------------------------

/// <summary>Envelope for <c>GET /points/{lat},{lon}</c>.</summary>
internal sealed record NwsPointResponse
{
    public NwsPointProperties? Properties { get; init; }
}

internal sealed record NwsPointProperties
{
    public string? GridId { get; init; }
    public int GridX { get; init; }
    public int GridY { get; init; }
    public string? Forecast { get; init; }
    public string? ForecastHourly { get; init; }
    public string? TimeZone { get; init; }
    public string? RadarStation { get; init; }
    public NwsRelativeLocation? RelativeLocation { get; init; }
}

internal sealed record NwsRelativeLocation
{
    public NwsRelativeLocationProperties? Properties { get; init; }
}

internal sealed record NwsRelativeLocationProperties
{
    public string? City { get; init; }
    public string? State { get; init; }
}

/// <summary>Envelope for <c>GET /gridpoints/{office}/{x},{y}/forecast</c>.</summary>
internal sealed record NwsForecastResponse
{
    public NwsForecastProperties? Properties { get; init; }
}

internal sealed record NwsForecastProperties
{
    public DateTimeOffset GeneratedAt { get; init; }
    public DateTimeOffset UpdateTime { get; init; }
    public IReadOnlyList<NwsPeriod>? Periods { get; init; }
}

internal sealed record NwsPeriod
{
    public int Number { get; init; }
    public string? Name { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public bool IsDaytime { get; init; }
    public int Temperature { get; init; }
    public string? TemperatureUnit { get; init; }
    public NwsQuantitativeValue? ProbabilityOfPrecipitation { get; init; }
    public string? WindSpeed { get; init; }
    public string? WindDirection { get; init; }
    public string? ShortForecast { get; init; }
    public string? DetailedForecast { get; init; }
    public string? Icon { get; init; }
}

/// <summary>NWS wraps scalar measurements as <c>{ unitCode, value }</c>; value may be null.</summary>
internal sealed record NwsQuantitativeValue
{
    public string? UnitCode { get; init; }
    public double? Value { get; init; }
}
