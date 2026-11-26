param([string]$Directory='bin/Stability125/net472', [switch]$GraphicsOnly, [ValidateRange(1,100)][int]$Cycles=30)
$ErrorActionPreference='Stop'
$binary=(Resolve-Path -LiteralPath $Directory).Path
$compiler='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('FMTPlanner.exe','MissionPlanner.ArduPilot.dll','MissionPlanner.Comms.dll','MissionPlanner.Controls.dll','MissionPlanner.Utilities.dll','MAVLink.dll','OpenTK.dll','OpenTK.GLControl.dll','GMap.NET.Core.dll','log4net.dll','Interfaces.dll','System.Memory.dll','System.Reactive.dll') | ForEach-Object { '/r:'+(Join-Path $binary $_) }
$exe=Join-Path $binary 'StabilityLifecycleHarness.exe'
& $compiler /nologo /target:exe "/out:$exe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' $refs "$PSScriptRoot/../MissionPlannerTests/StabilityLifecycleHarness.cs"
if($LASTEXITCODE){throw 'Stability lifecycle harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($exe+'.config') -Force
if ($GraphicsOnly) { & $exe --gl-only $Cycles }
else { & $exe }
if($LASTEXITCODE){throw 'Stability lifecycle regression failed'}
