param([string]$Directory = 'bin/LandingSetup121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-TakeoffSetup.ps1" -Directory $Directory
$landingType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigLanding',$true)
$defs = $landingType.GetField('LandingDefinitions',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
if ($defs.Count -ne 21 -or ($defs.Name | Sort-Object -Unique).Count -ne 21) { throw 'Expected 21 unique landing parameters' }
foreach ($def in $defs) {
    if (!$def.Meaning -or !$def.Advice -or !$def.Range) { throw 'Incomplete landing definition' }
    $accepts = $def.GetType().GetMethod('Accepts',[Reflection.BindingFlags]'Instance,NonPublic')
    foreach ($value in @([double]::NaN,[double]::PositiveInfinity,[double]($def.Min - 1),[double]($def.Max + 1))) {
        if ($accepts.Invoke($def,@($value))) { throw "Invalid value accepted: $($def.Name)" }
    }
}
$page = [Activator]::CreateInstance($landingType)
try {
    $page.Activate()
    $instance = [Reflection.BindingFlags]'Instance,NonPublic'
    $grid = $type.GetField('grid',$instance).GetValue($page)
    if ($grid.Rows.Count -ne 21 -or $grid.Rows[0].Tag.Name -ne 'TECS_LAND_ARSPD') { throw 'Wrong landing table' }
    if ($type.GetField('starter',$instance).GetValue($page).Visible) { throw 'ICE-only option visible' }
    if ($type.GetField('write',$instance).GetValue($page).Enabled) { throw 'Disconnected write enabled' }
    $page.CreateControl()
    if (!$grid.Visible -or $grid.Height -lt 100) { throw 'Landing table collapsed' }
    $bitmap = New-Object Drawing.Bitmap $page.Width,$page.Height
    try {
        $page.DrawToBitmap($bitmap,$page.ClientRectangle)
        $bitmap.Save((Join-Path (Resolve-Path $Directory) 'landing-setup-preview.png'))
    } finally { $bitmap.Dispose() }
    $page.Deactivate()
} finally { $page.Dispose() }
'PASS: LAND definitions, range validation, independent table, disconnected safeguards, preview; TAKEOFF and ICE regressions passed'
