param([string]$Directory='bin/HudWarning123/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$controls=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Controls.dll'))
$ardu=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.ArduPilot.dll'))
$stateType=$ardu.GetType('MissionPlanner.CurrentState',$true)
$state=[Activator]::CreateInstance($stateType)
$entryType=$state.messages.GetType().GetGenericArguments()[0]
foreach($severity in 0..7) {
    $state.messages.Clear()
    $entry=[Activator]::CreateInstance($entryType,[object[]]@([DateTime]::Now,'test',[byte]$severity))
    $state.messages.Add($entry)
    if($state.hudWarningActive -ne ($severity -le 4)) { throw "Wrong severity gate: $severity" }
}
$state.messages.Clear()
$state.messages.Add([Activator]::CreateInstance($entryType,[object[]]@([DateTime]::Now.AddSeconds(-11),'expired',[byte]2)))
if($state.hudWarningActive) { throw 'Expired message still triggers alarm' }
$hud=[Activator]::CreateInstance($controls.GetType('MissionPlanner.Controls.HUD',$true))
$guard=$hud.GetType().GetMethod('HasWarningBorder',[Reflection.BindingFlags]'Instance,NonPublic')
$form=New-Object Windows.Forms.Form
try {
    $hud.connected=$true; $hud.gpsfix=3; $hud.prearmstatus=$true
    if($guard.Invoke($hud,@())) { throw 'Healthy vehicle triggers alarm' }
    foreach($case in @(@('warningActive',$true),@('failsafe',$true),@('gpsfix',1),@('gpsfix2',2),@('ekfstatus',0.6),@('vibex',31),@('vibey',31),@('vibez',31),@('load',100),@('prearmstatus',$false))) {
        $name=$case[0]; $previous=$hud.$name; $hud.$name=$case[1]
        if(!$guard.Invoke($hud,@())) { throw "Missing alarm: $name" }
        $hud.$name=$previous
        if($guard.Invoke($hud,@())) { throw "Alarm failed to clear: $name" }
    }
    $hud.warningActive=$true; $hud.connected=$false
    if($guard.Invoke($hud,@())) { throw 'Disconnected stale data triggers alarm' }
    $hud.connected=$true; $hud.opengl=$false
    $hud.Size=[Drawing.Size]::new(480,360)
    $form.ShowInTaskbar=$false; $form.StartPosition='Manual'; $form.Location=[Drawing.Point]::new(-30000,-30000)
    $form.ClientSize=$hud.Size; $form.Controls.Add($hud); $form.Show()
    foreach($index in 0..3) {
        Start-Sleep -Milliseconds 250
        $hud.Refresh(); [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 60
        $bitmap=[Drawing.Bitmap]::new(480,360)
        try { $hud.DrawToBitmap($bitmap,$hud.ClientRectangle); $bitmap.Save((Join-Path $binaryDirectory "hud-warning-$index.png")) }
        finally { $bitmap.Dispose() }
    }
    $timer=$hud.GetType().GetField('warningPulseTimer',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($hud)
    if(!$timer.Enabled) { throw 'Animation timer not started' }
    $hud.warningActive=$false
    Start-Sleep -Milliseconds 60
    $hud.Refresh()
    if($timer.Enabled) { throw 'Animation timer did not stop after recovery' }
} finally { $form.Dispose(); $hud.Dispose() }
'PASS: 10 warning sources, recovery, disconnected suppression, animation start/stop and offline GDI rendering; no vehicle commands.'
