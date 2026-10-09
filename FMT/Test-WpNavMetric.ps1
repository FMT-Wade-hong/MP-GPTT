param([string]$Directory = 'bin/WpNavMetric122/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArducopter',$true)
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$setup = $type.GetMethod('SetupWpNavMetric',[Reflection.BindingFlags]'Static,NonPublic')
$listType = $setup.GetParameters()[2].ParameterType
$paramType = $listType.BaseType.GetGenericArguments()[0]
$ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 } | Select-Object -First 1
$wireType = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$page = [Activator]::CreateInstance($type)
$changes = $type.GetField('changes',$instanceFlags).GetValue($page)
$startup = $type.GetField('startup',$instanceFlags)
$cases = @(
    @('WPNAV_SPEED',@('WPNAV_SPEED','Q_WP_SPEED','WP_SPD','Q_WP_SPD'),8),
    @('WPNAV_SPEED_UP',@('WPNAV_SPEED_UP','Q_WP_SPEED_UP','WP_SPD_UP','Q_WP_SPD_UP'),3),
    @('WPNAV_SPEED_DN',@('WPNAV_SPEED_DN','Q_WP_SPEED_DN','WP_SPD_DN','Q_WP_SPD_DN'),2),
    @('WPNAV_RADIUS',@('WPNAV_RADIUS','Q_WP_RADIUS','WP_RADIUS_M','Q_WP_RADIUS_M'),3),
    @('WPNAV_LOIT_SPEED',@('WPNAV_LOIT_SPEED','LOIT_SPEED','Q_LOIT_SPEED','LOIT_SPEED_MS','Q_LOIT_SPEED_MS'),5)
)
$count = 0
try {
    foreach ($case in $cases) {
        $editor = $type.GetField($case[0],$instanceFlags).GetValue($page)
        foreach ($name in $case[1]) {
            $scale = if ($name -match '_SPEED_MS$|_RADIUS_M$|^(Q_)?WP_SPD(_UP|_DN)?$') { 1 } else { 100 }
            $list = [Activator]::CreateInstance($listType)
            $raw = [double]$case[2] * $scale
            $list.Add($ctor.Invoke(@($name,$raw,$wireType,$null)))
            $startup.SetValue($page,$true); $changes.Clear()
            $setup.Invoke($null,@($editor,[string[]]$case[1],$list)) | Out-Null
            if (!$editor.Enabled -or $editor.ParamName -ne $name -or [Math]::Abs([double]$editor.Value - $case[2]) -gt 0.001) { throw "Wrong display: $name = $($editor.Value)" }
            if ($changes.Count) { throw "Binding staged a write: $name" }
            $startup.SetValue($page,$false)
            # Subtract to remain below the actual value even when offline metadata has no range.
            $editor.Value = [decimal]$case[2] - 0.1
            $expected = ([double]$case[2] - 0.1) * $scale
            if ($changes.Count -ne 1 -or !$changes.ContainsKey($name) -or [Math]::Abs([double]$changes[$name] - $expected) -gt 0.001) { throw "Wrong wire conversion: $name = $($changes[$name]), expected $expected" }
            if ([Math]::Abs([double]$list[$name].Value - $raw) -gt 0.001) { throw 'UI changed the parameter cache' }
            $count++
        }
        $startup.SetValue($page,$true)
        $empty = [Activator]::CreateInstance($listType)
        $setup.Invoke($null,@($editor,[string[]]$case[1],$empty)) | Out-Null
        if ($editor.Enabled) { throw "Missing parameter enabled: $($case[0])" }
        # Restore screenshot values using the original cm-based names.
        $list = [Activator]::CreateInstance($listType)
        $list.Add($ctor.Invoke(@($case[1][0],([double]$case[2]*100),$wireType,$null)))
        $setup.Invoke($null,@($editor,[string[]]$case[1],$list)) | Out-Null
    }
    $group = $type.GetField('groupBox4',$instanceFlags).GetValue($page)
    if ($group.Text -notmatch 'm/s' -or $type.GetField('label15',$instanceFlags).GetValue($page).Text -notmatch '\u534a\u5f91 m') { throw 'Unit labels missing' }
    $hostForm = New-Object Windows.Forms.Form
    $hostForm.StartPosition = 'Manual'
    $hostForm.Location = New-Object Drawing.Point -30000,-30000
    $hostForm.Size = New-Object Drawing.Size 1400,1000
    $hostForm.Controls.Add($page)
    $hostForm.Show()
    [Windows.Forms.Application]::DoEvents()
    $bmp = New-Object Drawing.Bitmap $group.Width,$group.Height
    try { $group.DrawToBitmap($bmp,$group.ClientRectangle); $bmp.Save((Join-Path $binaryDirectory 'wpnav-metric-preview.png')) }
    finally { $bmp.Dispose(); $hostForm.Dispose() }
    "PASS: $count legacy/new Copter and QuadPlane bindings; display conversion; staged write roundtrip; unchanged cache; absent parameters disabled; metric labels and preview"
} finally { $page.Dispose() }
