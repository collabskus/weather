namespace Weather.Infrastructure.Tests.Fakes;

/// <summary>
/// Canned NWS API responses, trimmed to the fields the client consumes but
/// otherwise shaped exactly like the real <c>application/geo+json</c> payloads
/// (top-level <c>properties</c> envelope, <c>probabilityOfPrecipitation</c> as
/// <c>{ unitCode, value }</c>, camelCase keys).
/// </summary>
internal static class NwsPayloads
{
    /// <summary>A <c>/points/37.0879,-76.4505</c> response resolving to grid AKQ/83,61.</summary>
    public const string Points =
        """
        {
          "properties": {
            "gridId": "AKQ",
            "gridX": 83,
            "gridY": 61,
            "forecast": "https://api.weather.gov/gridpoints/AKQ/83,61/forecast",
            "forecastHourly": "https://api.weather.gov/gridpoints/AKQ/83,61/forecast/hourly",
            "timeZone": "America/New_York",
            "radarStation": "KAKQ",
            "relativeLocation": {
              "type": "Feature",
              "properties": {
                "city": "Bethel Manor",
                "state": "VA"
              }
            }
          }
        }
        """;

    /// <summary>A two-period <c>/gridpoints/AKQ/83,61/forecast</c> response.</summary>
    public const string Forecast =
        """
        {
          "properties": {
            "generatedAt": "2026-06-17T18:37:33+00:00",
            "updateTime": "2026-06-17T17:09:58+00:00",
            "periods": [
              {
                "number": 1,
                "name": "This Afternoon",
                "startTime": "2026-06-17T14:00:00-04:00",
                "endTime": "2026-06-17T18:00:00-04:00",
                "isDaytime": true,
                "temperature": 90,
                "temperatureUnit": "F",
                "probabilityOfPrecipitation": { "unitCode": "wmoUnit:percent", "value": 3 },
                "windSpeed": "5 to 12 mph",
                "windDirection": "S",
                "icon": "https://api.weather.gov/icons/land/day/few?size=medium",
                "shortForecast": "Sunny",
                "detailedForecast": "Sunny, with a high near 90. South wind 5 to 12 mph."
              },
              {
                "number": 2,
                "name": "Tonight",
                "startTime": "2026-06-17T18:00:00-04:00",
                "endTime": "2026-06-18T06:00:00-04:00",
                "isDaytime": false,
                "temperature": 74,
                "temperatureUnit": "F",
                "probabilityOfPrecipitation": { "unitCode": "wmoUnit:percent", "value": null },
                "windSpeed": "12 mph",
                "windDirection": "S",
                "icon": "https://api.weather.gov/icons/land/night/few?size=medium",
                "shortForecast": "Mostly Clear",
                "detailedForecast": "Mostly clear, with a low around 74. South wind around 12 mph."
              }
            ]
          }
        }
        """;

    /// <summary>A <c>/forecast</c> response that also carries polygon geometry,
    /// for exercising the cell-centre computation.</summary>
    public const string ForecastWithGeometry =
        """
        {
          "geometry": {
            "type": "Polygon",
            "coordinates": [
              [
                [-76.46, 37.08],
                [-76.44, 37.08],
                [-76.44, 37.10],
                [-76.46, 37.10],
                [-76.46, 37.08]
              ]
            ]
          },
          "properties": {
            "generatedAt": "2026-06-17T18:37:33+00:00",
            "updateTime": "2026-06-17T17:09:58+00:00",
            "periods": [
              {
                "number": 1,
                "name": "This Afternoon",
                "startTime": "2026-06-17T14:00:00-04:00",
                "endTime": "2026-06-17T18:00:00-04:00",
                "isDaytime": true,
                "temperature": 90,
                "temperatureUnit": "F",
                "probabilityOfPrecipitation": { "unitCode": "wmoUnit:percent", "value": 3 },
                "windSpeed": "5 to 12 mph",
                "windDirection": "S",
                "icon": "https://api.weather.gov/icons/land/day/few?size=medium",
                "shortForecast": "Sunny",
                "detailedForecast": "Sunny, with a high near 90."
              }
            ]
          }
        }
        """;

