using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Weather.Web.Components.Pages;
using Weather.Web.Tests.Fakes;
using TestContext = Bunit.TestContext;

namespace Weather.Web.Tests;

public sealed class HomePageTests
{
    private static ForecastDto SampleForecast() => new(
        "AKQ", 83, 61, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        new[]
        {
            new ForecastPeriodDto(1, "This Afternoon", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(4),
                true, 90, "F", 3, "5 mph", "S", "Sunny", "Sunny, with a high near 90.", "icon-day"),
            new ForecastPeriodDto(2, "Tonight", DateTimeOffset.UtcNow.AddHours(4), DateTimeOffset.UtcNow.AddHours(16),
                false, 74, "F", null, "12 mph", "S", "Mostly Clear", "Mostly clear.", "icon-night"),
        });

    private static GeolocationResult Success() =>
        new(true, 37.0879, -76.4505, 12, GeolocationError.None);

    private static TestContext CreateContext(IGeolocationService geo, IWeatherApiClient api)
    {
        var ctx = new TestContext();
        ctx.Services.AddSingleton(geo);
        ctx.Services.AddSingleton(api);
        return ctx;
    }

    [Test]
    public void WhenPermissionDeniedTheManualEntryFormIsShown()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(GeolocationResult.Failed(GeolocationError.PermissionDenied)),
            FakeWeatherApiClient.Returning(SampleForecast()));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-denied]"));
        cut.FindAll("[data-testid=manual-entry]").ShouldNotBeEmpty();
    }

    [Test]
    public void WhenPositionUnavailableTheManualEntryFormIsShown()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(GeolocationResult.Failed(GeolocationError.PositionUnavailable)),
            FakeWeatherApiClient.Returning(SampleForecast()));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-unavailable]"));
    }

    [Test]
    public void WhenLocationAndApiSucceedTheForecastIsRendered()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(Success()),
            FakeWeatherApiClient.Returning(SampleForecast()));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
        cut.Find("[data-testid=hero-temp]").TextContent.ShouldContain("90");
        cut.Markup.ShouldContain("AKQ");
    }

    [Test]
    public void WhenTheApiReportsNoCoverageTheNotCoveredStateIsShown()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(Success()),
            FakeWeatherApiClient.Returning(null));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-not-covered]"));
    }

    [Test]
    public void WhenTheApiThrowsTheErrorStateIsShown()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(Success()),
            FakeWeatherApiClient.Throwing(new HttpRequestException("boom")));

        var cut = ctx.RenderComponent<Home>();

        cut.WaitForAssertion(() => cut.Find("[data-testid=state-error]"));
    }

    [Test]
    public void WhileGeolocationIsPendingTheLocatingStateIsShownThenResolves()
    {
        var (geo, gate) = FakeGeolocationService.Gated();
        using var ctx = CreateContext(geo, FakeWeatherApiClient.Returning(SampleForecast()));

        var cut = ctx.RenderComponent<Home>();

        // Before the browser answers, the "finding your location" state is up.
        cut.WaitForAssertion(() => cut.Find("[data-testid=state-locating]"));

        // Browser answers → forecast loads.
        gate.SetResult(Success());
        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
    }

    [Test]
    public void ManualSubmissionLoadsTheForecast()
    {
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(GeolocationResult.Failed(GeolocationError.PermissionDenied)),
            FakeWeatherApiClient.Returning(SampleForecast()));

        var cut = ctx.RenderComponent<Home>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=manual-entry]"));

        // Re-query each element to avoid acting on a stale reference after re-render.
        cut.FindAll("[data-testid=manual-entry] input")[0].Input("37.0879");
        cut.FindAll("[data-testid=manual-entry] input")[1].Input("-76.4505");
        cut.FindAll("[data-testid=manual-entry] button")[0].Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
    }

    [Test]
    public void UseSampleButtonRequestsTheSampleCoordinates()
    {
        var api = FakeWeatherApiClient.Returning(SampleForecast());
        using var ctx = CreateContext(
            FakeGeolocationService.Returning(GeolocationResult.Failed(GeolocationError.PermissionDenied)),
            api);

        var cut = ctx.RenderComponent<Home>();
        cut.WaitForAssertion(() => cut.Find("[data-testid=manual-entry]"));

        cut.FindAll("[data-testid=manual-entry] button")[1].Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid=forecast]"));
        api.LastLatitude.ShouldBe(37.0879);
        api.LastLongitude.ShouldBe(-76.4505);
    }
}
