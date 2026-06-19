namespace Weather.Core.Models;

/// <summary>
/// One active NWS alert (watch, warning, advisory, or statement) affecting the
/// queried point. Fields mirror the CAP-derived alert <c>properties</c>.
/// </summary>
public sealed record WeatherAlert(
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
