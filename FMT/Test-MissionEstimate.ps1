param([string]$Directory='bin/MissionEstimate/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.FMT.FmtMissionEstimate',$true)
$method=$type.GetMethod('Calculate',[Reflection.BindingFlags]'Static,NonPublic')
$method.GetParameters() | Out-Null # Resolve the model assemblies before PowerShell type lookup.
$missionHome=New-Object MissionPlanner.Utilities.PointLatLngAlt 25,121,100
$items=New-Object 'System.Collections.Generic.List[MissionPlanner.Utilities.Locationwp]'
function Item($id,$lat=25,$lon=121,$alt=0,$p1=0,$p2=0,$p3=0) {
    $wp=New-Object MissionPlanner.Utilities.Locationwp
    $wp.id=$id; $wp.lat=$lat; $wp.lng=$lon; $wp.alt=$alt; $wp.frame=3
    $wp.p1=$p1; $wp.p2=$p2; $wp.p3=$p3
    return $wp
}
function Calculate {
    return $method.Invoke($null,@($items.PSObject.BaseObject,$missionHome.PSObject.BaseObject,10.0,2.0,2.0,10.0,$true))
}
$items.Add((Item 16 25 121 20 5))
$result=Calculate
if($result.Seconds -ne 15 -or $result.Uncertain) { throw 'Climb and waypoint hold incorrect' }
$items.Clear(); $items.Add((Item 16 25.001 121 0))
$result=Calculate
if($result.Metres -lt 110 -or $result.Metres -gt 112 -or [Math]::Abs($result.Seconds-$result.Metres/10) -gt .001) { throw 'Distance/time incorrect' }
$items.Add((Item 178 0 0 0 1 20 -1))
$result=Calculate
if([Math]::Abs($result.Seconds-$result.Metres/10) -gt .001) { throw 'DO speed must not retroactively change preceding NAV leg' }
$firstSeconds=$result.Seconds
$items.Add((Item 16 25.002 121 0))
$result=Calculate
if([Math]::Abs(($result.Seconds-$firstSeconds)-$firstSeconds/2) -gt .001) { throw 'DO speed must apply to following NAV leg' }
$items.Add((Item 178 0 0 0 0 8 -1)); $items.Add((Item 16 25.003 121 0))
$result=Calculate
if([Math]::Abs($result.Seconds-($firstSeconds*1.5+$firstSeconds*10/8)) -gt .001) { throw 'Consecutive speed changes assigned to wrong leg' }
$items.Clear(); $items.Add((Item 19 25 121 0 30))
$result=Calculate
if($result.Seconds -ne 30 -or $result.Metres -ne 0) { throw 'Timed hover incorrect' }
$result=$method.Invoke($null,@($items.PSObject.BaseObject,$missionHome.PSObject.BaseObject,10.0,2.0,2.0,10.0,$false))
if($result.Seconds -ne 30 -or $result.Metres -ne 300) { throw 'Plane timed loiter incorrect' }
$items.Clear(); $items.Add((Item 16 25.001 121)); $items.Add((Item 16 25 121)); $items.Add((Item 177 0 0 0 1 2))
$result=Calculate
if($result.Uncertain -or $result.Rows.Count -ne 9 -or $result.Metres -lt 666 -or $result.Metres -gt 672) { throw 'Finite jump must include initial pass and two repeats' }
$wp=$items[2]; $wp.p2=0; $items[2]=$wp
if((Calculate).Rows.Count -ne 3) { throw 'Zero-repeat jump incorrect' }
$wp=$items[2]; $wp.p2=-1; $items[2]=$wp
if(!(Calculate).Uncertain) { throw 'Infinite jump must be uncertain' }
$wp=$items[2]; $wp.p2=2; $wp.p1=99; $items[2]=$wp
if(!(Calculate).Uncertain) { throw 'Out-of-range jump target accepted' }
$wp=$items[2]; $wp.p1=1.5; $items[2]=$wp
if(!(Calculate).Uncertain) { throw 'Fractional jump target accepted' }
$items.Clear(); $items.Add((Item 177 0 0 0 1 32767))
$result=Calculate
if(!$result.Uncertain -or $result.Rows.Count -ne 20000) { throw 'Jump expansion safety bound failed' }
$summaryMethod=$type.GetMethod('Summary',[Reflection.BindingFlags]'Static,NonPublic')
$summary=$summaryMethod.Invoke($null,@($result,1))
if($summary -match 'km' -or $summary -match '32767') { throw 'Partial totals must not be presented as a total estimate' }
$items.Clear(); $items.Add((Item 31 25.001 121 40))
$result=$method.Invoke($null,@($items.PSObject.BaseObject,$missionHome.PSObject.BaseObject,10.0,2.0,2.0,10.0,$false))
if($result.Uncertain -or $result.Seconds -lt 31 -or $result.Seconds -gt 32 -or $result.Metres -lt 310) { throw 'Loiter altitude approach and climb must be separate phases' }
$wp=$items[0]; $wp.p1=1; $items[0]=$wp
$result=$method.Invoke($null,@($items.PSObject.BaseObject,$missionHome.PSObject.BaseObject,10.0,2.0,2.0,10.0,$false))
if(!$result.Uncertain) { throw 'Heading-required loiter exit cannot be silently omitted' }
foreach($id in @(17,177,20,83,112)) {
    $items.Clear(); $items.Add((Item $id)); $result=Calculate
    if(!$result.Uncertain) { throw "Uncertain command $id incorrectly reported complete" }
}
$items.Clear(); $items.Add((Item 93 0 0 0 25)); $items.Add((Item 3000 0 0 0 4))
$result=$method.Invoke($null,@($items.PSObject.BaseObject,$missionHome.PSObject.BaseObject,10.0,2.0,2.0,10.0,$false))
if($result.Seconds -ne 35 -or $result.Uncertain) { throw 'NAV delay / transition incorrect' }
if(!(Calculate).Uncertain) { throw 'Copter must not accept a VTOL transition' }
$items.Clear(); $items.Add((Item 3000))
$result=$method.Invoke($null,@($items.PSObject.BaseObject,$missionHome.PSObject.BaseObject,10.0,2.0,2.0,10.0,$false))
if(!$result.Uncertain) { throw 'Invalid VTOL target accepted' }
foreach($id in @(16,19)) {
    $items.Clear(); $items.Add((Item $id 25 121 0 -1))
    if(!(Calculate).Uncertain) { throw 'Negative hold time silently accepted' }
}
$savedLat=$missionHome.Lat
try {
    $missionHome.Lat=[double]::NaN
    $rejected=$false
    try { Calculate | Out-Null } catch { $rejected=$true }
    if(!$rejected) { throw 'Non-finite HOME accepted' }
} finally { $missionHome.Lat=$savedLat }
$wp=Item 16; $wp.frame=10; $items.Clear(); $items.Add($wp)
if(!(Calculate).Uncertain) { throw 'Terrain altitude incorrectly treated as home-relative' }
$formType=$assembly.GetType('MissionPlanner.FMT.FmtMissionEstimateForm',$true)
$items.Clear(); $items.Add((Item 22 25 121 40)); $items.Add((Item 16 25.001 121 40)); $items.Add((Item 19 25.001 121 40 30))
$form=[Activator]::CreateInstance($formType,[Reflection.BindingFlags]'Instance,NonPublic',$null,@($items.PSObject.BaseObject,$missionHome.PSObject.BaseObject),$null)
try {
    $form.ShowInTaskbar=$false; $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point -30000,-30000
    $form.Show(); [Windows.Forms.Application]::DoEvents()
    foreach($panel in $form.Controls) { foreach($control in $panel.Controls) { if($control -is [Windows.Forms.Button]) { $control.PerformClick() } } }
    [Windows.Forms.Application]::DoEvents()
    $bitmap=New-Object Drawing.Bitmap $form.Width,$form.Height
    try { $form.DrawToBitmap($bitmap,$form.ClientRectangle); $bitmap.Save((Join-Path $binaryDirectory 'mission-estimate-preview.png')) }
    finally { $bitmap.Dispose() }
} finally { $form.Dispose() }
$source=Get-Content "$PSScriptRoot/FmtMissionEstimate.cs" -Raw
if($source -match 'setParam\(|sendPacket\(|doCommand\(') { throw 'Estimator must remain offline' }
'PASS: distance, climb/hold, NAV-leg speed, loiter, uncertain commands, terrain, delay/transition, offline UI; no vehicle writes'
