param(
    [string]$Dotnet = "dotnet",
    [string]$OutputRoot = "dist",
    [switch]$SelfContained,
    [switch]$Clean,
    [switch]$CreateArchive
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootFull = [IO.Path]::GetFullPath($root).TrimEnd('\')

$dotnetCommand = Get-Command $Dotnet -ErrorAction SilentlyContinue
if ($dotnetCommand) {
    $dotnetPath = $dotnetCommand.Source
}
elseif (Test-Path -LiteralPath $Dotnet) {
    $dotnetPath = (Resolve-Path -LiteralPath $Dotnet).Path
}
else {
    throw "Could not find dotnet. Install the .NET 8 SDK or pass -Dotnet C:\path\to\dotnet.exe"
}

$dist = if ([IO.Path]::IsPathRooted($OutputRoot)) {
    [IO.Path]::GetFullPath($OutputRoot)
}
else {
    [IO.Path]::GetFullPath((Join-Path $root $OutputRoot))
}
$dist = $dist.TrimEnd('\')
if (-not $dist.StartsWith($rootFull + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must be inside the repository: $rootFull"
}

$snifferOut = Join-Path $dist 'RockSniffer'
$overlayOut = Join-Path $dist 'Overlay'
$projectPath = Join-Path $root 'src\RocksmithTabOverlay\RocksmithTabOverlay.csproj'
$versionNode = Select-Xml -LiteralPath $projectPath -XPath '//Version' | Select-Object -First 1
$version = if ($versionNode) { $versionNode.Node.InnerText } else { 'dev' }
$selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }

if ($Clean -and (Test-Path -LiteralPath $dist)) {
    Remove-Item -LiteralPath $dist -Recurse -Force
}
New-Item -ItemType Directory -Path $snifferOut, $overlayOut -Force | Out-Null

& $dotnetPath publish (Join-Path $root 'vendor\RockSniffer\RockSniffer\RockSniffer.csproj') `
    -c Release -r win-x64 --self-contained $selfContainedValue -o $snifferOut -m:1 `
    -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'RockSniffer build failed' }

& $dotnetPath publish $projectPath `
    -c Release -r win-x64 --self-contained $selfContainedValue -o $overlayOut -m:1 `
    -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Overlay build failed' }

Copy-Item -LiteralPath (Join-Path $root 'vendor\RockSniffer\addons') -Destination $snifferOut -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'vendor\RockSniffer\LICENSE') -Destination (Join-Path $snifferOut 'LICENSE-RockSniffer.txt') -Force

$licenses = Join-Path $dist 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'vendor\RockSniffer\LICENSE') -Destination (Join-Path $licenses 'RockSniffer-MIT.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'vendor\RockSniffer\RockSnifferLib\LICENSE') -Destination (Join-Path $licenses 'RockSnifferLib-MIT.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'vendor\RockSniffer\Rocksmith2014PsarcLib\LICENSE') -Destination (Join-Path $licenses 'Rocksmith2014PsarcLib-MIT.txt') -Force

foreach ($file in @('README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'start-tab-overlay.ps1', 'run-sniffer-elevated.ps1', 'diagnose-overlay-window.ps1')) {
    $source = Join-Path $root $file
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $dist $file) -Force
    }
}

$docs = Join-Path $root 'docs'
if (Test-Path -LiteralPath $docs) {
    Copy-Item -LiteralPath $docs -Destination $dist -Recurse -Force
}

if ($CreateArchive) {
    $artifacts = Join-Path $root 'artifacts'
    New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
    $archive = Join-Path $artifacts "RocksmithTabOverlay-$version-win-x64.zip"
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $archive -CompressionLevel Optimal
    Write-Host "Packaged $archive"
}

Write-Host "Built Rocksmith TAB Overlay $version in $dist"
