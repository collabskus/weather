I have this dependency 

    D:\DEV\personal\weather\tests\Weather.Infrastructure.Tests\Weather.Infrastructure.Tests.csproj : warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q

    D:\DEV\personal\weather\src\Weather.Api\Weather.Api.csproj : warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q

    D:\DEV\personal\weather\tests\Weather.Api.Tests\Weather.Api.Tests.csproj : warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q

    D:\DEV\personal\weather\src\Weather.Infrastructure\Weather.Infrastructure.csproj : warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11 has a known high severity vulnerability, https://github.com/advisories/GHSA-2m69-gcr7-jv3q

also this test failed 

tests\Weather.Core.Tests\bin\Debug\net10.0\Weather.Core.Tests.dll (net10.0|x64) passed (1s 387ms)

failed UpsertThenGetRoundTripsEveryField (813ms)

  [Test Failure] ShouldAssertException: read.Extras.Center!.Value.Latitude

    should be within

0.0001d

    of

37.088d

    but was

0d

  from tests\Weather.Infrastructure.Tests\bin\Debug\net10.0\Weather.Infrastructure.Tests.dll (net10.0|x64)

  TUnit.Engine.Exceptions.TestFailedException: [Test Failure] ShouldAssertException: read.Extras.Center!.Value.Latitude

      should be within

  0.0001d

      of

  37.088d

      but was

  0d

    at Weather.Infrastructure.Tests.Caching.SqliteCellExtrasCacheTests.UpsertThenGetRoundTripsEveryField() in tests\Weather.Infrastructure.Tests\Caching\SqliteCellExtrasCacheTests.cs:43

    at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()

    at Weather.Infrastructure.Tests.Caching.SqliteCellExtrasCacheTests.UpsertThenGetRoundTripsEveryField() in tests\Weather.Infrastructure.Tests\Caching\SqliteCellExtrasCacheTests.cs:43

    at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()

    at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task task)

    at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task task, ConfigureAwaitOptions options)

    at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()

    at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task task)

    at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task task, ConfigureAwaitOptions options)

    at System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw()

    at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task task)

    at System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification(Task task, ConfigureAwaitOptions options)

       --- TUnit internals omitted (run with --detailed-stacktrace for full trace) ---

tests\Weather.Infrastructure.Tests\bin\Debug\net10.0\Weather.Infrastructure.Tests.dll (net10.0|x64) failed with 1 error(s) (1s 824ms)

Exit code: 2

tests\Weather.Web.Tests\bin\Debug\net10.0\Weather.Web.Tests.dll (net10.0|x64) passed (2s 885ms)

tests\Weather.Api.Tests\bin\Debug\net10.0\Weather.Api.Tests.dll (net10.0|x64) passed (13s 481ms)


  In process file artifacts produced:

    - tests\Weather.Core.Tests\bin\Debug\net10.0\TestResults\Weather.Core.Tests-windows-net10.0-report.html

    - tests\Weather.Infrastructure.Tests\bin\Debug\net10.0\TestResults\Weather.Infrastructure.Tests-windows-net10.0-report.html

    - tests\Weather.Web.Tests\bin\Debug\net10.0\TestResults\Weather.Web.Tests-windows-net10.0-report.html

    - tests\Weather.Api.Tests\bin\Debug\net10.0\TestResults\Weather.Api.Tests-windows-net10.0-report.html


Test run summary: Failed!

  tests\Weather.Infrastructure.Tests\bin\Debug\net10.0\Weather.Infrastructure.Tests.dll (net10.0|x64) failed with 1 error(s) (1s 824ms)

  tests\Weather.Api.Tests\bin\Debug\net10.0\Weather.Api.Tests.dll (net10.0|x64) passed (13s 481ms)

  tests\Weather.Web.Tests\bin\Debug\net10.0\Weather.Web.Tests.dll (net10.0|x64) passed (2s 885ms)

  tests\Weather.Core.Tests\bin\Debug\net10.0\Weather.Core.Tests.dll (net10.0|x64) passed (1s 387ms)


  total: 138

  failed: 1

  succeeded: 137

  skipped: 0

  duration: 14s 161ms

Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exitcodes)


please review completely 



Gemini 3.1 Pro (free)

It looks like you've hit two common bumps when working with modern .NET, centralized package management, and `System.Text.Json`. Both are straightforward to fix.

Here is exactly what is happening and how to resolve each issue.

### **1. The Transitive Vulnerability Warning (NU1903)**

**The Problem:** `Microsoft.Data.Sqlite` depends on the native SQLite library via the `SQLitePCLRaw.lib.e_sqlite3` package. That underlying package has a known CVE in version 2.1.11, which MSBuild is flagging.

**The Fix:** Because you already have `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>` configured in your `Directory.Packages.props`, you can explicitly upgrade and pin that transitive dependency without having to add a direct reference to it in any of your `.csproj` files.

Open `Directory.Packages.props` and add a pinned `<PackageVersion>` for `SQLitePCLRaw.lib.e_sqlite3` to force the patched version (e.g., `2.1.12` or newer) across the entire solution:

```xml
  <ItemGroup>
    <PackageVersion Include="SourceGear.sqlite3" Version="3.50.4.5" />
    <PackageVersion Include="SQLitePCLRaw.lib.e_sqlite3" Version="2.1.12" />
  </ItemGroup>

```

---

### **2. The Failing `SqliteCellExtrasCacheTests` Test**

**The Problem:** Your test is failing because the `GeoCoordinate` comes back from the SQLite cache as `0, 0` instead of `37.088, -76.452`.

This is a silent deserialization failure. In `GeoCoordinate.cs`, you defined a `readonly record struct` with a custom parameterized constructor and get-only properties. By default, `System.Text.Json` tries to use the implicit parameterless constructor for structs (which zeros out everything), and then tries to assign the properties. Since `Latitude` and `Longitude` lack `init` or `set` accessors, the deserializer silently ignores them, leaving the values at zero.

**The Fix:** You need to tell `System.Text.Json` to use your parameterized constructor. This has the added benefit of ensuring your validation logic (`-90` to `90` bounds check) fires during deserialization too.

Open `src/Weather.Core/Models/GeoCoordinate.cs` and add the `[JsonConstructor]` attribute to your constructor:

```csharp
using System.Globalization;
using System.Text.Json.Serialization; // 1. Add this using

namespace Weather.Core.Models;

public readonly record struct GeoCoordinate
{
    public double Latitude { get; }
    public double Longitude { get; }

    [JsonConstructor] // 2. Add this attribute
    public GeoCoordinate(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees.");
        }

        // ... remaining code ...

```

Run `dotnet test` again, and you should have a green tree.
