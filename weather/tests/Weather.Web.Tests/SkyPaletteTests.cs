namespace Weather.Web.Tests;

public sealed class SkyPaletteTests
{
    [Test]
    [Arguments("Sunny", true, "Clear")]
    [Arguments("Mostly Sunny", true, "Clear")]
    [Arguments("Clear", false, "Clear")]
    [Arguments("Fair", true, "Clear")]
    [Arguments("Partly Cloudy", true, "Cloudy")]
    [Arguments("Mostly Cloudy", true, "Cloudy")]
    [Arguments("Cloudy", true, "Cloudy")]
    [Arguments("Overcast", false, "Cloudy")]
    [Arguments("Chance Showers And Thunderstorms", true, "Storms")]
    [Arguments("Showers And Thunderstorms", false, "Storms")]
    [Arguments("Severe Tstorms", true, "Storms")]
    [Arguments("Rain Showers", true, "Rain")]
    [Arguments("Light Rain", false, "Rain")]
    [Arguments("Drizzle", true, "Rain")]
    [Arguments("Patchy Fog", true, "Fog")]
    [Arguments("Areas Of Fog", false, "Fog")]
    [Arguments("Haze", true, "Fog")]
    [Arguments("Snow", true, "Snow")]
    [Arguments("Chance Snow Showers", false, "Snow")]
    [Arguments("Wintry Mix", true, "Snow")]
    public void ForPeriod_maps_short_forecast_to_a_sky_state(string shortForecast, bool isDaytime, string expectedLabel)
    {
        var theme = SkyPalette.ForPeriod(isDaytime, shortForecast);

        theme.Label.ShouldBe(expectedLabel);
    }

    [Test]
    public void ForPeriod_is_case_insensitive()
    {
        SkyPalette.ForPeriod(true, "RAIN").Label.ShouldBe("Rain");
        SkyPalette.ForPeriod(true, "sUnNy").Label.ShouldBe("Clear");
    }

    [Test]
    [Arguments((string?)null)]
    [Arguments("")]
    [Arguments("   ")]
    public void ForPeriod_defaults_to_clear_when_text_is_missing(string? shortForecast) =>
        SkyPalette.ForPeriod(true, shortForecast).Label.ShouldBe("Clear");

    [Test]
    public void ForPeriod_always_produces_non_empty_colours()
    {
        var theme = SkyPalette.ForPeriod(true, "Sunny");

        theme.From.ShouldNotBeNullOrWhiteSpace();
        theme.To.ShouldNotBeNullOrWhiteSpace();
        theme.Foreground.ShouldNotBeNullOrWhiteSpace();
        theme.Accent.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void ForPeriod_uses_distinct_gradients_for_day_and_night()
    {
        var day = SkyPalette.ForPeriod(true, "Clear");
        var night = SkyPalette.ForPeriod(false, "Clear");

        day.From.ShouldNotBe(night.From);
    }

    [Test]
    public void Thunderstorms_take_precedence_over_rain_keywords()
    {
        // "Showers And Thunderstorms" contains both "shower" and "thunder";
        // the stormier reading must win.
        SkyPalette.ForPeriod(true, "Showers And Thunderstorms").Label.ShouldBe("Storms");
    }

    [Test]
    public void Snow_takes_precedence_over_shower_keywords()
    {
        SkyPalette.ForPeriod(false, "Snow Showers").Label.ShouldBe("Snow");
    }
}
