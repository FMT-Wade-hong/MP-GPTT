param([string]$Directory = 'bin/GpsWarning124/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$type = $assembly.GetType('MissionPlanner.FMT.FmtGpsStatusControl',$true)
$icon = [Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'Assets/fmt-satellite-icon.png'))
$panel = [Activator]::CreateInstance($type,$flags,$null,[object[]]@($icon,$true),$null)
$update = $type.GetMethod('UpdateStatus',$flags)
$strip = New-Object Windows.Forms.ToolStrip
$hostItem = New-Object Windows.Forms.ToolStripControlHost ($panel.PSObject.BaseObject)
$hostItem.AutoSize = $false
$hostItem.Margin = [Windows.Forms.Padding]::Empty
$hostItem.Padding = [Windows.Forms.Padding]::Empty
[void]$strip.Items.Add($hostItem)
$strip.Size = New-Object Drawing.Size 700,40
$strip.CreateControl(); $panel.CreateControl()
$primary = $panel.Controls.Find('FmtGpsPrimaryLabel',$true)[0]
$primaryDop = $panel.Controls.Find('FmtGpsDopLabel',$true)[0]
$secondary = $panel.Controls.Find('FmtGpsSecondaryLabel',$true)[0]
$secondaryDop = $panel.Controls.Find('FmtGpsSecondaryDopLabel',$true)[0]
function Set-Status([bool]$connected,[bool]$chinese,[single[]]$values) {
    $update.Invoke($panel,[object[]]@($connected,$chinese,$values[0],$values[1],$values[2],$values[3],$values[4],$values[5],$values[6],$values[7])) | Out-Null
    $hostItem.Size = $panel.Size
    $strip.PerformLayout()
    foreach ($label in @($primary,$primaryDop,$secondary,$secondaryDop)) {
        if ($label.Visible -and ([Windows.Forms.TextRenderer]::MeasureText($label.Text,$label.Font).Width -gt $label.Width -or $label.Right -gt $panel.Width)) { throw "Clipped GPS label: $($label.Name)" }
    }
}
try {
    Set-Status $true $true @(6,32,0.7,1.1,0,0,0,0)
    $singleWidth = $panel.Width
    if ($secondary.Visible -or $primary.Text -notmatch '^衛星: 32' -or $primary.ForeColor.ToArgb() -ne [Drawing.Color]::LimeGreen.ToArgb()) { throw 'Single GPS presentation failed' }
    $snapshots = @()
    foreach ($case in @(
        @($true,$true,@(6,32,0.7,1.1,0,0,0,0)),
        @($true,$true,@(6,32,0.7,1.1,5,18,1.4,2.3)),
        @($true,$true,@(0,0,0,0,0,0,0,0)),
        @($true,$true,@(6,32,0.7,1.1,1,0,0,0)),
        @($false,$true,@(6,32,0.7,1.1,5,18,1.4,2.3)))) {
        Set-Status $case[0] $case[1] $case[2]
        $bitmap = New-Object Drawing.Bitmap $panel.Width,$panel.Height
        $panel.DrawToBitmap($bitmap,$panel.ClientRectangle)
        $snapshots += $bitmap
    }
    try {
        $preview = New-Object Drawing.Bitmap 430,(6+43*$snapshots.Count)
        $graphics = [Drawing.Graphics]::FromImage($preview)
        try {
            $graphics.Clear([Drawing.Color]::FromArgb(24,24,24))
            for ($i=0;$i -lt $snapshots.Count;$i++) { $graphics.DrawImageUnscaled($snapshots[$i],8,(6+$i*43)) }
            $preview.Save((Join-Path $binaryDirectory 'gps-toolbar-preview.png'))
        } finally { $graphics.Dispose(); $preview.Dispose() }
    } finally { foreach ($bitmap in $snapshots) { $bitmap.Dispose() } }
    foreach ($chinese in @($true,$false)) {
        Set-Status $true $chinese @(6,32,0.7,1.1,5,18,1.4,2.3)
        if (!$secondary.Visible -or $secondary.Text -notmatch ': 18' -or $secondaryDop.Text -cne 'H: 1.4  |  V: 2.3') { throw 'Second receiver data mixed with first' }
        if ($primaryDop.Text -cne 'H: 0.7  |  V: 1.1' -or $secondary.Left -le $primary.Right -or $panel.Width -le $singleWidth) { throw 'GPS columns overlap or fail to expand' }
        if ($primary.ForeColor.ToArgb() -ne [Drawing.Color]::LimeGreen.ToArgb() -or $secondary.ForeColor.ToArgb() -ne [Drawing.Color]::Gold.ToArgb() -or $secondaryDop.ForeColor.ToArgb() -ne [Drawing.Color]::Gold.ToArgb()) { throw 'Receiver colors incorrect' }
    }
    # Warning status overrides each receiver's identity color independently.
    foreach ($case in @(@(0,0,'OrangeRed'),@(1,3,'OrangeRed'),@(2,4,'Gold'),@(3,12,'LimeGreen'),@(6,32,'LimeGreen'))) {
        Set-Status $true $true @($case[0],$case[1],0.7,1.1,6,18,1.4,2.3)
        $expected = [Drawing.Color]::FromName($case[2]).ToArgb()
        if ($primary.ForeColor.ToArgb() -ne $expected -or $primaryDop.ForeColor.ToArgb() -ne $expected -or $secondary.ForeColor.ToArgb() -ne [Drawing.Color]::Gold.ToArgb()) { throw 'GPS1 warning/recovery color or receiver isolation failed' }
        Set-Status $true $true @(6,32,0.7,1.1,$case[0],$case[1],1.4,2.3)
        if ($case[0] -eq 0) {
            if ($secondary.Visible) { throw 'Absent GPS2 unexpectedly visible' }
        } else {
            $expectedSecond = if ($case[0] -lt 2) { [Drawing.Color]::OrangeRed } else { [Drawing.Color]::Gold }
            if ($secondary.ForeColor.ToArgb() -ne $expectedSecond.ToArgb() -or $secondaryDop.ForeColor.ToArgb() -ne $expectedSecond.ToArgb() -or $primary.ForeColor.ToArgb() -ne [Drawing.Color]::LimeGreen.ToArgb()) { throw 'GPS2 warning/recovery color or receiver isolation failed' }
        }
    }
    Set-Status $true $true @(6,32,0.7,1.1,1,0,0,0)
    if (!$secondary.Visible -or $secondary.Text -notmatch '未定位' -or $secondaryDop.Text -cne 'H: --  |  V: --') { throw 'Present receiver without fix was hidden' }
    Set-Status $true $true @(6,32,0.7,1.1,5,255,655.35,655.35)
    if ($secondary.Text -notmatch ': --' -or $secondaryDop.Text -cne 'H: --  |  V: --') { throw 'Unknown telemetry shown as real measurements' }
    Set-Status $true $true @(6,32,0.7,1.1,0,18,1.4,2.3)
    if ($secondary.Visible -or $panel.Width -ne $singleWidth) { throw 'Absent second receiver left stale columns' }
    Set-Status $false $true @(6,32,0.7,1.1,5,18,1.4,2.3)
    if ($secondary.Visible -or $primary.Text -notmatch '未連線' -or $primary.ForeColor.ToArgb() -ne [Drawing.Color]::Gray.ToArgb()) { throw 'Disconnect left stale GPS data' }
} finally { $strip.Dispose(); $icon.Dispose() }

