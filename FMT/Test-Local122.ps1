param([string]$Directory = 'bin/Release122Final/net461')
$ErrorActionPreference = 'Stop'
$tests = @('ReleaseVersion','ElrsSerialSettings','IceToolbar','InitialMapZoom','SettingsAudit','PasswordLink','RollPitchMix','LandingSetup','RelaySourceSync','RelayControlVisibility','MessagesRollingBuffer','CopterRefreshBindings','CopterVersionWarning','PowerModuleMenu','TraditionalMapLabels','TelemetryDisplayIsolation','JoystickRecovery','ParamCompareStaging','SafetyReadback','PerformanceStability','TaskbarIcon','ShutdownCleanup','PageParameterRefresh')
$failed = @()
$tests += @('ParameterUiFix','WpNavMetric','HoverControl','MapIcons','AirspeedZeroAsync','ConnectionTimeSync')
foreach ($name in $tests) {
    Write-Output "TEST: $name"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "Test-$name.ps1") -Directory $Directory
    if ($LASTEXITCODE -ne 0) { $failed += $name }
}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-TabMenuLabels.ps1')
if ($LASTEXITCODE -ne 0) { $failed += 'TabMenuLabels' }
if ($failed.Count) { throw "Failed: $($failed -join ', ')" }
Write-Output "PASS: $($tests.Count + 1) local regression scripts; no flight validation"
