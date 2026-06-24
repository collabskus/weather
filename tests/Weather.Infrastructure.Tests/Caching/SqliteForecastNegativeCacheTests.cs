using Weather.Core.Abstractions;
using Weather.Infrastructure.Caching;

namespace Weather.Infrastructure.Tests.Caching;

public sealed class SqliteForecastNegativeCacheTests
{
    private static readonly GridPoint Grid = new("AKQ", 83, 61);
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);

    private static NwsProblem MarineProblem() => new(
        "https://api.weather.gov/problems/MarineForecastNotSupported",
        "Marine Forecast Not Supported",
        404,
        "Forecasts for marine areas are not yet supported by this API.",
        "1d604a85");

    [Test]
    public async Task UpsertThenGetRoundTripsEveryProblemField()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastNegativeCache(harness.Factory);
        var entry = new CachedForecastNotFound(MarineProblem(), Now, Now.AddHours(6));

        await cache.UpsertAsync(Grid, entry);
        var read = await cache.GetAsync(Grid);

        read.ShouldNotBeNull();
        read!.RetrievedAtUtc.ShouldBe(Now);
        read.ExpiresAtUtc.ShouldBe(Now.AddHours(6));
        read.Problem.ShouldNotBeNull();
        read.Problem!.Type.ShouldBe("https://api.weather.gov/problems/MarineForecastNotSupported");
        read.Problem.Title.ShouldBe("Marine Forecast Not Supported");
        read.Problem.Status.ShouldBe(404);
        read.Problem.Detail.ShouldBe("Forecasts for marine areas are not yet supported by this API.");
        read.Problem.CorrelationId.ShouldBe("1d604a85");
        read.Problem.TypeName.ShouldBe("MarineForecastNotSupported");
    }

    [Test]
    public async Task GetReturnsNullWhenAbsent()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastNegativeCache(harness.Factory);

        (await cache.GetAsync(Grid)).ShouldBeNull();
    }

    [Test]
    public async Task NullProblemRoundTripsAsNull()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastNegativeCache(harness.Factory);

        // A 404 with no body is a valid negative entry with no problem detail.
        await cache.UpsertAsync(Grid, new CachedForecastNotFound(null, Now, Now.AddHours(6)));
        var read = await cache.GetAsync(Grid);

        read.ShouldNotBeNull();
        read!.Problem.ShouldBeNull();
        read.ExpiresAtUtc.ShouldBe(Now.AddHours(6));
    }

    [Test]
    public async Task UpsertOverwritesTheExistingEntry()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastNegativeCache(harness.Factory);

        await cache.UpsertAsync(Grid, new CachedForecastNotFound(MarineProblem(), Now, Now.AddHours(6)));
        await cache.UpsertAsync(Grid, new CachedForecastNotFound(null, Now.AddMinutes(1), Now.AddHours(7)));

        var read = await cache.GetAsync(Grid);

        read.ShouldNotBeNull();
        read!.Problem.ShouldBeNull();
        read.RetrievedAtUtc.ShouldBe(Now.AddMinutes(1));
        read.ExpiresAtUtc.ShouldBe(Now.AddHours(7));
    }

    [Test]
    public async Task RemoveDeletesTheEntry()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastNegativeCache(harness.Factory);
        await cache.UpsertAsync(Grid, new CachedForecastNotFound(MarineProblem(), Now, Now.AddHours(6)));

        await cache.RemoveAsync(Grid);

        (await cache.GetAsync(Grid)).ShouldBeNull();
    }

    [Test]
    public async Task RemoveIsANoOpWhenAbsent()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastNegativeCache(harness.Factory);

        // Must not throw when there is nothing to delete.
        await cache.RemoveAsync(Grid);

        (await cache.GetAsync(Grid)).ShouldBeNull();
    }

    [Test]
    public async Task EntriesAreKeyedPerCell()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastNegativeCache(harness.Factory);
        var other = Grid with { GridX = 84 };

        await cache.UpsertAsync(Grid, new CachedForecastNotFound(MarineProblem(), Now, Now.AddHours(6)));

        (await cache.GetAsync(Grid)).ShouldNotBeNull();
        (await cache.GetAsync(other)).ShouldBeNull();
    }

    [Test]
    public async Task FreshnessTracksTheExpiryInstant()
    {
        await using var harness = await SqliteCacheHarness.CreateAsync();
        var cache = new SqliteForecastNegativeCache(harness.Factory);
        await cache.UpsertAsync(Grid, new CachedForecastNotFound(MarineProblem(), Now, Now.AddHours(6)));

        var read = await cache.GetAsync(Grid);

        read!.IsFresh(Now.AddHours(3)).ShouldBeTrue();
        read.IsFresh(Now.AddHours(7)).ShouldBeFalse();
    }
}
