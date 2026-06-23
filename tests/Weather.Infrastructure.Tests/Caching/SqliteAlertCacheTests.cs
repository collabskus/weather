using Weather.Core.Abstractions;
using Weather.Infrastructure.Caching;

namespace Weather.Infrastructure.Tests.Caching;

public sealed class SqliteAlertCacheTests
{
    private static readonly GeoCoordinate Coord = new(37.0879, -76.4505);
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    private static WeatherAlert SampleAlert(string id = "id-1") => new(
        id, "Heat Advisory", "Moderate", "Likely", "Expected",
        "Heat Advisory in effect", "Hot.", "Drink fluids.", "Hampton",
        "NWS Wakefield VA", Now, Now, Now.AddHours(6), Now.AddHours(6));

    [Test]
    public async Task UpsertThenGetRoundTripsEveryField()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteAlertCache(harness.Factory);
        var entry = new CachedAlerts(new[] { SampleAlert() }, Now, Now.AddMinutes(5));

        await cache.UpsertAsync(Coord, entry);
        var read = await cache.GetAsync(Coord);

        read.ShouldNotBeNull();
        read!.RetrievedAtUtc.ShouldBe(Now);
        read.ExpiresAtUtc.ShouldBe(Now.AddMinutes(5));
        read.Alerts.Count.ShouldBe(1);
        read.Alerts[0].Id.ShouldBe("id-1");
        read.Alerts[0].Event.ShouldBe("Heat Advisory");
        read.Alerts[0].Severity.ShouldBe("Moderate");
        read.Alerts[0].Expires.ShouldBe(Now.AddHours(6));
    }

    [Test]
    public async Task GetReturnsNullWhenAbsent()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteAlertCache(harness.Factory);

        (await cache.GetAsync(Coord)).ShouldBeNull();
    }

    [Test]
    public async Task EmptyAlertListRoundTripsAsEmptyNotNull()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteAlertCache(harness.Factory);

        await cache.UpsertAsync(Coord, new CachedAlerts(Array.Empty<WeatherAlert>(), Now, Now.AddMinutes(5)));
        var read = await cache.GetAsync(Coord);

        read.ShouldNotBeNull();
        read!.Alerts.Count.ShouldBe(0);
    }

    [Test]
    public async Task UpsertOverwritesTheExistingEntry()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteAlertCache(harness.Factory);

        await cache.UpsertAsync(Coord, new CachedAlerts(new[] { SampleAlert("a") }, Now, Now.AddMinutes(5)));
        await cache.UpsertAsync(Coord, new CachedAlerts(
            new[] { SampleAlert("b"), SampleAlert("c") }, Now.AddMinutes(1), Now.AddMinutes(6)));

        var read = await cache.GetAsync(Coord);

        read.ShouldNotBeNull();
        read!.Alerts.Count.ShouldBe(2);
        read.Alerts[0].Id.ShouldBe("b");
        read.RetrievedAtUtc.ShouldBe(Now.AddMinutes(1));
    }

    [Test]
    public async Task RoundedCoordinatesShareTheSameCacheEntry()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteAlertCache(harness.Factory);

        // Two coordinates that round to the same 4-dp key collapse onto one
        // entry — the same de-duplication that protects /points and forecasts.
        var a = new GeoCoordinate(37.08791, -76.45049);
        var b = new GeoCoordinate(37.08793, -76.45051);

        await cache.UpsertAsync(a, new CachedAlerts(new[] { SampleAlert() }, Now, Now.AddMinutes(5)));
        var read = await cache.GetAsync(b);

        read.ShouldNotBeNull();
        read!.Alerts.Count.ShouldBe(1);
    }

    [Test]
    public async Task FreshnessTracksTheExpiryInstant()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteAlertCache(harness.Factory);
        await cache.UpsertAsync(Coord, new CachedAlerts(new[] { SampleAlert() }, Now, Now.AddMinutes(5)));

        var read = await cache.GetAsync(Coord);

        read!.IsFresh(Now.AddMinutes(2)).ShouldBeTrue();
        read.IsFresh(Now.AddMinutes(6)).ShouldBeFalse();
    }
}
