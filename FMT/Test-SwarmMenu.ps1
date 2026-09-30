param([string]$Directory = 'bin/SwarmMenu/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$swarmType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigFmtSwarm',$true)
$page = [Activator]::CreateInstance($swarmType)
$hostForm = New-Object Windows.Forms.Form
try {
    $hostForm.ClientSize = New-Object Drawing.Size 1020,570
    $hostForm.ShowInTaskbar = $false
    $hostForm.StartPosition = 'Manual'
    $hostForm.Location = New-Object Drawing.Point -32000,-32000
    $page.Dock = 'Fill'
    $hostForm.Controls.Add($page)
    $hostForm.Show()
    $page.Activate()
    [Windows.Forms.Application]::DoEvents()
    $fields = [Reflection.BindingFlags]'Instance,NonPublic'
    $button = $swarmType.GetField('formation',$fields).GetValue($page)
    if ($button.Enabled) { throw 'Swarm control enabled without a connected autopilot' }
    $label = $swarmType.GetField('status',$fields).GetValue($page)
    if ([string]::IsNullOrWhiteSpace($label.Text)) { throw 'Missing status' }
    $bitmap = New-Object Drawing.Bitmap $hostForm.Width,$hostForm.Height
    try { $hostForm.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle 0,0,$hostForm.Width,$hostForm.Height)); $bitmap.Save((Join-Path (Resolve-Path $Directory) 'swarm-menu-preview.png')) }
    finally { $bitmap.Dispose() }
    $source = Get-Content (Join-Path $PSScriptRoot '../GCSViews/SoftwareConfig.cs') -Raw -Encoding UTF8
    if ($source.IndexOf('typeof(ConfigFmtSwarm)') -gt $source.IndexOf('if (gotAllParams)')) { throw 'Menu gated on parameters' }
    'PASS: independent menu before connection gates; disconnected page loads; control entry disabled; offline screenshot rendered'
} finally { $hostForm.Dispose() }
