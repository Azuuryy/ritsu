param()
$ErrorActionPreference = 'Stop'
$ritsuRoot = $PSScriptRoot
$ritsuManifest = Get-Content -LiteralPath (Join-Path $ritsuRoot 'dependencies.json') -Raw | ConvertFrom-Json

function Invoke-RitsuGit {
    param([string[]]$GitArguments)
    & git @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($GitArguments -join ' ')" }
}

foreach ($ritsuDependency in @($ritsuManifest.client, $ritsuManifest.framework, $ritsuManifest.resources)) {
    $ritsuCheckout = Join-Path $ritsuRoot $ritsuDependency.directory
    $ritsuBundleMarker = Join-Path $ritsuRoot 'ritsu-source-bundle.json'
    if ($ritsuDependency.directory -eq 'client' -and (Test-Path -LiteralPath $ritsuBundleMarker)) {
        $ritsuBundledCommit = (Get-Content -LiteralPath $ritsuBundleMarker -Raw | ConvertFrom-Json).clientCommit
        if ($ritsuBundledCommit -ne $ritsuDependency.commit) { throw "Bundled client source does not match the pinned revision: $ritsuBundledCommit" }
        if (-not (Test-Path -LiteralPath (Join-Path $ritsuCheckout 'osu.Desktop/osu.Desktop.csproj'))) { throw 'Bundled client source is incomplete.' }
        $ritsuPatch = Join-Path $ritsuRoot $ritsuDependency.patch
        $ritsuSavedErrorPolicy = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & git -C $ritsuCheckout apply --reverse --check -- $ritsuPatch 2>$null
        $ritsuPatchIsPresent = $LASTEXITCODE -eq 0
        $ErrorActionPreference = $ritsuSavedErrorPolicy
        if (-not $ritsuPatchIsPresent) { throw 'Bundled client source does not contain the offline patch.' }
        Write-Host "client: $ritsuBundledCommit (bundled with offline patch)"
        continue
    }
    if (-not (Test-Path -LiteralPath (Join-Path $ritsuCheckout '.git'))) {
        if (Test-Path -LiteralPath $ritsuCheckout) { throw "Existing non-repository directory: $ritsuCheckout" }
        Invoke-RitsuGit -GitArguments @('-c','core.longpaths=true','clone','--no-checkout','--single-branch',$ritsuDependency.repository,$ritsuCheckout)
        Invoke-RitsuGit -GitArguments @('-C',$ritsuCheckout,'config','core.longpaths','true')
        Invoke-RitsuGit -GitArguments @('-C',$ritsuCheckout,'fetch','origin',$ritsuDependency.commit)
        Invoke-RitsuGit -GitArguments @('-C',$ritsuCheckout,'checkout','--detach',$ritsuDependency.commit)
        if ($ritsuDependency.directory -eq 'client') {
            Invoke-RitsuGit -GitArguments @('-C',$ritsuCheckout,'switch','-c','codex/ritsu-offline')
        }
    }
    $ritsuHead = (& git -C $ritsuCheckout rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $ritsuHead -ne $ritsuDependency.commit) { throw "Unexpected revision in $ritsuCheckout" }
    if ($ritsuDependency.patch) {
        $ritsuPatch = Join-Path $ritsuRoot $ritsuDependency.patch
        $ritsuSavedErrorPolicy = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & git -C $ritsuCheckout apply --reverse --check -- $ritsuPatch 2>$null
        $ritsuAlreadyApplied = $LASTEXITCODE -eq 0
        $ErrorActionPreference = $ritsuSavedErrorPolicy
        if (-not $ritsuAlreadyApplied) {
            Invoke-RitsuGit -GitArguments @('-C',$ritsuCheckout,'apply','--check','--',$ritsuPatch)
            Invoke-RitsuGit -GitArguments @('-C',$ritsuCheckout,'apply','--',$ritsuPatch)
        }
    }
    Write-Host "$($ritsuDependency.directory): $ritsuHead"
}