# Parse actual MAVLink packets into CurrentState; never open a transport.
$mav = [MissionPlanner.MainV2]::comPort.MAV
$mavAssembly = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetType('MAVLink+MavlinkParse') } | Select-Object -First 1
$parser = [Activator]::CreateInstance($mavAssembly.GetType('MAVLink+MavlinkParse'),[object[]]@($false))
$gpsType = $mavAssembly.GetType('MAVLink+mavlink_gps2_raw_t')
$idType = $mavAssembly.GetType('MAVLink+MAVLINK_MSG_ID')
$messageType = $mavAssembly.GetType('MAVLink+MAVLinkMessage')
$receive = $mav.cs.GetType().GetMethod('Parent_OnPacketReceived',$flags)
$mav.cs.gpshdop = 0.7; $mav.cs.gpsvdop = 1.1
foreach ($epv in @(230,65535)) {
    $gps = [Activator]::CreateInstance($gpsType)
    $gps.fix_type = 5; $gps.satellites_visible = 18; $gps.eph = 140; $gps.epv = [uint16]$epv
    $bytes = $parser.GenerateMAVLinkPacket20([Enum]::Parse($idType,'GPS2_RAW'),$gps,$false,[byte]0,[byte]0,-1)
    $message = [Activator]::CreateInstance($messageType,[object[]]@([byte[]]$bytes,[DateTime]::UtcNow))
    $receive.Invoke($mav.cs,@($null,$message)) | Out-Null
    $expectedVdop = if ($epv -eq 65535) { 0 } else { 2.3 }
    if ([Math]::Abs($mav.cs.gpsvdop2-$expectedVdop) -gt 0.0001 -or [Math]::Abs($mav.cs.gpshdop2-1.4) -gt 0.0001 -or $mav.cs.satcount2 -ne 18) { throw 'GPS2 packet did not populate independent H/V values' }
    if ([Math]::Abs($mav.cs.gpsvdop-1.1) -gt 0.0001 -or [Math]::Abs($mav.cs.gpshdop-0.7) -gt 0.0001) { throw 'GPS2 overwrote GPS1 DOP' }
}
'PASS: real toolbar rendering; normal GPS1 green/GPS2 yellow; red no-GPS/no-fix warnings override identity; yellow 2D; independent warning/recovery; single/dual/disconnected states; independent count/fix/H/V; unknown values; Chinese/English layout; actual GPS2_RAW decode; no transport opened'
