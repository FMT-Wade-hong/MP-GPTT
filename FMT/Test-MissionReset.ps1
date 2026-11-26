param([string]$Directory='bin/MissionActions125/net472', [switch]$Demo)
$ErrorActionPreference='Stop'
$binary=(Resolve-Path -LiteralPath $Directory).Path
$compiler='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('FMTPlanner.exe','MissionPlanner.ArduPilot.dll','MissionPlanner.Comms.dll','MissionPlanner.Controls.dll','MAVLink.dll','MissionPlanner.Utilities.dll','log4net.dll','Interfaces.dll','System.Reactive.dll') | ForEach-Object { '/r:'+(Join-Path $binary $_) }
$exe=Join-Path $binary $(if($Demo){'MissionResetDemo.exe'}else{'MissionResetHarness.exe'})
$target=if($Demo){'/target:winexe'}else{'/target:exe'}
& $compiler /nologo $target "/out:$exe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' $refs "$PSScriptRoot/../MissionPlannerTests/MissionResetHarness.cs"
if($LASTEXITCODE){throw 'Mission reset harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($exe+'.config') -Force
if($Demo){ & $exe --demo }else{ & $exe }
if($LASTEXITCODE){throw 'Mission reset regression failed'}
