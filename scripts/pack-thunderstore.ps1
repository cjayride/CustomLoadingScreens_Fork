# Thunderstore / Gale / r2modman: zip contents are the mod folder root (no plugins/ prefix inside the archive).
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

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)

Write-Host "DLL=$dll"
Write-Host "ZIP=$zip"
Write-Host "ZIP entries (top level):"
[System.IO.Compression.ZipFile]::OpenRead($zip).Entries |
    ForEach-Object { $_.FullName.Split('/')[0] } |
    Sort-Object -Unique
