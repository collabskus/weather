# Frontend — the "all tiles, all data" dashboard

This document describes how the Blazor Server dashboard
(`src/Weather.Web`) presents weather, and the decisions behind it. For the
geolocation bridge and fallback states specifically, see the Web project's own
README; this focuses on the area view.

## The idea

An NWS forecast is published per **grid cell** (a roughly 2.5 km square). The
`/points` endpoint maps a coordinate to one cell, but a user standing near a
cell boundary may be physically closer to the centre of an adjacent cell than to
their own. Rather than hide that, the dashboard embraces it:

- It resolves the user's coordinate to their **primary** cell.
- It also fetches **every neighbouring cell** in the surrounding ring.
- It orders those neighbours by **true distance** from the user, nearest first.
- Every cell — primary and neighbours alike — carries its **full** data set.

So the page is not a single forecast; it is the user's whole neighbourhood, with
the most relevant tiles surfaced first.

## What each tile shows

The dashboard calls one endpoint, `GET /api/forecast/area`, which returns the
primary cell, the distance-ordered neighbours, and any active alerts. For each
cell the API follows the NWS JSON links and returns:

- **Daily forecast** — the named periods ("This Afternoon", "Tonight", …) with
  temperature, precipitation chance, wind, and the short/detailed text.
- **Hourly forecast** — the near-term hours (capped to keep payloads sane),
  rendered as a horizontally scrollable strip.
- **Latest observation** — the most recent report from the cell's nearest
  station, converted from NWS's SI units to the US units the forecast uses
  (°F, mph, inHg, miles).

Point-based **active alerts** (watches, warnings, advisories) are shown in a
banner above everything else, colour-coded by severity.

## Layout and progressive disclosure

The primary cell is shown expanded: a sky-state hero, the "Right now"
observation panel, the hourly strip, and the full set of daily cards.

The neighbouring tiles are rendered as collapsible `<details>` elements, ordered
nearest-first and **collapsed by default**. Each tile's summary shows its grid
id, the current temperature and conditions, and its distance from the user;
opening it reveals that tile's full daily cards, hourly strip, and current
conditions. This keeps "all the data for all the tiles" genuinely present on the
page without forcing an overwhelming wall of content on first paint — the nearest
tiles are one tap away, the rest are there when wanted.

## Distance ordering

Each cell's approximate centre is parsed from the polygon **geometry** NWS
returns with the forecast (the average of the outer-ring vertices). The distance
from the user's coordinate to each centre is computed with the haversine formula
(`GeoCoordinate.DistanceMetersTo`). When a centre is unknown, ordering falls back
to grid-index distance so the list is always stable. The displayed distance is
shown only when a real centre is known.

## Rendering and culture

The component is written declaratively (Razor markup with small formatting
helpers) rather than with a hand-built render tree, which keeps the states and
sections easy to read and test. The app runs with `InvariantGlobalization`, so
all number, date, and time formatting passes `CultureInfo.InvariantCulture`
explicitly.

Stable `data-testid` attributes (`forecast`, `hero`, `hero-temp`, `observation`,
`hourly`, `neighbors`, `neighbor-tile`, and the various `state-*` markers) make
the bUnit tests resilient to styling changes.

## Caching is invisible

The dashboard never reasons about freshness — that lives entirely in the API and
its SQLite caches. Repeat views are quick because cells are served cache-aside;
the user simply sees current data. The cold-start cost of assembling a brand-new
neighbourhood, and how the background warmer and configurable radius mitigate it,
is discussed in [`OBSERVABILITY.md`](OBSERVABILITY.md) and the architecture doc.
