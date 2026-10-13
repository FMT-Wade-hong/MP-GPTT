param([string]$Directory='bin/HudCompact123/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$controls=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Controls.dll'))
$hud=[Activator]::CreateInstance($controls.GetType('MissionPlanner.Controls.HUD',$true))
$form=New-Object Windows.Forms.Form
try {
    $form.ShowInTaskbar=$false; $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point -30000,-30000
    $hud.opengl=$false; $hud.distunit='m'; $hud.altunit='m'; $hud.displayalt=$true; $hud.displaygps=$true
    $hud.Size=New-Object Drawing.Size 480,360
    $form.ClientSize=$hud.Size; $form.Controls.Add($hud); $form.Show()
    foreach($gps2 in @(0,3,6)) {
        $hud.gpsfix2=$gps2; $hud.disttowp=0; $hud.throttlePercent=0
        if($gps2 -eq 6) { $hud.disttowp=12340; $hud.throttlePercent=100 }
        $hud.Refresh(); [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 60
        $bitmap=New-Object Drawing.Bitmap 480,360
        try { $hud.DrawToBitmap($bitmap,$hud.ClientRectangle); $bitmap.Save((Join-Path $binaryDirectory "hud-compact-gps2-$gps2.png")) }
        finally { $bitmap.Dispose() }
    }
} finally { $form.Dispose() }
'PASS: offline GDI HUD rendered with absent/present GPS2; no vehicle connection'
