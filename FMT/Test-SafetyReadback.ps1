param([string]$Directory = 'bin/SafetySettingsReadbackFix/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$outer = $assembly.GetType('MissionPlanner.FMT.FmtSafetySettingsPanel')
$private = [Reflection.BindingFlags]'NonPublic'
$fields = [Reflection.BindingFlags]'Instance,NonPublic'
$cardType = $outer.GetNestedType('SafetyParameterCard',$private)
$optionType = $outer.GetNestedType('SafetyOption',$private)
$card = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($cardType)
$current = New-Object Windows.Forms.Label
$combo = New-Object Windows.Forms.ComboBox
$combo.DisplayMember = 'Text'
$combo.DropDownStyle = 'DropDownList'
foreach ($value in @(0,1,2)) {
    $option = [Activator]::CreateInstance($optionType,$true)
    $option.Value = $value
    $option.Text = "$value test"
    $combo.Items.Add($option) | Out-Null
}
$cardType.GetField('current',$fields).SetValue($card,$current.PSObject.BaseObject)
$cardType.GetField('choices',$fields).SetValue($card,$combo.PSObject.BaseObject)
$set = $cardType.GetMethod('SetCurrentValue',$fields)
$hostPanel = New-Object Windows.Forms.Panel
try {
    $set.Invoke($card,@([double]2)) | Out-Null
    $hostPanel.Controls.Add($combo)
    $hostPanel.BindingContext = New-Object Windows.Forms.BindingContext
    if ($combo.SelectedItem.Value -ne 2) { throw 'Readback reset by parent binding context' }
    $set.Invoke($card,@([double]1)) | Out-Null
    if ($combo.SelectedItem.Value -ne 1) { throw 'Changed readback not selected' }
    $set.Invoke($card,@([double]99)) | Out-Null
    if ($combo.SelectedItem.Value -ne 99) { throw 'Unknown current option lost' }
    'PASS: values 2/1/99 remain selected through parent binding and readback updates'
} finally { $hostPanel.Dispose(); $current.Dispose() }
$catalog = $assembly.GetType('MissionPlanner.FMT.FmtSafetyParameterCatalog')
$defs = $catalog.GetProperty('Definitions',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
$names = @($defs | ForEach-Object { $_.GetType().GetProperty('Names',$fields).GetValue($_) })
if ($names -notcontains 'RTL_ALT' -or $names -notcontains 'RTL_SPEED') { throw 'RTL cards missing' }
foreach ($pair in @(@('RTL_ALT_M', 'RTL_ALT'), @('RTL_SPEED_MS', 'RTL_SPEED'))) {
    $definition = $defs | Where-Object { $_.GetType().GetProperty('Names',$fields).GetValue($_) -contains $pair[0] } | Select-Object -First 1
    if ($null -eq $definition) { throw "Missing modern RTL alias $($pair[0])" }
    $resolve = $definition.GetType().GetMethod('Resolve',$fields)
    foreach ($available in @(@($pair[0]), @($pair[1]), @($pair[0], $pair[1]))) {
        $actual = $resolve.Invoke($definition, @(,[string[]]$available))
        if ($actual -ne $available[0]) { throw "Incorrect RTL alias resolution: $actual" }
    }
}
'PASS: RTL altitude and speed cards available only when matching parameters exist'
$maskCard = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($cardType)
$menu = New-Object Windows.Forms.ContextMenuStrip
$maskButton = New-Object Windows.Forms.Button
$maskLabel = New-Object Windows.Forms.Label
foreach ($bit in @(0,1,2)) {
    $item = New-Object Windows.Forms.ToolStripMenuItem
    $item.Tag = [int]$bit
    $item.CheckOnClick = $true
    $menu.Items.Add($item) | Out-Null
}
$cardType.GetField('current',$fields).SetValue($maskCard,$maskLabel.PSObject.BaseObject)
$cardType.GetField('bitmaskMenu',$fields).SetValue($maskCard,$menu.PSObject.BaseObject)
$cardType.GetField('bitmaskButton',$fields).SetValue($maskCard,$maskButton.PSObject.BaseObject)
try {
    $set.Invoke($maskCard,@([double]129)) | Out-Null
    if (!$menu.Items[0].Checked -or $menu.Items[1].Checked) { throw 'Mask check state incorrect' }
    $menu.Items[0].Checked = $false
    $getMask = $cardType.GetMethod('GetBitmaskValue',$fields)
    if ($getMask.Invoke($maskCard,@()) -ne 128) { throw 'Unlisted mask bits lost' }
    'PASS: FS_OPTIONS checks match readback and preserve unlisted bits when edited'
} finally { $menu.Dispose(); $maskButton.Dispose(); $maskLabel.Dispose() }
