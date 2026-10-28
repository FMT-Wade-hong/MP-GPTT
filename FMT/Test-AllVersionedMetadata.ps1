param([string]$Directory = 'bin/Local124/net461')
$ErrorActionPreference = 'Stop'
$binary = (Resolve-Path $Directory).Path
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExe = Join-Path $binary 'VersionedMetadataHarness.exe'
& $compiler /nologo /target:exe "/out:$testExe" "/r:$binary/MissionPlanner.Utilities.dll" "/r:$binary/System.Memory.dll" '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' /r:System.Xml.Linq.dll /r:System.IO.Compression.dll "$PSScriptRoot/../MissionPlannerTests/VersionedMetadataHarness.cs"
if ($LASTEXITCODE) { throw 'Metadata harness did not compile' }
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($testExe + '.config') -Force
& $testExe (Join-Path $PSScriptRoot '../ExtLibs/Utilities/ParameterMetadata')
if ($LASTEXITCODE) { throw 'Metadata coverage failed' }
