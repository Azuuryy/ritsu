param()
$ErrorActionPreference = 'Stop'
$ritsuRoot = $PSScriptRoot
$ritsuManifest = Get-Content -LiteralPath (Join-Path $ritsuRoot 'dependencies.json') -Raw | ConvertFrom-Json

function Invoke-RitsuGit {
    param([string[]]$GitArguments)
    & git @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($GitArguments -join ' ')" }
}

# A dependency lists its patches in application order, either as `patches` (array) or as a single `patch`.
function Get-RitsuPatches {
    param($Dependency)
    if ($Dependency.PSObject.Properties.Name -contains 'patches') { return @($Dependency.patches | ForEach-Object { Join-Path $ritsuRoot $_ }) }
    if ($Dependency.patch) { return @(Join-Path $ritsuRoot $Dependency.patch) }
    return @()
}

# True when every patch is applied to the working tree, checked by reverting them in reverse order.
# Stacked patches cannot be checked with a single `git apply --reverse --check`, so they are reverted
# in a temporary index built from the working tree; the working tree and the real index are untouched.
function Test-RitsuPatchesApplied {
    param([string]$Repository, [string[]]$Patches, [string]$Directory)
    [string[]]$ritsuDirectoryArguments = @()
    if ($Directory) { $ritsuDirectoryArguments = @("--directory=$Directory") }
    $ritsuSavedErrorPolicy = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($Patches.Count -eq 1) {
            & git -C $Repository apply --reverse --check @ritsuDirectoryArguments -- $Patches[0] 2>$null
            return $LASTEXITCODE -eq 0
        }
        $ritsuSavedIndex = $env:GIT_INDEX_FILE
        $ritsuIndex = Join-Path ([System.IO.Path]::GetTempPath()) ('ritsu-bootstrap-' + [guid]::NewGuid().ToString('N') + '.index')
        try {
            $env:GIT_INDEX_FILE = $ritsuIndex
            & git -C $Repository read-tree HEAD 2>$null
            if ($LASTEXITCODE -ne 0) { return $false }
            $ritsuPathspec = if ($Directory) { $Directory } else { '.' }
            & git -C $Repository add --all -- $ritsuPathspec 2>$null
            if ($LASTEXITCODE -ne 0) { return $false }
            for ($ritsuIndexInStack = $Patches.Count - 1; $ritsuIndexInStack -ge 0; $ritsuIndexInStack--) {
                & git -C $Repository apply --cached --reverse @ritsuDirectoryArguments -- $Patches[$ritsuIndexInStack] 2>$null
                if ($LASTEXITCODE -ne 0) { return $false }
            }
            return $true
        }
        finally {
            $env:GIT_INDEX_FILE = $ritsuSavedIndex
            Remove-Item -LiteralPath $ritsuIndex -Force -ErrorAction SilentlyContinue
        }
    }
    finally {
        $ErrorActionPreference = $ritsuSavedErrorPolicy
    }
}

foreach ($ritsuDependency in @($ritsuManifest.client, $ritsuManifest.framework, $ritsuManifest.resources)) {
    $ritsuCheckout = Join-Path $ritsuRoot $ritsuDependency.directory
    $ritsuPatches = @(Get-RitsuPatches $ritsuDependency)
    $ritsuBundleMarker = Join-Path $ritsuRoot 'ritsu-source-bundle.json'
    if ($ritsuDependency.directory -eq 'client' -and (Test-Path -LiteralPath $ritsuBundleMarker)) {
        $ritsuBundledCommit = (Get-Content -LiteralPath $ritsuBundleMarker -Raw | ConvertFrom-Json).clientCommit
        if ($ritsuBundledCommit -ne $ritsuDependency.commit) { throw "Bundled client source does not match the pinned revision: $ritsuBundledCommit" }
        if (-not (Test-Path -LiteralPath (Join-Path $ritsuCheckout 'osu.Desktop/osu.Desktop.csproj'))) { throw 'Bundled client source is incomplete.' }
        if (-not (Test-RitsuPatchesApplied -Repository $ritsuRoot -Patches $ritsuPatches -Directory 'client')) { throw 'Bundled client source does not contain the client patches.' }
        Write-Host "client: $ritsuBundledCommit (bundled with $($ritsuPatches.Count) client patches)"
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
    if ($ritsuPatches.Count -gt 0 -and -not (Test-RitsuPatchesApplied -Repository $ritsuCheckout -Patches $ritsuPatches)) {
        $ritsuAppliedCount = $ritsuPatches.Count - 1
        while ($ritsuAppliedCount -gt 0) {
            $ritsuPrefix = $ritsuPatches[0..($ritsuAppliedCount - 1)]
            if (Test-RitsuPatchesApplied -Repository $ritsuCheckout -Patches $ritsuPrefix) { break }
            $ritsuAppliedCount--
        }
        for ($ritsuPatchIndex = $ritsuAppliedCount; $ritsuPatchIndex -lt $ritsuPatches.Count; $ritsuPatchIndex++) {
            $ritsuPatch = $ritsuPatches[$ritsuPatchIndex]
            Invoke-RitsuGit -GitArguments @('-C',$ritsuCheckout,'apply','--check','--',$ritsuPatch)
            Invoke-RitsuGit -GitArguments @('-C',$ritsuCheckout,'apply','--',$ritsuPatch)
        }
    }
    Write-Host "$($ritsuDependency.directory): $ritsuHead"
}
