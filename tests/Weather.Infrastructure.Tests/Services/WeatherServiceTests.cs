using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Core.Abstractions;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Nws;
using Weather.Infrastructure.Services;
using Weather.Infrastructure.Tests.Fakes;

namespace Weather.Infrastructure.Tests.Services;

public sealed class WeatherServiceTests
{
    private static readonly GeoCoordinate Coord = new(37.0879, -76.4505);
    private static readonly GridPoint Grid = new("AKQ", 83, 61);
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    private static PointMetadata Meta() =>
        new(Coord, Grid, "forecast-url", "hourly-url", "Bethel Manor", "VA", "America/New_York", "KAKQ");

    private static Forecast ForecastFor(GridPoint grid) =>
        new(grid, Now, Now, new[]
        {
            new ForecastPeriod(1, "This Afternoon", Now, Now.AddHours(4), true, 90, "F", 3,
                "5 mph", "S", "Sunny", "Detailed.", "icon"),
        });

    private static CachedForecast FreshForecast(GridPoint grid) =>
        new(ForecastFor(grid), "\"e\"", Now, Now.AddHours(1));

    private static CachedForecast StaleForecast(GridPoint grid) =>
        new(ForecastFor(grid), "\"e\"", Now.AddHours(-2), Now.AddHours(-1));

    private static CachedPointMetadata FreshMeta() => new(Meta(), Now, Now.AddDays(30));

    private static CachedPointMetadata StaleMeta() => new(Meta(), Now.AddDays(-60), Now.AddDays(-30));

    private sealed class Harness
    {
        public IPointMetadataCache MetadataCache { get; } = Substitute.For<IPointMetadataCache>();
        public IForecastCache ForecastCache { get; } = Substitute.For<IForecastCache>();
        public ICellExtrasCache ExtrasCache { get; } = Substitute.For<ICellExtrasCache>();
        public INwsApiClient Nws { get; } = Substitute.For<INwsApiClient>();
        public INeighborhoodWarmer Warmer { get; } = Substitute.For<INeighborhoodWarmer>();

        // A REAL coalescer — its logic is pure and in-process, so wiring it in
        // keeps the existing behavioural tests honest (the hot path now always
        // runs through it) while still letting the stampede test exercise it.
        public IRequestCoalescer<GridPoint> Coalescer { get; } = new RequestCoalescer<GridPoint>();

        public WeatherService Service { get; }

        public Harness()
        {
            Service = new WeatherService(
                MetadataCache, ForecastCache, ExtrasCache, Nws, Warmer, Coalescer,
                new WeatherTelemetry(), new MutableTimeProvider(Now),
                Options.Create(new NwsClientOptions()), NullLogger<WeatherService>.Instance);
        }
    }

