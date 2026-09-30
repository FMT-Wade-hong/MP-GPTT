param([string]$Directory = 'bin/CopterVersionFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$controlType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArducopter',$true)
$control = [Activator]::CreateInstance($controlType)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$enter = $controlType.GetMethod('OnEnter_NumUpDown',$flags)
$utilities = [Reflection.Assembly]::LoadFrom((Join-Path (Resolve-Path $Directory) 'MissionPlanner.Utilities.dll'))
$detector = $utilities.GetType('MissionPlanner.Utilities.VersionDetection',$true)
$method = $detector.GetMethod('TryGetVersion')
$mav = [MissionPlanner.MainV2]::comPort.MAV
$original = $mav.VersionString
try {
    foreach ($text in @($null,'','unknown','vendor firmware','999999999999999999.7')) {
        $arguments = [object[]]@($text,$null)
        if ($method.Invoke($null,$arguments)) { throw "Invalid version accepted: $text" }
        $mav.VersionString = $text
        # No activation, connection or parameter write; exercise focus handler only.
        $enter.Invoke($control,@($control,[EventArgs]::Empty)) | Out-Null
    }
    foreach ($text in @('ArduCopter V4.6.3','ArduCopter V4.7.0-dev','ArduCopter V4.7.0-rc1')) {
        $arguments = [object[]]@($text,$null)
        if (!$method.Invoke($null,$arguments) -or !$arguments[1]) { throw "Version rejected: $text" }
        $mav.VersionString = $text
        $enter.Invoke($control,@($control,[EventArgs]::Empty)) | Out-Null
    }
    'PASS: missing/custom/malformed versions do not crash focus handler; release/dev/RC versions still parse'
} finally { $mav.VersionString = $original; $control.Dispose() }
