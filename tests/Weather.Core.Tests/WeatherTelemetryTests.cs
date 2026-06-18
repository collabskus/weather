using System.Diagnostics.Metrics;
using Weather.Core.Telemetry;

namespace Weather.Core.Tests;

public sealed class WeatherTelemetryTests
{
    [Test]
    public void Names_match_the_constants_registered_with_opentelemetry()
    {
        WeatherTelemetry.MeterName.ShouldBe("Weather.Cache");
        WeatherTelemetry.ActivitySourceName.ShouldBe("Weather.Nws");
    }

    [Test]
    public void ActivitySource_uses_the_published_name()
    {
        using var telemetry = new WeatherTelemetry();

        telemetry.ActivitySource.Name.ShouldBe(WeatherTelemetry.ActivitySourceName);
    }

    [Test]
    public void Cache_hit_and_miss_are_recorded_with_cache_and_result_tags()
    {
        using var telemetry = new WeatherTelemetry();
        var measurements = new List<(long Value, string? Cache, string? Result)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == WeatherTelemetry.MeterName &&
                instrument.Name == "weather.cache.requests")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            string? cache = null;
            string? result = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "cache")
                {
                    cache = tag.Value as string;
                }
                else if (tag.Key == "result")
                {
                    result = tag.Value as string;
                }
            }

            measurements.Add((value, cache, result));
        });
        listener.Start();

        telemetry.RecordCacheHit("forecast");
        telemetry.RecordCacheMiss("metadata");

        measurements.Count.ShouldBe(2);
        measurements.ShouldContain(m => m.Cache == "forecast" && m.Result == "hit" && m.Value == 1);
        measurements.ShouldContain(m => m.Cache == "metadata" && m.Result == "miss" && m.Value == 1);
    }

    [Test]
    public void Nws_request_duration_is_recorded_in_the_histogram()
    {
        using var telemetry = new WeatherTelemetry();
        var recorded = new List<double>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == WeatherTelemetry.MeterName &&
                instrument.Name == "weather.nws.request.duration")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => recorded.Add(value));
        listener.Start();

        telemetry.RecordNwsRequest("forecast", statusCode: 200, elapsedMs: 42.5, conditional: false);

        recorded.ShouldHaveSingleItem().ShouldBe(42.5);
    }
}
