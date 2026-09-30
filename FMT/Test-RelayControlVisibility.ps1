param([string]$Directory = 'bin/RelayVisibilityFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.MainV2',$true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
# Do not start the application, relay service, or vehicle connection.
$main = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($type)
$panel = New-Object Windows.Forms.Panel
$hostItem = New-Object Windows.Forms.ToolStripControlHost $panel
$type.GetField('MenuFmtControlSource',$flags).SetValue($main,$hostItem.PSObject.BaseObject)
$method = $type.GetMethod('UpdateFmtControlSourceVisibility',$flags)
$settings = [MissionPlanner.Utilities.Settings]::Instance
$key = 'FMT_ShowControlSourceButtons'
$previous = $settings[$key]
try {
    foreach ($choice in @('', 'True', 'False')) {
        $settings[$key] = $choice
        $method.Invoke($main,@()) | Out-Null
        if ($hostItem.Available -ne ($choice -eq 'True')) { throw "Wrong visibility: $choice" }
    }
    'PASS: absent/empty preference hidden; enabled shown; disabled hidden; no connection or control commands'
} finally { $settings[$key] = $previous; $hostItem.Dispose() }
