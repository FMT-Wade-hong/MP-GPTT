param([string]$Directory='bin/GitHubAudit125/net472')
$ErrorActionPreference='Stop'
$binary=(Resolve-Path -LiteralPath $Directory).Path
$compiler='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('FMTPlanner.exe','MissionPlanner.ArduPilot.dll','MissionPlanner.Comms.dll','MissionPlanner.Controls.dll','MissionPlanner.Utilities.dll','MAVLink.dll','DroneCAN.dll','GMap.NET.Core.dll','log4net.dll','Interfaces.dll','System.Memory.dll','System.Reactive.dll','OpenTK.dll','OpenTK.GLControl.dll') | ForEach-Object { '/r:'+(Join-Path $binary $_) }
$exe=Join-Path $binary 'GitHubAudit125Harness.exe'
& $compiler /nologo /target:exe "/out:$exe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' $refs "$PSScriptRoot/../MissionPlannerTests/GitHubAudit125Harness.cs"
if($LASTEXITCODE){throw 'GitHub audit harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($exe+'.config') -Force
& $exe (Join-Path $PSScriptRoot '../plugins/InitialParamsCalculator.cs')
if($LASTEXITCODE){throw 'GitHub audit regression failed'}
$source=Get-Content "$PSScriptRoot/../Utilities/httpserver.cs" -Raw
if (!$source.Contains('new TcpListener(IPAddress.Loopback, 56781)')) { throw 'Built-in listener must bind only to loopback' }
'PASS: built-in listener configuration is loopback-only.'
