58
61

I have updated the code and exported it all as `dump.txt` with my `export.ps1` so you can see the latest code at all times 
I have made this powershell export to show you the state of the project 
some tests are failing but I am unable to capture this in powershell output 
I have included the output from visual studio unit tests 
please review the `dump.txt` and fix ALL the issues 
also please make sure we can run this from dotnet test from the terminal 
opt into whatever new convention you need to 
we should be using the latest stable versions of everything anyway since this is a learning sandbox with no legacy users 
also please update all documentation as necessary 
don't go by your old memory 
as I have fixed some syntax such as removing underscores and adding async at the end of method names 
when your memory is older than my dump, the dump is canonical 

PowerShell 7.6.2
PS C:\Users\kushal> Get-Date -Format "yyyy-MM-dd-HH-mm-ss";
2026-06-18-09-06-09
PS C:\Users\kushal> Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; Set-Location "D:\DEV\personal\weather\"; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; git status; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; git remote show origin; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; dotnet list package; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; dotnet list package --outdated; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; dotnet format; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; git status; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; Get-Content "export.ps1"; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; .\export.ps1; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; dotnet clean; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; dotnet restore; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; dotnet build; Get-Date -Format "yyyy-MM-dd-HH-mm-ss"; dotnet test; Get-Date -Format "yyyy-MM-dd-HH-mm-ss";
2026-06-18-09-06-31
2026-06-18-09-06-31
On branch main
Your branch is ahead of 'origin/main' by 8 commits.
  (use "git push" to publish your local commits)

nothing to commit, working tree clean
2026-06-18-09-06-31
* remote origin
  Fetch URL: https://github.com/collabskus/weather.git
  Push  URL: https://github.com/collabskus/weather.git
  HEAD branch: main
  Remote branches:
    dependabot/github_actions/actions/dependency-review-action-5 new (next fetch will store in remotes/origin)
    dependabot/github_actions/actions/setup-node-6               new (next fetch will store in remotes/origin)
    main                                                         tracked
  Local branch configured for 'git pull':
    main merges with remote main
  Local ref configured for 'git push':
    main pushes to main (fast-forwardable)
2026-06-18-09-06-31
Restore complete (1.0s)

Build succeeded in 1.2s
Project 'Weather.Api' has the following package references
   [net10.0]:
   Top-level Package                   Requested   Resolved
   > Microsoft.AspNetCore.OpenApi      10.0.9      10.0.9

Project 'Weather.AppHost' has the following package references
   [net10.0]:
   Top-level Package                              Requested    Resolved
   > Aspire.Dashboard.Sdk.win-x64           (A)   [13.4.5, )   13.4.5
   > Aspire.Hosting.AppHost                       13.4.5       13.4.5
   > Aspire.Hosting.Orchestration.win-x64   (A)   [13.4.5, )   13.4.5

Project 'Weather.Core' has the following package references
   [net10.0]: No packages were found for this framework.
Project 'Weather.Infrastructure' has the following package references
   [net10.0]:
   Top-level Package                           Requested   Resolved
   > Dapper                                    2.1.79      2.1.79
   > Microsoft.Data.Sqlite                     10.0.9      10.0.9
   > Microsoft.Extensions.Hosting              10.0.9      10.0.9
   > Microsoft.Extensions.Http.Resilience      10.7.0      10.7.0
   > System.Threading.RateLimiting             10.0.9      10.0.9

Project 'Weather.ServiceDefaults' has the following package references
   [net10.0]:
   Top-level Package                                   Requested   Resolved
   > Microsoft.Extensions.Http.Resilience              10.7.0      10.7.0
   > Microsoft.Extensions.ServiceDiscovery             10.7.0      10.7.0
   > OpenTelemetry.Exporter.OpenTelemetryProtocol      1.16.0      1.16.0
   > OpenTelemetry.Extensions.Hosting                  1.16.0      1.16.0
   > OpenTelemetry.Instrumentation.AspNetCore          1.15.2      1.15.2
   > OpenTelemetry.Instrumentation.Http                1.15.1      1.15.1
   > OpenTelemetry.Instrumentation.Runtime             1.15.1      1.15.1

Project 'Weather.Web' has the following package references
   [net10.0]:
   Top-level Package                                  Requested    Resolved
   > Microsoft.AspNetCore.App.Internal.Assets   (A)   [10.0.9, )   10.0.9

