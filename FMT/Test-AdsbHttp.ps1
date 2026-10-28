param([string]$Directory = 'bin/AdsbFi123/net461', [switch]$Live)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = (Resolve-Path $Directory).Path
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$facade = 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll'
$output = Join-Path $root 'tmp/adsb-tests'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe = Join-Path $output 'AdsbHttpHarness.exe'
& $compiler /nologo /target:exe "/out:$exe" "/reference:$facade" "/reference:$runtime/MissionPlanner.Utilities.dll" "/reference:$runtime/GMap.NET.Core.dll" (Join-Path $root 'MissionPlannerTests/AdsbHttpHarness.cs')
if ($LASTEXITCODE -ne 0) { throw 'ADS-B harness compilation failed' }
if ($Live) { & $exe $runtime --live }
else { & $exe $runtime }
if ($LASTEXITCODE -ne 0) { throw 'ADS-B HTTP regression failed' }
