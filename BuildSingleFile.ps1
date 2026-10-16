$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'FamilyModManager.csproj'
$publish = Join-Path $PSScriptRoot 'single-file-publish'
& dotnet restore $project -r win-x64 -p:RestoreSources=https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) { throw 'Family Mod Manager restore failed.' }
& dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false `
    -p:EnableCompressionInSingleFile=true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Family Mod Manager publish failed.' }
$built = Join-Path $publish 'FamilyModManager.exe'
if (!(Test-Path -LiteralPath $built)) { throw 'Single-file executable was not produced.' }
$share = Join-Path $PSScriptRoot 'share'
New-Item -ItemType Directory -Path $share -Force | Out-Null
$output = Join-Path $share 'FamilyModManager-0.8.2-b108.exe'
Copy-Item -LiteralPath $built -Destination $output -Force
Write-Output "Share this file: $output"
