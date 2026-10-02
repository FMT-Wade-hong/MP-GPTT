param([string]$Directory = 'bin/Release121/net461', [string]$Version = '1.2.1')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$info = (Get-Item "$Directory/FMTPlanner.exe").VersionInfo
if ($info.FileVersion -ne "$Version.0" -or $info.ProductVersion -ne "V$Version") { throw 'EXE version mismatch' }
if ($assembly.GetName().Version.ToString() -ne "$Version.0") { throw 'Assembly version mismatch' }
$type = $assembly.GetType('MissionPlanner.FMT.FmtAuthentication', $true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$runtimeVersion = $type.GetField('ProductVersion', $flags).GetValue($null)
$title = $type.GetField('ProductTitle', $flags).GetValue($null)
$expectedTitle = "FeiMaoTecPlanner V$Version"
if ($runtimeVersion -ne $Version -or $title -ne $expectedTitle -or $info.FileDescription -ne $expectedTitle) { throw 'Runtime title/version mismatch' }
if ((Get-Content "$PSScriptRoot/VERSION" -Raw).Trim() -ne $Version) { throw 'Package VERSION mismatch' }
$project = [xml](Get-Content "$PSScriptRoot/../MissionPlanner.csproj" -Raw)
if (@($project.Project.PropertyGroup.Version | Where-Object { $_ })[0] -ne $Version) { throw 'Project version mismatch' }
foreach ($name in @('MissionPlanner.FMT.FmtLoginForm','MissionPlanner.Splash')) {
    $form = [Activator]::CreateInstance($assembly.GetType($name, $true), $true)
    try { if (!$form.Text.StartsWith($expectedTitle)) { throw "Actual form title mismatch: $name" } }
    finally { $form.Dispose() }
}
$parameterForm = [Activator]::CreateInstance($assembly.GetType('MissionPlanner.FMT.FmtParameterAccessForm', $true), $true)
try { if (!@($parameterForm.Controls | Where-Object { $_.Text.StartsWith($expectedTitle) }).Count) { throw 'Parameter dialog heading mismatch' } }
finally { $parameterForm.Dispose() }
$program = Get-Content "$PSScriptRoot/../Program.cs" -Raw
$main = Get-Content "$PSScriptRoot/../MainV2.cs" -Raw
$crash = Get-Content "$PSScriptRoot/FmtCrashReportForm.cs" -Raw
if (!$program.Contains('name = FMT.FmtAuthentication.ProductTitle;') -or !$main.Contains('Version.TryParse(FMT.FmtAuthentication.ProductVersion, out current)') -or !$crash.Contains('FmtAuthentication.ProductVersion')) { throw 'Runtime consumer no longer uses shared version' }
'PASS: EXE, assembly, runtime branding, login/splash/parameter UI, package version and update/report source bindings agree'
