# Builds the client for every supported platform and packages the release
# archives in artifacts/, with a SHA-256 sidecar for each archive.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Package.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Package.ps1 -Platforms win-x64,linux-x64
#
# Requires the .NET 10 SDK and Python 3. Restoring packages requires Internet
# access unless the packages are already cached. The Windows package is the
# only one that can be launched and validated on Windows; the Linux and macOS
# packages are cross-published previews.
param(
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')]
    [string[]] $Platforms = @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64'),

    [string] $OutputDirectory = 'artifacts',

    [switch] $SkipBootstrap,

    [switch] $SkipPublish
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
if (-not $SkipBootstrap)
{
    & (Join-Path $root 'Bootstrap.ps1')
}

$cache = Join-Path $root '.cache'
$version = (Get-Content -Raw -Encoding UTF8 (Join-Path $root 'VERSION')).Trim()
$project = Join-Path $root 'client/osu.Desktop/osu.Desktop.csproj'
$nuget = Join-Path $cache 'nuget'

if (-not $SkipPublish)
{
    foreach ($rid in $Platforms)
    {
        $publish = Join-Path $cache "Ritsu-$version-$rid"
        if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
        New-Item -ItemType Directory -Force $publish | Out-Null

        Write-Host "Publishing $rid..."
        & dotnet publish $project -c Release -r $rid --self-contained true -o $publish `
            "-p:NuGetPackages=$nuget" "-p:Version=$version" "-p:InformationalVersion=$version" `
            "-p:FileVersion=$version" -p:AssemblyVersion=1.0.0.0 -p:PublishSingleFile=false -v:minimal
        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid." }
    }
}

$python = Get-Command python -ErrorAction SilentlyContinue
$pythonArguments = @()
if ($null -eq $python)
{
    $python = Get-Command py -ErrorAction SilentlyContinue
    $pythonArguments = @('-3')
}
if ($null -eq $python) { throw 'Python 3 is required to package the artifacts.' }

$output = Join-Path $root $OutputDirectory
$packageScript = Join-Path $root 'tools/package-artifacts.py'

& $python.Source @pythonArguments $packageScript --root $root --output-dir $output --version $version --platforms ($Platforms -join ',')
if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }

Write-Host "Artifacts written to $output"
