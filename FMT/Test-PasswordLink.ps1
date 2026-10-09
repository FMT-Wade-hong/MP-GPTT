param([string]$Directory = 'bin/PasswordLink121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$auth = $assembly.GetType('MissionPlanner.FMT.FmtAuthentication',$true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$property = $auth.GetProperty('ParameterProtectionEnabled',$flags)
$null = $property.GetValue($null,$null)
$settingsType = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetType('MissionPlanner.Utilities.Settings',$false) } | Where-Object { $_ } | Select-Object -First 1
$settings = $settingsType.GetProperty('Instance').GetValue($null,$null)
$indexer = $settingsType.GetProperties() | Where-Object { $_.Name -eq 'Item' -and $_.GetIndexParameters().Count -eq 1 -and $_.GetIndexParameters()[0].ParameterType -eq [string] } | Select-Object -First 1
foreach ($state in @($true,$false)) {
    $indexer.SetValue($settings,$state.ToString(),@('password_protect'))
    if ($property.GetValue($null,$null) -ne $state) { throw 'Shared protection state mismatch' }
    $dialogType = $assembly.GetType('MissionPlanner.FMT.FmtChangeParameterPasswordForm',$true)
    $dialog = [Activator]::CreateInstance($dialogType,$true)
    try {
        $box = $dialogType.GetField('protection',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($dialog)
        if ($box.Checked -ne $state) { throw 'Dialog not linked to shared state' }
    } finally { $dialog.Dispose() }
}
$main = Get-Content (Join-Path $PSScriptRoot '../MainV2.cs') -Raw
foreach ($name in @('MenuSetup_Click','MenuTuning_Click','MenuFmtParameterSettings_Click')) {
    $match = [regex]::Match($main,'(?:public|private) void '+$name+'\(.*?\n        \}',[Text.RegularExpressions.RegexOptions]::Singleline).Value
    if (!$match.Contains('FmtAuthentication.ParameterProtectionEnabled') -or $match.Contains('Password.VerifyPassword')) { throw "Inconsistent gate: $name" }
}
'PASS: shared protection on/off state, dialog linkage and three unified entry gates; no settings saved or flight controller accessed'
