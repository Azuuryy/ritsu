param()
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Bootstrap.ps1')
& (Join-Path $PSScriptRoot 'Verify.ps1')
& (Join-Path $PSScriptRoot 'Build.ps1') -SkipBootstrap

Add-Type -AssemblyName System.IO.Compression.FileSystem
$ritsuZipPath = Join-Path $PSScriptRoot 'artifacts/Ritsu-0.1.0-alpha-win-x64.zip'
$ritsuArchive = [System.IO.Compression.ZipFile]::OpenRead($ritsuZipPath)
try {
    $ritsuEntries = @($ritsuArchive.Entries | ForEach-Object { $_.FullName })
    foreach ($ritsuRequired in @('torii.exe','torii.dll','torii.runtimeconfig.json','coreclr.dll','hostfxr.dll','SDL3.dll','bass.dll','Launch Ritsu.cmd','GUIDE.md','ROADMAP.md','VALIDATION.md','LICENCE-client.txt','LICENCE-framework.txt','LICENCE-resources.md','THIRD-PARTY-NOTICES.txt')) {
        if ($ritsuRequired -notin $ritsuEntries) { throw "Package missing: $ritsuRequired" }
    }
    if (@($ritsuEntries | Where-Object { $_ -match '\.(realm|ini|log|osz|osk|osr)$' }).Count -gt 0) { throw 'Package contains user or test data.' }
    Write-Host "PASS: package contains runtime, native dependencies and notices ($($ritsuEntries.Count) entries); no user data."
}
finally { $ritsuArchive.Dispose() }
Write-Host 'PASS: final pipeline completed without launching a game window.'
