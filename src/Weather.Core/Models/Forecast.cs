namespace Weather.Core.Models;

/// <summary>
/// A full multi-period forecast for a single grid cell, plus the NWS-reported
/// generation/update timestamps used to reason about freshness.
/// </summary>
public sealed record Forecast(
    GridPoint Grid,
    DateTimeOffset GeneratedAt,
    DateTimeOffset UpdateTime,
    IReadOnlyList<ForecastPeriod> Periods);
