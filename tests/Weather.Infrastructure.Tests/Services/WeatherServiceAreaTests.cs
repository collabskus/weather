using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Core.Abstractions;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Nws;
using Weather.Infrastructure.Services;
using Weather.Infrastructure.Tests.Fakes;

namespace Weather.Infrastructure.Tests.Services;

public sealed class WeatherServiceAreaTests
{
    private static readonly GeoCoordinate Coord = new(37.0879, -76.4505);
    private static readonly GridPoint Grid = new("AKQ", 83, 61);
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    private const string StationId = "KPHF";

    private static PointMetadata Meta() =>
        new(Coord, Grid, "forecast-url", "hourly-url", "Bethel Manor", "VA", "America/New_York", "KAKQ");

    private static Forecast ForecastFor(GridPoint grid) =>
        new(grid, Now, Now, new[]
        {
            new ForecastPeriod(1, "This Afternoon", Now, Now.AddHours(4), true, 90, "F", 3,
                "5 mph", "S", "Sunny", "Detailed.", "icon"),
            new ForecastPeriod(2, "Tonight", Now.AddHours(4), Now.AddHours(16), false, 74, "F", null,
                "12 mph", "S", "Mostly Clear", "Detailed.", "icon"),
        });

    // A deterministic, distinct centre per cell so distance ordering is testable:
    // cells further from the origin in grid space map to centres further from Coord.
    private static GeoCoordinate CenterFor(GridPoint grid) =>
        new(37.0 + (grid.GridY * 0.01), -76.0 - (grid.GridX * 0.01));

    // Return the concrete array type (CA1859): the method is private and every
    // caller is fine with ForecastPeriod[], which still satisfies the
    // IReadOnlyList<ForecastPeriod> parameters it is passed to.
    private static ForecastPeriod[] HourlyTwo() => new[]
    {
        new ForecastPeriod(1, string.Empty, Now, Now.AddHours(1), true, 90, "F", 2,
            "6 mph", "S", "Sunny", string.Empty, "icon"),
        new ForecastPeriod(2, string.Empty, Now.AddHours(1), Now.AddHours(2), true, 89, "F", 2,
            "7 mph", "S", "Sunny", string.Empty, "icon"),
    };

    private static Observation ObservationSample() =>
        new(StationId, "Newport News", Now, "Sunny", "icon", 88, 70, 55, 8, 15, "S", 30.06, 10.0);

    private static WeatherAlert AlertSample() =>
        new("id-1", "Heat Advisory", "Moderate", "Likely", "Expected", "Heat Advisory in effect",
            "Hot.", "Drink fluids.", "Hampton", "NWS Wakefield VA", Now, Now, Now.AddHours(6), Now.AddHours(6));

    private sealed class Harness
    {
        public IPointMetadataCache MetadataCache { get; } = Substitute.For<IPointMetadataCache>();
        public IForecastCache ForecastCache { get; } = Substitute.For<IForecastCache>();
        public ICellExtrasCache ExtrasCache { get; } = Substitute.For<ICellExtrasCache>();
        public IAlertCache AlertCache { get; } = Substitute.For<IAlertCache>();
        public INwsApiClient Nws { get; } = Substitute.For<INwsApiClient>();
        public INeighborhoodWarmer Warmer { get; } = Substitute.For<INeighborhoodWarmer>();

        // REAL coalescers: their logic is pure and in-process, so the area hot
        // path runs through them exactly as in production while these tests keep
        // asserting cache-aside behaviour. Forecast, extras, metadata and alert
        // flights are independent, matching the production registrations.
        public IRequestCoalescer<GridPoint> ForecastCoalescer { get; } = new RequestCoalescer<GridPoint>();
        public IRequestCoalescer<GridPoint> ExtrasCoalescer { get; } = new RequestCoalescer<GridPoint>();
        public IRequestCoalescer<GeoCoordinate> MetadataCoalescer { get; } = new RequestCoalescer<GeoCoordinate>();
        public IRequestCoalescer<string> AlertCoalescer { get; } = new RequestCoalescer<string>();

        public WeatherService Service { get; }

        public Harness()
        {
            Service = new WeatherService(
                MetadataCache, ForecastCache, ExtrasCache, AlertCache, Nws, Warmer,
                ForecastCoalescer, ExtrasCoalescer, MetadataCoalescer, AlertCoalescer,
                new WeatherTelemetry(), new MutableTimeProvider(Now),
                Options.Create(new NwsClientOptions()), NullLogger<WeatherService>.Instance);
        }

        public void ResolvesMetadata() =>
            Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(Meta());

