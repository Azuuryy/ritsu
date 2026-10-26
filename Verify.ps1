param([switch]$IncludeGameScenarios)
$ErrorActionPreference = 'Stop'
$ritsuCache = Join-Path $PSScriptRoot '.cache'
$ritsuEvidence = Join-Path $ritsuCache ('verification-' + [guid]::NewGuid().ToString('N'))
$ritsuFixtures = Join-Path $ritsuCache 'fixtures'
New-Item -ItemType Directory -Force $ritsuCache | Out-Null
& dotnet build (Join-Path $PSScriptRoot 'tools/Ritsu.Validation/Ritsu.Validation.csproj') -c Release "-p:NuGetPackages=$(Join-Path $ritsuCache 'nuget')" -v:quiet
if ($LASTEXITCODE -ne 0) { throw 'Verification build failed.' }
$ritsuExecutable = Join-Path $PSScriptRoot 'tools/Ritsu.Validation/bin/Release/net10.0/Ritsu.Validation.exe'
$ritsuTestDirectory = Split-Path -Parent $ritsuExecutable
foreach ($ritsuCheck in @('--checks','--audit-control')) {
    & $ritsuExecutable $ritsuCheck
    if ($LASTEXITCODE -ne 0) { throw "Failed: $ritsuCheck" }
}
if (-not $IncludeGameScenarios) {
    Write-Host 'Verified: build and offline checks. No game window launched.'
    return
}
New-Item -ItemType Directory -Force $ritsuEvidence | Out-Null
& python (Join-Path $PSScriptRoot 'tools/Create-Fixtures.py')
if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed. Python 3 is required for graphical verification.' }
@('WindowMode = Windowed','WindowedSize = 1280x720','VolumeUniversal = 0','InactiveVolume = 0') | Set-Content -LiteralPath (Join-Path $ritsuTestDirectory 'framework.ini')
foreach ($ritsuScenario in @('--smoke','--reopen')) {
    $ritsuLabel = $ritsuScenario.TrimStart('-')
    $ritsuArguments = @($ritsuScenario, ('"' + $ritsuFixtures + '"'), ('"' + $ritsuEvidence + '"'))
    $ritsuProcess = Start-Process -FilePath $ritsuExecutable -ArgumentList $ritsuArguments -WorkingDirectory $ritsuTestDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $ritsuEvidence "$ritsuLabel.log") -RedirectStandardError (Join-Path $ritsuEvidence "$ritsuLabel-errors.log")
    # Retain the native handle so Windows PowerShell can read ExitCode after exit.
    $null = $ritsuProcess.Handle
    $ritsuTimer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $ritsuProcess.WaitForExit(1000)) {
        if ($ritsuTimer.Elapsed.TotalSeconds -gt 175) {
            $ritsuProcess.Kill()
            throw "Timed out: $ritsuScenario"
        }
    }
    if ($ritsuProcess.ExitCode -ne 0) { throw "Failed: $ritsuScenario; see $ritsuEvidence" }
}
foreach ($ritsuReport in @('result.json','reopen-result.json')) {
    $ritsuResult = Get-Content -LiteralPath (Join-Path $ritsuEvidence $ritsuReport) -Raw | ConvertFrom-Json
    if (-not $ritsuResult.success) { throw "Failed: $ritsuReport" }
}
Write-Host "Verified: $ritsuEvidence"