Project 'Weather.Api.Tests' has the following package references
   [net10.0]:
   Top-level Package                       Requested   Resolved
   > Microsoft.AspNetCore.Mvc.Testing      10.0.9      10.0.9
   > Shouldly                              4.3.0       4.3.0
   > TUnit                                 1.56.0      1.56.0

Project 'Weather.Core.Tests' has the following package references
   [net10.0]:
   Top-level Package      Requested   Resolved
   > Shouldly             4.3.0       4.3.0
   > TUnit                1.56.0      1.56.0

Project 'Weather.Infrastructure.Tests' has the following package references
   [net10.0]:
   Top-level Package      Requested   Resolved
   > NSubstitute          5.3.0       5.3.0
   > Shouldly             4.3.0       4.3.0
   > TUnit                1.56.0      1.56.0

Project 'Weather.Web.Tests' has the following package references
   [net10.0]:
   Top-level Package      Requested   Resolved
   > bunit.web            1.40.0      1.40.0
   > Shouldly             4.3.0       4.3.0
   > TUnit                1.56.0      1.56.0

(A) : Auto-referenced package.
2026-06-18-09-06-35
Restore complete (1.0s)

Build succeeded in 1.2s

The following sources were used:
   https://api.nuget.org/v3/index.json

The given project `Weather.Api` has no updates given the current sources.
The given project `Weather.AppHost` has no updates given the current sources.
The given project `Weather.Core` has no updates given the current sources.
The given project `Weather.Infrastructure` has no updates given the current sources.
The given project `Weather.ServiceDefaults` has no updates given the current sources.
The given project `Weather.Web` has no updates given the current sources.
The given project `Weather.Api.Tests` has no updates given the current sources.
The given project `Weather.Core.Tests` has no updates given the current sources.
The given project `Weather.Infrastructure.Tests` has no updates given the current sources.
The given project `Weather.Web.Tests` has no updates given the current sources.
2026-06-18-09-06-40
2026-06-18-09-06-57
On branch main
Your branch is ahead of 'origin/main' by 8 commits.
  (use "git push" to publish your local commits)

nothing to commit, working tree clean
2026-06-18-09-06-57
# Export Git-tracked ASP.NET Project Files to Single Text File
# Uses `git ls-files` so only committed/staged files are included.

param(
    [string]$ProjectPath = ".",
    [string]$OutputFile = "docs/llm/dump.txt"
)

# File extensions to include (without the leading dot)
$IncludeExtensions = @(
    "cs", "json", "xml", "csproj", "slnx", "sln", "config",
    "cshtml", "razor", "js", "css", "scss", "html",
    "yml", "yaml", "sql", "props", "targets", "sh",
    "ps1"
)

# Exact filenames (no extension match needed)
$IncludeSpecificFiles = @(
    "Dockerfile", ".dockerignore", ".editorconfig",
    ".gitignore", ".gitattributes"
)

# Directories to skip even if tracked (e.g. this script's own output)
$ExcludeDirectories = @("docs")

Write-Host "Starting project export..." -ForegroundColor Green
Write-Host "Project Path: $ProjectPath" -ForegroundColor Yellow
Write-Host "Output File: $OutputFile" -ForegroundColor Yellow

# ── Resolve paths ────────────────────────────────────────────────────────────
Push-Location $ProjectPath
$ResolvedRoot = (Resolve-Path ".").Path

$OutputPath = Join-Path $ResolvedRoot $OutputFile
$outputDir  = Split-Path $OutputPath -Parent
if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }

# ── Get tracked files from Git ───────────────────────────────────────────────
Write-Host "Querying git for tracked files..." -ForegroundColor Cyan

$gitFiles = git ls-files --cached --others --exclude-standard 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: git ls-files failed. Are you inside a git repository?" -ForegroundColor Red
    Pop-Location
    exit 1
}

