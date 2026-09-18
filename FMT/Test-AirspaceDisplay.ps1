param([string]$Directory = 'bin/AirspaceDisplayTest/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/GMap.NET.Core.dll")) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$radiusType = $assembly.GetType('MissionPlanner.FMT.FmtAirspaceRadius')
$toKm = $radiusType.GetMethod('MetresToKilometres', [Reflection.BindingFlags]'Static,NonPublic')
$toMetres = $radiusType.GetMethod('KilometresToMetres', [Reflection.BindingFlags]'Static,NonPublic')
foreach ($metres in @(1, 30, 5000, 10000, 30000)) {
    $km = $toKm.Invoke($null, @([int]$metres))
    if ($km -ne ([decimal]$metres / 1000)) { throw 'Incorrect kilometre display' }
    if ($toMetres.Invoke($null, @([decimal]$km)) -ne $metres) { throw 'Saved radius changed during unit conversion' }
}
'PASS: kilometre display and metre storage round-trip; 5000 m = 5 km'
$uiSource = Get-Content (Join-Path $PSScriptRoot '../GCSViews/FlightPlanner.AirspaceDisplay.cs') -Raw
if (!$uiSource.Contains('GetInt32("fmt_airspace_radius_m", 5000)') -or !$uiSource.Contains('DecimalPlaces = 0')) { throw 'Default must be 5 km without decimals' }
$whole = $radiusType.GetMethod('WholeKilometres', [Reflection.BindingFlags]'Static,NonPublic')
foreach ($case in @(@(0.03,1), @(5,5), @(5.4,5), @(5.5,6), @(40,30))) {
    if ($whole.Invoke($null, @([decimal]$case[0])) -ne $case[1]) { throw 'Whole kilometre normalization failed' }
}
'PASS: default 5 km without decimals; old values and typed decimals normalize to 1-30 whole km'
$visibility = $assembly.GetType('MissionPlanner.FMT.FmtAirspaceRadius').GetMethod('ShouldDisplay', [Reflection.BindingFlags]'Static,NonPublic')
foreach ($zoom in @(9.99, 10.0, 14.0, 18.0, 18.01, [double]::NaN)) {
    $expected = $zoom -ge 10 -and $zoom -le 18
    if ($visibility.Invoke($null, @($true, [double]$zoom)) -ne $expected) { throw "Zoom boundary failed: $zoom" }
    if ($visibility.Invoke($null, @($false, [double]$zoom))) { throw 'Manual off ignored' }
}
'PASS: zoom 10 and 18 inclusive; outside range and manual off hidden'
$method = $assembly.GetType('MissionPlanner.FMT.FmtAirspaceRadius').GetMethod('Intersects', [Reflection.BindingFlags]'Static,NonPublic')
$homePoint = New-Object GMap.NET.PointLatLng(25,121)
function Test-Ring($coordinates, [bool]$expected, [string]$name) {
    $ring = New-Object 'System.Collections.Generic.List[GMap.NET.PointLatLng]'
    foreach ($xy in $coordinates) {
        $ring.Add((New-Object GMap.NET.PointLatLng((25 + $xy[1]/111320), (121 + $xy[0]/(111320*[Math]::Cos(25*[Math]::PI/180))))))
    }
    $arguments = New-Object 'System.Object[]' 3
    $arguments[0] = $ring.PSObject.BaseObject
    $arguments[1] = $homePoint.PSObject.BaseObject
    $arguments[2] = [double]30
    $actual = $method.Invoke($null, $arguments)
    if ($actual -ne $expected) { throw $name }
    "PASS: $name"
}
Test-Ring @(@(-100,-100),@(100,-100),@(100,100),@(-100,100)) $true 'HOME enclosed, all vertices outside radius'
Test-Ring @(@(20,-100),@(40,-100),@(40,100),@(20,100)) $true 'Edge crosses radius without nearby vertices'
Test-Ring @(@(100,100),@(120,100),@(120,120),@(100,120)) $false 'Distant zone excluded'
Test-Ring @(@(1,1),@(2,1),@(2,2),@(1,2)) $true 'Small nearby zone included'
$source = Get-Content (Join-Path $PSScriptRoot '../GCSViews/FlightPlanner.cs') -Raw
$display = $source.Substring($source.IndexOf('private async Task UpdateTaiwanCaaAirspace('))
$display = $display.Substring(0, $display.IndexOf('private void MainMap_OnCurrentPositionChanged('))
if (!$display.Contains('!fmtAirspaceVisible.Checked') -or !$display.Contains('LoadNearbyAsync(home)') -or !$display.Contains('IsHitTestVisible = false')) { throw 'Display guard missing' }
'PASS: display off guard, HOME-centered query and no polygon mouse hit testing (source checks)'
