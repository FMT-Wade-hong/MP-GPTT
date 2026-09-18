param([string]$Directory = 'bin/FramePresetListFix/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams', $true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
# Isolate list binding: no form activation, network calls, or flight controller access.
$view = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($type)
$comboField = $type.GetField('CMB_paramfiles', $flags)
$buttonField = $type.GetField('BUT_paramfileload', $flags)
$combo = [Activator]::CreateInstance($comboField.FieldType)
$button = [Activator]::CreateInstance($buttonField.FieldType)
$combo.BindingContext = New-Object Windows.Forms.BindingContext
$comboField.SetValue($view, $combo)
$buttonField.SetValue($view, $button)
$bind = $type.GetMethod('BindFramePresets', $flags)
try {
    $bind.Invoke($view, (, $null)) | Out-Null
    if ($combo.Text -ne 'H420.param' -or !$combo.Enabled -or !$button.Enabled) { throw 'Offline preset blank or disabled' }
    'PASS: offline preset is immediately selected and load button enabled'
    $listType = $bind.GetParameters()[0].ParameterType
    $itemType = $listType.GetGenericArguments()[0]
    $list = [Activator]::CreateInstance($listType)
    $item = [Activator]::CreateInstance($itemType)
    $item.name = 'Test.param'
    $item.path = 'test/Test.param'
    $list.Add($item)
    $bind.Invoke($view, (, $list)) | Out-Null
    if ($combo.Items.Count -ne 2 -or $combo.Text -ne 'H420.param') { throw 'Online merge lost bundled preset' }
    $combo.SelectedIndex = 1
    $bind.Invoke($view, (, $list)) | Out-Null
    if ($combo.Text -ne 'Test.param') { throw 'Refresh lost selection' }
    'PASS: online merge preserves bundled option and current selection'
    $stream = $assembly.GetManifestResourceStream('MissionPlanner.FMT.FrameParams.H420.param')
    if ($null -eq $stream -or $stream.Length -eq 0) { throw 'Bundled H420 resource missing' }
    $stream.Dispose()
    'PASS: H420 parameter resource included in build'
} finally { $combo.Dispose(); $button.Dispose() }
