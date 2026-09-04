param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$artifactDirectory = Join-Path $ProjectRoot 'tmp\throttle-mode-tests'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $installation 'MSBuild\Current\Bin\Roslyn\csc.exe'
$testExe = Join-Path $artifactDirectory 'FmtThrottleModeHarness.exe'
$sourceFiles = @('FMT\FmtThrottleMode.cs', 'MissionPlannerTests\FmtThrottleModeHarness.cs') |
    ForEach-Object { Join-Path $ProjectRoot $_ }

& $compiler /nologo /target:exe /langversion:7.3 "/out:$testExe" $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Throttle-mode harness compile failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Throttle-mode harness failed.' }

$radioInput = Get-Content -LiteralPath (Join-Path $ProjectRoot 'GCSViews\ConfigurationView\ConfigRadioInput.cs') -Raw
foreach ($required in @(
    'Text = "中立回中油門"',
    'Text = "手動油門（低位為零）"',
    '"PILOT_THR_BHV"',
    '"RCMAP_THROTTLE"',
    '"THR_DZ"',
    'MainV2.comPort.MAV.cs.armed',
    'FmtThrottleMode.SetCenteredThrottle',
    'FmtThrottleMode.CalibrationLooksConsistent')) {
    if (!$radioInput.Contains($required)) { throw "Missing radio throttle integration: $required" }
}
if ($radioInput -notmatch '(?s)Name = "fmtThrottleModeGroup".{0,500}Anchor = AnchorStyles.Top \| AnchorStyles.Left,') {
    throw 'Throttle-mode outer frame must keep a fixed content width.'
}
Write-Host 'PASS radio calibration page throttle-mode UI, armed guard, and parameter checks'
