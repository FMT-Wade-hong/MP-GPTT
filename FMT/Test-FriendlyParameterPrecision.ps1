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
# Raw-table display and editing must expand exponents without changing numeric values.
$rawType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams',$true)
$rawStartup = $rawType.GetField('startup',[Reflection.BindingFlags]'Static,NonPublic')
$oldStartup = $rawStartup.GetValue($null)
$oldCulture = [Threading.Thread]::CurrentThread.CurrentCulture
$raw = [Activator]::CreateInstance($rawType)
$form = New-Object Windows.Forms.Form
try {
    $rawStartup.SetValue($null,$true)
    $grid = $rawType.GetField('Params',$flags).GetValue($raw)
    $grid.AllowUserToAddRows=$false; $grid.ReadOnly=$false
    $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point -30000,-30000
    $form.Size=New-Object Drawing.Size 1000,700
    $raw.Dock='Fill'; $form.Controls.Add($raw); $form.Show()
    foreach ($culture in @('zh-TW','de-DE')) {
        [Threading.Thread]::CurrentThread.CurrentCulture=[Globalization.CultureInfo]$culture
        $separator=[Globalization.CultureInfo]::CurrentCulture.NumberFormat.NumberDecimalSeparator
        $grid.Rows.Clear()
        foreach ($case in @(@('1E-05','0.00001'),@('-1.25E-07','-0.000000125'),
            @('1.234567E+20','123456700000000000000'),@('0','0'),@('0.215434','0.215434'),
            @('1.401298E-45',('0.'+('0'*44)+'1401298')))) {
            $inputText=$case[0].Replace('.',$separator)
            $expected=$case[1].Replace('.',$separator)
            $row=New-Object Windows.Forms.DataGridViewRow
            $row.CreateCells($grid)
            $row.Cells[$grid.Columns['Command'].Index].Value='FMT_DECIMAL_'+$grid.Rows.Count
            $row.Cells[$grid.Columns['Fav'].Index].Value=$false
            $row.Cells[$grid.Columns['Value'].Index].Value=$inputText
            $row.Cells[$grid.Columns['Default_value'].Index].Value=$inputText
            $grid.Rows.Add($row) | Out-Null
            foreach ($column in @('Value','Default_value')) {
                $cell=$row.Cells[$column]; $cell.Value=$inputText
                if ($cell.FormattedValue -cne $expected -or $cell.Value -cne $inputText) { throw "Decimal display changed precision/cache: $culture $inputText" }
            }
        }
        $grid.CurrentCell=$grid.Rows[0].Cells['Value']
        if (!$grid.BeginEdit($false) -or $grid.EditingControl.Text -cne ('0'+$separator+'00001')) { throw 'Editor still shows scientific notation' }
        $grid.CancelEdit()
        $stage=$rawType.GetMethod('StageComparedParameter',$flags)
        if (!$stage.Invoke($raw,@('FMT_DECIMAL_0',[double]0.00001234567))) { throw 'Small decimal edit not staged exactly' }
        if ($grid.Rows[0].Cells['Value'].FormattedValue -cne ('0'+$separator+'00001234567')) { throw 'Staged small value displays exponent or rounding' }
        if ($rawType.GetField('_changes',$flags).GetValue($raw)['FMT_DECIMAL_0'] -ne [double]0.00001234567) { throw 'Display formatting altered staged value' }
    }
    'PASS: raw current/default display and actual editor use ordinary decimals; tiny/negative/large values; zh-TW/de-DE; staging precision preserved'
} finally {
    [Threading.Thread]::CurrentThread.CurrentCulture=$oldCulture
    $rawStartup.SetValue($null,$oldStartup)
    $form.Dispose(); $raw.Dispose()
}
'PASS: actual friendly page; fractional IMAX; sub-milliscale PID precision; staged writes/cache; version editor disposal; float/integer distinction; en-US/de-DE parsing'
