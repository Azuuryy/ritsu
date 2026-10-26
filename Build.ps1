param([switch]$SkipBootstrap)
$ErrorActionPreference = 'Stop'
if (-not $SkipBootstrap) { & (Join-Path $PSScriptRoot 'Bootstrap.ps1') }
$ritsuCache = Join-Path $PSScriptRoot '.cache'
$ritsuArtifacts = Join-Path $PSScriptRoot 'artifacts'
$ritsuBundleName = 'Ritsu-0.1.0-alpha-win-x64'
$ritsuPublish = Join-Path $ritsuCache ($ritsuBundleName + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $ritsuCache,$ritsuArtifacts,$ritsuPublish | Out-Null
$ritsuProject = Join-Path $PSScriptRoot 'client/osu.Desktop/osu.Desktop.csproj'
& dotnet publish $ritsuProject -c Release -r win-x64 --self-contained true -o $ritsuPublish "-p:NuGetPackages=$(Join-Path $ritsuCache 'nuget')" -p:Version=0.1.0 -p:InformationalVersion=0.1.0-alpha -p:PublishSingleFile=false -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
foreach ($ritsuDocument in @('GUIDE.md','ROADMAP.md','VALIDATION.md','dependencies.json')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $ritsuDocument) -Destination $ritsuPublish
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'client/LICENCE') -Destination (Join-Path $ritsuPublish 'LICENCE-client.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'toriirefresh-framework/LICENCE') -Destination (Join-Path $ritsuPublish 'LICENCE-framework.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'toriirefresh-resources/LICENCE.md') -Destination (Join-Path $ritsuPublish 'LICENCE-resources.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $ritsuPublish 'RITSU-README.md')
'@echo off', 'start "" "%~dp0torii.exe" %*' | Set-Content -LiteralPath (Join-Path $ritsuPublish 'Launch Ritsu.cmd') -Encoding ASCII
$ritsuZip = Join-Path $ritsuArtifacts ($ritsuBundleName + '.zip')
Compress-Archive -Path (Join-Path $ritsuPublish '*') -DestinationPath $ritsuZip -Force -CompressionLevel Optimal
$ritsuHash = (Get-FileHash -LiteralPath $ritsuZip -Algorithm SHA256).Hash.ToLowerInvariant()
"$ritsuHash  $ritsuBundleName.zip" | Set-Content -LiteralPath ($ritsuZip + '.sha256') -Encoding ASCII
$ritsuPublish | Set-Content -LiteralPath (Join-Path $ritsuCache 'last-publish-path.txt')
Write-Host "Created: $ritsuZip"
Write-Host "SHA256: $ritsuHash"
