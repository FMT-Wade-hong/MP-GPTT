param([string]$Directory='bin/ModeScale123/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$type=$assembly.GetType('MissionPlanner.FMT.FmtFlightModeBar',$true)
$update=$type.GetMethod('UpdateVehicle',$flags)
$limit=$type.GetMethod('SetAvailableHeight',$flags)
$firmwareType=$update.GetParameters()[0].ParameterType
$safetyType=$assembly.GetType('MissionPlanner.FMT.FmtSafetySettingsPanel',$true)
# Representative scenarios, not monitor specifications or a real OS DPI change.
$cases=@(
    @('14',1366,768,100),@('14',1920,1080,150),@('14',2560,1600,200),
    @('15.6',1920,1080,125),@('15.6',1920,1080,150),
    @('16',1920,1200,150),@('16',2560,1600,150),
    @('17',1920,1080,100),@('17',2560,1440,150),
    @('19',1280,1024,100),@('19',1440,900,100),@('19',1920,1080,125))
$results=@()
foreach($case in $cases) {
    $logicalWidth=[int][Math]::Floor($case[1]*100/$case[3])
    $logicalHeight=[int][Math]::Floor($case[2]*100/$case[3])-80
    # Conservative scenario: left pane 32%, HUD takes half of body height.
    $paneWidth=[int]($logicalWidth*.32)
    $paneHeight=[int](($logicalHeight-100)*.5)
    $form=New-Object Windows.Forms.Form
    $bar=[Activator]::CreateInstance($type,$true)
    $safety=[Activator]::CreateInstance($safetyType,$true)
    try {
        $form.ShowInTaskbar=$false; $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point -30000,-30000
        $form.ClientSize=New-Object Drawing.Size $logicalWidth,$logicalHeight
        $form.Controls.Add($bar); $bar.Width=$paneWidth
        $form.Show()
        $modeFindings=@(); $scroll=$false; $tabsHeight=$paneHeight; $statusClipped=$false
        foreach($vehicle in @(@('ArduCopter2',$false),@('ArduPlane',$false),@('ArduPlane',$true),@('ArduRover',$false))) {
            $update.Invoke($bar,@([Enum]::Parse($firmwareType,$vehicle[0]),[bool]$vehicle[1],$false,$false,'',([string[]]@()))) | Out-Null
            $limit.Invoke($bar,@($paneHeight)) | Out-Null
            [Windows.Forms.Application]::DoEvents()
            $panel=$bar.Controls.Find('fmtFlightModeGroups',$true)[0]
            if($panel.AutoScroll -or $panel.HorizontalScroll.Visible -or $panel.VerticalScroll.Visible) { throw 'Flight mode bar must fit without scrolling' }
            $y=$null; $right=-1
            foreach($control in $panel.Controls) {
                if($control.Right -gt $panel.ClientSize.Width -or $control.Bottom -gt $panel.ClientSize.Height) { throw 'Mode control is clipped' }
                if($control -is [Windows.Forms.Label]) { $y=$null; $right=-1; continue }
                if($null -eq $y) { $y=$control.Top }
                if($control.Top -ne $y -or $control.Left -lt $right) { $modeFindings+='overlap/wrap' }
                $right=$control.Right
            }
            $scroll=$scroll -or $panel.HorizontalScroll.Visible -or $panel.VerticalScroll.Visible
            $tabsHeight=[Math]::Min($tabsHeight,$paneHeight-$bar.Height)
            $status=$bar.Controls.Find('fmtFlightModeStatus',$true)[0]
            $statusClipped=$statusClipped -or ([Windows.Forms.TextRenderer]::MeasureText($status.Text,$status.Font).Width -gt $status.ClientSize.Width)
            if($vehicle[0] -eq 'ArduPlane' -and $vehicle[1]) {
                $bitmap=New-Object Drawing.Bitmap $bar.Width,$bar.Height
                try { $bar.DrawToBitmap($bitmap,$bar.ClientRectangle); $bitmap.Save((Join-Path $binaryDirectory "screen-$($case[0])-$($case[3])-$logicalWidth.png")) }
                finally { $bitmap.Dispose() }
            }
        }
        if($tabsHeight -lt 100) { $modeFindings+='action area <100px' }
        $bar.Visible=$false
        $safety.Dock='None'; $safety.Size=New-Object Drawing.Size ([int]($logicalWidth*.65)),([int]($logicalHeight-100))
        $form.Controls.Add($safety); $safety.BringToFront(); $safety.PerformLayout()
        [Windows.Forms.Application]::DoEvents()
        $refresh=$safety.Controls.Find('fmtSafetyRefresh',$true)[0]
        $header=$refresh.Parent
        $headerClipped=$refresh.Right -gt $header.ClientSize.Width -or $refresh.Bottom -gt $header.ClientSize.Height
        $results += [pscustomobject]@{Inches=$case[0];Resolution="$($case[1])x$($case[2])";Scale=$case[3];Logical="$logicalWidth x $logicalHeight";ModeWidth=$paneWidth;ModeScroll=$scroll;ModeStatusClipped=$statusClipped;ActionHeight=$tabsHeight;SafetyHeaderClipped=$headerClipped;Findings=($modeFindings -join ',')}
    } finally { $form.Dispose(); $safety.Dispose() }
}
$results | Format-Table -AutoSize | Out-String -Width 240 | Write-Output
$results | ConvertTo-Json | Set-Content (Join-Path $binaryDirectory 'screen-size-matrix.json') -Encoding UTF8
'Completed: logical-workspace simulation only; no real per-monitor DPI, connected vehicle, full main window or populated safety cards tested.'
