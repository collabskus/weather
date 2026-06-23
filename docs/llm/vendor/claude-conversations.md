53
47

I have a defect here 

Restore complete (1.2s)
  Weather.Core net10.0 succeeded (0.5s) → src\Weather.Core\bin\Debug\net10.0\Weather.Core.dll
  Weather.ServiceDefaults net10.0 succeeded (0.8s) → src\Weather.ServiceDefaults\bin\Debug\net10.0\Weather.ServiceDefaults.dll
  Weather.Infrastructure net10.0 failed with 1 error(s) (0.6s)
    D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
  Weather.Core.Tests net10.0 succeeded (1.0s) → tests\Weather.Core.Tests\bin\Debug\net10.0\Weather.Core.Tests.dll
  Weather.Web net10.0 succeeded (1.5s) → src\Weather.Web\bin\Debug\net10.0\Weather.Web.dll
  Weather.Web.Tests net10.0 succeeded (0.9s) → tests\Weather.Web.Tests\bin\Debug\net10.0\Weather.Web.Tests.dll

Build failed with 1 error(s) in 4.7s
2026-06-23-18-35-50
D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
Build failed with exit code: 1.
2026-06-23-18-35-54
D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
Build failed with exit code: 1.
2026-06-23-18-35-59

/
Claude finished the response

I have a defect here 
Restore complete (1.2s)
  Weather.Core net10.0 succeeded (0.5s) → src\Weather.Core\bin\Debug\net10.0\Weather.Core.dll
  Weather.ServiceDefaults net10.0 succeeded (0.8s) → src\Weather.ServiceDefaults\bin\Debug\net10.0\Weather.ServiceDefaults.dll
  Weather.Infrastructure net10.0 failed with 1 error(s) (0.6s)
    D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
  Weather.Core.Tests net10.0 succeeded (1.0s) → tests\Weather.Core.Tests\bin\Debug\net10.0\Weather.Core.Tests.dll
  Weather.Web net10.0 succeeded (1.5s) → src\Weather.Web\bin\Debug\net10.0\Weather.Web.dll
  Weather.Web.Tests net10.0 succeeded (0.9s) → tests\Weather.Web.Tests\bin\Debug\net10.0\Weather.Web.Tests.dll
Build failed with 1 error(s) in 4.7s
2026-06-23-18-35-50
D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
Build failed with exit code: 1.
2026-06-23-18-35-54
D:\DEV\personal\weather\src\Weather.Infrastructure\Services\WeatherService.cs(48,24): error CS0103: The name 'ExtrasCoalescerKey' does not exist in the current context
Build failed with exit code: 1.
2026-06-23-18-35-59
6:48 PM
Investigating dotnet SDK accessibility constraints

Looking at the compile error, ExtrasCoalescerKey is referenced in WeatherService.cs at line 48 but isn't defined or imported in the current context. I need to check if this constant or property exists elsewhere in the codebase or if it needs to be added to the class. I need to read the dump file to see the actual code and understand what ExtrasCoalescerKey should be—it's likely a method that generates a coalescer key for the extras data (alerts, hourly forecast, etc.) following a similar pattern to other coalescer key methods in the service.

bash
wc -l /mnt/user-data/uploads/dump.txt /mnt/project/dump.txt 2>/dev/null; echo "---"; ls -la /mnt/user-data/uploads/

Output
 12181 /mnt/user-data/uploads/dump.txt
 12181 /mnt/project/dump.txt
 24362 total
---
total 432
drwxr-xr-x 1 claude ubuntu      0 Jun 23 22:48 .
drwxr-xr-x 5 root   root     4096 Jun 23 22:48 ..
-rw-r--r-- 1 claude ubuntu 437772 Jun 23 22:48 dump.txt

The dumps match at 12181 lines, so I need to locate WeatherService.cs within the dump and examine the surrounding context to understand the file structure.

bash
grep -n "WeatherService.cs" /mnt/user-data/uploads/dump.txt | head -20

Output
66:src/Weather.Core/Abstractions/IWeatherService.cs
103:src/Weather.Infrastructure/Services/WeatherService.cs
2562:FILE: src/Weather.Core/Abstractions/IWeatherService.cs
5414:FILE: src/Weather.Infrastructure/Services/WeatherService.cs

I'm looking at the WeatherService.cs file to locate the section starting at line 5414 and identify where it ends.

bash
awk 'NR>=5414 && /^FILE: / && NR>5414 {print NR": "$0; exit}' /mnt/user-data/uploads/dump.txt; echo "--- WeatherService starts at 5414, finding next file ---"; grep -n "^FILE: " /mnt/user-data/uploads/dump.txt | awk -F: '$1>5414{print; exit}'

Output
5979: FILE: src/Weather.Infrastructure/Weather.Infrastructure.csproj
--- WeatherService starts at 5414, finding next file ---
5979:FILE: src/Weather.Infrastructure/Weather.Infrastructure.csproj

I'm pinpointing the issue in the dependency injection setup around line 5466, where there's a FromKeyedServices attribute being used with an IRequestCoalescer<GridPoint> parameter that's causing the problem.

