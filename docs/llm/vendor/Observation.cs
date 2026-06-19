namespace Weather.Core.Models;

/// <summary>
/// The latest surface observation from the nearest reporting station. NWS
/// reports these in SI units; the infrastructure layer converts them to the
/// US units the forecast uses (°F, mph, inHg, miles) so the UI is consistent.
/// Every measurement is nullable because a station may not report all of them.
/// </summary>
public sealed record Observation(
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
