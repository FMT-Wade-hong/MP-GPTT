param([string]$Directory = 'bin/RelayLatencyFix/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.ArduPilot.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Comms.dll')) | Out-Null
Add-Type -TypeDefinition @'
using System;
using System.Net;
public static class RelayLatencyGate {
    public static volatile bool Allowed;
    public static Func<IPEndPoint,bool> Endpoint = p => Allowed;
    public static Func<bool> Dedicated = () => Allowed;
}
'@
$flight=New-Object Net.Sockets.UdpClient 0
$sender=New-Object Net.Sockets.UdpClient 0
$link=New-Object MissionPlanner.MAVLinkInterface
$output=New-Object MissionPlanner.Comms.UdpSerialConnect
$output.hostEndPoint=New-Object Net.IPEndPoint ([Net.IPAddress]::Loopback),($flight.Client.LocalEndPoint.Port)
$output.IsOpen=$true
$link.BaseStream=$output
$flight.Client.ReceiveTimeout=1000
try {
    foreach($shared in @($true,$false)) {
        $input=New-Object MissionPlanner.Comms.UdpSerial
        $input.client.Dispose()
        $input.client=New-Object Net.Sockets.UdpClient 0
        $input.IsOpen=$true
        $endpoint=New-Object Net.IPEndPoint ([Net.IPAddress]::Loopback),($input.client.Client.LocalEndPoint.Port)
        $mirror=New-Object 'MissionPlanner.MAVLinkInterface+Mirror'
        $mirror.MirrorStream=$input
        $mirror.MirrorStreamWrite=$true
        if($shared) { $mirror.UdpWriteAllowed=[RelayLatencyGate]::Endpoint }
        else { $mirror.MirrorStreamWriteAllowed=[RelayLatencyGate]::Dedicated }
        $link.Mirrors.Add($mirror)
        try {
            [RelayLatencyGate]::Allowed=$true
            $input.IsOpen=$false
            $link.StartMirrorInput($mirror)
            $link.StartMirrorInput($mirror) # Duplicate start must not create a second reader.
            Start-Sleep -Milliseconds 50
            $input.IsOpen=$true # Listener opens after the worker starts.
            $watch=[Diagnostics.Stopwatch]::StartNew()
            $packet=[byte[]]@(253,1,2,3,4,5,6,7)
            $sender.Send($packet,$packet.Length,$endpoint) | Out-Null
            $from=New-Object Net.IPEndPoint ([Net.IPAddress]::Any),0
            $received=$flight.Receive([ref]$from)
            if([Convert]::ToBase64String($received) -ne [Convert]::ToBase64String($packet)) { throw 'Forwarded bytes differ' }
            if($watch.ElapsedMilliseconds -gt 500) { throw 'Forwarding unexpectedly slow on loopback' }
            "PASS: shared=$shared forwards without ANY flight telemetry in $($watch.ElapsedMilliseconds) ms"
            [RelayLatencyGate]::Allowed=$false
            $sender.Send($packet,$packet.Length,$endpoint) | Out-Null
            Start-Sleep -Milliseconds 100
            if($flight.Available -gt 0) { throw 'Revoked source was forwarded' }
            [RelayLatencyGate]::Allowed=$true
            $link.ReadOnly=$true
            $sender.Send($packet,$packet.Length,$endpoint) | Out-Null
            Start-Sleep -Milliseconds 100
            if($flight.Available -gt 0) { throw 'Read-only link was written' }
            $link.ReadOnly=$false
            $mirror.StopInput()
            $sender.Send($packet,$packet.Length,$endpoint) | Out-Null
            Start-Sleep -Milliseconds 100
            if($flight.Available -gt 0) { throw 'Stopped worker still forwarded' }
        } finally { $mirror.StopInput(); $input.Close(); $link.Mirrors.Remove($mirror) | Out-Null }
    }
} finally { $link.Dispose(); $flight.Dispose(); $sender.Dispose() }
'PASS: local UDP shared/dedicated paths, no telemetry dependency, byte preservation, duplicate start, revoked permission, read-only, worker stop; no real vehicle connected'