# Filter to desired extensions / specific filenames, and exclude dirs
$AllFiles = $gitFiles | ForEach-Object {
    $rel = $_
    $name = Split-Path $rel -Leaf
    $ext  = ($name -replace '^.*\.', '').ToLower()

    # Skip excluded directories
    $skip = $false
    foreach ($d in $ExcludeDirectories) {
        if ($rel -like "$d/*" -or $rel -like "$d\*") { $skip = $true; break }
    }
    if ($skip) { return }

    # Match by extension or specific filename
    if ($IncludeExtensions -contains $ext -or $IncludeSpecificFiles -contains $name) {
        $fullPath = Join-Path $ResolvedRoot $rel
        if (Test-Path $fullPath) {
            [PSCustomObject]@{ Relative = $rel; Full = $fullPath }
        }
    }
} | Sort-Object Relative

Write-Host "Found $($AllFiles.Count) files to export" -ForegroundColor Green

# ── Write header ─────────────────────────────────────────────────────────────
$header = @"
===============================================================================
ASP.NET PROJECT EXPORT  (git-tracked files only)
Generated: $(Get-Date)
Project Path: $ResolvedRoot
===============================================================================

"@
$header | Out-File -FilePath $OutputPath -Encoding UTF8

# ── Directory tree (git ls-tree) ─────────────────────────────────────────────
"DIRECTORY STRUCTURE (tracked):" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"==============================" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
""  | Out-File -FilePath $OutputPath -Append -Encoding UTF8

# Show a compact tree of tracked paths
$gitFiles | Sort-Object | Out-File -FilePath $OutputPath -Append -Encoding UTF8

""  | Out-File -FilePath $OutputPath -Append -Encoding UTF8
""  | Out-File -FilePath $OutputPath -Append -Encoding UTF8

# ── File contents ────────────────────────────────────────────────────────────
"FILE CONTENTS:" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"==============" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
""  | Out-File -FilePath $OutputPath -Append -Encoding UTF8

$i = 0
foreach ($f in $AllFiles) {
    $i++
    $info = Get-Item $f.Full
    Write-Host "Processing ($i/$($AllFiles.Count)): $($f.Relative)" -ForegroundColor White

    $sep = "=" * 80
    @"
$sep
FILE: $($f.Relative)
SIZE: $([math]::Round($info.Length / 1KB, 2)) KB
MODIFIED: $($info.LastWriteTime)
$sep

"@ | Out-File -FilePath $OutputPath -Append -Encoding UTF8

    try {
        $content = Get-Content -Path $f.Full -Raw -ErrorAction Stop
        if ($content) {
            $content | Out-File -FilePath $OutputPath -Append -Encoding UTF8
        } else {
            "[EMPTY FILE]" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
        }
    } catch {
        "[ERROR READING FILE: $($_.Exception.Message)]" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    }

    "" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
    "" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
}

# ── Footer ───────────────────────────────────────────────────────────────────
@"
===============================================================================
EXPORT COMPLETED: $(Get-Date)
Total Files Exported: $i
Output File: $OutputPath
===============================================================================
"@ | Out-File -FilePath $OutputPath -Append -Encoding UTF8

Pop-Location