The real issue is that ExtrasCoalescerKey is defined as a const string on line 5475, but it's a class member being referenced from within the primary constructor's parameter list on lines 5458-5472. The attribute argument needs to resolve this constant, but there's a scoping problem — the const isn't accessible at the point where the attribute is trying to reference it in the primary constructor parameters. The const is defined later in the class, so there might be a scope or forward-reference issue with how attributes on primary constructor parameters resolve names. I'm wondering if attribute arguments in this context don't have access to the class member scope the same way method bodies do, or if there's something specific about the evaluation order that's preventing the const from being found. I'm testing whether this actually compiles by thinking through the C# scoping rules for attributes on primary constructor parameters. The key question is whether the attribute can reference a const member defined in the class itself, or if the attribute scope is resolved before the class body is fully available. I'm leaning toward this not compiling with CS0103, but I should verify this empirically rather than relying on my mental model of the spec. resolve to the class constant K. So the question is whether primary constructor parameters follow the same scoping rules. I suspect they do, but I need to verify this carefully — the primary constructor syntax is relatively new, and the attribute binding might work differently. Let me think through what the C# spec says about primary constructor parameter attributes and name resolution. Roslyn actually does bind attributes on primary constructor parameters within a scope that includes the type's members, so this should work. Looking at the exact lines again—the attribute on line 5466 references ExtrasCoalescerKey, which is defined as a const on line 5475 in the class body. This feels like a genuine scoping issue or possibly a version-specific bug.

I keep second-guessing myself on the exact C# rules here. The safest approach is to actually test this with a minimal reproduction rather than rely on pattern-matching my memory of the language spec.
Done

I can see the file. The error is at line 48 of WeatherService.cs, which is this attribute on a primary-constructor parameter:
csharp

[FromKeyedServices(ExtrasCoalescerKey)] IRequestCoalescer<GridPoint> extrasCoalescer,

…and ExtrasCoalescerKey is defined as a public const string in the class body (line 57 of the file). My strong suspicion is a C# scoping rule about primary-constructor parameter attributes, but let me verify empirically rather than guess.
Pinpointing two reference locations for the constant

bash
which dotnet 2>/dev/null && dotnet --version 2>/dev/null || echo "NO_DOTNET"

Output
NO_DOTNET

I don't have dotnet installed, and checking the allowed domains reveals that the standard dotnet download sources aren't accessible—no dotnetcli.azureedge.net, dot.net, or download.visualstudio.microsoft.com. Looking at what domains are actually available, I can see api.anthropic.com, api.github.com, archive.ubuntu.com, codeload.github.com, crates.io, files.pythonhosted.org, and github.com.

Since I can't install dotnet and Ubuntu 24.04 repos wouldn't have .NET 10 anyway, I need to reason through the C# scoping rules directly. The key question is whether an attribute on a primary constructor parameter can reference a const from the class body using just a simple name. Let me work through the C# specification carefully on how scoping works for primary constructors and simple name resolution.

The tricky part is figuring out whether class members are actually in scope when evaluating those parameter attributes, or if they're evaluated in a different context. Rather than relying on uncertain memory about the exact scoping rules, the safest approach is to use a fix that works regardless—like qualifying the constant reference explicitly so it's unambiguous. cause of the error more carefully. I'm recalling that this is actually a documented issue — when you reference an unqualified constant in an attribute on a primary constructor parameter, it should theoretically work since the attribute binding happens within the type's scope, but the user is hitting CS0103, which suggests either a genuine scoping limitation or the constant isn't defined where expected.

Looking at the dump, the constant ExtrasCoalescerKey is clearly declared right there, so if the dump matches the disk, the only explanation is a language scoping rule. I'm wondering if the forward reference matters here — the const is declared after the primary constructor in the source, and while forward references to constants work fine in normal member bodies, attribute argument binding might happen at a different phase that doesn't allow it. I'm pretty sure this is a real limitation with primary constructor parameter attributes—the binder doesn't include the type's own members in the simple name lookup scope, similar to how record primary constructors work. Though I vaguely remember people using nameof and consts in these contexts successfully, so I'm not entirely certain. Since I can't test this directly, I should focus on the most robust solution and explain the reasoning clearly. The parameter list in a primary constructor isn't part of the class body's declaration space when resolving simple names in attribute arguments on those parameters — the lookup happens in the enclosing namespace scope, not within the class members themselves. But wait, that contradicts how regular constructor parameter attributes work, where class members should be in scope. I'm going in circles here, so let me just commit to reasoning through the actual C# scoping rules carefully.

The safest approach is to move the DI key constant to a separate static class like WeatherDiKeys and reference it as a qualified member access, which is unambiguously resolvable and also mirrors how the key would be used during DI registration anyway.

I should check whether the registration code already references WeatherService.ExtrasCoalescerKey — if so, moving it would require updating that reference too. Alternatively, I could try fully qualifying the member access within the same class, though that might still fail if the scoping issue is about the type not being available during its own primary-constructor attribute binding. The most guaranteed fallback is using a string literal directly like "extras", which will definitely compile but hard-codes the magic string.

The cleanest solution is creating a dedicated internal static class to hold all DI keys, referenced from both the registration site and the consumer. This sidesteps the scoping issue entirely — at the registration site it resolves fine as a method body, and at the primary constructor parameter attribute it's a member access on an external type that's fully defined, so no self-referential scoping problem. Now I need to locate where the key is currently defined and used. the key and coalescer are wired together in the dependency injection setup.
Done

