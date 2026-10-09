param([string]$Directory = 'bin/ParamUiFix122/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArducopter',$true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$page = [Activator]::CreateInstance($type)
$editor = $type.GetField('THR_ACCEL_P',$flags).GetValue($page)
$setup = $editor.GetType().GetMethods() | Where-Object { $_.Name -eq 'setup' -and $_.GetParameters()[4].ParameterType -eq [string] } | Select-Object -First 1
$listType = $setup.GetParameters()[5].ParameterType
$paramType = $listType.BaseType.GetGenericArguments()[0]
$ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 } | Select-Object -First 1
$wire = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$changes = $type.GetField('changes',$flags).GetValue($page)
$startup = $type.GetField('startup',$flags)
$validate = [Windows.Forms.NumericUpDown].GetMethod('ValidateEditText',$flags)
try {
    # Missing metadata must allow both larger and smaller values, including negative values.
    foreach ($initial in @([double]0,[double]0.3,[double]-0.3)) {
        $list = [Activator]::CreateInstance($listType)
        $list.Add($ctor.Invoke(@('FMT_TEST_RANGE',$initial,$wire,$null)))
        $startup.SetValue($page,$true); $changes.Clear()
        $setup.Invoke($editor,@([single]0,[single]0,[single]1,[single]0.001,'FMT_TEST_RANGE',$list,$null)) | Out-Null
        if ($changes.Count) { throw 'Binding queued a write' }
        $startup.SetValue($page,$false)
        $editor.Text = '0.4'; $validate.Invoke($editor,@()) | Out-Null
        if ([Math]::Abs([double]$editor.Value - 0.4) -gt 0.0001 -or [Math]::Abs([double]$changes['FMT_TEST_RANGE'] - 0.4) -gt 0.0001) { throw 'Typed 0.4 clamped or not staged' }
        $editor.Text = '-0.4'; $validate.Invoke($editor,@()) | Out-Null
        if ([Math]::Abs([double]$editor.Value + 0.4) -gt 0.0001) { throw 'Unknown lower range clamped' }
        if ([Math]::Abs([double]$list['FMT_TEST_RANGE'].Value - $initial) -gt 0.0001) { throw 'Cache changed before write' }
    }
    foreach ($name in @('PSC_ACCZ_P','Q_P_ACCZ_P')) {
        $list = [Activator]::CreateInstance($listType)
        $list.Add($ctor.Invoke(@($name,[double]0.3,$wire,$null)))
        $startup.SetValue($page,$true); $changes.Clear()
        $setup.Invoke($editor,@([single]0,[single]0,[single]1,[single]0.001,$name,$list,$null)) | Out-Null
        $startup.SetValue($page,$false)
        $editor.Text = '0.4'; $validate.Invoke($editor,@()) | Out-Null
        if ([Math]::Abs([double]$changes[$name] - 0.4) -gt 0.0001 -or !$changes.ContainsKey($name)) { throw "ACC P typing failed: $name" }
    }
    # Explicit fallback bounds are already display units; retain both endpoints and scaling.
    $startup.SetValue($page,$true)
    $list = [Activator]::CreateInstance($listType)
    $list.Add($ctor.Invoke(@('FMT_TEST_SCALE',[double]800,$wire,$null)))
    $setup.Invoke($editor,@([single]1,[single]20,[single]100,[single]0.01,'FMT_TEST_SCALE',$list,$null)) | Out-Null
    if ($editor.Minimum -ne 1 -or $editor.Maximum -ne 20 -or $editor.Value -ne 8) { throw 'Display fallback limits/scaling changed' }
    $startup.SetValue($page,$false); $changes.Clear()
    $editor.Text='8.5'; $validate.Invoke($editor,@()) | Out-Null
    if ([Math]::Abs([double]$changes['FMT_TEST_SCALE'] - 850) -gt 0.001) { throw 'Scaled staged write incorrect' }
    $groups = @($page.Controls | Where-Object { $_ -is [Windows.Forms.GroupBox] })
    for ($i=0; $i -lt $groups.Count; $i++) {
        for ($j=$i+1; $j -lt $groups.Count; $j++) {
            if ($groups[$i].Bounds.IntersectsWith($groups[$j].Bounds)) { throw "Overlapping groups: $($groups[$i].Name), $($groups[$j].Name)" }
        }
    }
    foreach ($field in @('TUNE','TUNE_LOW','TUNE_HIGH','CH6_OPTION','CH7_OPTION','CH8_OPTION','CH9_OPTION','CH10_OPTION')) {
        $control=$type.GetField($field,$flags).GetValue($page)
        foreach ($group in $groups) { if ($group.Bounds.IntersectsWith($control.Bounds)) { throw "Selector $field overlaps $($group.Name)" } }
    }
    $startup.SetValue($page,$true)
    $list = [Activator]::CreateInstance($listType)
    $list.Add($ctor.Invoke(@('PSC_ACCZ_P',[double]0.3,$wire,$null)))
    $setup.Invoke($editor,@([single]0,[single]0,[single]1,[single]0.001,'PSC_ACCZ_P',$list,$null)) | Out-Null
    $startup.SetValue($page,$false); $editor.Text='0.4'; $validate.Invoke($editor,@()) | Out-Null
    $form = New-Object Windows.Forms.Form
    $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point -30000,-30000
    $form.Size=New-Object Drawing.Size 1450,1100
    $form.Controls.Add($page); $form.Show(); [Windows.Forms.Application]::DoEvents()
    $bmp = New-Object Drawing.Bitmap $page.Width,$page.Height
    try { $page.DrawToBitmap($bmp,$page.ClientRectangle); $bmp.Save((Join-Path $binaryDirectory 'parameter-ui-preview.png')) }
    finally { $bmp.Dispose(); $form.Dispose() }
} finally { $page.Dispose() }

$root = New-Object Windows.Forms.Panel
$guide = New-Object Windows.Forms.RichTextBox
$number = New-Object Windows.Forms.NumericUpDown
$disabled = New-Object Windows.Forms.Button
$grid = New-Object Windows.Forms.DataGridView
try {
    $guide.ReadOnly=$true; $guide.BackColor=[Drawing.Color]::FromArgb(18,36,45)
    $disabled.Enabled=$false
    $root.Controls.AddRange(@($guide,$number,$disabled,$grid))
    $lockType = $assembly.GetType('MissionPlanner.FmtPageParameterRefresh+EditorLock',$true)
    $lockCtor = $lockType.GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
    $lock = $lockCtor.Invoke(@($root.PSObject.BaseObject))
    if (!$root.Enabled -or !$guide.Enabled -or !$guide.ReadOnly -or $number.Enabled -or !$grid.ReadOnly) { throw 'Refresh lock changed guide or failed to lock input' }
    if ($guide.BackColor.ToArgb() -ne [Drawing.Color]::FromArgb(18,36,45).ToArgb()) { throw 'Guide colour changed' }
    $lock.Dispose()
    if (!$number.Enabled -or $disabled.Enabled -or $grid.ReadOnly -or !$guide.ReadOnly) { throw 'Original control state not restored' }
} finally { $root.Dispose() }
$source = Get-Content (Join-Path $PSScriptRoot '../GCSViews/ConfigurationView/ConfigArducopter.cs') -Raw
if (!$source.Contains('if (!MainV2.comPort.setParam(value, (float)temp[value]))') -or !$source.Contains('ValidateChildren();')) { throw 'Write result or pending text not checked' }
'PASS: typed 0.4 with missing metadata/ACC P aliases, negative and zero baselines, staged writes/cache isolation, all Copter group bounds, dark read-only guide during refresh, state restoration, write failure guards'