Write-Host "`nExport completed!" -ForegroundColor Green
Write-Host "Total files exported: $i" -ForegroundColor Green
Write-Host "Output file size: $([math]::Round((Get-Item $OutputPath).Length / 1KB, 2)) KB" -ForegroundColor Cyan
2026-06-18-09-06-57
Starting project export...
Project Path: .
Output File: docs/llm/dump.txt
Querying git for tracked files...
Found 110 files to export
Processing (1/110): .editorconfig
Processing (2/110): .gitattributes
Processing (3/110): .github/dependabot.yml
Processing (4/110): .github/workflows/build.yml
Processing (5/110): .github/workflows/codeql.yml
Processing (6/110): .github/workflows/dependency-review.yml
Processing (7/110): .github/workflows/format.yml
Processing (8/110): .github/workflows/markdown-lint.yml
Processing (9/110): .github/workflows/test.yml
Processing (10/110): .gitignore
Processing (11/110): Directory.Build.props
Processing (12/110): Directory.Packages.props
Processing (13/110): export.ps1
Processing (14/110): global.json
Processing (15/110): nuget.config
Processing (16/110): src/Weather.Api/appsettings.Development.json
Processing (17/110): src/Weather.Api/appsettings.json
Processing (18/110): src/Weather.Api/Contracts/ForecastResponse.cs
Processing (19/110): src/Weather.Api/Contracts/NeighborhoodResponse.cs
Processing (20/110): src/Weather.Api/Endpoints/WeatherEndpoints.cs
Processing (21/110): src/Weather.Api/Program.cs
Processing (22/110): src/Weather.Api/Properties/launchSettings.json
Processing (23/110): src/Weather.Api/Weather.Api.csproj
Processing (24/110): src/Weather.AppHost/appsettings.json
Processing (25/110): src/Weather.AppHost/Program.cs
Processing (26/110): src/Weather.AppHost/Weather.AppHost.csproj
Processing (27/110): src/Weather.Core/Abstractions/IForecastCache.cs
Processing (28/110): src/Weather.Core/Abstractions/INeighborhoodWarmer.cs
Processing (29/110): src/Weather.Core/Abstractions/INwsApiClient.cs
Processing (30/110): src/Weather.Core/Abstractions/IPointMetadataCache.cs
Processing (31/110): src/Weather.Core/Abstractions/IWeatherService.cs
Processing (32/110): src/Weather.Core/Models/CachedForecast.cs
Processing (33/110): src/Weather.Core/Models/CachedPointMetadata.cs
Processing (34/110): src/Weather.Core/Models/Forecast.cs
Processing (35/110): src/Weather.Core/Models/ForecastFetchResult.cs
Processing (36/110): src/Weather.Core/Models/ForecastPeriod.cs
Processing (37/110): src/Weather.Core/Models/GeoCoordinate.cs
Processing (38/110): src/Weather.Core/Models/GridNeighborhood.cs
Processing (39/110): src/Weather.Core/Models/GridPoint.cs
Processing (40/110): src/Weather.Core/Models/NeighborhoodForecast.cs
Processing (41/110): src/Weather.Core/Models/PointMetadata.cs
Processing (42/110): src/Weather.Core/Telemetry/WeatherTelemetry.cs
Processing (43/110): src/Weather.Core/Weather.Core.csproj
Processing (44/110): src/Weather.Infrastructure/Caching/DatabaseInitializer.cs
Processing (45/110): src/Weather.Infrastructure/Caching/SqliteCacheOptions.cs
Processing (46/110): src/Weather.Infrastructure/Caching/SqliteConnectionFactory.cs
Processing (47/110): src/Weather.Infrastructure/Caching/SqliteForecastCache.cs
Processing (48/110): src/Weather.Infrastructure/Caching/SqlitePointMetadataCache.cs
Processing (49/110): src/Weather.Infrastructure/Caching/TimestampText.cs
Processing (50/110): src/Weather.Infrastructure/DependencyInjection.cs
Processing (51/110): src/Weather.Infrastructure/Nws/NwsApiClient.cs
Processing (52/110): src/Weather.Infrastructure/Nws/NwsClientOptions.cs
Processing (53/110): src/Weather.Infrastructure/Nws/NwsJsonModels.cs
Processing (54/110): src/Weather.Infrastructure/Serialization/WeatherJson.cs
Processing (55/110): src/Weather.Infrastructure/Services/NeighborhoodWarmer.cs
Processing (56/110): src/Weather.Infrastructure/Services/NeighborhoodWarmingBackgroundService.cs
Processing (57/110): src/Weather.Infrastructure/Services/WeatherService.cs
Processing (58/110): src/Weather.Infrastructure/Weather.Infrastructure.csproj
Processing (59/110): src/Weather.ServiceDefaults/Extensions.cs
Processing (60/110): src/Weather.ServiceDefaults/Weather.ServiceDefaults.csproj
Processing (61/110): src/Weather.Web/appsettings.Development.json
Processing (62/110): src/Weather.Web/appsettings.json
Processing (63/110): src/Weather.Web/Components/_Imports.razor
Processing (64/110): src/Weather.Web/Components/App.razor
Processing (65/110): src/Weather.Web/Components/Layout/MainLayout.razor
Processing (66/110): src/Weather.Web/Components/Layout/NavMenu.razor
Processing (67/110): src/Weather.Web/Components/Pages/About.razor
Processing (68/110): src/Weather.Web/Components/Pages/Error.razor
Processing (69/110): src/Weather.Web/Components/Pages/Home.razor
Processing (70/110): src/Weather.Web/Components/Routes.razor
Processing (71/110): src/Weather.Web/Program.cs
Processing (72/110): src/Weather.Web/Properties/launchSettings.json
Processing (73/110): src/Weather.Web/Services/GeolocationService.cs
Processing (74/110): src/Weather.Web/Services/IGeolocationService.cs
Processing (75/110): src/Weather.Web/Services/IWeatherApiClient.cs
Processing (76/110): src/Weather.Web/Services/SkyPalette.cs
Processing (77/110): src/Weather.Web/Services/WeatherApiClient.cs
Processing (78/110): src/Weather.Web/Services/WeatherViewModels.cs
Processing (79/110): src/Weather.Web/Weather.Web.csproj
Processing (80/110): src/Weather.Web/wwwroot/app.css
Processing (81/110): src/Weather.Web/wwwroot/js/geolocation.js
Processing (82/110): tests/Weather.Api.Tests/Fakes/FakeNwsApiClient.cs
Processing (83/110): tests/Weather.Api.Tests/ForecastEndpointsTests.cs
Processing (84/110): tests/Weather.Api.Tests/GlobalUsings.cs
Processing (85/110): tests/Weather.Api.Tests/Weather.Api.Tests.csproj
Processing (86/110): tests/Weather.Api.Tests/WeatherApiFactory.cs
Processing (87/110): tests/Weather.Core.Tests/ForecastFetchResultTests.cs
Processing (88/110): tests/Weather.Core.Tests/GeoCoordinateTests.cs
Processing (89/110): tests/Weather.Core.Tests/GlobalUsings.cs
Processing (90/110): tests/Weather.Core.Tests/GridNeighborhoodTests.cs
Processing (91/110): tests/Weather.Core.Tests/Weather.Core.Tests.csproj
Processing (92/110): tests/Weather.Core.Tests/WeatherTelemetryTests.cs
Processing (93/110): tests/Weather.Infrastructure.Tests/Caching/SqliteCacheHarness.cs
Processing (94/110): tests/Weather.Infrastructure.Tests/Caching/SqliteForecastCacheTests.cs
Processing (95/110): tests/Weather.Infrastructure.Tests/Caching/SqlitePointMetadataCacheTests.cs
Processing (96/110): tests/Weather.Infrastructure.Tests/Fakes/MutableTimeProvider.cs
Processing (97/110): tests/Weather.Infrastructure.Tests/Fakes/NwsPayloads.cs
Processing (98/110): tests/Weather.Infrastructure.Tests/Fakes/StubHttpMessageHandler.cs
Processing (99/110): tests/Weather.Infrastructure.Tests/GlobalUsings.cs
Processing (100/110): tests/Weather.Infrastructure.Tests/Nws/NwsApiClientTests.cs
Processing (101/110): tests/Weather.Infrastructure.Tests/Services/NeighborhoodWarmerTests.cs
Processing (102/110): tests/Weather.Infrastructure.Tests/Services/WeatherServiceTests.cs
Processing (103/110): tests/Weather.Infrastructure.Tests/Weather.Infrastructure.Tests.csproj
Processing (104/110): tests/Weather.Web.Tests/Fakes/FakeGeolocationService.cs
Processing (105/110): tests/Weather.Web.Tests/Fakes/FakeWeatherApiClient.cs
Processing (106/110): tests/Weather.Web.Tests/GlobalUsings.cs
Processing (107/110): tests/Weather.Web.Tests/HomePageTests.cs
Processing (108/110): tests/Weather.Web.Tests/SkyPaletteTests.cs
Processing (109/110): tests/Weather.Web.Tests/Weather.Web.Tests.csproj
Processing (110/110): weather.slnx

