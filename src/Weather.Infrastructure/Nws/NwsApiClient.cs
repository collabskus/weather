using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Serialization;

namespace Weather.Infrastructure.Nws;

/// <summary>
/// Typed <see cref="HttpClient"/> over the NWS API. Owns HTTP concerns,
/// conditional GETs and telemetry; it performs NO caching (that is the job of
/// the caches and <c>WeatherService</c>). The <see cref="HttpClient"/> itself —
/// base address, User-Agent, Accept header and the resilience pipeline — is
/// configured in <c>DependencyInjection</c>.
/// </summary>
internal sealed class NwsApiClient(
    HttpClient httpClient,
    WeatherTelemetry telemetry,
    ILogger<NwsApiClient> logger) : INwsApiClient
{
    private const string PointsEndpoint = "points";
    private const string ForecastEndpoint = "forecast";
    private const string HourlyEndpoint = "forecast/hourly";
    private const string StationsEndpoint = "stations";
    private const string ObservationEndpoint = "observation";
    private const string AlertsEndpoint = "alerts";

    // Hourly forecasts run ~156 periods; the dashboard only needs the near term,
    // and capping keeps the cached payload (and the page) reasonable per cell.
    private const int HourlyPeriodCap = 24;

    public async Task<PointMetadata?> GetPointMetadataAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var requestUri = $"points/{coordinate.ToApiString()}";
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            RecordDuration(PointsEndpoint, (int)response.StatusCode, timestamp, conditional: false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("NWS has no grid coverage for {Coordinate}.", coordinate);
                }

                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure(PointsEndpoint, response.StatusCode);
                logger.LogWarning("NWS /points returned {StatusCode} for {Coordinate}.",
                    (int)response.StatusCode, coordinate);
                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NwsPointResponse>(WeatherJson.Options, cancellationToken)
                .ConfigureAwait(false);

            var properties = payload?.Properties;
            if (properties?.GridId is null)
            {
                logger.LogWarning("NWS /points payload for {Coordinate} was missing grid metadata.", coordinate);
                return null;
            }

            var location = properties.RelativeLocation?.Properties;
            return new PointMetadata(
                Query: coordinate,
                Grid: new GridPoint(properties.GridId, properties.GridX, properties.GridY),
                ForecastUrl: properties.Forecast ?? string.Empty,
                ForecastHourlyUrl: properties.ForecastHourly ?? string.Empty,
                City: location?.City,
                State: location?.State,
                TimeZone: properties.TimeZone,
                RadarStation: properties.RadarStation);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            RecordFailure(PointsEndpoint, statusCode: null);
            logger.LogWarning(ex, "NWS /points request failed for {Coordinate}.", coordinate);
            return null;
        }
    }

    public async Task<ForecastFetchResult> GetForecastAsync(
        GridPoint grid, string? etag = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.Create(
            CultureInfo.InvariantCulture,
            $"gridpoints/{grid.GridId}/{grid.GridX},{grid.GridY}/forecast");

        var conditional = !string.IsNullOrEmpty(etag);
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (conditional)
            {
                // Use TryAddWithoutValidation so weak ("W/...") and quoted tags
                // round-trip exactly as NWS sent them.
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            RecordDuration(ForecastEndpoint, (int)response.StatusCode, timestamp, conditional);

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotModified:
                    return ForecastFetchResult.NotModified(
                        response.Headers.ETag?.ToString() ?? etag,
                        response.Headers.CacheControl?.MaxAge);

                case HttpStatusCode.NotFound:
                    {
                        // Capture the RFC 7807 reason (e.g. MarineForecastNotSupported)
                        // so the caller can remember and surface WHY this cell is
                        // uncovered. Best-effort: a missing/non-JSON body yields null.
                        var problem = await TryReadProblemAsync(response, cancellationToken).ConfigureAwait(false);

                        if (logger.IsEnabled(LogLevel.Information))
                        {
                            logger.LogInformation(
                                "NWS has no forecast for grid {Grid} ({Problem}).",
                                grid, problem?.Title ?? problem?.TypeName ?? "404");
                        }

                        return ForecastFetchResult.NotFound(problem);
                    }

                case HttpStatusCode.TooManyRequests:
                    telemetry.RecordThrottled(ForecastEndpoint);
                    logger.LogWarning("NWS throttled the forecast request for grid {Grid} (HTTP 429).", grid);
                    return ForecastFetchResult.Unavailable;
            }

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure(ForecastEndpoint, response.StatusCode);
                logger.LogWarning("NWS /forecast returned {StatusCode} for grid {Grid}.",
                    (int)response.StatusCode, grid);
                return ForecastFetchResult.Unavailable;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NwsForecastResponse>(WeatherJson.Options, cancellationToken)
                .ConfigureAwait(false);

            if (payload?.Properties is not { } properties)
            {
                logger.LogWarning("NWS /forecast payload for grid {Grid} was empty.", grid);
                return ForecastFetchResult.Unavailable;
            }

            var periods = MapPeriods(properties.Periods);
            var forecast = new Forecast(grid, properties.GeneratedAt, properties.UpdateTime, periods);
            var center = TryComputePolygonCenter(payload.Geometry);

            return ForecastFetchResult.Success(
                forecast,
                response.Headers.ETag?.ToString(),
                response.Headers.CacheControl?.MaxAge,
                center);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            RecordFailure(ForecastEndpoint, statusCode: null);
            logger.LogWarning(ex, "NWS /forecast request failed for grid {Grid}.", grid);
            return ForecastFetchResult.Unavailable;
        }
    }

    public async Task<IReadOnlyList<ForecastPeriod>> GetHourlyForecastAsync(
        GridPoint grid, CancellationToken cancellationToken = default)
    {
        var requestUri = string.Create(
            CultureInfo.InvariantCulture,
            $"gridpoints/{grid.GridId}/{grid.GridX},{grid.GridY}/forecast/hourly");

        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            RecordDuration(HourlyEndpoint, (int)response.StatusCode, timestamp, conditional: false);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is not HttpStatusCode.NotFound)
                {
                    RecordFailure(HourlyEndpoint, response.StatusCode);
                }

                return [];
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NwsForecastResponse>(WeatherJson.Options, cancellationToken)
                .ConfigureAwait(false);

            var periods = MapPeriods(payload?.Properties?.Periods);
            return periods.Count > HourlyPeriodCap ? periods.Take(HourlyPeriodCap).ToList() : periods;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            RecordFailure(HourlyEndpoint, statusCode: null);
            logger.LogWarning(ex, "NWS /forecast/hourly request failed for grid {Grid}.", grid);
            return [];
        }
    }

    public async Task<string?> GetNearestStationIdAsync(GridPoint grid, CancellationToken cancellationToken = default)
    {
        var requestUri = string.Create(
            CultureInfo.InvariantCulture,
            $"gridpoints/{grid.GridId}/{grid.GridX},{grid.GridY}/stations");

        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            RecordDuration(StationsEndpoint, (int)response.StatusCode, timestamp, conditional: false);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is not HttpStatusCode.NotFound)
                {
                    RecordFailure(StationsEndpoint, response.StatusCode);
                }

                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NwsStationsResponse>(WeatherJson.Options, cancellationToken)
                .ConfigureAwait(false);

            // Stations are returned nearest-first.
            var features = payload?.Features;
            if (features is null)
            {
                return null;
            }

            foreach (var feature in features)
            {
                var id = feature.Properties?.StationIdentifier;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            RecordFailure(StationsEndpoint, statusCode: null);
            logger.LogWarning(ex, "NWS /stations request failed for grid {Grid}.", grid);
            return null;
        }
    }

    public async Task<Observation?> GetLatestObservationAsync(
        GridPoint grid, CancellationToken cancellationToken = default)
    {
        // Two-step convenience path: resolve the nearest station, then observe
        // by id. Callers that already know the station should skip straight to
        // the by-id overload to avoid the extra /stations request.
        var stationId = await GetNearestStationIdAsync(grid, cancellationToken).ConfigureAwait(false);
        if (stationId is null)
        {
            return null;
        }

        return await GetLatestObservationAsync(grid, stationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Observation?> GetLatestObservationAsync(
        GridPoint grid, string stationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stationId);

        var requestUri = $"stations/{Uri.EscapeDataString(stationId)}/observations/latest";
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            RecordDuration(ObservationEndpoint, (int)response.StatusCode, timestamp, conditional: false);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is not HttpStatusCode.NotFound)
                {
                    RecordFailure(ObservationEndpoint, response.StatusCode);
                }

                return null;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NwsObservationResponse>(WeatherJson.Options, cancellationToken)
                .ConfigureAwait(false);

            if (payload?.Properties is not { } p)
            {
                return null;
            }

            return new Observation(
                StationId: stationId,
                StationName: null,
                Timestamp: p.Timestamp,
                TextDescription: string.IsNullOrWhiteSpace(p.TextDescription) ? null : p.TextDescription,
                Icon: p.Icon,
                TemperatureF: NwsUnits.CelsiusToFahrenheit(p.Temperature?.Value),
                DewpointF: NwsUnits.CelsiusToFahrenheit(p.Dewpoint?.Value),
                RelativeHumidity: NwsUnits.RoundToInt(p.RelativeHumidity?.Value),
                WindSpeedMph: WindToMph(p.WindSpeed),
                WindGustMph: WindToMph(p.WindGust),
                WindDirection: NwsUnits.DegreesToCompass(p.WindDirection?.Value),
                PressureInHg: NwsUnits.PascalsToInHg(p.BarometricPressure?.Value),
                VisibilityMiles: NwsUnits.MetersToMiles(p.Visibility?.Value));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            RecordFailure(ObservationEndpoint, statusCode: null);
            logger.LogWarning(ex, "NWS latest-observation request failed for station {Station} (grid {Grid}).",
                stationId, grid);
            return null;
        }
    }

    public async Task<IReadOnlyList<WeatherAlert>> GetActiveAlertsAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var requestUri = $"alerts/active?point={coordinate.ToApiString()}";
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            RecordDuration(AlertsEndpoint, (int)response.StatusCode, timestamp, conditional: false);

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure(AlertsEndpoint, response.StatusCode);
                return [];
            }

            var payload = await response.Content
                .ReadFromJsonAsync<NwsAlertsResponse>(WeatherJson.Options, cancellationToken)
                .ConfigureAwait(false);

            var features = payload?.Features;
            if (features is null || features.Count == 0)
            {
                return [];
            }

            var alerts = new List<WeatherAlert>(features.Count);
            foreach (var feature in features)
            {
                if (feature.Properties is not { } a || string.IsNullOrWhiteSpace(a.Event))
                {
                    continue;
                }

                alerts.Add(new WeatherAlert(
                    Id: a.Id ?? Guid.NewGuid().ToString("N"),
                    Event: a.Event,
                    Severity: a.Severity,
                    Certainty: a.Certainty,
                    Urgency: a.Urgency,
                    Headline: a.Headline,
                    Description: a.Description,
                    Instruction: a.Instruction,
                    AreaDescription: a.AreaDesc,
                    SenderName: a.SenderName,
                    Effective: a.Effective,
                    Onset: a.Onset,
                    Expires: a.Expires,
                    Ends: a.Ends));
            }

            return alerts;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            RecordFailure(AlertsEndpoint, statusCode: null);
            logger.LogWarning(ex, "NWS /alerts/active request failed for {Coordinate}.", coordinate);
            return [];
        }
    }

    private static List<ForecastPeriod> MapPeriods(IReadOnlyList<NwsPeriod>? source)
    {
        source ??= [];
        var periods = new List<ForecastPeriod>(source.Count);
        foreach (var p in source)
        {
            periods.Add(new ForecastPeriod(
                Number: p.Number,
                Name: p.Name ?? string.Empty,
                StartTime: p.StartTime,
                EndTime: p.EndTime,
                IsDaytime: p.IsDaytime,
                Temperature: p.Temperature,
                TemperatureUnit: p.TemperatureUnit ?? "F",
                ProbabilityOfPrecipitation: p.ProbabilityOfPrecipitation?.Value is { } pop
                    ? (int)Math.Round(pop, MidpointRounding.AwayFromZero)
                    : null,
                WindSpeed: p.WindSpeed ?? string.Empty,
                WindDirection: p.WindDirection ?? string.Empty,
                ShortForecast: p.ShortForecast ?? string.Empty,
                DetailedForecast: p.DetailedForecast ?? string.Empty,
                Icon: p.Icon ?? string.Empty));
        }

        return periods;
    }

    /// <summary>
    /// Observation wind speed is usually km/h (<c>wmoUnit:km_h-1</c>) but can be
    /// m/s; convert based on the reported unit code.
    /// </summary>
    private static int? WindToMph(NwsQuantitativeValue? value)
    {
        if (value?.Value is not { } v)
        {
            return null;
        }

        var unit = value.UnitCode ?? string.Empty;
        if (unit.Contains("m_s-1", StringComparison.OrdinalIgnoreCase))
        {
            return (int)Math.Round(v * 2.236936d, MidpointRounding.AwayFromZero);
        }

        return NwsUnits.KmhToMph(v);
    }

    /// <summary>
    /// Approximate a polygon's centre by averaging the vertices of its outer
    /// ring. NWS forecast geometry is GeoJSON <c>[[[lon,lat], ...]]</c>.
    /// </summary>
    private static GeoCoordinate? TryComputePolygonCenter(NwsGeometry? geometry)
    {
        if (geometry?.Coordinates is not { ValueKind: JsonValueKind.Array } coords)
        {
            return null;
        }

        try
        {
            if (coords.GetArrayLength() == 0)
            {
                return null;
            }

            var ring = coords[0];
            if (ring.ValueKind != JsonValueKind.Array || ring.GetArrayLength() == 0)
            {
                return null;
            }

            double sumLat = 0;
            double sumLon = 0;
            var count = 0;

            foreach (var point in ring.EnumerateArray())
            {
                if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2)
                {
                    continue;
                }

                var lon = point[0].GetDouble();
                var lat = point[1].GetDouble();
                sumLon += lon;
                sumLat += lat;
                count++;
            }

            if (count == 0)
            {
                return null;
            }

            var avgLat = sumLat / count;
            var avgLon = sumLon / count;
            return GeoCoordinate.IsValid(avgLat, avgLon) ? new GeoCoordinate(avgLat, avgLon) : null;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Best-effort parse of the RFC 7807 problem document NWS returns on a 404
    /// (e.g. <c>MarineForecastNotSupported</c>). Returns <c>null</c> when the
    /// body is absent, empty or not parseable — a malformed or non-JSON error
    /// body must never turn a 404 into a throw. Cancellation propagates.
    /// </summary>
    private static async Task<NwsProblem?> TryReadProblemAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return null;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            var dto = JsonSerializer.Deserialize<NwsProblemResponse>(body, WeatherJson.Options);
            if (dto is null ||
                (dto.Type is null && dto.Title is null && dto.Status is null &&
                 dto.Detail is null && dto.CorrelationId is null))
            {
                return null;
            }

            return new NwsProblem(dto.Type, dto.Title, dto.Status, dto.Detail, dto.CorrelationId);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException or IOException)
        {
            return null;
        }
    }

    private void RecordDuration(string endpoint, int statusCode, long startTimestamp, bool conditional) =>
        telemetry.RecordNwsRequest(
            endpoint,
            statusCode,
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            conditional);

    private void RecordFailure(string endpoint, HttpStatusCode? statusCode) =>
        telemetry.RecordError(endpoint, statusCode is null ? 0 : (int)statusCode);
}