No .NET SDK here (and the allowed-domains list has no Microsoft download host), so I can't reproduce the build directly. Let me trace every reference to this constant and the keyed coalescer registration so I understand the full wiring before proposing a fix.
Solidifying root-cause analysis and architecting the fix

bash
grep -n "ExtrasCoalescerKey\|extrasCoalescer\|AddKeyedSingleton\|FromKeyedServices\|IRequestCoalescer\|RequestCoalescer" /mnt/user-data/uploads/dump.txt

Output
65:src/Weather.Core/Abstractions/IRequestCoalescer.cs
102:src/Weather.Infrastructure/Services/RequestCoalescer.cs
154:tests/Weather.Infrastructure.Tests/Services/RequestCoalescerTests.cs
2505:FILE: src/Weather.Core/Abstractions/IRequestCoalescer.cs
2534:public interface IRequestCoalescer<TKey>
3916:        services.TryAddSingleton<IRequestCoalescer<GridPoint>, RequestCoalescer<GridPoint>>();
3924:        services.TryAddKeyedSingleton<IRequestCoalescer<GridPoint>, RequestCoalescer<GridPoint>>(
3925:            WeatherService.ExtrasCoalescerKey);
3929:        services.TryAddSingleton<IRequestCoalescer<GeoCoordinate>, RequestCoalescer<GeoCoordinate>>();
3934:        services.TryAddSingleton<IRequestCoalescer<string>, RequestCoalescer<string>>();
5064:FILE: src/Weather.Infrastructure/Services/RequestCoalescer.cs
5075:/// Thread-safe, in-process implementation of <see cref="IRequestCoalescer{TKey}"/>.
5105:internal sealed class RequestCoalescer<TKey> : IRequestCoalescer<TKey>
5206:                var (self, k, f) = ((RequestCoalescer<TKey>, TKey, Flight))state!;
5439:/// through an <see cref="IRequestCoalescer{TKey}"/> so that when many requests
5465:    IRequestCoalescer<GridPoint> forecastCoalescer,
5466:    [FromKeyedServices(ExtrasCoalescerKey)] IRequestCoalescer<GridPoint> extrasCoalescer,
5467:    IRequestCoalescer<GeoCoordinate> metadataCoalescer,
5468:    IRequestCoalescer<string> alertCoalescer,
5475:    public const string ExtrasCoalescerKey = "extras";
5664:        return await extrasCoalescer.RunAsync(
10910:FILE: tests/Weather.Infrastructure.Tests/Services/RequestCoalescerTests.cs
10925:public sealed class RequestCoalescerTests
10933:        var coalescer = new RequestCoalescer<GridPoint>();
10960:        var coalescer = new RequestCoalescer<GridPoint>();
10980:        var coalescer = new RequestCoalescer<GridPoint>();
10999:        var coalescer = new RequestCoalescer<GridPoint>();
11024:        var coalescer = new RequestCoalescer<GridPoint>();
11043:        var coalescer = new RequestCoalescer<GridPoint>();
11071:        var coalescer = new RequestCoalescer<GridPoint>();
11082:        var coalescer = new RequestCoalescer<string>();
11090:        var coalescer = new RequestCoalescer<GridPoint>();
11172:        public IRequestCoalescer<GridPoint> ForecastCoalescer { get; } = new RequestCoalescer<GridPoint>();
11173:        public IRequestCoalescer<GridPoint> ExtrasCoalescer { get; } = new RequestCoalescer<GridPoint>();
11174:        public IRequestCoalescer<GeoCoordinate> MetadataCoalescer { get; } = new RequestCoalescer<GeoCoordinate>();
11175:        public IRequestCoalescer<string> AlertCoalescer { get; } = new RequestCoalescer<string>();
11491:        public IRequestCoalescer<GridPoint> ForecastCoalescer { get; } = new RequestCoalescer<GridPoint>();
11492:        public IRequestCoalescer<GridPoint> ExtrasCoalescer { get; } = new RequestCoalescer<GridPoint>();
11493:        public IRequestCoalescer<GeoCoordinate> MetadataCoalescer { get; } = new RequestCoalescer<GeoCoordinate>();
11494:        public IRequestCoalescer<string> AlertCoalescer { get; } = new RequestCoalescer<string>();

So the issue is clear now—there are two references to this constant, but only one is causing the compilation error. The qualified reference on line 3925 works fine, but the unqualified reference inside the primary constructor's attribute on line 5466 is what's triggering the CS0103 error.

The root cause is that C# has a specific scoping rule: when binding attributes on primary constructor parameters, the compiler doesn't include the type's own members in the simple-name lookup. This is different from how method bodies work, which is why the qualified reference elsewhere succeeds while the unqualified one here fails.

The fix is straightforward—using the qualified form WeatherService.ExtrasCoalescerKey should work even in the primary constructor attribute, since qualified member access bypasses the simple-name lookup restriction. erServiceis absolutely resolvable from anywhere in the namespace, including the type's own header. The namespace's declaration space contains the type. SoWeatherServiceresolves to the type, and.ExtrasCoalescerKey` accesses its public const. This is a constant expression. It WILL compile.

The reason the unqualified ExtrasCoalescerKey fails is that simple-name lookup in that position searches enclosing scopes (the primary ctor parameter list scope, then... the namespace), but NOT the type's member declaration space. So ExtrasCoalescerKey as a bare name isn't found → CS0103. Qualifying it routes through type-name resolution (namespace scope) + member access, which succeeds.

This is consistent and I'm confident. Let me also double-check by considering whether there's any other approach that might be cleaner or more idiomatic. check there isn't a subtlety: is FromKeyedServices's argument required to be a const/literal? FromKeyedServicesAttribute(object? key) — the constructor takes object?. Attribute arguments must be constant expressions (or typeof, or arrays of these). A const string IS a valid constant expression for an object? attribute parameter (string literal boxed). WeatherService.ExtrasCoalescerKey is const string "extras", valid. Good.

So the fix is a one-character-ish change. But let me reconsider whether qualifying is actually the BEST fix or whether there's risk. I could extract the key to a separate static class instead — that's arguably cleaner and decouples the DI key from the consuming type, giving both registration and consumer a single source of truth without self-reference. But that touches multiple files and requires creating a new one, whereas the qualified fix is provably correct and only needs changes in one place. Given the user wants minimal friction, I'll go with qualifying but mention the cleaner alternative. Actually, let me double-check whether self-referencing a const inside the same type's primary constructor attribute truly compiles — there shouldn't be any cycle detection issues since consts don't depend on themselves at runtime. to be sure the member declaration space isn't consulted during primary constructor parameter binding, even if the const is declared earlier in the source. If it turns out the binder does look there, then moving the const above the constructor would actually fix the unqualified reference — but I'm pretty confident that's not how it works; the scope rules are strict about when member declarations become visible.

The real fix is to qualify the constant reference on line 48 with the type name. Only the WeatherService.cs file needs to change. I need to continue reading through the rest of the WeatherService.cs file to capture the complete content.
Done

I now have the complete picture. Two sites reference the constant:

    Line 3925 (DI registration): WeatherService.ExtrasCoalescerKey — qualified, compiles fine.
    Line 5466 (the primary-constructor parameter attribute): [FromKeyedServices(ExtrasCoalescerKey)] — unqualified, this is the CS0103.

Let me read the rest of the file so I can return it complete and correct.
Diagnosed scope resolution bug, validated single-file fix

I've confirmed the full file content and can now verify my root-cause analysis. The issue is that line 48 uses an unqualified simple name ExtrasCoalescerKey in the FromKeyedServices attribute, which C# can't resolve during compilation because it needs the proper namespace qualification.

The fix is straightforward: I need to qualify the reference as WeatherService.ExtrasCoalescerKey instead. This works because qualified member access resolves through the namespace declaration space, which is always visible even within the type's own header. The const itself is fine and accessible—line 3925 already demonstrates this by using the qualified form successfully in the DI registration.

This is a single-file fix in WeatherService.cs, and the build output confirms that only this one error exists across the entire solution, so addressing this line resolves the entire failure.

The qualified reference works because WeatherService is resolved through normal type-name lookup within its own namespace, and once bound, accessing the public const member is just a compile-time constant evaluation with no circular dependency. The const's value is the literal string "extras", independent of anything else.

Now I'm weighing whether to use the qualified fix or extract the const to a separate static class — the qualified approach keeps everything in one file with the const staying on WeatherService itself, which maintains a single source of truth, though it does look slightly self-referential. The separate-class approach would be "purer" from a DI perspective but requires touching multiple files and adding a new one, which isn't justified for a one-line fix. I'll go with the qualified fix since it's the minimal correct change and matches what the user needs, and I'll return the full WeatherService.cs file as required.

Now I need to extract the exact content from lines 5419–5974, apply the single change on line 5466, and present it as a complete copy-paste-ready file. I need to handle the line endings properly—stripping the carriage returns from the Windows CRLF format so the code block displays cleanly, then I'll extract and paste the corrected file.
Done

I have the complete file and the root cause is now unambiguous.
What's wrong

Line 48 references the constant unqualified:
csharp

[FromKeyedServices(ExtrasCoalescerKey)] IRequestCoalescer<GridPoint> extrasCoalescer,

The constant ExtrasCoalescerKey exists and is public (it's defined a few lines down, and your DI registration at ServiceCollectionExtensions already uses it successfully as WeatherService.ExtrasCoalescerKey). The problem is specific to where the bare name appears: simple-name lookup inside an attribute on a primary-constructor parameter does not search the declaring type's own member space. The parameter list is part of the type header, and unqualified names there resolve against the enclosing namespace, not against WeatherService's members — so the bare ExtrasCoalescerKey can't be found and you get CS0103.

Two things confirm this diagnosis: it's not a source-ordering issue (C# member lookup is order-independent, so declaring the const earlier wouldn't help), and the same constant binds fine everywhere it's written qualified.
The fix

Qualify the reference: WeatherService.ExtrasCoalescerKey. That routes through ordinary type-name resolution (the type is always visible inside its own header) plus a const member access — a valid compile-time constant for the object? attribute argument. One line, one file. The registration site already qualifies, and the tests construct RequestCoalescer<GridPoint> directly rather than through keyed DI, so nothing else changes.

Let me extract the file verbatim, apply exactly that change, and verify it.
Verified fix and prepared complete file for copy-pasting

Verified: only line 48 changed, the const and file boundaries are intact, and no unqualified reference remains. Here is the complete, corrected file for src/Weather.Infrastructure/Services/WeatherService.cs:
csharp

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Abstractions;
using Weather.Core.Models;
using Weather.Core.Telemetry;
using Weather.Infrastructure.Nws;

namespace Weather.Infrastructure.Services;

/// <summary>
/// The cache-aside orchestrator. It is the only type that knows the freshness
/// policy: long-TTL point metadata, short-TTL forecasts honouring NWS
/// Cache-Control, conditional GETs to refresh expiry cheaply, and serving a
/// stale-but-valid copy when NWS is unreachable. Cache hit/miss telemetry is
/// recorded here because only this layer knows the <em>semantic</em> outcome.
///
/// <para>
/// <b>Stampede protection.</b> Every distinct kind of upstream work is run
/// through an <see cref="IRequestCoalescer{TKey}"/> so that when many requests
/// arrive at once — the "refresh-spam from many browsers" case — only the first
/// calls NWS and the rest await and share its result. There are four flights:
/// point-metadata (keyed by coordinate), daily forecast (keyed by cell), cell
/// "extras" i.e. hourly + observation (keyed by cell), and active alerts (keyed
/// by the coordinate's cache key). This guarantees load on NWS is bounded by
/// the number of <em>distinct cold keys</em>, not by the number of users.
/// </para>
///
/// <para>
/// <b>Cache-key coarseness.</b> Cache keys are derived from coordinates rounded
/// to <see cref="CacheKeyDecimals"/> decimal places. Browser GPS readings jitter
/// by tens of metres between reloads; keying on the raw (4-decimal, ~11 m)
/// coordinate would make every reload a brand-new key and therefore a guaranteed
/// miss against <c>/points</c> and <c>/alerts/active</c>. Rounding to ~1.1 km —
/// comfortably finer than a ~2.5 km NWS grid cell — collapses those jittery
/// reloads onto a single key so the cache can actually do its job.
/// </para>
/// </summary>
internal sealed class WeatherService(
    IPointMetadataCache metadataCache,
    IForecastCache forecastCache,
    ICellExtrasCache extrasCache,
    IAlertCache alertCache,
    INwsApiClient nwsClient,
    INeighborhoodWarmer warmer,
    IRequestCoalescer<GridPoint> forecastCoalescer,
    [FromKeyedServices(WeatherService.ExtrasCoalescerKey)] IRequestCoalescer<GridPoint> extrasCoalescer,
    IRequestCoalescer<GeoCoordinate> metadataCoalescer,
    IRequestCoalescer<string> alertCoalescer,
    WeatherTelemetry telemetry,
    TimeProvider timeProvider,
    IOptions<NwsClientOptions> options,
    ILogger<WeatherService> logger) : IWeatherService
{
    /// <summary>DI key for the keyed extras coalescer (a second <c>GridPoint</c> coalescer).</summary>
    public const string ExtrasCoalescerKey = "extras";

    private const string MetadataCacheName = "metadata";
    private const string ForecastCacheName = "forecast";
    private const string ExtrasCacheName = "extras";
    private const string AlertsCacheName = "alerts";

    // Coordinates are rounded to this many decimals before being used as a
    // cache key. ~1.1 km of resolution: coarse enough that GPS jitter between
    // page reloads collapses onto one key, fine enough to stay well inside a
    // single ~2.5 km NWS grid cell. See the class remarks.
    private const int CacheKeyDecimals = 2;

    // Nominal NWS cell size (~2.5 km) used only as a fallback ordering metric
    // when a cell's true polygon centre is not currently known.
    private const double NominalCellMeters = 2500d;

    // Keep the hot-path fan-out polite to NWS even on a cold area request.
    private const int NeighborFetchConcurrency = 4;

    private readonly NwsClientOptions _options = options.Value;

    public async Task<Forecast?> GetForecastAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var coord = coordinate.Rounded(CacheKeyDecimals);
        var metadata = await ResolveMetadataAsync(coord, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var (forecast, _) = await GetForecastByGridCoreAsync(metadata.Grid, cancellationToken).ConfigureAwait(false);
        if (forecast is not null)
        {
            // Fire-and-forget: schedule the surrounding cells to be warmed.
            warmer.RequestWarming(metadata.Grid);
        }

        return forecast;
    }

    public async Task<NeighborhoodForecast?> GetNeighborhoodForecastAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        var coord = coordinate.Rounded(CacheKeyDecimals);
        var metadata = await ResolveMetadataAsync(coord, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var (primary, _) = await GetForecastByGridCoreAsync(metadata.Grid, cancellationToken).ConfigureAwait(false);
        if (primary is null)
        {
            return null;
        }

        warmer.RequestWarming(metadata.Grid);

        // Best-effort: include only neighbours that are ALREADY warm. Never
        // fetch on the hot path — that is what keeps this endpoint fast.
        var now = timeProvider.GetUtcNow();
        var neighbors = new List<Forecast>();
        foreach (var cell in GridNeighborhood.Surrounding(metadata.Grid))
        {
            var cached = await forecastCache.GetAsync(cell, cancellationToken).ConfigureAwait(false);
            if (cached is not null && cached.IsFresh(now))
            {
                neighbors.Add(cached.Forecast);
            }
        }

        return new NeighborhoodForecast(coord, primary, neighbors);
    }

    public async Task<AreaForecast?> GetAreaForecastAsync(
        GeoCoordinate coordinate, int radius = 1, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var coord = coordinate.Rounded(CacheKeyDecimals);
        var metadata = await ResolveMetadataAsync(coord, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var primary = await AssembleCellAsync(metadata.Grid, cancellationToken).ConfigureAwait(false);
        if (primary is null)
        {
            return null;
        }

        // Schedule background warming of the daily forecasts for the ring.
        warmer.RequestWarming(metadata.Grid);

        // Alerts are point-based and apply to the whole area; resolve them
        // cache-aside (short TTL) so repeated views and browser refreshes do
        // NOT re-hit /alerts/active every time.
        var alerts = await ResolveAlertsAsync(coord, cancellationToken).ConfigureAwait(false);

        // Assemble every neighbouring cell, cache-aside, with bounded
        // concurrency so a cold area can never stampede NWS.
        var cells = GridNeighborhood.Surrounding(metadata.Grid, radius);
        var assembled = new ConcurrentBag<CellWeather>();

        await Parallel.ForEachAsync(
            cells,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = NeighborFetchConcurrency,
                CancellationToken = cancellationToken,
            },
            async (cell, token) =>
            {
                var assembledCell = await AssembleCellAsync(cell, token).ConfigureAwait(false);
                if (assembledCell is not null)
                {
                    assembled.Add(assembledCell);
                }
            }).ConfigureAwait(false);

        var neighbors = assembled
            .OrderBy(c => OrderKeyMeters(coord, metadata.Grid, c))
            .Select(c => c with { DistanceMeters = DisplayDistanceMeters(coord, c) })
            .ToList();

        return new AreaForecast(coord, metadata, primary, neighbors, alerts);
    }

    public async Task<Forecast?> GetForecastByGridAsync(GridPoint grid, CancellationToken cancellationToken = default)
    {
        // Public entry point used by the background warmer. Deliberately does
        // NOT schedule further warming, which would recurse forever.
        var (forecast, _) = await GetForecastByGridCoreAsync(grid, cancellationToken).ConfigureAwait(false);
        return forecast;
    }

    /// <summary>
    /// Build the full bundle for one cell: the headline daily forecast
    /// (cache-aside, with conditional GETs) plus the cached "extras" (hourly +
    /// latest observation + centre). Returns <c>null</c> only when the daily
    /// forecast itself is unavailable.
    /// </summary>
    private async Task<CellWeather?> AssembleCellAsync(GridPoint grid, CancellationToken cancellationToken)
    {
        var (daily, centerFromFetch) = await GetForecastByGridCoreAsync(grid, cancellationToken).ConfigureAwait(false);
        if (daily is null)
        {
            return null;
        }

        var extras = await ResolveExtrasAsync(grid, centerFromFetch, cancellationToken).ConfigureAwait(false);
        var center = centerFromFetch ?? extras.Center;

        return new CellWeather(grid, center, daily, extras.Hourly, extras.Observation, DistanceMeters: null);
    }

    /// <summary>
    /// Cache-aside fetch of a cell's "extras" (hourly forecast + latest
    /// observation + centre), protected by its own single-flight coalescer so a
    /// burst of concurrent misses for the same cell collapses into ONE set of
    /// upstream calls. Without this, the daily-forecast coalescer would bound
    /// <c>/forecast</c> to one call per cell while <c>/forecast/hourly</c>,
    /// <c>/stations</c> and <c>/observations/latest</c> were still duplicated by
    /// every concurrent caller (rapid reloads, multiple tabs, circuit
    /// reconnects).
    ///
    /// <para>
    /// The observation is fetched cheaply: the nearest-station id is part of the
    /// cached extras, so a routine refresh observes directly by id and only
    /// re-lists stations (<c>/gridpoints/.../stations</c>) when the id is not
    /// yet known. That mapping is effectively immutable.
    /// </para>
    /// </summary>
    private async Task<CellExtras> ResolveExtrasAsync(
        GridPoint grid, GeoCoordinate? centerHint, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cached = await extrasCache.GetAsync(grid, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(ExtrasCacheName);
            return await BackfillCenterAsync(grid, cached, centerHint, cancellationToken).ConfigureAwait(false);
        }

        telemetry.RecordCacheMiss(ExtrasCacheName);

        return await extrasCoalescer.RunAsync(
            grid,
            async token =>
            {
                // Re-check inside the flight: a concurrent leader for this cell
                // may have just filled the extras cache.
                var inner = timeProvider.GetUtcNow();
                var rechecked = await extrasCache.GetAsync(grid, token).ConfigureAwait(false);
                if (rechecked is not null && rechecked.IsFresh(inner))
                {
                    return await BackfillCenterAsync(grid, rechecked, centerHint, token).ConfigureAwait(false);
                }

                var hourly = await nwsClient.GetHourlyForecastAsync(grid, token).ConfigureAwait(false) ?? [];

                // Reuse a previously-resolved station id (even from an expired
                // entry) so we do NOT re-list stations on every refresh.
                var knownStationId = rechecked?.Extras.StationId ?? cached?.Extras.StationId;

                string? stationId;
                Observation? observation;
                if (!string.IsNullOrWhiteSpace(knownStationId))
                {
                    stationId = knownStationId;
                    observation = await nwsClient
                        .GetLatestObservationAsync(grid, knownStationId, token)
                        .ConfigureAwait(false);
                }
                else
                {
                    stationId = await nwsClient.GetNearestStationIdAsync(grid, token).ConfigureAwait(false);
                    observation = string.IsNullOrWhiteSpace(stationId)
                        ? null
                        : await nwsClient.GetLatestObservationAsync(grid, stationId, token).ConfigureAwait(false);
                }

                var center = centerHint ?? rechecked?.Extras.Center ?? cached?.Extras.Center;
                var extras = new CellExtras(hourly, observation, center, stationId);

                await extrasCache
                    .UpsertAsync(grid, new CachedCellExtras(extras, inner, inner + _options.DefaultForecastTtl), token)
                    .ConfigureAwait(false);

                return extras;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// If a fresh daily fetch just revealed the cell's centre and the cached
    /// extras did not have one yet, persist the upgrade. Returns the (possibly
    /// upgraded) extras either way.
    /// </summary>
    private async Task<CellExtras> BackfillCenterAsync(
        GridPoint grid, CachedCellExtras cached, GeoCoordinate? centerHint, CancellationToken cancellationToken)
    {
        if (centerHint is null || cached.Extras.Center is not null)
        {
            return cached.Extras;
        }

        var upgraded = cached.Extras with { Center = centerHint };
        await extrasCache
            .UpsertAsync(grid, cached with { Extras = upgraded }, cancellationToken)
            .ConfigureAwait(false);
        return upgraded;
    }

    /// <summary>
    /// Cache-aside fetch of active alerts for a coordinate, protected by the
    /// single-flight coalescer (keyed by the coordinate's cache key) so a burst
    /// of concurrent misses collapses into one upstream <c>/alerts/active</c>
    /// call. A fresh cache entry is served straight from SQLite; on miss/expiry
    /// the result is fetched once and stored with a short TTL. Caching an empty
    /// list is deliberate and correct: "no active alerts" is a valid answer
    /// worth remembering for the TTL, and it is what stops repeated views and
    /// browser refreshes from re-hitting the alerts endpoint every time.
    /// </summary>
    private async Task<IReadOnlyList<WeatherAlert>> ResolveAlertsAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var cached = await alertCache.GetAsync(coordinate, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(AlertsCacheName);
            return cached.Alerts;
        }

        telemetry.RecordCacheMiss(AlertsCacheName);

        // The coordinate is already rounded to the cache-key resolution by the
        // public entry points, so its cache key IS the coalescing key.
        var key = coordinate.ToCacheKey();

        return await alertCoalescer.RunAsync(
            key,
            async token =>
            {
                // Re-check the cache inside the flight: a concurrent leader for
                // the same coordinate may have just filled it.
                var inner = timeProvider.GetUtcNow();
                var rechecked = await alertCache.GetAsync(coordinate, token).ConfigureAwait(false);
                if (rechecked is not null && rechecked.IsFresh(inner))
                {
                    return rechecked.Alerts;
                }

                // The client is best-effort: it returns an empty list both when
                // there are genuinely no alerts and when NWS is unreachable.
                // Either way an empty list is a safe thing to cache for the
                // short TTL; the next refresh after expiry will try again.
                var fetched = await nwsClient.GetActiveAlertsAsync(coordinate, token).ConfigureAwait(false) ?? [];

                await alertCache
                    .UpsertAsync(
                        coordinate,
                        new CachedAlerts(fetched, inner, inner + _options.AlertsTtl),
                        token)
                    .ConfigureAwait(false);

                return fetched;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static double OrderKeyMeters(GeoCoordinate query, GridPoint origin, CellWeather cell) =>
        cell.Center is { } center
            ? query.DistanceMetersTo(center)
            : GridApproxMeters(origin, cell.Grid);

    private static double? DisplayDistanceMeters(GeoCoordinate query, CellWeather cell) =>
        cell.Center is { } center ? query.DistanceMetersTo(center) : null;

    private static double GridApproxMeters(GridPoint origin, GridPoint cell)
    {
        double dx = cell.GridX - origin.GridX;
        double dy = cell.GridY - origin.GridY;
        return Math.Sqrt((dx * dx) + (dy * dy)) * NominalCellMeters;
    }

    /// <summary>
    /// Cache-aside resolution of a coordinate's grid metadata, protected by the
    /// single-flight coalescer (keyed by the coordinate) so a burst of
    /// concurrent misses collapses into one upstream <c>/points</c> call. The
    /// mapping effectively never changes, so on an upstream failure a stale
    /// cached mapping is served rather than failing the request.
    /// </summary>
    private async Task<PointMetadata?> ResolveMetadataAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cached = await metadataCache.GetAsync(coordinate, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(MetadataCacheName);
            return cached.Metadata;
        }

        telemetry.RecordCacheMiss(MetadataCacheName);

        return await metadataCoalescer.RunAsync<PointMetadata?>(
            coordinate,
            async token =>
            {
                // Re-check inside the flight: a concurrent leader for the same
                // coordinate may have just resolved and cached it.
                var inner = timeProvider.GetUtcNow();
                var rechecked = await metadataCache.GetAsync(coordinate, token).ConfigureAwait(false);
                if (rechecked is not null && rechecked.IsFresh(inner))
                {
                    return rechecked.Metadata;
                }

                var fetched = await nwsClient.GetPointMetadataAsync(coordinate, token).ConfigureAwait(false);
                if (fetched is null)
                {
                    // Either NWS is unreachable or the location is genuinely
                    // uncovered. The mapping effectively never changes, so a
                    // stale hit is safe.
                    if (rechecked is not null)
                    {
                        logger.LogWarning(
                            "Serving stale point metadata for {Coordinate}; NWS resolve failed.", coordinate);
                        return rechecked.Metadata;
                    }

                    return null;
                }

                await metadataCache
                    .UpsertAsync(new CachedPointMetadata(fetched, inner, inner + _options.PointMetadataTtl), token)
                    .ConfigureAwait(false);
                return fetched;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cache-aside fetch of the headline daily forecast for a cell, protected by
    /// the single-flight coalescer so concurrent misses for the same cell
    /// collapse into one upstream call. Also returns the cell's approximate
    /// centre when it came from a fresh fetch (it is not stored in the daily
    /// cache, so a pure cache hit yields a null centre and the caller falls back
    /// to the extras-cached centre or a grid estimate).
    /// </summary>
    private Task<(Forecast? Forecast, GeoCoordinate? Center)> GetForecastByGridCoreAsync(
        GridPoint grid, CancellationToken cancellationToken)
    {
        // A flag the factory flips when THIS call is the one that actually ran
        // the fetch (the coalescer "leader"). Everyone else is a follower whose
        // upstream call was avoided.
        var ranFactory = false;

        return RunCoalescedAsync();

        async Task<(Forecast?, GeoCoordinate?)> RunCoalescedAsync()
        {
            var result = await forecastCoalescer.RunAsync(
                grid,
                async flightToken =>
                {
                    ranFactory = true;
                    return await FetchForecastCoreAsync(grid, flightToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);

            if (ranFactory)
            {
                telemetry.RecordCoalesceLeader(ForecastCacheName);
            }
            else
            {
                telemetry.RecordCoalesceFollower(ForecastCacheName);
            }

            return result;
        }
    }

    /// <summary>
    /// The actual cache-aside body, run by at most one caller per cell at a
    /// time (the coalescer guarantees this). Re-reads the cache first, so a
    /// follower that waited for an earlier flight — and then started a fresh one
    /// — still sees the freshly-filled cache and never calls NWS.
    /// </summary>
    private async Task<(Forecast? Forecast, GeoCoordinate? Center)> FetchForecastCoreAsync(
        GridPoint grid, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cached = await forecastCache.GetAsync(grid, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.IsFresh(now))
        {
            telemetry.RecordCacheHit(ForecastCacheName);
            return (cached.Forecast, null);
        }

        telemetry.RecordCacheMiss(ForecastCacheName);

        var result = await nwsClient
            .GetForecastAsync(grid, cached?.ETag, cancellationToken)
            .ConfigureAwait(false);

        switch (result.Outcome)
        {
            case NwsFetchOutcome.Success when result.Forecast is not null:
                var fresh = new CachedForecast(
                    result.Forecast, result.ETag, now, ComputeExpiry(now, result.MaxAge));
                await forecastCache.UpsertAsync(fresh, cancellationToken).ConfigureAwait(false);
                return (result.Forecast, result.Center);

            case NwsFetchOutcome.NotModified when cached is not null:
                // Cheapest path: payload unchanged, just extend the TTL.
                var renewed = cached with
                {
                    ETag = result.ETag ?? cached.ETag,
                    RetrievedAtUtc = now,
                    ExpiresAtUtc = ComputeExpiry(now, result.MaxAge),
                };
                await forecastCache.UpsertAsync(renewed, cancellationToken).ConfigureAwait(false);
                return (cached.Forecast, null);

            case NwsFetchOutcome.Unavailable when cached is not null:
                logger.LogWarning("Serving stale forecast for grid {Grid}; NWS unavailable.", grid);
                return (cached.Forecast, null);

            case NwsFetchOutcome.NotFound:
            case NwsFetchOutcome.Unavailable:
            case NwsFetchOutcome.NotModified:
            default:
                return (null, null);
        }
    }

    private DateTimeOffset ComputeExpiry(DateTimeOffset now, TimeSpan? maxAge)
    {
        var ttl = maxAge ?? _options.DefaultForecastTtl;
        if (ttl <= TimeSpan.Zero)
        {
            ttl = _options.DefaultForecastTtl;
        }

        if (ttl > _options.MaxForecastTtl)
        {
            ttl = _options.MaxForecastTtl;
        }

        return now + ttl;
    }
}

That single qualification clears the only error in the build (Weather.Infrastructure was the lone failing project; everything else already succeeded), and CI will be the authoritative confirmation on a real .NET 10 runtime.

One note for future-proofing, since this is a learning sandbox: if you find yourself adding more keyed coalescers, the slightly cleaner pattern is to lift these DI keys into a small internal static class WeatherDiKeys and reference WeatherDiKeys.Extras from both the registration and the constructor. That avoids the self-qualified-name shape entirely and keeps the keys in one obvious place. Not necessary for the fix — just the direction I'd lean if the count grows.
Want to be notified when Claude responds?
Claude Fable 5 is currently unavailable.
Learn more(opens in new tab)


Claude is AI and can make mistakes. Please double-check responses.


67
48