Export completed!
Total files exported: 110
Output file size: 246.49 KB
2026-06-18-09-06-58

Build succeeded in 2.0s
2026-06-18-09-07-00
Restore complete (1.3s)

Build succeeded in 1.5s
2026-06-18-09-07-02
Restore complete (1.2s)
  Weather.Core net10.0 succeeded (3.9s) → src\Weather.Core\bin\Debug\net10.0\Weather.Core.dll
  Weather.ServiceDefaults net10.0 succeeded (4.1s) → src\Weather.ServiceDefaults\bin\Debug\net10.0\Weather.ServiceDefaults.dll
  Weather.Infrastructure net10.0 succeeded with 4 warning(s) (1.9s) → src\Weather.Infrastructure\bin\Debug\net10.0\Weather.Infrastructure.dll
    D:\DEV\personal\weather\src\Weather.Infrastructure\Services\NeighborhoodWarmer.cs(37,13): warning CA1873: Evaluation of this argument may be expensive and unnecessary if logging is disabled (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1873)
    D:\DEV\personal\weather\src\Weather.Infrastructure\Services\NeighborhoodWarmingBackgroundService.cs(91,21): warning CA1873: Evaluation of this argument may be expensive and unnecessary if logging is disabled (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1873)
    D:\DEV\personal\weather\src\Weather.Infrastructure\Nws\NwsApiClient.cs(41,17): warning CA1873: Evaluation of this argument may be expensive and unnecessary if logging is disabled (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1873)
    D:\DEV\personal\weather\src\Weather.Infrastructure\Nws\NwsApiClient.cs(114,21): warning CA1873: Evaluation of this argument may be expensive and unnecessary if logging is disabled (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1873)
  Weather.Api net10.0 succeeded (2.7s) → src\Weather.Api\bin\Debug\net10.0\Weather.Api.dll
  Weather.Web net10.0 succeeded (4.4s) → src\Weather.Web\bin\Debug\net10.0\Weather.Web.dll
  Weather.Web.Tests net10.0 succeeded with 1 warning(s) (2.8s) → tests\Weather.Web.Tests\bin\Debug\net10.0\Weather.Web.Tests.dll
    D:\DEV\personal\weather\tests\Weather.Web.Tests\Fakes\FakeGeolocationService.cs(25,44): warning CS8625: Cannot convert null literal to non-nullable reference type.
  Weather.Core.Tests net10.0 succeeded (7.2s) → tests\Weather.Core.Tests\bin\Debug\net10.0\Weather.Core.Tests.dll
  Weather.Api.Tests net10.0 succeeded (3.4s) → tests\Weather.Api.Tests\bin\Debug\net10.0\Weather.Api.Tests.dll
  Weather.AppHost net10.0 succeeded (4.2s) → src\Weather.AppHost\bin\Debug\net10.0\Weather.AppHost.dll
  Weather.Infrastructure.Tests net10.0 succeeded (2.2s) → tests\Weather.Infrastructure.Tests\bin\Debug\net10.0\Weather.Infrastructure.Tests.dll

