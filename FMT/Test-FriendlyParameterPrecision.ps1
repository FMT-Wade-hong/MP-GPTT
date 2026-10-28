param([string]$Directory = 'bin/Local124/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$mav = [MissionPlanner.MainV2]::comPort.MAV
$mav.cs.firmware = [Enum]::Parse($mav.cs.firmware.GetType(),'ArduCopter2')
$mav.VersionString = 'ArduCopter V4.7.2'
$paramType = $mav.param.GetType().BaseType.GetGenericArguments()[0]
$ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 } | Select-Object -First 1
$wire = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigFriendlyParams',$true)
$validate = [Windows.Forms.NumericUpDown].GetMethod('ValidateEditText',$flags)
foreach ($case in @(@('PSC_NE_VEL_IMAX','Advanced',10,8.5),@('PSC_D_ACC_D','Standard',0.01234567,0.02345678))) {
    $mav.param.Clear(); $mav.param.Add($ctor.Invoke(@($case[0],[double]$case[2],$wire,$null)))
    $page = [Activator]::CreateInstance($type)
    try {
        $page.ParameterMode = $case[1]
        $page.Activate()
        $panel = $type.GetField('flowLayoutPanel1',$flags).GetValue($page)
        $changes = $type.GetField('_params_changed',$flags).GetValue($page)
        $control = @($panel.Controls | Where-Object { $_.Name -eq $case[0] })
        if ($control.Count -ne 1) { throw 'Friendly parameter disappeared' }
        $editor = $control[0].NumericUpDownControl
        if ([single]$control[0].Value -ne [single]$mav.param[$case[0]].Value -or $changes.Count) { throw 'Initial float value truncated or staged' }
        $editor.Text = ([single]$case[3]).ToString('R',[Globalization.CultureInfo]::CurrentCulture)
        $validate.Invoke($editor,@()) | Out-Null
        if ([single]$changes[$case[0]] -ne [single]$case[3]) { throw "Friendly input silently rounded: $($case[0]) expected $($case[3]), got $($changes[$case[0]])" }
        if ([single]$mav.param[$case[0]].Value -ne [single]$case[2]) { throw 'Friendly edit changed cache' }
        # A changed firmware must rebuild controls and discard the other target's staged writes.
        $old = $control[0]
        $mav.VersionString = 'ArduCopter V4.6.3'
        $mav.param.Clear(); $mav.param.Add($ctor.Invoke(@('PSC_VELXY_IMAX',[double]100,$wire,$null)))
        $page.ParameterMode = 'Advanced'; $page.Activate()
        if (!$old.IsDisposed -or $changes.Count -or @($panel.Controls | Where-Object {$_.Name -eq $case[0]}).Count) { throw 'Old version editor survived reconnect' }
    } finally { $page.Dispose(); $mav.VersionString='ArduCopter V4.7.2' }
}
$controls = [Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Controls.dll'))
$rangeType = $controls.GetType('MissionPlanner.Controls.RangeControl',$true)
$rangeCtor = $rangeType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 9 }
foreach ($culture in @('en-US','de-DE')) {
    $oldCulture = [Threading.Thread]::CurrentThread.CurrentCulture
    [Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]$culture
    try {
        foreach ($integer in @($false,$true)) {
            $range = $rangeCtor.Invoke(@('FMT_TEST','Test','Test',[single]1,[single]1,[single]0,[single]10,'8',$integer))
            try {
                $range.NumericUpDownControl.Text = ([double]8.4).ToString([Globalization.CultureInfo]::CurrentCulture)
                $validate.Invoke($range.NumericUpDownControl,@()) | Out-Null
                $expected = if ($integer) { 8 } else { 8.4 }
                if ([Math]::Abs([double]::Parse($range.Value,[Globalization.CultureInfo]::InvariantCulture)-$expected) -gt 0.00001) { throw 'Float/integer/culture handling failed' }
            } finally { $range.Dispose() }
        }
    } finally { [Threading.Thread]::CurrentThread.CurrentCulture=$oldCulture }
}
$mav.param.Clear()
'PASS: actual friendly page; fractional IMAX; sub-milliscale PID precision; staged writes/cache; version editor disposal; float/integer distinction; en-US/de-DE parsing'
