param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot), [string]$PreviewDirectory = '')
$ErrorActionPreference = 'Stop'
if (!$PreviewDirectory) { $PreviewDirectory = Join-Path $ProjectRoot 'bin\Release\net461' }
$artifacts = Join-Path $ProjectRoot 'tmp\sik-tests'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $installation 'MSBuild\Current\Bin\Roslyn\csc.exe'
$testExe = Join-Path $artifacts 'FmtSiKRadioHarness.exe'
& $compiler /nologo /target:exe /langversion:7.3 "/out:$testExe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $ProjectRoot 'MissionPlannerTests\FmtSiKRadioHarness.cs')
if ($LASTEXITCODE -ne 0) { throw 'SiK harness compile failed.' }
Copy-Item -LiteralPath (Join-Path $PreviewDirectory 'FMTPlanner.exe.config') -Destination ($testExe + '.config') -Force
& $testExe $PreviewDirectory $artifacts $ProjectRoot
if ($LASTEXITCODE -ne 0) { throw 'SiK harness failed.' }
$initial = Get-Content -LiteralPath (Join-Path $ProjectRoot 'GCSViews\InitialSetup.cs') -Raw -Encoding UTF8
$rtk = $initial.IndexOf('AddBackstageViewPage(typeof(ConfigSerialInjectGPS)')
$sik = $initial.IndexOf('AddBackstageViewPage(typeof(ConfigSiKRadio), "數傳設定", true, mand)')
$hidden = $initial.IndexOf('if (ShowFmtOptionalHardware)')
if ($rtk -lt 0 -or $sik -le $rtk -or $sik -ge $hidden) { throw 'SiK page must be visible immediately below RTK.' }
$page = Get-Content -LiteralPath (Join-Path $ProjectRoot 'GCSViews\ConfigurationView\ConfigSiKRadio.cs') -Raw -Encoding UTF8
if ($page.Contains('MainV2.comPort.Close(') -or $page.Contains('MainV2.comPort.BaseStream =')) { throw 'SiK page must never hijack MP transport.' }
if (!$page.Contains('if (HasActiveTelemetry())')) { throw 'Missing connected-MP operation guard.' }
$localization = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Radio\Sikradio.Fmt.cs') -Raw -Encoding UTF8
if (!$localization.Contains('MessageBoxDefaultButton.Button2')) { throw 'Destructive operations must default to No.' }
Write-Host 'PASS RTK/SiK navigation order, independent serial ownership and write confirmation defaults'
