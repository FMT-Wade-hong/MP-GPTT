param([string]$Directory = 'bin/RollPitchMix121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArduplane',$true)
$page = [Activator]::CreateInstance($type)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
try {
    $control = $type.GetField('PTCH2SRV_RLL',$flags).GetValue($page)
    if ($control.Parent.Name -ne 'groupBox16' -or $control.Bottom -gt $control.Parent.ClientSize.Height) { throw 'Mixing control missing or clipped' }
    $setup = $control.GetType().GetMethods() | Where-Object { $_.Name -eq 'setup' -and $_.GetParameters()[4].ParameterType -eq [string] } | Select-Object -First 1
    $listType = $setup.GetParameters()[5].ParameterType
    $list = [Activator]::CreateInstance($listType)
    $setup.Invoke($control,@([single]0.7,[single]1.5,[single]1,[single]0.05,'PTCH2SRV_RLL',$list,$null)) | Out-Null
    if ($control.Enabled) { throw 'Missing parameter must be disabled' }
    $paramType = $listType.BaseType.GetGenericArguments()[0]
    $ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 } | Select-Object -First 1
    $wireType = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
    $list.Add($ctor.Invoke(@('PTCH2SRV_RLL',[double]1.1,$wireType,$null)))
    $setup.Invoke($control,@([single]0.7,[single]1.5,[single]1,[single]0.05,'PTCH2SRV_RLL',$list,$null)) | Out-Null
    if (!$control.Enabled -or [Math]::Abs([double]$control.Value - 1.1) -gt 0.001) { throw 'Parameter binding failed' }
    $changes = $type.GetField('changes',$flags).GetValue($page)
    if ($changes.Count -ne 0) { throw 'Binding must not queue writes' }
    $type.GetField('startup',$flags).SetValue($page,$false)
    $control.Value = 1.15
    if (!$changes.ContainsKey('PTCH2SRV_RLL') -or $changes.Count -ne 1) { throw 'Only selected parameter should be staged' }
    $page.CreateControl()
    $bitmap = New-Object Drawing.Bitmap $page.Width,$page.Height
    try { $page.DrawToBitmap($bitmap,$page.ClientRectangle); $bitmap.Save((Join-Path (Resolve-Path $Directory) 'roll-pitch-mix-preview.png')) }
    finally { $bitmap.Dispose() }
    'PASS: PTCH2SRV_RLL layout, missing-parameter disable, value binding and staged write isolation'
} finally { $page.Dispose() }
