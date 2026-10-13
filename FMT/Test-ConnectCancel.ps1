param([string]$Directory='bin/ConnectCancel/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.ArduPilot.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Comms.dll')) | Out-Null
Add-Type -TypeDefinition @'
using System;
public static class ConnectCancelGate {
    public static volatile bool Cancelled;
    public static Func<bool> Check = () => Cancelled;
    public static System.Threading.Tasks.Task<bool> WaitForSettle(System.Reflection.MethodInfo method, object reporter) {
        return System.Threading.Tasks.Task.Run(() => {
            try { method.Invoke(null, new object[] {reporter, 1000}); return false; }
            catch (System.Reflection.TargetInvocationException ex) { return ex.InnerException is OperationCanceledException; }
        });
    }
}
'@
$linkType=$assembly.GetType('MissionPlanner.MAVLinkInterface')
if($null -eq $linkType) { $linkType=[MissionPlanner.MAVLinkInterface] }
$reporterType=$linkType.GetNestedType('NoUIReporter',[Reflection.BindingFlags]'NonPublic')
$reporter=[Activator]::CreateInstance($reporterType,$true)
$waitMethod=$linkType.GetMethod('WaitForConnectSettle',[Reflection.BindingFlags]'Static,NonPublic')
$settleTask=[ConnectCancelGate]::WaitForSettle($waitMethod,$reporter)
Start-Sleep -Milliseconds 50
$watch=[Diagnostics.Stopwatch]::StartNew()
$reporter.doWorkArgs.CancelRequested=$true
if(!$settleTask.Wait(300) -or !$settleTask.Result) { throw 'Serial settle did not cancel promptly' }
"PASS: serial settle cancellation $($watch.ElapsedMilliseconds) ms"
$link=New-Object MissionPlanner.MAVLinkInterface
$input=New-Object MissionPlanner.Comms.UdpSerial
$input.client.Dispose()
$input.client=New-Object Net.Sockets.UdpClient 0
$input.IsOpen=$true
$link.BaseStream=$input
$sender=New-Object Net.Sockets.UdpClient 0
$endpoint=New-Object Net.IPEndPoint ([Net.IPAddress]::Loopback),($input.client.Client.LocalEndPoint.Port)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$read=$link.GetType().GetMethod('readPacketAsync',$flags)
$gate=$link.GetType().GetField('readlock',$flags).GetValue($link)
try {
    # No data, partial header, then a complete header with missing payload.
    foreach($bytes in @([byte[]]@(),[byte[]]@(254),[byte[]]@(254,9,0,1,1,0))) {
        [ConnectCancelGate]::Cancelled=$false
        if($bytes.Length) { $sender.Send($bytes,$bytes.Length,$endpoint) | Out-Null }
        $task=$read.Invoke($link,@([ConnectCancelGate]::Check))
        Start-Sleep -Milliseconds 50
        $watch=[Diagnostics.Stopwatch]::StartNew()
        [ConnectCancelGate]::Cancelled=$true
        if(!$task.Wait(500)) { throw 'Cancellation waited for the packet timeout' }
        if($task.Result.Length -ne 0) { throw 'Cancelled packet should be invalid' }
        if($gate.CurrentCount -ne 1) { throw 'Read lock leaked on cancellation' }
        "PASS: buffered=$($bytes.Length), cancellation=$($watch.ElapsedMilliseconds) ms, read lock released"
    }
    [ConnectCancelGate]::Cancelled=$false
    $gate.Wait()
    try {
        $task=$read.Invoke($link,@([ConnectCancelGate]::Check))
        [ConnectCancelGate]::Cancelled=$true
        if(!$task.Wait(500)) { throw 'Read-lock wait ignored cancellation' }
        if($gate.CurrentCount -ne 0) { throw 'Cancelled waiter released another reader lock' }
    } finally { $gate.Release() | Out-Null }
    'PASS: cancellation while waiting for occupied read lock'
} finally { $link.Dispose(); $sender.Dispose() }