    [Test]
    public async Task GetForecastOnFullMissFetchesStoresAndSchedulesWarming()
    {
        var h = new Harness();
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(Meta());
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30)));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        result!.Grid.ShouldBe(Grid);
        await h.MetadataCache.Received(1).UpsertAsync(Arg.Any<CachedPointMetadata>(), Arg.Any<CancellationToken>());
        await h.ForecastCache.Received(1).UpsertAsync(Arg.Any<CachedForecast>(), Arg.Any<CancellationToken>());
        h.Warmer.Received(1).RequestWarming(Grid);
    }

    [Test]
    public async Task GetForecastWithFreshMetadataDoesNotCallPoints()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(FreshForecast(Grid));

        await h.Service.GetForecastAsync(Coord);

        await h.Nws.DidNotReceive().GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastWithFreshForecastDoesNotCallForecast()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(FreshForecast(Grid));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        await h.Nws.DidNotReceive().GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastStaleThenNotModifiedRenewsTtlAndReturnsCached()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(StaleForecast(Grid));
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.NotModified("\"e2\"", TimeSpan.FromMinutes(20)));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        result!.Periods[0].ShortForecast.ShouldBe("Sunny");
        await h.ForecastCache.Received(1).UpsertAsync(
            Arg.Is<CachedForecast>(c =>
                c.ETag == "\"e2\"" &&
                c.RetrievedAtUtc == Now &&
                c.ExpiresAtUtc == Now.AddMinutes(20)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastStaleThenUnavailableServesStaleWithoutUpsert()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(StaleForecast(Grid));
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Unavailable);

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        result!.Grid.ShouldBe(Grid);
        await h.ForecastCache.DidNotReceive().UpsertAsync(Arg.Any<CachedForecast>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastNotFoundWithoutCacheReturnsNull()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.NotFound);

        (await h.Service.GetForecastAsync(Coord)).ShouldBeNull();
    }

    [Test]
    public async Task GetForecastWithUnresolvableMetadataAndNoCacheReturnsNull()
    {
        var h = new Harness();
        // metadata cache empty + NWS resolve returns null
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((PointMetadata?)null);

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldBeNull();
        await h.Nws.DidNotReceive().GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetForecastWithUnresolvableMetadataButStaleCacheServesStaleMetadata()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(StaleMeta());
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((PointMetadata?)null);
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30)));

        var result = await h.Service.GetForecastAsync(Coord);

        result.ShouldNotBeNull();
        await h.Nws.Received(1).GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetNeighborhoodReturnsOnlyAlreadyFreshNeighbors()
    {
        var h = new Harness();
        var neighborA = new GridPoint("AKQ", 82, 61);
        var neighborB = new GridPoint("AKQ", 84, 61);

        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns(FreshForecast(Grid));
        h.ForecastCache.GetAsync(neighborA, Arg.Any<CancellationToken>()).Returns(FreshForecast(neighborA));
        h.ForecastCache.GetAsync(neighborB, Arg.Any<CancellationToken>()).Returns(FreshForecast(neighborB));
        // every other surrounding cell returns null (the NSubstitute default)

        var result = await h.Service.GetNeighborhoodForecastAsync(Coord);

        result.ShouldNotBeNull();
        result!.Primary.Grid.ShouldBe(Grid);
        result.Neighbors.Count.ShouldBe(2);
        result.Neighbors.Select(n => n.Grid).ShouldBe(new[] { neighborA, neighborB }, ignoreOrder: true);
        h.Warmer.Received(1).RequestWarming(Grid);
    }

    [Test]
    public async Task GetNeighborhoodWithoutMetadataReturnsNull()
    {
        var h = new Harness();
        h.Nws.GetPointMetadataAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((PointMetadata?)null);

        (await h.Service.GetNeighborhoodForecastAsync(Coord)).ShouldBeNull();
    }

    [Test]
    public async Task GetForecastByGridDoesNotScheduleWarming()
    {
        var h = new Harness();
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30)));

        var result = await h.Service.GetForecastByGridAsync(Grid);

        result.ShouldNotBeNull();
        h.Warmer.DidNotReceive().RequestWarming(Arg.Any<GridPoint>());
    }

    // ----------------------------------------------------------------------
    // Stampede protection. This is the behaviour the whole change exists for:
    // many concurrent cold requests for the SAME cell must collapse into a
    // single upstream NWS call.
    // ----------------------------------------------------------------------

    [Test]
    public async Task ConcurrentColdRequestsForSameGridCallNwsOnce()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());

        // The forecast cache starts empty. After the first upsert, subsequent
        // reads return the freshly-stored value — exactly like the real SQLite
        // cache. We model that here so a "follower" that starts a fresh flight
        // after the leader finishes sees the filled cache and does NOT call NWS.
        CachedForecast? stored = null;
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>())
            .Returns(_ => stored);
        h.ForecastCache
            .When(c => c.UpsertAsync(Arg.Any<CachedForecast>(), Arg.Any<CancellationToken>()))
            .Do(call => stored = call.Arg<CachedForecast>());

        // Gate the NWS call so all callers pile up behind one slow, in-flight
        // fetch — the precise condition that used to stampede.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nwsCalls = 0;
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                Interlocked.Increment(ref nwsCalls);
                await gate.Task;
                return ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30));
            });

        // Fire 50 concurrent requests for the same coordinate.
        var requests = Enumerable.Range(0, 50)
            .Select(_ => h.Service.GetForecastAsync(Coord))
            .ToArray();

        // Let them all attach to the single flight, then release NWS.
        await Task.Delay(50);
        gate.SetResult();

        var results = await Task.WhenAll(requests);

        results.ShouldAllBe(r => r != null && r.Grid == Grid);
        // The crux: 50 concurrent cold requests → exactly ONE upstream call.
        nwsCalls.ShouldBe(1);
        await h.ForecastCache.Received(1).UpsertAsync(Arg.Any<CachedForecast>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SequentialRequestsAfterExpiryEachStartANewFlight()
    {
        var h = new Harness();
        h.MetadataCache.GetAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(FreshMeta());
        // Cache stays cold (always null) → each sequential call is a genuine miss.
        h.ForecastCache.GetAsync(Grid, Arg.Any<CancellationToken>()).Returns((CachedForecast?)null);

        var nwsCalls = 0;
        h.Nws.GetForecastAsync(Arg.Any<GridPoint>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref nwsCalls);
                return ForecastFetchResult.Success(ForecastFor(Grid), "\"e\"", TimeSpan.FromMinutes(30));
            });

        // Three calls, one after another (no overlap) → coalescer never merges
        // them, so we expect three upstream calls. This proves the coalescer
        // only suppresses CONCURRENT duplicates, not across-time ones (that is
        // the cache's job, and is exercised separately).
        await h.Service.GetForecastAsync(Coord);
        await h.Service.GetForecastAsync(Coord);
        await h.Service.GetForecastAsync(Coord);

        nwsCalls.ShouldBe(3);
    }
}
