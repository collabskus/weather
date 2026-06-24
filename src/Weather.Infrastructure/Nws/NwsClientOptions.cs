namespace Weather.Infrastructure.Nws;

/// <summary>
/// Strongly-typed configuration for the NWS client, bound from the
/// <c>"Nws"</c> configuration section.
/// </summary>
public sealed class NwsClientOptions
{
    public const string SectionName = "Nws";

    /// <summary>Base address of the public NWS API.</summary>
    public string BaseUrl { get; set; } = "https://api.weather.gov";

    /// <summary>
    /// NWS REQUIRES a descriptive User-Agent that identifies the application
    /// and a contact. Requests without one are rejected. Override this in
    /// configuration with your own contact details.
    /// See https://www.weather.gov/documentation/services-web-api.
    /// </summary>
    public string UserAgent { get; set; } = "weather-dashboard/1.0 (+https://github.com/collabskus/weather)";

    /// <summary>Fallback TTL for a forecast when NWS sends no usable Cache-Control max-age.</summary>
    public TimeSpan DefaultForecastTtl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How long a coordinate-&gt;grid mapping is trusted. It effectively never changes.</summary>
    public TimeSpan PointMetadataTtl { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Clamp so a misconfigured/odd upstream max-age can't pin stale data for too long.</summary>
    public TimeSpan MaxForecastTtl { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// How long active alerts for a coordinate are trusted before NWS is asked
    /// again. Alerts can appear and clear faster than forecasts, so this is
    /// deliberately short — but long enough that a burst of browser refreshes
    /// collapses into a single upstream <c>/alerts/active</c> call.
    /// </summary>
    public TimeSpan AlertsTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a forecast 404 is remembered as a negative cache entry before
    /// NWS is asked again. A 404 from <c>/gridpoints/.../forecast</c> — for a
    /// marine cell this is <c>MarineForecastNotSupported</c> — is structurally
    /// stable, so this is deliberately longer than <see cref="DefaultForecastTtl"/>
    /// to keep the area fan-out and warmer from re-requesting uncovered cells.
    /// It is still finite so a misrouted/transient 404 self-heals and a cell
    /// NWS later starts covering is picked up. Set to <see cref="TimeSpan.Zero"/>
    /// (or negative) to disable negative caching entirely.
    /// </summary>
    public TimeSpan NotFoundForecastTtl { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Timeout for a SINGLE upstream attempt. Wired into the resilience
    /// pipeline's per-attempt timeout (see <c>DependencyInjection</c>). Kept
    /// modest so one slow/hung NWS response is abandoned quickly and retried
    /// rather than holding a request open for tens of seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Ceiling on the TOTAL time spent on one logical request across all
    /// retries. Wired into the resilience pipeline's total-request timeout, it
    /// is the hard upper bound a caller can ever wait — the backstop that stops
    /// a retry storm from compounding into a multi-minute hang. Must be greater
    /// than <see cref="RequestTimeout"/>.
    /// </summary>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of RETRIES (i.e. attempts beyond the first) the resilience
    /// pipeline makes on a transient failure. Two retries (three attempts total)
    /// is a sensible default for a free public API; raising it risks amplifying
    /// load during an NWS outage.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// Base delay for the exponential, jittered backoff between retries.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);
}
