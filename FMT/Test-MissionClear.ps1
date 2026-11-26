param([string]$Directory='bin/MissionClearVehicle125/net472')
$ErrorActionPreference='Stop'
$binary=(Resolve-Path -LiteralPath $Directory).Path
$compiler='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('FMTPlanner.exe','MissionPlanner.ArduPilot.dll','MissionPlanner.Comms.dll','MissionPlanner.Controls.dll','MAVLink.dll','MissionPlanner.Utilities.dll','log4net.dll','Interfaces.dll','System.Reactive.dll','System.Memory.dll','GMap.NET.Core.dll','GMap.NET.Drawing.dll','GMap.NET.WindowsForms.dll') | ForEach-Object { '/r:'+(Join-Path $binary $_) }
$exe=Join-Path $binary 'MissionClearHarness.exe'
& $compiler /nologo /target:exe "/out:$exe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' $refs "$PSScriptRoot/../MissionPlannerTests/MissionClearHarness.cs" "$PSScriptRoot/../MissionPlannerTests/MissionClearVehicleTests.cs"
if($LASTEXITCODE){throw 'Mission clear harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($exe+'.config') -Force
& $exe
if($LASTEXITCODE){throw 'Mission clear regression failed'}
