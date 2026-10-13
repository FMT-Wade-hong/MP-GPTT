param([string]$Directory='bin/HomeSymbol123/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$maps=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Maps.dll'))
$type=$maps.GetType('MissionPlanner.Maps.GMapMarkerWP',$true)
$bitmap=$type.GetField('homeSymbol',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
$bitmap.Save((Join-Path $binaryDirectory 'home-symbol.png'))
if($bitmap.GetPixel(0,0).A -ne 0 -or $bitmap.GetPixel(32,17).A -ne 0) { throw 'Background not transparent' }
for($x=0;$x -lt 64;$x++) { for($y=0;$y -lt 64;$y++) {
    $c=$bitmap.GetPixel($x,$y)
    if($c.A -gt 128 -and ($c.R -ne $c.G -or $c.R -eq 0 -or $c.B -gt 15)) { throw 'Non-yellow icon pixel' }
} }
$pointType=$type.GetConstructors()[0].GetParameters()[0].ParameterType
$point=[Activator]::CreateInstance($pointType,[object[]]@(23.0,120.0))
foreach($label in @('H','1')) {
    $marker=[Activator]::CreateInstance($type,[object[]]@($point,$label))
    try {
        if($marker.Position.Lat -ne 23 -or $marker.Position.Lng -ne 120) { throw 'Coordinate changed' }
        if($label -eq 'H' -and ($marker.Offset.X -ne -32 -or $marker.Offset.Y -ne -32)) { throw 'HOME not centred' }
        if($label -eq '1' -and $marker.Size.Width -eq 64) { throw 'Normal waypoint changed' }
    } finally { $marker.Dispose() }
}
'PASS: transparent yellow icon, centred HOME, unchanged normal waypoint and coordinates; offline only.'
