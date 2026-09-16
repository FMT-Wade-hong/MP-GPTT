param([string]$Directory = 'bin/UdpRelayServerTest/net461')
$ErrorActionPreference = 'Stop'
$dependencyDirectory = (Resolve-Path $Directory).Path
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $path = Join-Path $dependencyDirectory ((New-Object Reflection.AssemblyName($eventArgs.Name)).Name + '.dll')
    if (Test-Path $path) { return [Reflection.Assembly]::LoadFrom($path) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
[Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/MissionPlanner.Comms.dll")) | Out-Null
[Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/MissionPlanner.ArduPilot.dll")) | Out-Null
$loopback = [Net.IPAddress]::Loopback
$sockets = @()
function New-LocalUdp {
    $socket = New-Object Net.Sockets.UdpClient (New-Object Net.IPEndPoint($loopback, 0))
    $socket.Client.ReceiveTimeout = 1000
    $script:sockets += $socket
    return $socket
}
function Receive-Text($socket) {
    $remote = New-Object Net.IPEndPoint([Net.IPAddress]::Any, 0)
    [Text.Encoding]::ASCII.GetString($socket.Receive([ref]$remote))
}
try {
    $listener = New-LocalUdp
    $first = New-LocalUdp
    $second = New-LocalUdp
    $sink = New-LocalUdp
    $output = New-LocalUdp
    $mav = New-Object MissionPlanner.MAVLinkInterface
    $fc = New-Object MissionPlanner.Comms.UdpSerial($output)
    $fc.EndPointList.Add($sink.Client.LocalEndPoint)
    $mav.BaseStream = $fc
    $mirror = New-Object 'MissionPlanner.MAVLinkInterface+Mirror'
    $mirror.MirrorStream = New-Object MissionPlanner.Comms.UdpSerial($listener)
    $allowedPort = $first.Client.LocalEndPoint.Port
    $mirror.UdpWriteAllowed = [Func[Net.IPEndPoint,bool]] { param($endpoint) $endpoint.Port -eq $allowedPort }
    $mirror.MirrorStreamWrite = $true
    $mav.Mirrors.Add($mirror)
    $process = $mav.GetType().GetMethod('ProcessMirrorStream', [Reflection.BindingFlags]'Instance,NonPublic')
    $a = [Text.Encoding]::ASCII.GetBytes('allowed')
    $b = [Text.Encoding]::ASCII.GetBytes('denied')
    $telemetry = [Text.Encoding]::ASCII.GetBytes('telemetry')
    $first.Send($a, $a.Length, $listener.Client.LocalEndPoint) | Out-Null
    $second.Send($b, $b.Length, $listener.Client.LocalEndPoint) | Out-Null
    $process.Invoke($mav, (, $telemetry)) | Out-Null
    if ((Receive-Text $first) -ne 'telemetry' -or (Receive-Text $second) -ne 'telemetry') { throw 'Fanout failed' }
    if ((Receive-Text $sink) -ne 'allowed' -or $sink.Available -ne 0) { throw 'Unauthorized write reached sink' }
    'PASS: two clients receive telemetry; only authorized datagram reaches simulated flight controller'
    $mirror.MirrorStreamWrite = $false
    $first.Send($a, $a.Length, $listener.Client.LocalEndPoint) | Out-Null
    $process.Invoke($mav, (, $telemetry)) | Out-Null
    if ($sink.Available -ne 0) { throw 'Read-only server forwarded input' }
    'PASS: read-only mode blocks all writes'
    $peers = $mirror.GetType().GetField('UdpPeers', [Reflection.BindingFlags]'Instance,NonPublic').GetValue($mirror)
    foreach ($key in @($peers.Keys)) { $peers[$key] = [DateTime]::UtcNow.AddSeconds(-31) }
    $process.Invoke($mav, (, $telemetry)) | Out-Null
    if ($peers.Count -ne 0) { throw 'Expired peers retained' }
    'PASS: inactive endpoints expire after 30 seconds'
} finally {
    foreach ($socket in $sockets) { $socket.Close() }
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
