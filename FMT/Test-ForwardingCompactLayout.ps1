param([string]$Directory = 'bin/UdpRelayLayoutFix/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$type = $assembly.GetType('MissionPlanner.FMT.FmtRelayControlPanel')
$method = $type.GetMethod('LocalizeForwardingGrid', [Reflection.BindingFlags]'Static,NonPublic')
$panel = New-Object Windows.Forms.Panel
$panel.Size = New-Object Drawing.Size(1900, 600)
$grid = New-Object Windows.Forms.DataGridView
$grid.Location = New-Object Drawing.Point(12, 69)
$panel.Controls.Add($grid)
foreach ($name in @('RuntimeStatus','RelayStation','Type','Direction','Port','Extra','Write','Go')) {
    $grid.Columns.Add($name, $name) | Out-Null
}
$grid.Rows.Add(@('*','2','UDP','Inbound','14550','192.168.100.100','False','Start')) | Out-Null
try {
    $arguments = New-Object 'System.Object[]' 1
    $arguments[0] = $panel.PSObject.BaseObject
    $method.Invoke($null, $arguments) | Out-Null
    $grid.AutoResizeColumns()
    if ($grid.AutoSizeColumnsMode -ne 'AllCells' -or $grid.Width -ge 1500) { throw 'Grid still stretches' }
    "PASS: content-sized grid width $($grid.Width) in 1900px container"
    $panel.Width = 600
    if ($grid.Right -gt 588) { throw 'Grid exceeds narrow container' }
    'PASS: narrow container keeps horizontal scrolling within bounds'
    $panel.Width = 1900
    if ($grid.Width -ge 1500) { throw 'Grid stretched after resize' }
    'PASS: expanding container does not stretch columns'
    for ($i = 0; $i -lt 6; $i++) { $grid.Rows.Add(@('*','2','UDP','Inbound','14550','192.168.100.100','False','Start')) | Out-Null }
    $required = $grid.ColumnHeadersHeight + 4
    foreach ($row in $grid.Rows) { $required += $row.Height }
    if ($grid.Height -lt $required) { throw 'Rows clipped despite available space' }
    if ($grid.ColumnHeadersDefaultCellStyle.WrapMode -ne 'False') { throw 'Headers still wrap' }
    'PASS: all rows including new-row entry fit; headers do not wrap'
    $panel.Height = 220
    if ($grid.Bottom -gt 208) { throw 'Grid exceeds short container' }
    'PASS: short container retains bounded scrollable grid'
} finally { $panel.Dispose() }
