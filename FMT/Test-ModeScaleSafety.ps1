param([string]$Directory='bin/ModeFit124/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.FMT.FmtFlightModeBar',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$bar=[Activator]::CreateInstance($type,$true)
$update=$type.GetMethod('UpdateVehicle',$flags)
$height=$type.GetMethod('SetAvailableHeight',$flags)
$firmwareType=$update.GetParameters()[0].ParameterType
$hostForm=New-Object Windows.Forms.Form
$fontProbe=New-Object Drawing.Bitmap 2,2
$fontGraphics=[Drawing.Graphics]::FromImage($fontProbe)
try {
    $hostForm.ShowInTaskbar=$false
    $hostForm.StartPosition='Manual'; $hostForm.Location=New-Object Drawing.Point -30000,-30000
    $hostForm.Controls.Add($bar); $hostForm.Show()
    foreach($vehicle in @(@('ArduCopter2',$false,$false),@('ArduPlane',$false,$false),@('ArduPlane',$true,$false),@('ArduCopter2',$false,$true),@('ArduRover',$false,$false),@('ArduSub',$false,$false))) {
        $firmware=$vehicle[0]
        $update.Invoke($bar,@([Enum]::Parse($firmwareType,$firmware),[bool]$vehicle[1],[bool]$vehicle[2],$false,'',([string[]]@()))) | Out-Null
        foreach($available in @(180,300,600)) {
        $previousWidth=0; $previousFont=0
        foreach($width in @(280,420,620,900,1200,280)) {
            $bar.Width=$width
            $height.Invoke($bar,@($available)) | Out-Null
            $bar.PerformLayout(); [Windows.Forms.Application]::DoEvents()
            if($bar.Height -gt ($available/2)) { throw 'Mode area starves action tabs' }
            $panel=$bar.Controls.Find('fmtFlightModeGroups',$true)[0]
            if($panel.AutoScroll -or $panel.HorizontalScroll.Visible -or $panel.VerticalScroll.Visible) { throw 'Mode area must never scroll' }
            $rowY=$null; $previousRight=-1
            foreach($control in $panel.Controls) {
                [void]$fontGraphics.MeasureString($control.Text,$control.Font)
                if($control.Left -lt 0 -or $control.Top -lt 0 -or $control.Right -gt $panel.ClientSize.Width -or $control.Bottom -gt $panel.ClientSize.Height) { throw "Clipped mode control: $firmware $width x $available $($control.Text)" }
                if($control -is [Windows.Forms.Label]) { $rowY=$null; $previousRight=-1; continue }
                if($null -eq $rowY) { $rowY=$control.Top }
                if($control.Top -ne $rowY -or $control.Left -lt $previousRight) { throw 'Mode buttons wrapped or overlap' }
                $textSize=[Windows.Forms.TextRenderer]::MeasureText($control.Text,$control.Font,[Drawing.Size]::Empty,([Windows.Forms.TextFormatFlags]'NoPadding,NoPrefix'))
                if($textSize.Width -gt $control.Width -or $textSize.Height -gt $control.Height) { throw "Mode caption clipped: $firmware $width x $available $($control.Text) $textSize vs $($control.Size)" }
                if($control.Enabled){throw 'Disconnected mode became clickable'}
                $previousRight=$control.Right
            }
            $first=@($panel.Controls | Where-Object {$_ -is [Windows.Forms.Button]})[0]
            if($width -gt 280 -and ($first.Width -le $previousWidth -or $first.Font.Size -lt $previousFont)){throw 'Buttons/text did not scale with larger viewport'}
            $previousWidth=$first.Width; $previousFont=$first.Font.Size
            if($available -eq 600 -and $width -in @(280,620,900) -and ($firmware -eq 'ArduCopter2' -or $vehicle[1])) {
                $bitmap=New-Object Drawing.Bitmap $bar.Width,$bar.Height
                try { $bar.DrawToBitmap($bitmap,$bar.ClientRectangle); $bitmap.Save((Join-Path $binaryDirectory "mode-scale-$firmware-$($vehicle[1])-$($vehicle[2])-$width-preview.png")) }
                finally { $bitmap.Dispose() }
            }
        }
        }
    }
} finally { $hostForm.Dispose(); $fontGraphics.Dispose(); $fontProbe.Dispose() }
$source=Get-Content "$PSScriptRoot/FmtSafetySettingsPanel.cs" -Raw -Encoding UTF8
if($source -match 'if\s*\(port.MAV.cs.armed\)\s*\{') { throw 'Armed hard block returned' }
foreach($required in @('port.MAV.cs.armed ?','MessageBoxDefaultButton.Button2','port.ReadOnly','port.logreadmode','port.sysidcurrent != targetSystem')) {
    if(!$source.Contains($required)) { throw "Missing safety confirmation/guard: $required" }
}
'PASS: 108 viewport/profile cases; no scrolling, wrapping, overlap, clipped buttons or captions; buttons and fonts scale with the viewport; disconnected and existing safety guards preserved; no vehicle writes.'
