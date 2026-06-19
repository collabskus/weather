using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>Envelope for the daily and hourly forecast endpoints (same shape).</summary>
internal sealed record NwsForecastResponse
{
    public NwsGeometry? Geometry { get; init; }
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

/// <summary>
/// Geometry for a forecast feature. The forecast is a Polygon; observations are
/// a Point. Coordinates are decoded leniently from the raw JSON because the
/// nesting depth differs by geometry type.
/// </summary>
internal sealed record NwsGeometry
{
    public string? Type { get; init; }

    [JsonPropertyName("coordinates")]
    public JsonElement Coordinates { get; init; }
}

/// <summary>Envelope for <c>GET /gridpoints/{office}/{x},{y}/stations</c>.</summary>
internal sealed record NwsStationsResponse
{
    public IReadOnlyList<NwsStationFeature>? Features { get; init; }
}

internal sealed record NwsStationFeature
{
    public NwsStationProperties? Properties { get; init; }
}

internal sealed record NwsStationProperties
{
    public string? StationIdentifier { get; init; }
    public string? Name { get; init; }
}

/// <summary>Envelope for <c>GET /stations/{id}/observations/latest</c>.</summary>
internal sealed record NwsObservationResponse
{
    public NwsObservationProperties? Properties { get; init; }
}

internal sealed record NwsObservationProperties
{
    public DateTimeOffset? Timestamp { get; init; }
    public string? TextDescription { get; init; }
    public string? Icon { get; init; }
    public NwsQuantitativeValue? Temperature { get; init; }
    public NwsQuantitativeValue? Dewpoint { get; init; }
    public NwsQuantitativeValue? WindDirection { get; init; }
    public NwsQuantitativeValue? WindSpeed { get; init; }
    public NwsQuantitativeValue? WindGust { get; init; }
    public NwsQuantitativeValue? BarometricPressure { get; init; }
    public NwsQuantitativeValue? RelativeHumidity { get; init; }
    public NwsQuantitativeValue? Visibility { get; init; }
}

/// <summary>Envelope for <c>GET /alerts/active?point={lat},{lon}</c>.</summary>
internal sealed record NwsAlertsResponse
{
    public IReadOnlyList<NwsAlertFeature>? Features { get; init; }
}

internal sealed record NwsAlertFeature
{
    public NwsAlertProperties? Properties { get; init; }
}

internal sealed record NwsAlertProperties
{
    public string? Id { get; init; }
    public string? Event { get; init; }
    public string? Severity { get; init; }
    public string? Certainty { get; init; }
    public string? Urgency { get; init; }
    public string? Headline { get; init; }
    public string? Description { get; init; }
    public string? Instruction { get; init; }
    public string? AreaDesc { get; init; }
    public string? SenderName { get; init; }
    public DateTimeOffset? Effective { get; init; }
    public DateTimeOffset? Onset { get; init; }
    public DateTimeOffset? Expires { get; init; }
    public DateTimeOffset? Ends { get; init; }
}