        public void AllDailyFetchesSucceed() =>
            Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var grid = ci.Arg<GridPoint>();
                    return ForecastFetchResult.Success(ForecastFor(grid), "\"e\"", TimeSpan.FromMinutes(30), CenterFor(grid));
                });

        public void ExtrasAlwaysMiss() =>
            ExtrasCache.GetAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>())
                .Returns((CachedCellExtras?)null);

        // The extras path now resolves the nearest station once and observes by
        // id, so stub BOTH the station lookup and the by-id observation.
        public void HourlyAndObservationAvailable()
        {
            Nws.GetHourlyForecastAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>()).Returns(HourlyTwo());
            Nws.GetNearestStationIdAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>()).Returns(StationId);
            Nws.GetLatestObservationAsync(Arg.Any<GridPoint>(), StationId, Arg.Any<CancellationToken>())
                .Returns(ObservationSample());
        }

        public void HourlyAndObservationAndAlertsAvailable()
        {
            HourlyAndObservationAvailable();
            Nws.GetActiveAlertsAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
                .Returns(new[] { AlertSample() });
        }
    }

    [Test]
    public async Task GetAreaAssemblesPrimaryAllNeighboursHourlyObservationAndAlerts()
    {
        var h = new Harness();
        h.ResolvesMetadata();
        h.AllDailyFetchesSucceed();
        h.ExtrasAlwaysMiss();
        h.HourlyAndObservationAndAlertsAvailable();

        var area = await h.Service.GetAreaForecastAsync(Coord);

        area.ShouldNotBeNull();
        area!.Primary.Grid.ShouldBe(Grid);
        area.Primary.Daily.Periods.Count.ShouldBe(2);
        area.Primary.Hourly.Count.ShouldBe(2);
        area.Primary.Observation.ShouldNotBeNull();
        area.Primary.Observation!.TemperatureF.ShouldBe(88);

        // The 3x3 ring around the origin has eight neighbours.
        area.Neighbors.Count.ShouldBe(8);
        area.Alerts.Count.ShouldBe(1);
        area.Alerts[0].Event.ShouldBe("Heat Advisory");

        h.Warmer.Received(1).RequestWarming(Grid);
    }

    [Test]
    public async Task GetAreaOrdersNeighboursByDistanceFromTheUser()
    {
        var h = new Harness();
        h.ResolvesMetadata();
        h.AllDailyFetchesSucceed();
        h.ExtrasAlwaysMiss();
        h.HourlyAndObservationAndAlertsAvailable();

        var area = await h.Service.GetAreaForecastAsync(Coord);

        area.ShouldNotBeNull();
        var distances = area!.Neighbors
            .Where(n => n.DistanceMeters is not null)
            .Select(n => n.DistanceMeters!.Value)
            .ToList();

        distances.Count.ShouldBe(8);
        distances.ShouldBe(distances.OrderBy(d => d).ToList());
    }

    [Test]
    public async Task GetAreaServesExtrasFromCacheWithoutRefetchingHourly()
    {
        var h = new Harness();
        h.ResolvesMetadata();
        h.AllDailyFetchesSucceed();
        h.Nws.GetActiveAlertsAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<WeatherAlert>());

        // Every cell already has fresh extras cached.
        var cached = new CachedCellExtras(
            new CellExtras(HourlyTwo(), ObservationSample(), CenterFor(Grid), StationId),
            Now, Now.AddMinutes(30));
        h.ExtrasCache.GetAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>()).Returns(cached);

        var area = await h.Service.GetAreaForecastAsync(Coord);

        area.ShouldNotBeNull();
        area!.Primary.Hourly.Count.ShouldBe(2);

        // A fresh extras hit means ZERO upstream extras work — no hourly fetch
        // and no station lookup, no matter how many cells are assembled.
        await h.Nws.DidNotReceive().GetHourlyForecastAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>());
        await h.Nws.DidNotReceive().GetNearestStationIdAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>());
        await h.Nws.DidNotReceive().GetLatestObservationAsync(
            Arg.Any<GridPoint>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAreaReusesCachedStationIdAndSkipsStationLookupOnRefresh()
    {
        var h = new Harness();
        h.ResolvesMetadata();
        h.AllDailyFetchesSucceed();
        h.Nws.GetActiveAlertsAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<WeatherAlert>());

        // Every cell has an EXPIRED extras entry that still remembers its
        // nearest station. A refresh must reuse that id rather than re-listing
        // stations, since the cell→station mapping is effectively immutable.
        var expired = new CachedCellExtras(
            new CellExtras(HourlyTwo(), ObservationSample(), CenterFor(Grid), StationId),
            Now.AddMinutes(-60), Now.AddMinutes(-30));
        h.ExtrasCache.GetAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>()).Returns(expired);

        h.Nws.GetHourlyForecastAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>()).Returns(HourlyTwo());
        h.Nws.GetLatestObservationAsync(Arg.Any<GridPoint>(), StationId, Arg.Any<CancellationToken>())
            .Returns(ObservationSample());
        // Deliberately NOT stubbing GetNearestStationIdAsync: it must not be called.

        var area = await h.Service.GetAreaForecastAsync(Coord);

        area.ShouldNotBeNull();
        area!.Primary.Observation.ShouldNotBeNull();
        area.Primary.Observation!.TemperatureF.ShouldBe(88);

        await h.Nws.DidNotReceive().GetNearestStationIdAsync(Arg.Any<GridPoint>(), Arg.Any<CancellationToken>());
        await h.Nws.Received().GetLatestObservationAsync(Arg.Any<GridPoint>(), StationId, Arg.Any<CancellationToken>());

        // Refreshed extras were written back (for every assembled cell).
        await h.ExtrasCache.Received().UpsertAsync(
            Arg.Any<GridPoint>(), Arg.Any<CachedCellExtras>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAreaReturnsNullWhenLocationUncovered()
    {
        var h = new Harness();
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((PointMetadata?)null);

        (await h.Service.GetAreaForecastAsync(Coord)).ShouldBeNull();
    }

    [Test]
    public async Task GetAreaWithRadiusTwoReturnsTwentyFourNeighbours()
    {
        var h = new Harness();
        h.ResolvesMetadata();
        h.AllDailyFetchesSucceed();
        h.ExtrasAlwaysMiss();
        h.HourlyAndObservationAndAlertsAvailable();

        var area = await h.Service.GetAreaForecastAsync(Coord, radius: 2);

        area.ShouldNotBeNull();
        area!.Neighbors.Count.ShouldBe(24);
    }

    [Test]
    public async Task GetAreaOnAlertCacheMissFetchesOnceAndStores()
    {
        var h = new Harness();
        h.ResolvesMetadata();
        h.AllDailyFetchesSucceed();
        h.ExtrasAlwaysMiss();
        h.HourlyAndObservationAndAlertsAvailable();

        // Alert cache is cold for this coordinate.
        h.AlertCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((CachedAlerts?)null);

        var area = await h.Service.GetAreaForecastAsync(Coord);

        area.ShouldNotBeNull();
        area!.Alerts.Count.ShouldBe(1);

        // Fetched exactly once and the result was written back to the cache.
        await h.Nws.Received(1).GetActiveAlertsAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
        await h.AlertCache.Received(1).UpsertAsync(
            Arg.Any<GeoCoordinate>(), Arg.Any<CachedAlerts>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAreaServesAlertsFromCacheWithoutHittingNws()
    {
        var h = new Harness();
        h.ResolvesMetadata();
        h.AllDailyFetchesSucceed();
        h.ExtrasAlwaysMiss();
        h.HourlyAndObservationAvailable();

        // A FRESH alert entry already cached for this coordinate.
        var cachedAlerts = new CachedAlerts(new[] { AlertSample() }, Now, Now.AddMinutes(5));
        h.AlertCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(cachedAlerts);

        var area = await h.Service.GetAreaForecastAsync(Coord);

        area.ShouldNotBeNull();
        area!.Alerts.Count.ShouldBe(1);
        area.Alerts[0].Event.ShouldBe("Heat Advisory");

        // The whole point of the fix: a fresh cache entry means ZERO calls to
        // the /alerts/active endpoint, no matter how many times the page loads.
        await h.Nws.DidNotReceive().GetActiveAlertsAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
        await h.AlertCache.DidNotReceive().UpsertAsync(
            Arg.Any<GeoCoordinate>(), Arg.Any<CachedAlerts>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetAreaCachesEmptyAlertResultToAvoidRefetching()
    {
        var h = new Harness();
        h.ResolvesMetadata();
        h.AllDailyFetchesSucceed();
        h.ExtrasAlwaysMiss();
        h.HourlyAndObservationAvailable();

        h.AlertCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((CachedAlerts?)null);
        // No active alerts (and/or NWS best-effort empty).
        h.Nws.GetActiveAlertsAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<WeatherAlert>());

        var area = await h.Service.GetAreaForecastAsync(Coord);

        area.ShouldNotBeNull();
        area!.Alerts.Count.ShouldBe(0);

        // "No alerts" is itself cached, so the next view will not re-hit NWS.
        await h.AlertCache.Received(1).UpsertAsync(
            Arg.Any<GeoCoordinate>(), Arg.Any<CachedAlerts>(), Arg.Any<CancellationToken>());
    }
}
