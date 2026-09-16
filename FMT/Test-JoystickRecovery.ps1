param([string]$Directory = 'bin/JoystickRecoveryTest/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$type = $assembly.GetType('MissionPlanner.Joystick.JoystickSetup')
$main = $assembly.GetType('MissionPlanner.MainV2')
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$static = [Reflection.BindingFlags]'Static,Public,NonPublic'
# No window, hardware enumeration, serial connection or control output.
$instance = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($type)
$buttonField = $type.GetField('BUT_enable', $flags)
$button = [Activator]::CreateInstance($buttonField.FieldType)
$label = New-Object Windows.Forms.Label
$timer = New-Object Windows.Forms.Timer
$buttonField.SetValue($instance, $button)
$type.GetField('inputStatus', $flags).SetValue($instance, $label)
$type.GetField('timer1', $flags).SetValue($instance, $timer)
$gate = $main.GetField('FmtGroundControlInputEnabled', $static)
$select = $type.GetMethod('CMB_joysticks_SelectedIndexChanged', $flags)
try {
    foreach ($guard in @('startup', 'refreshingDevices')) {
        $type.GetField($guard, $flags).SetValue($instance, $true)
        $gate.SetValue($null, $true)
        $select.Invoke($instance, @($null, [EventArgs]::Empty)) | Out-Null
        if (!$gate.GetValue($null)) { throw "$guard changed the output gate" }
        $type.GetField($guard, $flags).SetValue($instance, $false)
        "PASS: $guard ignores selection events"
    }
    $select.Invoke($instance, @($null, [EventArgs]::Empty)) | Out-Null
    if ($gate.GetValue($null) -or $button.Text -ne 'Enable') { throw 'Selection did not require manual enable' }
    'PASS: device selection disables the output gate'
    $type.GetMethod('ReportInputFailure', $flags).Invoke($instance, @()) | Out-Null
    if (!$type.GetField('inputRecoveryRequired', $flags).GetValue($instance)) { throw 'Recovery not latched' }
    if (!$label.Text) { throw 'Missing visible error' }
    $type.GetMethod('timer1_Tick', $flags).Invoke($instance, @($null, [EventArgs]::Empty)) | Out-Null
    if ($gate.GetValue($null)) { throw 'Timer resumed control' }
    'PASS: read failure is visible and timer cannot auto-resume'
} finally {
    $gate.SetValue($null, $false)
    $timer.Dispose()
    $button.Dispose()
    $label.Dispose()
}
