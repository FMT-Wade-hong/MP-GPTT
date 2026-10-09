param([string]$Directory = 'bin/MapIcons122/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.FMT.FmtMapIconSettings',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$choiceType=$type.GetNestedType('Choice',[Reflection.BindingFlags]::NonPublic)
$render=$type.GetMethod('Render',$flags)
foreach($style in @('Plane','Copter','Heli','VTOL','Boat','Rover')) {
    $c=[Activator]::CreateInstance($choiceType,$true)
    $c.Style=$style; $c.Size=96
    $image=$render.Invoke($null,@($c,$false))
    try {
        if($image.Width -ne 96 -or $image.Height -ne 96) { throw 'Wrong icon size' }
        $nonzero=0
        for($x=0;$x -lt 96;$x+=4) { for($y=0;$y -lt 96;$y+=4) { if($image.GetPixel($x,$y).A -gt 0) { $nonzero++ } } }
        if($nonzero -lt 10) { throw "Empty icon: $style" }
    } finally { $image.Dispose() }
}
$c=[Activator]::CreateInstance($choiceType,$true)
if($null -ne $render.Invoke($null,@($c,$false))) { throw 'Default must retain original marker' }
$png=New-Object Drawing.Bitmap 40,20
$stream=New-Object IO.MemoryStream
try {
    $g=[Drawing.Graphics]::FromImage($png)
    try { $g.Clear([Drawing.Color]::Red) } finally { $g.Dispose() }
    $png.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $c.Style='PNG'; $c.Png=[Convert]::ToBase64String($stream.ToArray()); $c.Opacity=50
    $image=$render.Invoke($null,@($c,$false))
    try { if($image.GetPixel(32,32).A -lt 120 -or $image.GetPixel(32,32).A -gt 130) { throw 'PNG opacity failed' } }
    finally { $image.Dispose() }
    $c.Png='invalid'
    $rejected=$false
    try { $render.Invoke($null,@($c,$false)) | Out-Null } catch { $rejected=$true }
    if(!$rejected) { throw 'Malformed PNG accepted' }
} finally { $png.Dispose(); $stream.Dispose() }
$choices=$type.GetMethod('Load',$flags).Invoke($null,@())
foreach($key in @('Plane','Copter','Heli','VTOL','Boat','Rover')) { $choices[$key].Style=$key; $choices[$key].Size=80 }
$type.GetMethod('Apply',$flags).Invoke($null,@($choices,$false)) | Out-Null
$json=[Newtonsoft.Json.JsonConvert]::SerializeObject($choices)
$restored=[Newtonsoft.Json.JsonConvert]::DeserializeObject($json,$choices.GetType())
if($restored['Boat'].Style -ne 'Boat' -or $restored['Rover'].Size -ne 80) { throw 'Preference roundtrip failed' }
$base=[MissionPlanner.Maps.GMapMarkerBase]
foreach($key in @('Plane','Copter','Heli','VTOL','Boat','Rover')) {
    if($base::CustomIconProvider.Invoke($key,$true).Width -ne 80) { throw 'Provider mapping failed' }
}
$formType=$assembly.GetType('MissionPlanner.FMT.FmtMapIconSettingsForm',$true)
$form=[Activator]::CreateInstance($formType,$true)
try {
    $instance=[Reflection.BindingFlags]'Instance,NonPublic'
    $kind=$formType.GetField('kind',$instance).GetValue($form)
    $style=$formType.GetField('style',$instance).GetValue($form)
    if($kind.Items.Count -ne 6 -or $style.Items.Count -ne 8) { throw 'Missing vehicle/style choice' }
    $preview=$formType.GetField('preview',$instance).GetValue($form)
    $sizeControl=$formType.GetField('size',$instance).GetValue($form)
    foreach($index in @(0,3,4)) {
        $kind.SelectedIndex=$index; $style.SelectedIndex=0
        if($null -eq $preview.Image -or $sizeControl.Enabled) { throw 'Default preview missing or ineffective size editor enabled' }
        if($preview.Image.GetPixel(0,0).A -ne 0) { throw 'Default background must be transparent' }
    }
    $boat=[MissionPlanner.Maps.GMapMarkerBoat]::CreateDefaultPreview()
    try {
        if($boat.Width -ne 64 -or $boat.GetPixel(32,32).A -lt 200) { throw 'Boat default missing' }
    } finally { $boat.Dispose() }
    $kind.SelectedIndex=4; $style.SelectedIndex=5
    if(!$sizeControl.Enabled) { throw 'Custom size editor remains locked' }
    $style.SelectedIndex=0
    $form.ShowInTaskbar=$false
    $form.StartPosition=[Windows.Forms.FormStartPosition]::Manual
    $form.Location=New-Object Drawing.Point -30000,-30000
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    $bitmap=New-Object Drawing.Bitmap $form.Width,$form.Height
    try { $form.DrawToBitmap($bitmap,$form.ClientRectangle); $bitmap.Save((Join-Path (Resolve-Path $Directory) 'map-icons-preview.png')) }
    finally { $bitmap.Dispose() }
} finally { $form.Dispose() }
$source=Get-Content "$PSScriptRoot/FmtMapIconSettings.cs" -Raw
if($source -match 'setParam\(|doCommand\(|comPort') { throw 'Display preferences must not access flight control' }
foreach($file in @('Plane','Quad','Heli','Boat','Rover')) {
    if(!(Get-Content "$PSScriptRoot/../ExtLibs/Maps/GMapMarker$file.cs" -Raw).Contains('DrawCustomIcon(')) { throw 'Missing marker integration' }
}
'PASS: six rendered vehicles, size, original fallback, provider mapping, editor choices, preview and display-only isolation; no settings saved'
