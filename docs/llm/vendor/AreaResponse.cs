using Weather.Core.Models;

namespace Weather.Api.Contracts;

/// <summary>
/// The full "all tiles, all data" response: the user's own cell, every
/// neighbouring cell ordered by distance, the location metadata, and any
/// active alerts.
/// </summary>
public sealed record AreaResponse(
    double Latitude,
    double Longitude,
    string? City,
    string? State,
    string? TimeZone,
    string? RadarStation,
    CellResponse Primary,
    IReadOnlyList<CellResponse> Neighbors,
    IReadOnlyList<AlertResponse> Alerts)
{
    public static AreaResponse FromDomain(AreaForecast area)
    {
        var neighbors = new List<CellResponse>(area.Neighbors.Count);
        foreach (var neighbor in area.Neighbors)
        {
            neighbors.Add(CellResponse.FromDomain(neighbor));
        }

        var alerts = new List<AlertResponse>(area.Alerts.Count);
        foreach (var alert in area.Alerts)
        {
            alerts.Add(AlertResponse.FromDomain(alert));
        }

        return new AreaResponse(
            area.Query.Latitude,
            area.Query.Longitude,
            area.Metadata.City,
            area.Metadata.State,
            area.Metadata.TimeZone,
            area.Metadata.RadarStation,
            CellResponse.FromDomain(area.Primary),
            neighbors,
            alerts);
    }
}

/// <summary>
/// One grid cell with all of its data. The daily forecast fields sit at the top
/// level (so this is a superset of <see cref="ForecastResponse"/>), with the
/// hourly forecast, the latest observation, the cell centre, and the distance
/// from the user alongside.
/// </summary>
public sealed record CellResponse(
    string GridId,
    int GridX,
    int GridY,
    double? CenterLatitude,
    double? CenterLongitude,
    double? DistanceMeters,
    DateTimeOffset GeneratedAt,
    DateTimeOffset UpdateTime,
    IReadOnlyList<ForecastPeriodResponse> Periods,
    IReadOnlyList<ForecastPeriodResponse> Hourly,
    ObservationResponse? Observation)
{
    public static CellResponse FromDomain(CellWeather cell)
    {
        var periods = new List<ForecastPeriodResponse>(cell.Daily.Periods.Count);
        foreach (var period in cell.Daily.Periods)
        {
            periods.Add(ForecastPeriodResponse.FromDomain(period));
        }

        var hourly = new List<ForecastPeriodResponse>(cell.Hourly.Count);
        foreach (var period in cell.Hourly)
        {
            hourly.Add(ForecastPeriodResponse.FromDomain(period));
        }

        return new CellResponse(
            cell.Grid.GridId,
            cell.Grid.GridX,
            cell.Grid.GridY,
            cell.Center?.Latitude,
            cell.Center?.Longitude,
            cell.DistanceMeters,
            cell.Daily.GeneratedAt,
            cell.Daily.UpdateTime,
            periods,
            hourly,
            cell.Observation is { } observation ? ObservationResponse.FromDomain(observation) : null);
    }
}

/// <summary>The latest observation for a cell, in US units.</summary>
public sealed record ObservationResponse(
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
    double? VisibilityMiles)
{
    public static ObservationResponse FromDomain(Observation observation) => new(
        observation.StationId,
        observation.StationName,
        observation.Timestamp,
        observation.TextDescription,
        observation.Icon,
        observation.TemperatureF,
        observation.DewpointF,
        observation.RelativeHumidity,
        observation.WindSpeedMph,
        observation.WindGustMph,
        observation.WindDirection,
        observation.PressureInHg,
        observation.VisibilityMiles);
}

/// <summary>One active alert affecting the area.</summary>
public sealed record AlertResponse(
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
    DateTimeOffset? Ends)
{
    public static AlertResponse FromDomain(WeatherAlert alert) => new(
        alert.Id,
        alert.Event,
        alert.Severity,
        alert.Certainty,
        alert.Urgency,
        alert.Headline,
        alert.Description,
        alert.Instruction,
        alert.AreaDescription,
        alert.SenderName,
        alert.Effective,
        alert.Onset,
        alert.Expires,
        alert.Ends);
}
