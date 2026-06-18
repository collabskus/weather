namespace Weather.Core.Models;

/// <summary>One named forecast period (e.g. "This Afternoon", "Tonight").</summary>
public sealed record ForecastPeriod(
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
