# Weather.Web

The Blazor Server front end: a single-page **sky-state dashboard** that reads the
user's location from the browser, asks the [Weather API](../Weather.Api/README.md)
for a forecast, and renders it. Its defining job is to handle the *unhappy* paths
of browser geolocation as gracefully as the happy one.

## What it does

1. On first interactive render it asks the browser for the current position
   (`navigator.geolocation`).
2. On success it calls `GET /api/forecast?latitude={lat}&longitude={lon}` and
   renders the result.
3. On **any** failure — permission denied, position unavailable, timeout, or no
   Geolocation API at all — it falls back to a manual latitude/longitude form
   plus a one-click sample location (Newport News, VA).

## The geolocation contract

Browser geolocation can fail in several ways, and the W3C API reports them by
rejecting a callback. To keep "the user said no" off the C# exception path, the
JS helper in [`wwwroot/js/geolocation.js`](wwwroot/js/geolocation.js) **never
rejects** — it always resolves an object:

```js
{ success, latitude, longitude, accuracy, errorCode }
// errorCode ∈ "PermissionDenied" | "PositionUnavailable" | "Timeout" | "NotSupported" | null
```

`GeolocationService` maps that object onto a typed `GeolocationResult` with a
`GeolocationError` enum. The component then branches on the result:

| Outcome | State shown | Fallback offered |
| --- | --- | --- |
| Position obtained | forecast loads | — |
| Permission denied | "Location is off" | manual entry + sample |
| Position unavailable / timeout / unsupported / JS fault | "Couldn't pin down where you are" | manual entry + sample |
| Coordinate outside NWS coverage (API 404) | "No coverage for that spot" | manual entry + sample |
| API/network error | "The forecast didn't load" | retry + sample |

Because geolocation needs JS interop, it runs in `OnAfterRenderAsync(firstRender:
true)` — never during prerender, where interop isn't available.

## Talking to the API

`IWeatherApiClient` / `WeatherApiClient` is a typed `HttpClient`. Its base address
comes from configuration:

- **Under .NET Aspire** the value is `https+http://api`, resolved by service
  discovery (wired up in `Weather.ServiceDefaults`).
- **Standalone**, set `WeatherApi:BaseUrl` (or the `services__api__https__0`
  environment variable) to the API's real URL.

A `404` from the API is mapped to `null` (an expected "no coverage" outcome),
not an exception.

## The signature element

The hero panel paints the sky implied by the current period: the gradient is
derived at runtime from whether the period is day or night and from keywords in
its short forecast (clear / cloud / rain / storms / snow / fog). That logic lives
in [`Services/SkyPalette.cs`](Services/SkyPalette.cs). Foreground colours are
chosen per sky-state so text stays legible against every gradient. Everything
else — cool haze-blue paper, white cards, one amber accent reused for focus
rings — stays deliberately quiet.

The quality floor is built in, not bolted on: responsive to mobile, visible
keyboard focus, and `prefers-reduced-motion` respected.

## Testability

The component depends only on the `IGeolocationService` and `IWeatherApiClient`
abstractions, so [`Weather.Web.Tests`](../../tests/Weather.Web.Tests/README.md)
renders it with bUnit using fakes — no real browser, JS runtime, or HTTP server
required.

## Layout

```
Weather.Web/
├─ Components/
│  ├─ App.razor              # document, fonts, script + style references
│  ├─ Routes.razor           # router + not-found
│  ├─ _Imports.razor
│  ├─ Layout/
│  │  ├─ MainLayout.razor    # header, brand, nav, footer
│  │  └─ NavMenu.razor
│  └─ Pages/
│     ├─ Home.razor          # the dashboard + state machine
│     ├─ About.razor
│     └─ Error.razor
├─ Services/
│  ├─ IGeolocationService.cs / GeolocationService.cs
│  ├─ IWeatherApiClient.cs   / WeatherApiClient.cs
│  ├─ WeatherViewModels.cs   # ForecastDto, ForecastPeriodDto, NeighborhoodDto
│  └─ SkyPalette.cs          # the signature gradient logic
├─ wwwroot/
│  ├─ app.css                # sky-state design system
│  └─ js/geolocation.js      # never-rejecting geolocation bridge
└─ Program.cs
```
