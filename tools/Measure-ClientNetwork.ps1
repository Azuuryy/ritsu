param([Parameter(Mandatory=$true)][int]$RootProcessId, [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Get-Command Get-NetTCPConnection,Get-NetUDPEndpoint -ErrorAction Stop | Out-Null
$ritsuKnownProcesses = [System.Collections.Generic.HashSet[int]]::new()
$ritsuKnownProcesses.Add($RootProcessId) | Out-Null
$ritsuChildren = [System.Collections.Generic.List[object]]::new()
$ritsuSockets = [System.Collections.Generic.List[object]]::new()
$ritsuSamples = 0
$ritsuDeadline = [DateTime]::UtcNow.AddMinutes(4)
while ((Get-Process -Id $RootProcessId -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $ritsuDeadline) {
    foreach ($ritsuParent in @($ritsuKnownProcesses)) {
        foreach ($ritsuChild in @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $ritsuParent" -ErrorAction Stop)) {
            if ($ritsuKnownProcesses.Add([int]$ritsuChild.ProcessId)) {
                $ritsuChildren.Add([pscustomobject]@{ processId=$ritsuChild.ProcessId; parentId=$ritsuParent; name=$ritsuChild.Name })
            }
        }
    }
    foreach ($ritsuObservedPid in @($ritsuKnownProcesses)) {
        foreach ($ritsuConnection in @(Get-NetTCPConnection -OwningProcess $ritsuObservedPid -ErrorAction SilentlyContinue)) {
            $ritsuSockets.Add([pscustomobject]@{ protocol='TCP'; processId=$ritsuObservedPid; state=[string]$ritsuConnection.State; localPort=$ritsuConnection.LocalPort; remoteAddress=$ritsuConnection.RemoteAddress; remotePort=$ritsuConnection.RemotePort })
        }
        foreach ($ritsuConnection in @(Get-NetUDPEndpoint -OwningProcess $ritsuObservedPid -ErrorAction SilentlyContinue)) {
            $ritsuSockets.Add([pscustomobject]@{ protocol='UDP'; processId=$ritsuObservedPid; localPort=$ritsuConnection.LocalPort })
        }
    }
    $ritsuSamples++
    Start-Sleep -Milliseconds 250
}
@{ rootProcessId=$RootProcessId; samples=$ritsuSamples; children=@($ritsuChildren); socketObservations=@($ritsuSockets); note='Process tree and OS endpoint snapshots; transient sockets between samples may not be observed.' } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
