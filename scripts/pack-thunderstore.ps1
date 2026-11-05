# Thunderstore / Gale / r2modman / Hexium: flat mod root in the zip.
# Entry paths must use forward slashes only (no backslashes or absolute paths).
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$GamePath = 'D:\SteamLibrary\steamapps\common\Valheim'
$csproj = Join-Path $ProjectRoot 'CustomLoadingScreens\CustomLoadingScreens.csproj'

dotnet msbuild $csproj -p:Configuration=$Configuration -p:SolutionDir="$ProjectRoot\" -p:GamePath=$GamePath -v:q
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$manifest = Get-Content (Join-Path $ProjectRoot 'manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version_number
$dll = Join-Path $ProjectRoot "CustomLoadingScreens\bin\$Configuration\net481\CustomLoadingScreens.dll"
if (-not (Test-Path $dll)) { throw "Missing DLL: $dll" }

$stage = Join-Path $env:TEMP "cls-thunderstore-$version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item $dll (Join-Path $stage 'CustomLoadingScreens.dll')
$assetSrc = Join-Path $ProjectRoot 'plugins\CustomLoadingScreens'
if (Test-Path $assetSrc) {
    Copy-Item $assetSrc (Join-Path $stage 'CustomLoadingScreens') -Recurse -Force
}
foreach ($name in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
    Copy-Item (Join-Path $ProjectRoot $name) $stage -Force
}

$dist = Join-Path $ProjectRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "cjayride-CustomLoadingScreens-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-SafeZipEntryName {
    param([string]$StageRoot, [string]$FilePath)
    $root = $StageRoot.TrimEnd('\', '/')
    $full = [System.IO.Path]::GetFullPath($FilePath)
    if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "File is outside staging root: $FilePath"
    }
    $relative = $full.Substring($root.Length).TrimStart('\', '/')
    $entry = $relative.Replace('\', '/')
    if ($entry.Contains('..')) { throw "Unsafe zip entry: $entry" }
    if ($entry.StartsWith('/')) { throw "Unsafe zip entry: $entry" }
    return $entry
}

$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $stage -Recurse -File | ForEach-Object {
        $entryName = Get-SafeZipEntryName -StageRoot $stage -FilePath $_.FullName
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $_.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

Write-Host "DLL=$dll"
Write-Host "ZIP=$zip"
Write-Host "Sample entries:"
[System.IO.Compression.ZipFile]::OpenRead($zip).Entries |
    Select-Object -First 8 -ExpandProperty FullName |
    ForEach-Object { Write-Host $_ }

$bad = [System.IO.Compression.ZipFile]::OpenRead($zip).Entries |
    Where-Object { $_.FullName -match '\\' -or $_.FullName -match '^\.' -or $_.FullName -match '/\.\./' -or $_.FullName -match '^/' }
if ($bad) {
    throw "Zip still contains unsafe entry paths."
}