Build succeeded with 5 warning(s) in 14.9s
2026-06-18-09-07-17
Restore complete (1.6s)
  Weather.Core.Tests net10.0 failed with 1 error(s) (0.0s)
    C:\Users\kushal\.nuget\packages\microsoft.testing.platform.msbuild\2.2.3\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(263,5): error Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later. If you use dotnet test, you should opt-in to the new dotnet test experience. For more information, see https://aka.ms/dotnet-test-mtp-error
  Weather.Web.Tests net10.0 failed with 1 error(s) (0.0s)
    C:\Users\kushal\.nuget\packages\microsoft.testing.platform.msbuild\2.2.3\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(263,5): error Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later. If you use dotnet test, you should opt-in to the new dotnet test experience. For more information, see https://aka.ms/dotnet-test-mtp-error
  Weather.Api.Tests net10.0 failed with 1 error(s) (0.0s)
    C:\Users\kushal\.nuget\packages\microsoft.testing.platform.msbuild\2.2.3\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(263,5): error Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later. If you use dotnet test, you should opt-in to the new dotnet test experience. For more information, see https://aka.ms/dotnet-test-mtp-error
  Weather.Infrastructure.Tests net10.0 failed with 1 error(s) (0.0s)
    C:\Users\kushal\.nuget\packages\microsoft.testing.platform.msbuild\2.2.3\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(263,5): error Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later. If you use dotnet test, you should opt-in to the new dotnet test experience. For more information, see https://aka.ms/dotnet-test-mtp-error

Build failed with 4 error(s) in 2.4s
2026-06-18-09-07-20
PS D:\DEV\personal\weather>

Test	Duration	Traits	Error Message
Project: Weather.Api.Tests Passed (4)	2.6 sec		
Project: Weather.Core.Tests Failed (41)	256 ms		
Project: Weather.Infrastructure.Tests Failed (34)	3.8 sec		
Project: Weather.Web.Tests Passed (36)	4.9 sec		

