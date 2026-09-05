param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$artifactDirectory = Join-Path $ProjectRoot 'tmp\safety-settings-tests'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $installation 'MSBuild\Current\Bin\Roslyn\csc.exe'
$testExe = Join-Path $artifactDirectory 'FmtSafetySettingsHarness.exe'
$sourceFiles = @('FMT\FmtSafetyParameterCatalog.cs', 'MissionPlannerTests\FmtSafetySettingsHarness.cs') |
    ForEach-Object { Join-Path $ProjectRoot $_ }

& $compiler /nologo /target:exe /langversion:7.3 "/out:$testExe" /reference:System.Core.dll $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Safety settings harness compile failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Safety settings harness failed.' }

$flightData = Get-Content -LiteralPath (Join-Path $ProjectRoot 'GCSViews\FlightData.cs') -Raw
$panel = Get-Content -LiteralPath (Join-Path $ProjectRoot 'FMT\FmtSafetySettingsPanel.cs') -Raw
foreach ($required in @(
    'Text = "安全設定"',
    'chkFmtRelay, chkFmt3DMap, chkFmtMqtt, chkFmtSafety',
    'splitContainer1.Panel1.Controls.Add(fmtSafetyPanel)',
    'fmtSafetyPanel.RefreshParameters()',
    'fmtSafetyPanel.Visible = showSafety')) {
    if (!$flightData.Contains($required)) { throw "Missing embedded safety integration: $required" }
}
foreach ($required in @(
    'Text = "套用"',
    'Size = new Size(300, 102)',
    'Size = new Size(194, 25)',
    'parameterName == "FS_OPTIONS"',
    'GetParameterBitMaskInt',
    'CheckOnClick = true',
    'MaximumSize = new Size(320, 0)',
    'Size = new Size(300, 28)',
    'port.MAV.cs.armed',
    'MessageBoxDefaultButton.Button2',
    'port.setParam',
    '其他安全項目未變更')) {
    if (!$panel.Contains($required)) { throw "Missing independent safety write protection: $required" }
}
foreach ($removed in @('"遙控器油門門檻"', '"低容量門檻"', '"嚴重容量門檻"')) {
    $catalog = Get-Content -LiteralPath (Join-Path $ProjectRoot 'FMT\FmtSafetyParameterCatalog.cs') -Raw
    if ($catalog.Contains($removed)) { throw "Removed safety card is still present: $removed" }
}
Write-Host 'PASS embedded safety panel is mutually exclusive and writes one confirmed parameter at a time'
