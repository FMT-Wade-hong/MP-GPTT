param(
    [string]$BinaryDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Release\net461'),
    [string]$PackagePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Package\FMTPlanner-V1.0.2.zip')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$results = [System.Collections.Generic.List[object]]::new()

function Test-FmtCondition {
    param([string]$Name, [bool]$Condition, [string]$Detail)
    $results.Add([PSCustomObject]@{
        Name = $Name
        Passed = $Condition
        Detail = $Detail
    })
    if (-not $Condition) {
        throw "Verification failed: $Name - $Detail"
    }
}

$binaryPath = Join-Path $BinaryDirectory 'FMTPlanner.exe'
Test-FmtCondition 'Release binary exists' (Test-Path -LiteralPath $binaryPath -PathType Leaf) $binaryPath

$bytes = [System.IO.File]::ReadAllBytes($binaryPath)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
$subsystem = [BitConverter]::ToUInt16($bytes, $peOffset + 24 + 68)
Test-FmtCondition 'Windows GUI subsystem' ($subsystem -eq 2) "PE subsystem=$subsystem (2 means no console window)"

$assembly = [Reflection.Assembly]::LoadFrom($binaryPath)
$flightPlannerType = $assembly.GetType('MissionPlanner.GCSViews.FlightPlanner', $true)
$flightDataType = $assembly.GetType('MissionPlanner.GCSViews.FlightData', $true)
$mainType = $assembly.GetType('MissionPlanner.MainV2', $true)
$controlsAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $BinaryDirectory 'MissionPlanner.Controls.dll'))
$hudType = $controlsAssembly.GetType('MissionPlanner.Controls.HUD', $true)
$binding = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'

foreach ($methodName in @(
    'BUT_fmtAltitudeCheck_Click',
    'BUT_fmtAirspaceCheck_Click',
    'MainMap_MouseMove',
    'RefreshMapContextAfterDragAsync')) {
    Test-FmtCondition "Flight Planner handler: $methodName" ($null -ne $flightPlannerType.GetMethod($methodName, $binding)) 'compiled handler present'
}
foreach ($fieldName in @('BUT_fmtAltitudeCheck', 'BUT_fmtAirspaceCheck')) {
    Test-FmtCondition "Flight Planner button: $fieldName" ($null -ne $flightPlannerType.GetField($fieldName, $binding)) 'compiled button field present'
}
Test-FmtCondition 'Flight Data action handler' ($null -ne $flightDataType.GetMethod('BUTactiondo_Click', $binding)) 'compiled action handler present'
Test-FmtCondition 'Ready-to-Arm click event' ($null -ne $hudType.GetEvent('prearmclick', $binding)) 'compiled HUD click event present'
Test-FmtCondition 'FMT website toolbar setup' ($null -ne $mainType.GetMethod('ConfigureFmtMainMenu', $binding)) 'compiled toolbar setup present'

$resources = $assembly.GetManifestResourceNames()
Test-FmtCondition 'Login logo embedded' ($resources -contains 'MissionPlanner.FMT.Assets.fmt-logo.png') 'login logo resource'
Test-FmtCondition 'Toolbar logo embedded' ($resources -contains 'MissionPlanner.FMT.Assets.fmt-app-icon-source.png') 'toolbar logo resource'

$gmapAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $BinaryDirectory 'GMap.NET.Core.dll'))
$pointType = $gmapAssembly.GetType('GMap.NET.PointLatLng', $true)
$pointListType = [System.Collections.Generic.List``1].MakeGenericType($pointType)
$polygon = [Activator]::CreateInstance($pointListType)
foreach ($coords in @(@(0.0, 0.0), @(0.0, 1.0), @(1.0, 1.0), @(1.0, 0.0))) {
    $polygon.Add([Activator]::CreateInstance($pointType, @([double]$coords[0], [double]$coords[1])))
}
$crossingFrom = [Activator]::CreateInstance($pointType, @([double]0.5, [double]-1.0))
$crossingTo = [Activator]::CreateInstance($pointType, @([double]0.5, [double]2.0))
$outsideFrom = [Activator]::CreateInstance($pointType, @([double]2.0, [double]2.0))
$outsideTo = [Activator]::CreateInstance($pointType, @([double]3.0, [double]3.0))
$intersection = $flightPlannerType.GetMethod('RouteSegmentIntersectsPolygon', $binding)
Test-FmtCondition 'Airspace crossing positive case' ([bool]$intersection.Invoke($null, @($crossingFrom, $crossingTo, $polygon))) 'crossing segment detected'
Test-FmtCondition 'Airspace crossing negative case' (-not [bool]$intersection.Invoke($null, @($outsideFrom, $outsideTo, $polygon))) 'outside segment ignored'

