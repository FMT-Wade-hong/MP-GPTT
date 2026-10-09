param([string]$Directory = 'bin/IceSetup121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigIceEngine',$true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$defs = $type.GetField('Definitions',$flags).GetValue($null)
if ($defs.Count -ne 19) { throw 'Missing ICE definitions' }
if (($defs.Name | Sort-Object -Unique).Count -ne $defs.Count) { throw 'Duplicate definitions' }
foreach ($def in $defs) {
    foreach ($field in @('Name','Title','Range','Meaning','Advice')) {
        if ([string]::IsNullOrWhiteSpace($def.$field)) { throw "Missing $field for $($def.Name)" }
    }
    $accepts = $def.GetType().GetMethod('Accepts',[Reflection.BindingFlags]'Instance,NonPublic')
    foreach ($invalid in @([double]::NaN,[double]::PositiveInfinity,[double]::NegativeInfinity)) {
        if ($accepts.Invoke($def,@($invalid))) { throw "Non-finite accepted: $($def.Name)" }
    }
    if ($null -ne $def.Min -and $accepts.Invoke($def,@([double]($def.Min - 1)))) { throw 'Lower bound failed' }
    if ($null -ne $def.Max -and $accepts.Invoke($def,@([double]($def.Max + 1)))) { throw 'Upper bound failed' }
}
$page = [Activator]::CreateInstance($type)
try {
    $page.Activate()
    $instance = [Reflection.BindingFlags]'Instance,NonPublic'
    $grid = $type.GetField('grid',$instance).GetValue($page)
    if ($grid.Rows.Count -ne 19) { throw 'Offline guide must show all parameters' }
    if ($type.GetField('write',$instance).GetValue($page).Enabled) { throw 'Offline write enabled' }
    $page.CreateControl()
    $starter = $type.GetField('starter',$instance).GetValue($page)
    $proposed = $type.GetField('proposed',$instance).GetValue($page)
    if ($starter.Checked) { throw 'Starter must default to unchecked' }
    foreach ($name in @('ICE_STARTER_TIME','ICE_START_DELAY','ICE_STRT_MX_RTRY','ICE_PWM_STRT_ON','ICE_PWM_STRT_OFF')) {
        $row = $grid.Rows | Where-Object { $_.Tag.Name -eq $name }
        $grid.CurrentCell = $row.Cells[0]
        if ($proposed.Enabled) { throw "Starter field not locked: $name" }
        $starter.Checked = $true
        if (!$proposed.Enabled) { throw "Starter field did not unlock: $name" }
        $starter.Checked = $false
        if ($proposed.Enabled) { throw "Starter field did not relock: $name" }
    }
    $ignitionRow = $grid.Rows | Where-Object { $_.Tag.Name -eq 'ICE_PWM_IGN_ON' }
    $grid.CurrentCell = $ignitionRow.Cells[0]
    if (!$proposed.Enabled) { throw 'Ignition must not be locked by starter option' }
    $grid.CurrentCell = $grid.Rows[0].Cells[0]
    if ($grid.AutoSizeColumnsMode -ne [Windows.Forms.DataGridViewAutoSizeColumnsMode]::AllCells) {
        throw 'Columns must fit all text, not stretch to fill'
    }
    $contentWidth = ($grid.Columns | Measure-Object Width -Sum).Sum + [Windows.Forms.SystemInformation]::VerticalScrollBarWidth + 8
    if ($grid.Width -gt $contentWidth) { throw 'Table wider than text requires' }
    $bitmap = New-Object Drawing.Bitmap $page.Width,$page.Height
    try {
        $page.DrawToBitmap($bitmap,$page.ClientRectangle)
        $bitmap.Save((Join-Path (Resolve-Path $Directory) 'ice-setup-preview.png'))
    } finally { $bitmap.Dispose() }
    $page.Deactivate()
} finally { $page.Dispose() }
$source = Get-Content (Join-Path $PSScriptRoot '../GCSViews/ConfigurationView/ConfigIceEngine.cs') -Raw
foreach ($guard in @('!target.cs.armed','!port.ReadOnly','!port.logreadmode','!safe.Checked','originalPort.GetParam','CanLocalStationTransmitControl')) {
    if (!$source.Contains($guard)) { throw "Missing guard: $guard" }
}
if ($source.Contains('doCommand(')) { throw 'Settings page must not run the engine' }
'PASS: 19 complete definitions, numeric validation, disconnected write protection, offline UI rendering and write/readback safeguards'
