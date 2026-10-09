param([string]$Directory = 'bin/TakeoffSetup121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-IceSetup.ps1" -Directory $Directory
$takeoffType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigTakeoff',$true)
$defs = $takeoffType.GetField('TakeoffDefinitions',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
if ($defs.Count -ne 19) { throw 'Expected 19 takeoff parameters' }
foreach ($def in $defs) {
    if (!$def.Meaning -or !$def.Advice -or !$def.Range) { throw 'Incomplete definition' }
    $accepts = $def.GetType().GetMethod('Accepts',[Reflection.BindingFlags]'Instance,NonPublic')
    foreach ($value in @([double]::NaN,[double]::PositiveInfinity,[double]($def.Min - 1),[double]($def.Max + 1))) {
        if ($accepts.Invoke($def,@($value))) { throw "Invalid value accepted: $($def.Name)" }
    }
}
$page = [Activator]::CreateInstance($takeoffType)
try {
    $page.Activate()
    $instance = [Reflection.BindingFlags]'Instance,NonPublic'
    $grid = $type.GetField('grid',$instance).GetValue($page)
    if ($grid.Rows.Count -ne 19 -or $grid.Rows[0].Tag.Name -ne 'TKOFF_THR_SLEW') { throw 'Wrong table' }
    if ($type.GetField('starter',$instance).GetValue($page).Visible) { throw 'ICE-only option visible' }
    if ($type.GetField('write',$instance).GetValue($page).Enabled) { throw 'Disconnected write enabled' }
    $page.CreateControl()
    $profile = $type.GetField('profile',$instance).GetValue($page)
    $showAll = $type.GetField('showAll',$instance).GetValue($page)
    $proposed = $type.GetField('proposed',$instance).GetValue($page)
    $writeButton = $type.GetField('write',$instance).GetValue($page)
    foreach ($size in @(@(850,650),@(1100,730))) {
        $page.Size = New-Object Drawing.Size $size[0],$size[1]
        $page.PerformLayout()
        $writePosition = $page.PointToClient($writeButton.PointToScreen([Drawing.Point]::Empty))
        $gridPosition = $page.PointToClient($grid.PointToScreen([Drawing.Point]::Empty))
        if ($writePosition.Y -lt 0 -or $writePosition.Y -ge $gridPosition.Y -or $writePosition.Y -ge $page.Height) {
            throw 'Write editor must remain above the parameter table and inside the page'
        }
    }
    if ($profile.Items.Count -ne 9 -or $proposed.Enabled) { throw 'Choose a method before editing' }
    foreach ($pair in @(@(6,3),@(7,5),@(8,4))) {
        $profile.SelectedIndex = $pair[0]
        if ($grid.Rows.Count -ne $pair[1]) { throw 'Incorrect vehicle definitions' }
        $showAll.Checked = $true
        if ($grid.Rows.Count -ne $pair[1] -or ($grid.Rows | Where-Object {$_.Tag.Name -like 'TKOFF_*'})) { throw 'Fixed-wing settings leaked into vertical takeoff profile' }
        $showAll.Checked = $false
    }
    foreach ($method in @(1,2,3,4,5)) {
        $profile.SelectedIndex = $method
        $expected = if ($method -le 3) { 15 } else { 16 }
        if ($grid.Rows.Count -ne $expected) { throw "Wrong profile rows: $method" }
        $showAll.Checked = $true
        if ($grid.Rows.Count -ne 19) { throw 'Advanced review must retain all rows' }
        $showAll.Checked = $false
    }
    $mav = $type.GetField('target',$instance).GetValue($page)
    $paramType = $mav.param.GetType().BaseType.GetGenericArguments()[0]
    $paramCtor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 }
    $wireType = [Enum]::ToObject($paramCtor.GetParameters()[2].ParameterType,9)
    $mav.param.Add($paramCtor.Invoke(@('TKOFF_ROTATE_SPD',[double]12,$wireType,$null)))
    $profile.SelectedIndex = 1
    if ($grid.Rows.Count -ne 16 -or !($grid.Rows | Where-Object { $_.Tag.Name -eq 'TKOFF_ROTATE_SPD' })) {
        throw 'Nonzero legacy runway setting must remain visible for hand launch review'
    }
    $profile.SelectedIndex = 4
    if (!$grid.Visible -or $grid.Height -lt 100) { throw 'Takeoff table hidden or collapsed' }
    $bitmap = New-Object Drawing.Bitmap $page.Width,$page.Height
    try {
        $page.DrawToBitmap($bitmap,$page.ClientRectangle)
        $bitmap.Save((Join-Path (Resolve-Path $Directory) 'takeoff-setup-preview.png'))
    } finally { $bitmap.Dispose() }
    $page.Deactivate()
} finally { $page.Dispose() }
'PASS: TAKEOFF definitions, range checks, independent table, no starter option, offline write protection and UI preview; ICE regression also passed'
