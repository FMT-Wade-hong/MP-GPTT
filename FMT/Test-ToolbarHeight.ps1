param([string]$Directory = 'bin/ToolbarHeightFix/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$type = $assembly.GetType('MissionPlanner.MainV2')
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$instance = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($type)
$panel = New-Object Windows.Forms.Panel
$menu = New-Object Windows.Forms.MenuStrip
$button = New-Object Windows.Forms.ToolStripButton
$button.Image = New-Object Drawing.Bitmap 58,58
$button.AutoSize = $false
$button.Size = New-Object Drawing.Size 76,64
$statusField = $type.GetField('status1', $flags)
$status = [Activator]::CreateInstance($statusField.FieldType)
$status.Visible = $false
$type.GetField('panel1', $flags).SetValue($instance, $panel)
$type.GetField('MainMenu', $flags).SetValue($instance, $menu)
$type.GetField('MenuFlightData', $flags).SetValue($instance, $button)
$statusField.SetValue($instance, $status)
$panel.Controls.Add($menu)
$menu.AutoSize = $false
$resize = $type.GetMethod('ResizeFmtToolbarContainer', $flags)
foreach ($height in @(10,47,100,160)) {
    $panel.MinimumSize = [Drawing.Size]::Empty
    $menu.MinimumSize = [Drawing.Size]::Empty
    $panel.Height = $height
    $menu.Height = $height
    $resize.Invoke($instance, @()) | Out-Null
    if ($panel.Height -ne 64 -or $menu.Height -ne 64) { throw "Height failed: $height" }
    "PASS: initial height $height -> frame height 64 with image height 58"
}
$button.Image.Dispose()
$button.Dispose()
$status.Dispose()
$panel.Dispose()
