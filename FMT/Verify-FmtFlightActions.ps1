param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot), [string]$PreviewDirectory = '')
$ErrorActionPreference = 'Stop'
if (!$PreviewDirectory) { $PreviewDirectory = Join-Path $ProjectRoot 'bin\Release\net461' }
$artifacts = Join-Path $ProjectRoot 'tmp\actions-tests'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $installation 'MSBuild\Current\Bin\Roslyn\csc.exe'
$testExe = Join-Path $artifacts 'FmtFlightActionsHarness.exe'
& $compiler /nologo /target:exe /langversion:7.3 "/out:$testExe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $ProjectRoot 'MissionPlannerTests\FmtFlightActionsHarness.cs')
if ($LASTEXITCODE -ne 0) { throw 'Flight actions harness compile failed.' }
Copy-Item -LiteralPath (Join-Path $PreviewDirectory 'FMTPlanner.exe.config') -Destination ($testExe + '.config') -Force
& $testExe $PreviewDirectory $artifacts
if ($LASTEXITCODE -ne 0) { throw 'Flight actions harness failed.' }
$source = Get-Content -LiteralPath (Join-Path $ProjectRoot 'GCSViews\FlightData.cs') -Raw -Encoding UTF8
if (!$source.Contains('FmtFlightActionsLayout.Configure(tabActions, tableLayoutPanel1, IsFmtTraditionalChineseUi);')) { throw 'FlightData integration missing.' }
if (!$source.Contains('RequestFmtFlightMode(CMB_modes.Text);') -or !$source.Contains('Enum.Parse(typeof(MAVLink.MAV_CMD)')) { throw 'Original mode/action command mapping missing.' }
Write-Host 'PASS FlightData integration and original command mapping retained'
