param([string]$Directory = 'bin/SwarmUnifiedUI/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
# Match application startup without reading or writing the user's theme settings.
$theme = $assembly.GetType('MissionPlanner.Utilities.ThemeManager',$true)
$theme.GetMethod('Init').Invoke($null,@()) | Out-Null
$colors = $theme.GetField('thmColor').GetValue($null)
$colors.InitColors()
$colors.SetTheme()
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigFmtSwarm',$true)
$page = [Activator]::CreateInstance($type)
$hostForm = New-Object Windows.Forms.Form
$fields = [Reflection.BindingFlags]'Instance,NonPublic'
try {
    $hostForm.ClientSize = New-Object Drawing.Size 1250,850
    $hostForm.StartPosition = 'Manual'
    $hostForm.Location = New-Object Drawing.Point -32000,-32000
    $hostForm.ShowInTaskbar = $false
    $page.Dock = 'Fill'
    $hostForm.Controls.Add($page)
    $hostForm.Show()
    $page.Activate()
    $tabs = $type.GetField('methods',$fields).GetValue($page)
    if ($tabs.TabPages.Count -ne 3) { throw 'Expected overview and two formation tabs' }
    $prior = $null
    foreach ($index in @(1,2,0,1)) {
        $tabs.SelectedIndex = $index
        [Windows.Forms.Application]::DoEvents()
        if ($prior -and !$prior.IsDisposed) { throw 'Previous controller was not disposed' }
        $controller = $type.GetField('formationWindow',$fields).GetValue($page)
        if ($index -eq 0) {
            if ($controller) { throw 'Overview retained a controller' }
        } else {
            if (!$controller -or $controller.TopLevel -or $controller.Parent -ne $tabs.SelectedTab) { throw 'Controller is not embedded' }
            if ($controller.GetType().GetProperty('IsRunning',$fields).GetValue($controller)) { throw 'Opening tab started control' }
            $bitmap = New-Object Drawing.Bitmap $hostForm.Width,$hostForm.Height
            try { $hostForm.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle 0,0,$hostForm.Width,$hostForm.Height)); $bitmap.Save((Join-Path (Resolve-Path $Directory) "swarm-tab-$index.png")) }
            finally { $bitmap.Dispose() }
        }
        $prior = $controller
    }
    # Verify running-state signal without starting a worker or sending commands.
    $flag = $controller.GetType().GetField('threadrun',$fields)
    $flag.SetValue($controller,$true)
    if (!$type.GetProperty('ControllerRunning',$fields).GetValue($page)) { throw 'Active controller guard missed running state' }
    $flag.SetValue($controller,$false)
    'PASS: three tabs; embedded controllers; previous controller disposed; no automatic start; running guard detects active controller'
} finally { $hostForm.Dispose() }
