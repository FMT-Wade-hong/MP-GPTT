param([string]$Directory='bin/ModeScale123/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.FMT.FmtFlightModeBar',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$bar=[Activator]::CreateInstance($type,$true)
$update=$type.GetMethod('UpdateVehicle',$flags)
$height=$type.GetMethod('SetAvailableHeight',$flags)
$firmwareType=$update.GetParameters()[0].ParameterType
$hostForm=New-Object Windows.Forms.Form
try {
    $hostForm.ShowInTaskbar=$false
    $hostForm.StartPosition='Manual'; $hostForm.Location=New-Object Drawing.Point -30000,-30000
    $hostForm.Controls.Add($bar); $hostForm.Show()
    foreach($firmware in @('ArduCopter2','ArduPlane','ArduRover')) {
        $update.Invoke($bar,@([Enum]::Parse($firmwareType,$firmware),$true,$false,$false,'',([string[]]@()))) | Out-Null
        foreach($width in @(280,420,620)) {
            $bar.Width=$width
            $height.Invoke($bar,@(300)) | Out-Null
            $bar.PerformLayout(); [Windows.Forms.Application]::DoEvents()
            if($bar.Height -gt 150) { throw 'Mode area starves action tabs' }
            $panel=$bar.Controls.Find('fmtFlightModeGroups',$true)[0]
            if(!$panel.AutoScroll -or $panel -is [Windows.Forms.FlowLayoutPanel]) { throw 'Fixed scaling layout required' }
            $rowY=$null; $previousRight=-1
            foreach($control in $panel.Controls) {
                if($control -is [Windows.Forms.Label]) { $rowY=$null; $previousRight=-1; continue }
                if($null -eq $rowY) { $rowY=$control.Top }
                if($control.Top -ne $rowY -or $control.Left -lt $previousRight) { throw 'Mode buttons wrapped or overlap' }
                if($control.Font.Size -lt 7) { throw 'Unreadable scaled font' }
                $previousRight=$control.Right
            }
            if($width -eq 420 -and $firmware -eq 'ArduPlane') {
                $bitmap=New-Object Drawing.Bitmap $bar.Width,$bar.Height
                try { $bar.DrawToBitmap($bitmap,$bar.ClientRectangle); $bitmap.Save((Join-Path $binaryDirectory 'mode-scale-preview.png')) }
                finally { $bitmap.Dispose() }
            }
        }
    }
} finally { $hostForm.Dispose() }
$source=Get-Content "$PSScriptRoot/FmtSafetySettingsPanel.cs" -Raw -Encoding UTF8
if($source -match 'if\s*\(port.MAV.cs.armed\)\s*\{') { throw 'Armed hard block returned' }
foreach($required in @('port.MAV.cs.armed ?','MessageBoxDefaultButton.Button2','port.ReadOnly','port.logreadmode','port.sysidcurrent != targetSystem')) {
    if(!$source.Contains($required)) { throw "Missing safety confirmation/guard: $required" }
}
'PASS: fixed mode rows at 280/420/620 px; bounded height and readable scaling; safety warning/read-only/target guards checked structurally; no vehicle writes'
