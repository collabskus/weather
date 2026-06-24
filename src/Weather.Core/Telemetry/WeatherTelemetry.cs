using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Weather.Core.Telemetry;

/// <summary>
/// Single source of truth for the application's custom metrics and traces.
/// Registered as a singleton; the <see cref="Meter"/> and
/// <see cref="ActivitySource"/> names are wired into OpenTelemetry in
/// <c>Weather.Api/Program.cs</c> via <c>AddMeter</c>/<c>AddSource</c>.
/// </summary>
public sealed class WeatherTelemetry : IDisposable
{
    /// <summary>Meter name — register with <c>AddMeter(WeatherTelemetry.MeterName)</c>.</summary>
    public const string MeterName = "Weather.Cache";

    /// <summary>Activity source name — register with <c>AddSource(WeatherTelemetry.ActivitySourceName)</c>.</summary>
    public const string ActivitySourceName = "Weather.Nws";

    private readonly Meter _meter;
    private readonly Counter<long> _cacheRequests;
    private readonly Counter<long> _coalescedRequests;
    private readonly Histogram<double> _nwsRequestDuration;
    private readonly Counter<long> _nwsThrottled;
    private readonly Counter<long> _nwsErrors;
    private readonly Counter<long> _neighborhoodWarmed;
    private readonly Counter<long> _nwsNotFoundCached;

    public ActivitySource ActivitySource { get; }

    public WeatherTelemetry()
    {
        _meter = new Meter(MeterName);
        ActivitySource = new ActivitySource(ActivitySourceName);

        _cacheRequests = _meter.CreateCounter<long>(
            "weather.cache.requests",
            unit: "{request}",
            description: "Cache look-ups, tagged by cache name and hit/miss outcome.");

        _coalescedRequests = _meter.CreateCounter<long>(
            "weather.cache.coalesced",
            unit: "{request}",
            description: "Forecast fetches collapsed into an in-flight request by the single-flight coalescer, tagged by leader/follower. Followers are upstream calls that were AVOIDED — a high follower count under load means stampede protection is working.");

        _nwsRequestDuration = _meter.CreateHistogram<double>(
            "weather.nws.request.duration",
            unit: "ms",
            description: "Wall-clock duration of outbound National Weather Service API calls.");

        _nwsThrottled = _meter.CreateCounter<long>(
            "weather.nws.throttled",
            unit: "{response}",
            description: "HTTP 429 (Too Many Requests) responses observed from NWS.");

        _nwsErrors = _meter.CreateCounter<long>(
            "weather.nws.errors",
            unit: "{response}",
            description: "Failed NWS calls (non-success, non-429: 5xx, timeouts, transport errors).");

        _neighborhoodWarmed = _meter.CreateCounter<long>(
            "weather.neighborhood.warmed",
            unit: "{cell}",
            description: "Neighbouring grid cells warmed by the background service.");

        _nwsNotFoundCached = _meter.CreateCounter<long>(
            "weather.nws.notfound",
            unit: "{cell}",
            description: "NWS forecast 404s remembered as negative cache entries (e.g. MarineForecastNotSupported for marine cells) so the area fan-out and warmer stop re-requesting uncovered cells. Tagged by problem type.");
    }

    public void RecordCacheHit(string cache) => _cacheRequests.Add(
        1,
        new KeyValuePair<string, object?>("cache", cache),
        new KeyValuePair<string, object?>("result", "hit"));

    public void RecordCacheMiss(string cache) => _cacheRequests.Add(
        1,
        new KeyValuePair<string, object?>("cache", cache),
        new KeyValuePair<string, object?>("result", "miss"));

    /// <summary>The caller that actually ran the upstream fetch for a cold key.</summary>
    public void RecordCoalesceLeader(string cache) => _coalescedRequests.Add(
        1,
        new KeyValuePair<string, object?>("cache", cache),
        new KeyValuePair<string, object?>("role", "leader"));

    /// <summary>A caller that shared an in-flight fetch instead of starting its own.</summary>
    public void RecordCoalesceFollower(string cache) => _coalescedRequests.Add(
        1,
        new KeyValuePair<string, object?>("cache", cache),
        new KeyValuePair<string, object?>("role", "follower"));

    public void RecordNwsRequest(string endpoint, int statusCode, double elapsedMs, bool conditional) =>
        _nwsRequestDuration.Record(
            elapsedMs,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("conditional", conditional));

    public void RecordThrottled(string endpoint) =>
        _nwsThrottled.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));

    public void RecordError(string endpoint, int statusCode) => _nwsErrors.Add(
        1,
        new KeyValuePair<string, object?>("endpoint", endpoint),
        new KeyValuePair<string, object?>("status_code", statusCode));

    public void RecordNeighborWarmed(string gridId) =>
        _neighborhoodWarmed.Add(1, new KeyValuePair<string, object?>("grid_id", gridId));

    /// <summary>
    /// A forecast 404 was just remembered in the negative cache. The
    /// <paramref name="problemType"/> (e.g. <c>MarineForecastNotSupported</c>)
    /// is the low-cardinality reason; <c>"unknown"</c> when NWS sent no type.
    /// </summary>
    public void RecordForecastNotFoundCached(string? problemType) => _nwsNotFoundCached.Add(
        1,
        new KeyValuePair<string, object?>("type", problemType ?? "unknown"));

    public void Dispose()
    {
        _meter.Dispose();
        ActivitySource.Dispose();
    }
}
