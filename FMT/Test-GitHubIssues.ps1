param([string]$Directory='bin/GitHubIssueFix/net472')
$ErrorActionPreference='Stop'
$binary=(Resolve-Path -LiteralPath $Directory).Path
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $installation 'MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('FMTPlanner.exe','MissionPlanner.ArduPilot.dll','MissionPlanner.Comms.dll','MissionPlanner.Controls.dll','MAVLink.dll','MissionPlanner.Utilities.dll','System.Memory.dll','log4net.dll','Interfaces.dll') | ForEach-Object { '/r:'+(Join-Path $binary $_) }
$exe=Join-Path $binary 'GitHubIssuesHarness.exe'
& $compiler /nologo /target:exe "/out:$exe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' $refs "$PSScriptRoot/../MissionPlannerTests/GitHubIssuesHarness.cs"
if($LASTEXITCODE){throw 'GitHub issues harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($exe+'.config') -Force
& $exe
if($LASTEXITCODE){throw 'GitHub bitmask/serial regression failed'}
