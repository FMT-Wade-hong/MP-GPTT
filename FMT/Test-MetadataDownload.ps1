param([string]$Directory='bin/Metadata473/net472', [switch]$Demo, [switch]$Live)
$ErrorActionPreference='Stop'
$binary=(Resolve-Path -LiteralPath $Directory).Path
$compiler='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('FMTPlanner.exe','MissionPlanner.ArduPilot.dll','MissionPlanner.Comms.dll','MissionPlanner.Controls.dll','MAVLink.dll','MissionPlanner.Utilities.dll','GMap.NET.Core.dll','System.Memory.dll','log4net.dll','Interfaces.dll','System.Reactive.dll') | ForEach-Object { '/r:'+(Join-Path $binary $_) }
$exe=Join-Path $binary $(if($Demo){'MetadataDemoHarness.exe'}elseif($Live){'MetadataLiveHarness.exe'}else{'MetadataDownloadHarness.exe'})
$target=if($Demo){'/target:winexe'}else{'/target:exe'}
& $compiler /nologo $target "/out:$exe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Net.Http.dll /r:System.IO.Compression.dll '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' $refs "$PSScriptRoot/../MissionPlannerTests/MetadataDownloadHarness.cs"
if($LASTEXITCODE){throw 'Metadata harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($exe+'.config') -Force
if($Demo){ & $exe --demo }elseif($Live){ & $exe --live }else{ & $exe }
if($LASTEXITCODE){throw 'Metadata regression failed'}
