param([string]$Directory = 'bin/CopterRefreshFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArducopter',$true)
$get = $type.GetMethod('GetBoundRefreshParameters',[Reflection.BindingFlags]'Static,NonPublic')
$root = New-Object Windows.Forms.Panel
$nested = New-Object Windows.Forms.Panel
$root.Controls.Add($nested)
try {
    foreach ($binding in @(@('RATE_RLL_P','ATC_RAT_RLL_P'),@('OLD_ALIAS','ATC_RAT_RLL_P'),@('Unused',$null))) {
        $editor = [Activator]::CreateInstance($assembly.GetType('MissionPlanner.Controls.MavlinkNumericUpDown',$true))
        $editor.Name = $binding[0]
        $editor.ParamName = $binding[1]
        $nested.Controls.Add($editor)
    }
    $combo = [Activator]::CreateInstance($assembly.GetType('MissionPlanner.Controls.MavlinkComboBox',$true))
    $combo.Name = 'CH7_OPTION'
    $combo.ParamName = 'RC7_OPTION'
    $root.Controls.Add($combo)
    $plain = New-Object Windows.Forms.ComboBox
    $plain.Name = 'NotAParameter'
    $root.Controls.Add($plain)
    $result = @($get.Invoke($null,@($root.PSObject.BaseObject)))
    if ($result.Count -ne 2 -or $result -notcontains 'ATC_RAT_RLL_P' -or $result -notcontains 'RC7_OPTION') {
        throw "Wrong refresh targets: $result"
    }
    $handler = $type.GetMethod('BUT_refreshpart_Click',[Reflection.BindingFlags]'Instance,NonPublic')
    if (!($handler.GetCustomAttributes($false) | Where-Object { $_ -is [Runtime.CompilerServices.AsyncStateMachineAttribute] })) {
        throw 'Refresh handler is not asynchronous'
    }
    $page = [Activator]::CreateInstance($type)
    $cancellation = New-Object Threading.CancellationTokenSource
    try {
        $field = $type.GetField('refreshCancellation',[Reflection.BindingFlags]'Instance,NonPublic')
        $field.SetValue($page,$cancellation.PSObject.BaseObject)
        $page.Deactivate()
        if (!$cancellation.IsCancellationRequested) { throw 'Leaving page did not cancel refresh' }
    } finally { $page.Dispose(); $cancellation.Dispose() }
    'PASS: actual bound parameter names; duplicate removal; nested fields; combo bindings; unbound controls skipped; async handler; cancellation on leaving page'
} finally { $root.Dispose() }
