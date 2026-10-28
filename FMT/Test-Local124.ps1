param([string]$Directory = 'bin/Local124/net461')
$ErrorActionPreference = 'Stop'
$failed = @()
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/Test-Local122.ps1" -Directory $Directory
if ($LASTEXITCODE) { $failed += 'Base regression suite' }
$tests = @('ArduPilot47Parameters','AllVersionedMetadata','ParameterCompatibility124','FriendlyParameterPrecision','CompleteParameterTable','TakeoffSetup','VersionedParameterMetadata')
foreach ($name in $tests) {
    Write-Output "TEST: $name"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "Test-$name.ps1") -Directory $Directory
    if ($LASTEXITCODE) { $failed += $name }
}
if ($failed.Count) { throw "Failed: $($failed -join ', ')" }
Write-Output 'PASS: 50 offline regression scripts; no aircraft connection or flight validation'