    /// <summary>A two-period <c>/forecast/hourly</c> response.</summary>
    public const string Hourly =
        """
        {
          "properties": {
            "generatedAt": "2026-06-17T18:37:33+00:00",
            "updateTime": "2026-06-17T17:09:58+00:00",
            "periods": [
              {
                "number": 1,
                "name": "",
                "startTime": "2026-06-17T15:00:00-04:00",
                "endTime": "2026-06-17T16:00:00-04:00",
                "isDaytime": true,
                "temperature": 90,
                "temperatureUnit": "F",
                "probabilityOfPrecipitation": { "unitCode": "wmoUnit:percent", "value": 2 },
                "windSpeed": "6 mph",
                "windDirection": "S",
                "icon": "https://api.weather.gov/icons/land/day/few,2?size=small",
                "shortForecast": "Sunny",
                "detailedForecast": ""
              },
              {
                "number": 2,
                "name": "",
                "startTime": "2026-06-17T16:00:00-04:00",
                "endTime": "2026-06-17T17:00:00-04:00",
                "isDaytime": true,
                "temperature": 89,
                "temperatureUnit": "F",
                "probabilityOfPrecipitation": { "unitCode": "wmoUnit:percent", "value": 2 },
                "windSpeed": "7 mph",
                "windDirection": "S",
                "icon": "https://api.weather.gov/icons/land/day/few,2?size=small",
                "shortForecast": "Sunny",
                "detailedForecast": ""
              }
            ]
          }
        }
        """;

    /// <summary>A <c>/gridpoints/AKQ/83,61/stations</c> response (nearest first).</summary>
    public const string Stations =
        """
        {
          "features": [
            {
              "properties": {
                "stationIdentifier": "KPHF",
                "name": "Newport News / Williamsburg"
              }
            },
            {
              "properties": {
                "stationIdentifier": "KLFI",
                "name": "Langley AFB"
              }
            }
          ]
        }
        """;

    /// <summary>A <c>/stations/KPHF/observations/latest</c> response in SI units.</summary>
    public const string Observation =
        """
        {
          "properties": {
            "timestamp": "2026-06-17T18:53:00+00:00",
            "textDescription": "Sunny",
            "icon": "https://api.weather.gov/icons/land/day/skc?size=medium",
            "temperature": { "unitCode": "wmoUnit:degC", "value": 31.1 },
            "dewpoint": { "unitCode": "wmoUnit:degC", "value": 21.0 },
            "windDirection": { "unitCode": "wmoUnit:degree_(angle)", "value": 180 },
            "windSpeed": { "unitCode": "wmoUnit:km_h-1", "value": 13.0 },
            "windGust": { "unitCode": "wmoUnit:km_h-1", "value": 24.1 },
            "barometricPressure": { "unitCode": "wmoUnit:Pa", "value": 101800 },
            "relativeHumidity": { "unitCode": "wmoUnit:percent", "value": 55.0 },
            "visibility": { "unitCode": "wmoUnit:m", "value": 16093 }
          }
        }
        """;

    /// <summary>An <c>/alerts/active?point=...</c> response with a single advisory.</summary>
    public const string Alerts =
        """
        {
          "features": [
            {
              "properties": {
                "id": "urn:oid:2.49.0.1.840.0.test.1",
                "event": "Heat Advisory",
                "severity": "Moderate",
                "certainty": "Likely",
                "urgency": "Expected",
                "headline": "Heat Advisory in effect",
                "description": "Hot temperatures expected.",
                "instruction": "Drink plenty of fluids.",
                "areaDesc": "Hampton; Newport News",
                "senderName": "NWS Wakefield VA",
                "effective": "2026-06-17T10:00:00-04:00",
                "onset": "2026-06-17T12:00:00-04:00",
                "expires": "2026-06-17T20:00:00-04:00",
                "ends": "2026-06-17T20:00:00-04:00"
              }
            }
          ]
        }
        """;

    /// <summary>An empty alerts FeatureCollection.</summary>
    public const string NoAlerts =
        """
        { "features": [] }
        """;

}
