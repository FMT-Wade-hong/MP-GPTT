param([string]$Directory = 'bin/ParamCompareFix/net461')
$ErrorActionPreference = 'Stop'
# Reuse the offline WinForms assembly resolver; this does not connect to a vehicle.
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$rawType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams',$true)
$instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
$staticFlags = [Reflection.BindingFlags]'Static,NonPublic'
$raw = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($rawType)
$grid = [Activator]::CreateInstance($rawType.GetField('Params',$instanceFlags).FieldType)
$grid.AllowUserToAddRows = $false
$nameColumn = New-Object Windows.Forms.DataGridViewTextBoxColumn
$valueColumn = New-Object Windows.Forms.DataGridViewTextBoxColumn
$grid.Columns.Add($nameColumn) | Out-Null
$grid.Columns.Add($valueColumn) | Out-Null
$grid.Rows.Add('FMT_COMPARE_TEST','1') | Out-Null
$pending = New-Object Collections.Hashtable
foreach ($entry in @{Params=$grid; Command=$nameColumn; Value=$valueColumn; _changes=$pending}.GetEnumerator()) {
    $rawType.GetField($entry.Key,$instanceFlags).SetValue($raw,$entry.Value.PSObject.BaseObject)
}
$startup = $rawType.GetField('startup',$staticFlags)
$previous = $startup.GetValue($null)
try {
    $startup.SetValue($null,$true)
    $stage = $rawType.GetMethod('StageComparedParameter',$instanceFlags)
    $accepted = $stage.Invoke($raw,@('FMT_COMPARE_TEST',[double]2.5))
    if (!$accepted -or $pending['FMT_COMPARE_TEST'] -ne 2.5 -or $grid.Rows[0].Cells[1].Value -ne '2.5') {
        throw 'Compare did not update both table and pending changes with startup suppressed'
    }
    if ($stage.Invoke($raw,@('MISSING',[double]1))) { throw 'Missing row reported successful' }
    if ($stage.Invoke($raw,@('FMT_COMPARE_TEST',[double]::NaN))) { throw 'NaN accepted' }
    if ($pending.Count -ne 1) { throw 'Invalid input changed pending set' }
    'PASS: explicit staging updates grid and pending changes during startup; missing/invalid values rejected; no vehicle writes'
} finally { $startup.SetValue($null,$previous); $grid.Dispose() }
