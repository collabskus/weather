using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Nws;
using Weather.Infrastructure.Tests.Fakes;

namespace Weather.Infrastructure.Tests.Nws;

public sealed class NwsApiClientExtraTests
{
    private static readonly GeoCoordinate SampleCoordinate = new(37.0879, -76.4505);
    private static readonly GridPoint SampleGrid = new("AKQ", 83, 61);

    private static NwsApiClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.weather.gov/") };
        return new NwsApiClient(httpClient, new WeatherTelemetry(), NullLogger<NwsApiClient>.Instance);
    }

    /// <summary>Route each NWS path to its canned payload (handles the two-step observation flow).</summary>
    private static StubHttpMessageHandler RoutingHandler() => new(request =>
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = path switch
        {
            _ when path.EndsWith("/forecast/hourly", StringComparison.Ordinal) => NwsPayloads.Hourly,
            _ when path.EndsWith("/stations", StringComparison.Ordinal) => NwsPayloads.Stations,
            _ when path.Contains("/observations/latest", StringComparison.Ordinal) => NwsPayloads.Observation,
            _ when path.StartsWith("/alerts/active", StringComparison.Ordinal) => NwsPayloads.Alerts,
            _ when path.EndsWith("/forecast", StringComparison.Ordinal) => NwsPayloads.ForecastWithGeometry,
            _ => NwsPayloads.Points,
        };

        return StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, body);
    });

    [Test]
    public async Task GetHourlyForecastParsesPeriods()
    {
        var client = CreateClient(RoutingHandler());

        var hourly = await client.GetHourlyForecastAsync(SampleGrid);

        hourly.Count.ShouldBe(2);
        hourly[0].Temperature.ShouldBe(90);
        hourly[1].Temperature.ShouldBe(89);
    }

    [Test]
    public async Task GetHourlyForecastReturnsEmptyOn404()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        (await client.GetHourlyForecastAsync(SampleGrid)).ShouldBeEmpty();
    }

    [Test]
    public async Task GetNearestStationIdReturnsTheNearestStation()
    {
        var client = CreateClient(RoutingHandler());

        var stationId = await client.GetNearestStationIdAsync(SampleGrid);

        // The stations payload lists KPHF first (nearest), then KLFI.
        stationId.ShouldBe("KPHF");
    }

    [Test]
    public async Task GetNearestStationIdReturnsNullWhenNoStations()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, """{ "features": [] }"""));
        var client = CreateClient(handler);

        (await client.GetNearestStationIdAsync(SampleGrid)).ShouldBeNull();
    }

    [Test]
    public async Task GetLatestObservationByIdSkipsTheStationLookup()
    {
        var handler = RoutingHandler();
        var client = CreateClient(handler);

        var observation = await client.GetLatestObservationAsync(SampleGrid, "KPHF");

        observation.ShouldNotBeNull();
        observation!.StationId.ShouldBe("KPHF");
        observation.TemperatureF.ShouldBe(88);

        // The by-id overload must hit /observations/latest directly and NEVER
        // list the cell's stations — that is the whole point of caching the id.
        handler.Requests.ShouldContain(r =>
            r.RequestUri!.AbsolutePath.Contains("/observations/latest", StringComparison.Ordinal));
        handler.Requests.ShouldNotContain(r =>
            r.RequestUri!.AbsolutePath.EndsWith("/stations", StringComparison.Ordinal));
    }

    [Test]
    public async Task GetLatestObservationFollowsStationsThenConvertsUnits()
    {
        var client = CreateClient(RoutingHandler());

        var observation = await client.GetLatestObservationAsync(SampleGrid);

        observation.ShouldNotBeNull();
        observation!.StationId.ShouldBe("KPHF");
        observation.TemperatureF.ShouldBe(88);    // 31.1 degC
        observation.DewpointF.ShouldBe(70);       // 21.0 degC
        observation.RelativeHumidity.ShouldBe(55);
        observation.WindDirection.ShouldBe("S");  // 180 degrees
        observation.WindSpeedMph.ShouldBe(8);     // 13 km/h
        observation.WindGustMph.ShouldBe(15);     // 24.1 km/h
        observation.PressureInHg!.Value.ShouldBe(30.06, tolerance: 0.05);  // 101800 Pa
        observation.VisibilityMiles!.Value.ShouldBe(10.0, tolerance: 0.1); // 16093 m
    }

    [Test]
    public async Task GetLatestObservationReturnsNullWhenNoStations()
    {
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/stations", StringComparison.Ordinal)
                ? StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, """{ "features": [] }""")
                : StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.Observation));
        var client = CreateClient(handler);

        (await client.GetLatestObservationAsync(SampleGrid)).ShouldBeNull();
    }

    [Test]
    public async Task GetActiveAlertsParsesAnAdvisory()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.Alerts));
        var client = CreateClient(handler);

        var alerts = await client.GetActiveAlertsAsync(SampleCoordinate);

        alerts.Count.ShouldBe(1);
        alerts[0].Event.ShouldBe("Heat Advisory");
        alerts[0].Severity.ShouldBe("Moderate");
        alerts[0].Instruction.ShouldBe("Drink plenty of fluids.");
    }

    [Test]
    public async Task GetActiveAlertsReturnsEmptyWhenNone()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.NoAlerts));
        var client = CreateClient(handler);

        (await client.GetActiveAlertsAsync(SampleCoordinate)).ShouldBeEmpty();
    }

    [Test]
    public async Task GetForecastComputesCellCentreFromGeometry()
    {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.GeoJson(HttpStatusCode.OK, NwsPayloads.ForecastWithGeometry));
        var client = CreateClient(handler);

        var result = await client.GetForecastAsync(SampleGrid);

        result.Center.ShouldNotBeNull();
        result.Center!.Value.Latitude.ShouldBe(37.088, tolerance: 0.001);
        result.Center.Value.Longitude.ShouldBe(-76.452, tolerance: 0.001);
    }
}
