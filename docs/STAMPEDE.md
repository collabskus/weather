# Cache Stampede Protection

## The problem

The forecast cache is *cache-aside*: read cache → on miss, fetch from NWS →
store. That pattern alone does **not** bound upstream load. During the window
between a miss and the store, every concurrent request for the same grid cell
independently sees the miss and independently calls NWS. When many browsers
refresh the same location at once — or a popular cell's TTL expires under load —
the result is a burst of identical upstream calls. This is the **cache
stampede** (also "dogpile" or "thundering herd").

`IWeatherService` is registered *scoped* (one instance per request), so there is
no shared in-process coordination point inside the service itself.

## The fix: single-flight coalescing

`IRequestCoalescer<TKey>` (in `Weather.Core.Abstractions`) collapses concurrent
work for the same key into one execution. For a given grid cell:

- The **first** caller (the "leader") runs the real fetch.
- Every caller that arrives **while that fetch is in flight** (a "follower")
  awaits the same task and shares its result.
- The instant the fetch settles, the flight is evicted, so the **next** caller
  (after the cache has presumably been filled) starts fresh.

`RequestCoalescer<GridPoint>` is registered as a **singleton** in
`DependencyInjection`. This is essential: a scoped coalescer would give each
request its own dictionary and reintroduce the stampede. A singleton means all
requests across the process share one in-flight map.

The cache read happens *inside* the coalesced factory, so a follower that waited
on an earlier flight — and then began a fresh one — re-reads the now-filled
cache and returns a hit without calling NWS.

## What it does and does not guarantee

- **Guarantees:** at most one upstream fetch per cell *at any instant*. Load on
  NWS is bounded by the number of *distinct cold cells*, not by user count.
- **Does not guarantee:** suppression of fetches *across time*. Two sequential
  (non-overlapping) misses for a cold cell are two fetches — that is the cache's
  job (TTL + conditional GETs), which the coalescer composes with rather than
  replaces.

## Cancellation semantics

A flight is shared, so one caller cancelling must not abort the work the others
await. Every caller — leader and follower alike — awaits the shared task via
`Task.WaitAsync(callerToken)`, which detaches only that caller. The factory runs
under a flight-owned token that is cancelled only when **all** callers have
detached (reference-counted), so a genuinely abandoned flight stops its upstream
call, while a partially-abandoned one continues for whoever is still waiting.

## Observability

`WeatherTelemetry` emits `weather.cache.coalesced` (a counter tagged
`role=leader|follower`). Under refresh-spam you should see one `leader` and many
`follower` increments per cold cell: each follower is an upstream call that was
**avoided**. Combined with the existing `weather.cache.requests` (hit/miss) and
`weather.nws.request.duration` metrics, the Aspire dashboard / Uptrace shows the
protection working in real time.

## Tests

- `RequestCoalescerTests` — pins the coalescer contract in isolation: concurrent
  collapse, sequential non-collapse, exception propagation to all waiters,
  per-caller cancellation not aborting the shared flight, distinct keys running
  in parallel, and argument validation.
- `WeatherServiceTests.ConcurrentColdRequestsForSameGridCallNwsOnce` — fires 50
  concurrent requests at one cold cell behind a gated NWS stub and asserts
  exactly one upstream call.
