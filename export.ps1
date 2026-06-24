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

# Exact filenames (no extension match needed).
# NOTE: Containerfile.* have no extension that matches the list above, so they
# must be enumerated here or they silently fall out of the dump. The previous
# version only listed Dockerfile/.dockerignore, which is why the Podman
# Containerfiles never appeared.
$IncludeSpecificFiles = @(
    "Dockerfile", ".dockerignore", ".editorconfig",
    ".gitignore", ".gitattributes",
    "Containerfile", ".containerignore",
    "Containerfile.api", "Containerfile.web",
    "Containerfile.otelcol"
)

# Also include any file whose name STARTS WITH "Containerfile" (e.g. a future
# Containerfile.worker) without having to list each one.
$IncludeFilenamePrefixes = @("Containerfile")

# Directories whose contents are skipped even if tracked. The LLM output lives
# under docs/llm (dump.txt, output.txt, vendor conversation logs); excluding it
# keeps the dump from containing itself and keeps those large logs out. NOTE:
# this is a path PREFIX match, so it also covers docs/llm/vendor/*.
$ExcludeDirectories = @("docs/llm")

# Directories whose ENTIRE contents are exported regardless of file extension,
# so docs/*.md (ARCHITECTURE, OBSERVABILITY, STAMPEDE, ...) and any other docs
# assets are included. Anything matched by $ExcludeDirectories above still wins
# and is skipped, so docs/llm stays out.
$IncludeDirectories = @("docs")

Write-Host "Starting project export..." -ForegroundColor Green
Write-Host "Project Path: $ProjectPath" -ForegroundColor Yellow
Write-Host "Output File: $OutputFile" -ForegroundColor Yellow

# -- Resolve paths ------------------------------------------------------------
Push-Location $ProjectPath
$ResolvedRoot = (Resolve-Path ".").Path

$OutputPath = Join-Path $ResolvedRoot $OutputFile
$outputDir  = Split-Path $OutputPath -Parent
if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }

# -- Get tracked files from Git -----------------------------------------------
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

    # Force-include everything under an included directory (e.g. docs/), no
    # matter the extension. Excluded dirs above already took precedence.
    $inIncludedDir = $false
    foreach ($d in $IncludeDirectories) {
        if ($rel -like "$d/*" -or $rel -like "$d\*") { $inIncludedDir = $true; break }
    }

    # Match by extension, exact filename, or filename prefix
    $matchesPrefix = $false
    foreach ($p in $IncludeFilenamePrefixes) {
        if ($name.StartsWith($p)) { $matchesPrefix = $true; break }
    }

    if ($inIncludedDir -or
        $IncludeExtensions -contains $ext -or
        $IncludeSpecificFiles -contains $name -or
        $matchesPrefix) {
        $fullPath = Join-Path $ResolvedRoot $rel
        if (Test-Path $fullPath) {
            [PSCustomObject]@{ Relative = $rel; Full = $fullPath }
        }
    }
} | Sort-Object Relative -Unique

Write-Host "Found $($AllFiles.Count) files to export" -ForegroundColor Green

# -- Write header -------------------------------------------------------------
$header = @"
===============================================================================
ASP.NET PROJECT EXPORT  (git-tracked files only)
Generated: $(Get-Date)
Project Path: $ResolvedRoot
===============================================================================

"@
$header | Out-File -FilePath $OutputPath -Encoding UTF8

# -- Directory tree (git ls-tree) ---------------------------------------------
"DIRECTORY STRUCTURE (tracked):" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
"==============================" | Out-File -FilePath $OutputPath -Append -Encoding UTF8
""  | Out-File -FilePath $OutputPath -Append -Encoding UTF8

# Show a compact tree of tracked paths. Drop $ExcludeDirectories (docs/llm) so
# the export never lists — or contains — anything inside the LLM folder.
$treeFiles = $gitFiles | Where-Object {
    $rel = $_
    $hide = $false
    foreach ($d in $ExcludeDirectories) {
        if ($rel -like "$d/*" -or $rel -like "$d\*") { $hide = $true; break }
    }
    -not $hide
}
$treeFiles | Sort-Object | Out-File -FilePath $OutputPath -Append -Encoding UTF8

""  | Out-File -FilePath $OutputPath -Append -Encoding UTF8
""  | Out-File -FilePath $OutputPath -Append -Encoding UTF8

# -- File contents ------------------------------------------------------------
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

# -- Footer -------------------------------------------------------------------
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
