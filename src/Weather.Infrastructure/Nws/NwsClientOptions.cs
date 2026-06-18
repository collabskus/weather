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

    /// <summary>Per-request timeout applied by the resilience pipeline.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
}
