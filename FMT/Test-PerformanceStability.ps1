param([string]$Directory = 'bin/PerformanceStabilityTest/net461')
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$type = $assembly.GetType('MissionPlanner.FMT.FmtUiUpdateGate', $true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$enter = $type.GetMethod('TryEnter', $flags)
$complete = $type.GetMethod('Complete', $flags)
$gate = [Activator]::CreateInstance($type, $true)
function Enter-Gate([int]$Tick) { $enter.Invoke($gate, @($Tick, [uint32]250)) }
if (!(Enter-Gate 0)) { throw 'Initial update rejected' }
for ($i = 1; $i -le 10000; $i++) {
    if (Enter-Gate $i) { throw 'UI queue can grow while callback is pending' }
}
$complete.Invoke($gate, @()) | Out-Null
if (Enter-Gate 249) { throw 'Refresh not rate limited' }
if (!(Enter-Gate 250)) { throw 'Refresh does not resume after completion' }
$complete.Invoke($gate, @()) | Out-Null
'PASS: 10000 repeated requests retain only one pending callback; 250 ms refresh limit'
$gate = [Activator]::CreateInstance($type, $true)
if (!(Enter-Gate 2147483600)) { throw 'Initial wrap test failed' }
$complete.Invoke($gate, @()) | Out-Null
if (Enter-Gate -2147483600) { throw 'Tick wrap bypasses interval' }
if (!(Enter-Gate -2147483300)) { throw 'Tick wrap permanently blocks refresh' }
$complete.Invoke($gate, @()) | Out-Null
'PASS: Environment.TickCount signed wrap remains safe'

# Structural regressions supplement the gate behavior test; these are not UI benchmarks.
$planner = Get-Content (Join-Path $PSScriptRoot '../GCSViews/FlightPlanner.cs') -Raw
if ($planner -notmatch 'callMeDrag\(CurentRectMarker.InnerMarker.Tag.ToString\(\), MouseDownEnd.Lat,\s*MouseDownEnd.Lng, -2\)') {
    throw 'Final waypoint position must use mouse-release coordinates'
}
if ($planner -notmatch 'if \(fmtMouseTerrainBusy \|\|' -or $planner -notmatch 'version != fmtMouseTerrainVersion') {
    throw 'Single-flight terrain guard or stale-result check missing'
}
'PASS: waypoint release endpoint and terrain coalescing guards present (source checks)'
