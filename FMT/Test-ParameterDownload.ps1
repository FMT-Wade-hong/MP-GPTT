param([string]$Directory='bin/ParameterDownload125/net472')
$ErrorActionPreference='Stop'
$binary=(Resolve-Path -LiteralPath $Directory).Path
$compiler='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('FMTPlanner.exe','MissionPlanner.ArduPilot.dll','MissionPlanner.Comms.dll','MAVLink.dll','MissionPlanner.Utilities.dll','log4net.dll','Interfaces.dll','System.Reactive.dll','System.Memory.dll','Newtonsoft.Json.dll') | ForEach-Object { '/r:'+(Join-Path $binary $_) }
$exe=Join-Path $binary 'ParameterDownloadHarness.exe'
& $compiler /nologo /target:exe "/out:$exe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' $refs "$PSScriptRoot/../MissionPlannerTests/ParameterDownloadHarness.cs"
if($LASTEXITCODE){throw 'Parameter download harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($exe+'.config') -Force
& $exe
if($LASTEXITCODE){throw 'Parameter download regression failed'}
