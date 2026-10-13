param([string]$Directory='bin/HomePreserve123/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.GCSViews.FlightPlanner',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$method=$type.GetMethod('processToScreen',$flags)
if($method.GetParameters().Count -ne 3 -or $method.GetParameters()[2].Name -ne 'preserveHome') { throw 'Missing HOME preservation argument' }
$source=Get-Content "$PSScriptRoot/../GCSViews/FlightPlanner.cs" -Raw
foreach($required in @('WPtoScreen(values.wps.Select(a => (Locationwp)a).ToList(), ShouldPreserveMissionHome());','WPtoScreen(cmds, ShouldPreserveMissionHome());','processToScreen(cmds, false, preserveHome);','if (!preserveHome && cellhome.Value != null)','Commands.Rows.Remove(Commands.Rows[0]);','wps.OrderBy(a => a.Key).Select(a => (Locationwp) a.Value).ToList(), false, ShouldPreserveMissionHome())')) {
    if(!$source.Contains($required)) { throw "Missing reload HOME guard: $required" }
}
$mav=Get-Content "$PSScriptRoot/../ExtLibs/ArduPilot/Mavlink/MAVLinkInterface.cs" -Raw
$flightGuard=$type.GetMethod('PreserveMissionHomeDuringFlight',[Reflection.BindingFlags]'Static,NonPublic')
foreach($armed in @($false,$true)) {
    foreach($state in 0..4) {
        $expected=if($state -eq 1) {$false} elseif($state -ge 2) {$true} else {$armed}
        $actual=$flightGuard.Invoke($null,[object[]]@([bool]$armed,[byte]$state))
        if($actual -ne $expected) { throw "Incorrect HOME guard: armed=$armed, landed=$state" }
    }
}
if(!$mav.Contains('MAVlist[buffer.sysid, buffer.compid].cs.HomeLocation = loc;')) { throw 'Actual HOME telemetry must not be frozen' }
'PASS: compiled preservation option; normal/FTP/cache reload guards and HOME row removal verified structurally; actual HOME telemetry retained. No flight-controller commands sent.'