Test	Duration	Traits	Error Message
Project: Weather.Core.Tests Failed (41)	256 ms		
Project: Weather.Api.Tests Passed (4)	2.6 sec		
Project: Weather.Infrastructure.Tests Failed (34)	3.8 sec		
Project: Weather.Web.Tests Passed (36)	4.9 sec		
Namespace: Weather.Core.Tests Failed (41)	256 ms		
Class: ForecastFetchResultTests Passed (4)	69 ms		
Class: GeoCoordinateTests Failed (21)	78 ms		
Test Group: ConstructorAcceptsBoundaryValues(System.Double,System.Double) Passed (3)	7 ms		
ConstructorAcceptsValidCoordinates Passed	4 ms		
ConstructorRejectsNaN Passed	5 ms		
Test Group: ConstructorRejectsOutOfRangeLatitude(System.Double,System.Double) Passed (2)	< 1 ms		
Test Group: ConstructorRejectsOutOfRangeLongitude(System.Double,System.Double) Passed (2)	5 ms		
Test Group: IsValidMatchesConstructorAcceptance(System.Double,System.Double,System.Boolean) Passed (5)	34 ms		
IsValidRejectsNaN Passed	< 1 ms		
RoundedCollapsesNearbyCoordinatesToTheSameValue Passed	2 ms		
RoundedReducesPrecisionToFourDecimals Passed	2 ms		
ToApiStringIsCultureIndependent Failed	19 ms		[Test Failure] Only the invariant culture is supported in globalization-invariant mode. See https://aka.ms/GlobalizationInvariantMode for more information. (Parameter 'name') de-DE is an invalid culture identifier.
ToApiStringTrimsTrailingZerosToFourPlaces Passed	< 1 ms		
ToApiStringUsesInvariantDecimalPoint Passed	< 1 ms		
ToCacheKeyEqualsApiString Passed	< 1 ms		
Class: GridNeighborhoodTests Passed (8)	63 ms		
Class: GridPointTests Passed (4)	11 ms		
Class: WeatherTelemetryTests Passed (4)	35 ms		
Namespace: Weather.Infrastructure.Tests.Caching Failed (8)	2.2 sec		
Class: SqliteForecastCacheTests Passed (4)	1.3 sec		
Class: SqlitePointMetadataCacheTests Failed (4)	966 ms		
GetReturnsNullForAnUnknownCoordinateAsync Passed	199 ms		
NearbyCoordinatesCollapseOntoOneRowAsync Failed	254 ms		[Test Failure] A parameterless default constructor or one matching signature (System.Double Latitude, System.Double Longitude, System.String GridId, System.Int64 GridX, System.Int64 GridY, System.String ForecastUrl, System.String ForecastHourlyUrl, System.String City, System.String State, System.String TimeZone, System.String RadarStation, System.String RetrievedAtUtc, System.String ExpiresAtUtc) is required for Weather.Infrastructure.Caching.SqlitePointMetadataCache+MetadataRow materialization
UpsertOverwritesMetadataForTheSameKeyAsync Failed	259 ms		[Test Failure] A parameterless default constructor or one matching signature (System.Double Latitude, System.Double Longitude, System.String GridId, System.Int64 GridX, System.Int64 GridY, System.String ForecastUrl, System.String ForecastHourlyUrl, System.String City, System.String State, System.String TimeZone, System.String RadarStation, System.String RetrievedAtUtc, System.String ExpiresAtUtc) is required for Weather.Infrastructure.Caching.SqlitePointMetadataCache+MetadataRow materialization
UpsertThenGetRoundTripsTheMetadataAsync Failed	254 ms		[Test Failure] A parameterless default constructor or one matching signature (System.Double Latitude, System.Double Longitude, System.String GridId, System.Int64 GridX, System.Int64 GridY, System.String ForecastUrl, System.String ForecastHourlyUrl, System.String City, System.String State, System.String TimeZone, System.String RadarStation, System.String RetrievedAtUtc, System.String ExpiresAtUtc) is required for Weather.Infrastructure.Caching.SqlitePointMetadataCache+MetadataRow materialization
Namespace: Weather.Infrastructure.Tests.Nws Passed (12)	256 ms		
Namespace: Weather.Infrastructure.Tests.Services Passed (14)	1.2 sec		
