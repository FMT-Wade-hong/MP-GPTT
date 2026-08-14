param(
    [string]$BinaryDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Release\net461'),
    [string]$PackagePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Package\FMTPlanner-V1.0.5.zip')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$results = [System.Collections.Generic.List[object]]::new()

function Test-FmtCondition([string]$Name, [bool]$Condition, [string]$Detail) {
    $results.Add([PSCustomObject]@{ Name = $Name; Passed = $Condition; Detail = $Detail })
    if (-not $Condition) { throw "Verification failed: $Name - $Detail" }
}

$binaryPath = Join-Path $BinaryDirectory 'FMTPlanner.exe'
Test-FmtCondition 'Release binary exists' (Test-Path -LiteralPath $binaryPath -PathType Leaf) $binaryPath
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($binaryPath)
Test-FmtCondition 'Windows file version' ($fileVersion.FileVersion -eq '1.0.5.0') "FileVersion=$($fileVersion.FileVersion)"
Test-FmtCondition 'Windows product version' ($fileVersion.ProductVersion -eq '1.0.5.0') "ProductVersion=$($fileVersion.ProductVersion)"

$bytes = [IO.File]::ReadAllBytes($binaryPath)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
$subsystem = [BitConverter]::ToUInt16($bytes, $peOffset + 24 + 68)
Test-FmtCondition 'No console window' ($subsystem -eq 2) "PE subsystem=$subsystem"

$flightData = Get-Content (Join-Path $projectRoot 'GCSViews\FlightData.cs') -Raw -Encoding UTF8
$localizer = Get-Content (Join-Path $projectRoot 'FMT\FmtTelemetryLocalization.cs') -Raw -Encoding UTF8
$modeBar = Get-Content (Join-Path $projectRoot 'FMT\FmtFlightModeBar.cs') -Raw -Encoding UTF8
$mapsProject = Get-Content (Join-Path $projectRoot 'ExtLibs\Maps\MissionPlanner.Maps.csproj') -Raw -Encoding UTF8

Test-FmtCondition 'All selector titles localized' (-not $flightData.Contains('Text = "Display This"') -and
    ([regex]::Matches($flightData, 'Text = "[^\"]+"[,]\r?\n\s+AutoSize')).Count -ge 3) 'three selectors no longer use the English title'
Test-FmtCondition 'QuickView visible labels localized' (([regex]::Matches($flightData, 'FmtTelemetryLocalization\.Display')).Count -ge 4) 'all assignment paths translate descriptions'
Test-FmtCondition 'Telemetry keys preserved' ($flightData.Contains('Name = fields[i].name') -and $flightData.Contains('checkbox.Name')) 'visible text is separate from binding key'
Test-FmtCondition 'Dashboard examples translated' ($localizer.Contains('{ "Accel Strength",') -and $localizer.Contains('{ "Dist to Home (m)",') -and $localizer.Contains('{ "Sat Count",')) 'requested dashboard fields have explicit localized mappings'
Test-FmtCondition 'Flight Data tabs translated' ($flightData.Contains('tabAuxFunction.Text =') -and $flightData.Contains('tabPayload.Text =')) 'remaining English tabs are assigned localized labels'
Test-FmtCondition 'Vehicle-aware mode bar' ($modeBar.Contains('isQuadPlane') -and $modeBar.Contains('Firmwares.ArduPlane') -and $modeBar.Contains('Firmwares.ArduRover') -and $modeBar.Contains('Firmwares.ArduSub')) 'Copter, Plane, VTOL, Rover and Sub profiles compiled'
Test-FmtCondition 'Map icons embedded' ($mapsProject.Contains('FMTMapFixedWingGlow.png') -and $mapsProject.Contains('FMTMapMultirotorGlow.png') -and $mapsProject.Contains('FMTMapVtolGlow.png')) 'three active glow markers are packaged in map assembly'

$mapsAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $BinaryDirectory 'MissionPlanner.Maps.dll'))
$mapResources = @($mapsAssembly.GetManifestResourceNames())
foreach ($resource in 'FMTMapFixedWingGlow.png', 'FMTMapMultirotorGlow.png', 'FMTMapVtolGlow.png') {
    Test-FmtCondition "Embedded $resource" ($mapResources -contains "MissionPlanner.Maps.$resource") 'runtime resource exists'
}

Test-FmtCondition 'Portable package exists' (Test-Path -LiteralPath $PackagePath -PathType Leaf) $PackagePath
Test-FmtCondition 'Portable package is non-empty' ((Get-Item -LiteralPath $PackagePath).Length -gt 1MB) ((Get-Item -LiteralPath $PackagePath).Length.ToString())
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    Test-FmtCondition 'Package contains executable' ($entries -contains 'FMTPlanner-V1.0.5/FMTPlanner.exe') 'direct executable is included'
    Test-FmtCondition 'Package contains manual' ($entries -contains 'FMTPlanner-V1.0.5/README-FMT.md') 'Traditional Chinese manual is included'
    Test-FmtCondition 'Package excludes upstream executable' (-not ($entries -contains 'FMTPlanner-V1.0.5/MissionPlanner.exe')) 'only FMTPlanner executable is published'
}
finally { $archive.Dispose() }

$results | Format-Table -AutoSize
[PSCustomObject]@{ Passed = @($results | Where-Object Passed).Count; Failed = @($results | Where-Object { -not $_.Passed }).Count }
