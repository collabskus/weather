using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace Weather.Web.Services;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper for the Weather API. The base address
/// is configured in <c>Program.cs</c> (resolved by Aspire service discovery, or
/// from <c>WeatherApi:BaseUrl</c> when running standalone). A 404 is mapped to
/// <c>null</c> rather than an exception, because "no NWS coverage here" is an
/// expected, user-facing outcome — not a fault.
/// </summary>
public sealed class WeatherApiClient(HttpClient httpClient) : IWeatherApiClient
{
    public async Task<ForecastDto?> GetForecastAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildUri("/api/forecast", latitude, longitude);

        using var response = await httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ForecastDto>(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<NeighborhoodDto?> GetNeighborhoodAsync(
        double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var requestUri = BuildUri("/api/forecast/neighborhood", latitude, longitude);

        using var response = await httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<NeighborhoodDto>(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildUri(string path, double latitude, double longitude)
    {
        // Format invariantly so the decimal separator is always '.', regardless
        // of the server's culture. Latitude/longitude ranges never trip
        // scientific notation, so the default round-trippable form is safe.
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        return $"{path}?latitude={lat}&longitude={lon}";
    }
}