$flightPlannerSource = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\FlightPlanner.cs') -Raw
$flightDataSource = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\FlightData.cs') -Raw
$hudSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ExtLibs\Controls\HUD.cs') -Raw
$mainSource = Get-Content -LiteralPath (Join-Path $projectRoot 'MainV2.cs') -Raw
$initialSetupSource = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\InitialSetup.cs') -Raw

Test-FmtCondition 'Built-in action dictionary guard' ($flightDataSource.Contains('CustomActions.TryGetValue')) 'prevents KeyNotFoundException'
Test-FmtCondition 'Ready-to-Arm hit zone assignment' ($hudSource.Contains('prearmhitzone = Rectangle.Round')) 'click target is updated while rendering'
Test-FmtCondition 'Waypoint live marker refresh' ($flightPlannerSource.Contains('MainMap.UpdateMarkerLocalPosition(CurentRectMarker)')) 'marker follows mouse'
Test-FmtCondition 'Waypoint live route refresh' ($flightPlannerSource.Contains('MainMap.UpdateRouteLocalPosition(route)')) 'route follows dragged waypoint'
Test-FmtCondition 'Map drag debounce' ($flightPlannerSource.Contains('Task.Delay(300, cancellationToken)')) 'heavy airspace and airport refresh is delayed'
Test-FmtCondition 'Altitude button order' ($flightPlannerSource.Contains('BUT_fmtAltitudeCheck') -and $flightPlannerSource.Contains('BUT_write.Location = new Point(3, 90)')) 'height check is above upload'
Test-FmtCondition 'Airspace button order' ($flightPlannerSource.Contains('BUT_fmtAirspaceCheck')) 'airspace check is above upload'
Test-FmtCondition 'FMT website URL' ($mainSource.Contains('https://www.feimaotec.com')) 'toolbar logo opens official site'
Test-FmtCondition 'Simulator screen removed' (-not $mainSource.Contains('AddScreen(new MainSwitcher.Screen("Simulation"')) 'no simulator screen registration'
Test-FmtCondition 'Help screen removed' (-not $mainSource.Contains('AddScreen(new MainSwitcher.Screen("Help"')) 'no help/about screen registration'
Test-FmtCondition 'Optional Hardware hidden' ($initialSetupSource.Contains('ShowFmtOptionalHardware => false')) 'optional hardware page tree is not created'

$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
Test-FmtCondition 'Release version' ($version -eq '1.0.2') "FMT/VERSION=$version"
if (Test-Path -LiteralPath $PackagePath -PathType Leaf) {
    Test-FmtCondition 'Versioned package filename' ((Split-Path $PackagePath -Leaf) -eq 'FMTPlanner-V1.0.2.zip') $PackagePath
    Test-FmtCondition 'Versioned package is non-empty' ((Get-Item -LiteralPath $PackagePath).Length -gt 1MB) ((Get-Item -LiteralPath $PackagePath).Length.ToString())
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $packageArchive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entryNames = @($packageArchive.Entries | ForEach-Object FullName)
        Test-FmtCondition 'Portable package contains FMTPlanner.exe' ($entryNames -contains 'FMTPlanner-V1.0.2/FMTPlanner.exe') 'direct application executable is present'
        Test-FmtCondition 'Portable package excludes self-extracting launcher' (-not ($entryNames | Where-Object { $_ -like '*FMTPlanner.Start.exe' -or $_ -like '*FMTPlanner.Payload.zip' })) 'no extraction launcher or nested payload'
    }
    finally {
        $packageArchive.Dispose()
    }
}

$results | Format-Table -AutoSize
[PSCustomObject]@{
    Passed = @($results | Where-Object Passed).Count
    Failed = @($results | Where-Object { -not $_.Passed }).Count
}
