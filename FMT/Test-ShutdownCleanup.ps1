param([string]$Directory = 'bin/Shutdown119Fix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.FMT.FmtRelayControlService', $true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$udp = New-Object Net.Sockets.UdpClient
$udp.Client.Bind((New-Object Net.IPEndPoint ([Net.IPAddress]::Loopback),0))
$endpoint = $udp.Client.LocalEndPoint
$type.GetField('client',$flags).SetValue($null,$udp)
$stop = $type.GetMethod('Shutdown',$flags)
try {
    $stop.Invoke($null,@()) | Out-Null
    $stop.Invoke($null,@()) | Out-Null
    if ($type.GetField('client',$flags).GetValue($null)) { throw 'Socket retained' }
    if (!$type.GetField('stopping',$flags).GetValue($null)) { throw 'Shutdown not terminal' }
    $type.GetMethod('EnsureStarted',$flags).Invoke($null,@()) | Out-Null
    $type.GetMethod('StartListener',$flags).Invoke($null,@(1)) | Out-Null
    if ($type.GetField('client',$flags).GetValue($null)) { throw 'Late callback reopened socket' }
    $probe = New-Object Net.Sockets.UdpClient
    try { $probe.Client.Bind($endpoint) } finally { $probe.Close() }
    if ($type.GetField('activeStation',$flags).GetValue($null) -ne 0) { throw 'Authority retained' }
} finally { $udp.Close() }
$main = Get-Content "$PSScriptRoot/../MainV2.cs" -Raw
$program = Get-Content "$PSScriptRoot/../Program.cs" -Raw
if ($main -notmatch 'fmtShutdownStarted = true;\s*//[^\r\n]*\s*FMT.FmtRelayControlService.Shutdown\(\)') { throw 'Shutdown missing after close consent' }
if ($main -match 'pluginthread.Join\(\);|while \(!joysendThreadExited\)') { throw 'Unbounded worker wait remains' }
if ($program -notmatch 'Environment.Exit\(Environment.ExitCode\)' -or $program -match 'Console.ReadLine\(\)') { throw 'Process exit fallback missing' }
'PASS: relay socket released/rebindable, shutdown idempotent and terminal, authority cleared; close consent and bounded waits checked. No vehicle connection.'
