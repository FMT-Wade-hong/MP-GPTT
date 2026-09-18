param([string]$Directory = 'bin/WaypointLabelTest/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$type = $assembly.GetType('MissionPlanner.FMT.FmtWaypointDistanceMarker', $true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$format = $type.GetMethod('FormatDistance', $flags)
if ($format.Invoke($null, @([double]10)) -ne '<-10M->') { throw 'Wrong label format' }
if ($format.Invoke($null, @([double]1234)) -ne '<-1234M->') { throw 'Wrong large distance' }
$bounds = $type.GetMethod('LabelBounds', $flags)
foreach ($width in @(55, 110)) {
    $rect = $bounds.Invoke($null, @([Drawing.Point]::new(100,100), [int]$width, [int]24))
    $plus = [Drawing.Rectangle]::new(90,90,20,20)
    if ($rect.IntersectsWith($plus) -or $rect.Bottom -gt 84) { throw 'Label overlaps insertion button' }
}
'PASS: <-10M-> format; labels remain above 20px insertion button without overlap'
