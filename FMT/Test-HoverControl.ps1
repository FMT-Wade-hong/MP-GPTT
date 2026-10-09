param([string]$Directory = 'bin/Hover122/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArducopter',$true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$page = [Activator]::CreateInstance($type)
$bind = $type.GetMethod('BindHoverControl',$flags)
$listType = $bind.GetParameters()[0].ParameterType
$paramType = $listType.BaseType.GetGenericArguments()[0]
$ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 } | Select-Object -First 1
$wire = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$hover = $type.GetField('THR_HOVER',$flags).GetValue($page)
$imax = $type.GetField('THR_ACCEL_IMAX',$flags).GetValue($page)
$changes = $type.GetField('changes',$flags).GetValue($page)
$startup = $type.GetField('startup',$flags)
try {
    if ($hover.Parent -ne $imax.Parent -or $hover.Top -lt $imax.Bottom -or $hover.Bottom -gt $hover.Parent.ClientSize.Height) { throw 'HOVER misplaced/clipped' }
    foreach ($control in $page.Controls) {
        if ($control -ne $hover.Parent -and $control.Bounds.IntersectsWith($hover.Parent.Bounds)) { throw "Group overlaps $($control.Name)" }
    }
    foreach ($quad in @($false,$true)) {
        $name = if ($quad) { 'Q_M_THST_HOVER' } else { 'MOT_THST_HOVER' }
        $other = if ($quad) { 'MOT_THST_HOVER' } else { 'Q_M_THST_HOVER' }
        $list = [Activator]::CreateInstance($listType)
        $startup.SetValue($page,$true); $changes.Clear()
        $bind.Invoke($page,@($list,$quad)) | Out-Null
        if ($hover.Enabled) { throw 'Missing hover enabled' }
        $list.Add($ctor.Invoke(@($other,[double]0.25,$wire,$null)))
        $bind.Invoke($page,@($list,$quad)) | Out-Null
        if ($hover.Enabled) { throw 'Bound wrong vehicle family' }
        $list.Add($ctor.Invoke(@($name,[double]0.35,$wire,$null)))
        $bind.Invoke($page,@($list,$quad)) | Out-Null
        if (!$hover.Enabled -or $hover.ParamName -ne $name -or [Math]::Abs([double]$hover.Value - 0.35) -gt 0.0001 -or $changes.Count) { throw 'Binding changed values or selected wrong target' }
        $startup.SetValue($page,$false)
        $hover.Value = 0.36
        if ($changes.Count -ne 1 -or !$changes.ContainsKey($name) -or [Math]::Abs([double]$changes[$name] - 0.36) -gt 0.0001) { throw 'Staged write not isolated' }
        if ([Math]::Abs([double]$list[$name].Value - 0.35) -gt 0.0001) { throw 'Cache modified' }
        $get = $type.GetMethod('GetBoundRefreshParameters',[Reflection.BindingFlags]'Static,NonPublic')
        if (@($get.Invoke($null,@($page))) -notcontains $name) { throw 'HOVER missing from page refresh' }
    }
    $hostForm = New-Object Windows.Forms.Form
    $hostForm.StartPosition = 'Manual'; $hostForm.Location = New-Object Drawing.Point -30000,-30000
    $hostForm.Size = New-Object Drawing.Size 1400,1000
    $hostForm.Controls.Add($page); $hostForm.Show(); [Windows.Forms.Application]::DoEvents()
    $group = $hover.Parent
    $bmp = New-Object Drawing.Bitmap $group.Width,$group.Height
    try { $group.DrawToBitmap($bmp,$group.ClientRectangle); $bmp.Save((Join-Path $binaryDirectory 'hover-preview.png')) }
    finally { $bmp.Dispose(); $hostForm.Dispose() }
    'PASS: HOVER below IMAX, no overlap, absent/wrong family disabled, Copter/QuadPlane binding, staged write only, cache preserved, page refresh target, rendered preview'
} finally { $page.Dispose() }
