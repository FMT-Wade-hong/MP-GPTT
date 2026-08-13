param(
    [string]$BinaryDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Release\net461')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$results = [System.Collections.Generic.List[object]]::new()

function Test-FmtCondition {
    param([string]$Name, [bool]$Condition, [string]$Detail)
    $results.Add([PSCustomObject]@{ Name = $Name; Passed = $Condition; Detail = $Detail })
    if (-not $Condition) {
        throw "Verification failed: $Name - $Detail"
    }
}

$binaryPath = Join-Path $BinaryDirectory 'FMTPlanner.exe'
Test-FmtCondition 'Release binary exists' (Test-Path -LiteralPath $binaryPath -PathType Leaf) $binaryPath

$assembly = [Reflection.Assembly]::LoadFrom($binaryPath)
$flightPlannerType = $assembly.GetType('MissionPlanner.GCSViews.FlightPlanner', $true)
$flightDataType = $assembly.GetType('MissionPlanner.GCSViews.FlightData', $true)
$mainType = $assembly.GetType('MissionPlanner.MainV2', $true)
$binding = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$builder = $flightPlannerType.GetMethod('BuildFmtAirspaceSegments', $binding)
Test-FmtCondition 'Airspace segment builder exists' ($null -ne $builder) 'compiled helper present'

$utilitiesAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $BinaryDirectory 'MissionPlanner.Utilities.dll'))
$pointType = $utilitiesAssembly.GetType('MissionPlanner.Utilities.PointLatLngAlt', $true)
$pointListType = [System.Collections.Generic.List``1].MakeGenericType($pointType)
$points = [Activator]::CreateInstance($pointListType)
$points.Add([Activator]::CreateInstance($pointType, @([double]24.0, [double]121.0, [double]0.0, 'H')))
$points.Add([Activator]::CreateInstance($pointType, @([double]24.1, [double]121.1, [double]50.0, '1')))
$points.Add([Activator]::CreateInstance($pointType, @([double]24.2, [double]121.2, [double]60.0, '2')))

$builderArguments = [object[]]::new(1)
$builderArguments[0] = $points
$segments = @($builder.Invoke($null, $builderArguments))
Test-FmtCondition 'Complete route segment count' ($segments.Count -eq 3) 'takeoff + mission + return'

$kinds = @($segments | ForEach-Object { $_.GetType().GetProperty('Kind', $binding).GetValue($_, $null).ToString() })
Test-FmtCondition 'Takeoff segment included' ($kinds[0] -eq 'Takeoff') 'Home to WP1 is checked first'
Test-FmtCondition 'Mission segment included' ($kinds[1] -eq 'Mission') 'WP1 to WP2 is checked'
Test-FmtCondition 'Return segment included' ($kinds[2] -eq 'Return') 'final WP to Home is checked last'

$gmapAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $BinaryDirectory 'GMap.NET.Core.dll'))
$mapPointType = $gmapAssembly.GetType('GMap.NET.PointLatLng', $true)
$polygonType = [System.Collections.Generic.List``1].MakeGenericType($mapPointType)
$homeLegZone = [Activator]::CreateInstance($polygonType)
foreach ($coordinates in @(
    @(24.04, 121.04),
    @(24.04, 121.08),
    @(24.08, 121.08),
    @(24.08, 121.04))) {
    $homeLegZone.Add([Activator]::CreateInstance($mapPointType,
        @([double]$coordinates[0], [double]$coordinates[1])))
}

$intersection = $flightPlannerType.GetMethod('RouteSegmentIntersectsPolygon', $binding)
function Test-AirspaceSegmentIntersection {
    param($Segment)
    $fromValue = $Segment.GetType().GetProperty('From', $binding).GetValue($Segment, $null)
    $toValue = $Segment.GetType().GetProperty('To', $binding).GetValue($Segment, $null)
    $from = [Activator]::CreateInstance($mapPointType, @([double]$fromValue.Lat, [double]$fromValue.Lng))
    $to = [Activator]::CreateInstance($mapPointType, @([double]$toValue.Lat, [double]$toValue.Lng))
    $arguments = [object[]]::new(3)
    $arguments[0] = $from
    $arguments[1] = $to
    $arguments[2] = $homeLegZone
    return [bool]$intersection.Invoke($null, $arguments)
}

Test-FmtCondition 'Takeoff path crossing detected' (Test-AirspaceSegmentIntersection $segments[0]) 'Home to WP1 intersects the test zone'
Test-FmtCondition 'Mission path false positive avoided' (-not (Test-AirspaceSegmentIntersection $segments[1])) 'WP1 to WP2 stays outside the test zone'
Test-FmtCondition 'Return path crossing detected' (Test-AirspaceSegmentIntersection $segments[2]) 'final WP to Home intersects the test zone'

$singleWaypointPoints = [Activator]::CreateInstance($pointListType)
$singleWaypointPoints.Add([Activator]::CreateInstance($pointType, @([double]24.0, [double]121.0, [double]0.0, 'H')))
$singleWaypointPoints.Add([Activator]::CreateInstance($pointType, @([double]24.1, [double]121.1, [double]50.0, '1')))
$builderArguments[0] = $singleWaypointPoints
$singleWaypointSegments = @($builder.Invoke($null, $builderArguments))
Test-FmtCondition 'Single-WP takeoff and return' ($singleWaypointSegments.Count -eq 2) 'both Home legs are checked'

$source = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\FlightPlanner.cs') -Raw -Encoding UTF8
$programSource = Get-Content -LiteralPath (Join-Path $projectRoot 'Program.cs') -Raw -Encoding UTF8
$mainSource = Get-Content -LiteralPath (Join-Path $projectRoot 'MainV2.cs') -Raw -Encoding UTF8
$authenticationSource = Get-Content -LiteralPath (Join-Path $projectRoot 'FMT\FmtAuthentication.cs') -Raw -Encoding UTF8
$plannerConfigSource = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\ConfigurationView\ConfigPlanner.cs') -Raw -Encoding UTF8
$flightDataSource = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\FlightData.cs') -Raw -Encoding UTF8
$currentStateSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ExtLibs\ArduPilot\CurrentState.cs') -Raw -Encoding UTF8
Test-FmtCondition 'Takeoff warning label' ($source.Contains('Takeoff path Home')) 'warning identifies takeoff path'
Test-FmtCondition 'Return warning label' ($source.Contains('Return path WP')) 'warning identifies return path'
Test-FmtCondition 'Home required for complete check' ($source.Contains('Please set the Home location before checking takeoff and return paths.')) 'prevents incomplete clear result'
Test-FmtCondition 'FMT product title version' ($authenticationSource.Contains('ProductTitle = ProductName + " V" + ProductVersion')) 'title uses FMT release version'
Test-FmtCondition 'Splash hides upstream build version' ($programSource.Contains('Splash.Text = name;') -and -not $programSource.Contains('Application.ProductVersion + " build "')) 'only FMT title is displayed'
Test-FmtCondition 'Connected title remains FMT version' ($mainSource.Contains('this.Text = titlebar;') -and -not $mainSource.Contains('this.Text = titlebar + " " + comPort.MAV.VersionString')) 'flight-controller version is not appended'
Test-FmtCondition 'FMT theme forced at startup' ($authenticationSource.Contains('Settings.Instance["theme"] = ThemeName;') -and $mainSource.Contains('Settings.Instance["theme"] = FMT.FmtAuthentication.ThemeName;')) 'stored themes cannot override FMT branding'
Test-FmtCondition 'Only FMT theme is listed' ($plannerConfigSource.Contains('CMB_theme.DataSource = new[] { FMT.FmtAuthentication.ThemeName };')) 'theme selector has one entry'
Test-FmtCondition 'Custom theme editor hidden' ($plannerConfigSource.Contains('BUT_themecustom.Visible = false;') -and -not $plannerConfigSource.Contains('ThemeManager.StartThemeEditor();')) 'custom entry is unavailable'
Test-FmtCondition 'Toolbar Arm/Disarm handler' ($null -ne $mainType.GetMethod('MenuFmtArmDisarm_Click', $binding)) 'toolbar action is compiled'
Test-FmtCondition 'Toolbar airspeed-zero handler' ($null -ne $mainType.GetMethod('MenuFmtAirspeedZero_Click', $binding)) 'toolbar action is compiled'
Test-FmtCondition 'Existing Arm/Disarm action reused' ($null -ne $flightDataType.GetMethod('ExecuteFmtArmDisarm', $binding)) 'toolbar delegates to Flight Data action'
Test-FmtCondition 'Airspeed-zero action compiled' ($null -ne $flightDataType.GetMethod('ExecuteFmtAirspeedZero', $binding)) 'preflight action is compiled'
Test-FmtCondition 'Arm/Disarm button is red' ($mainSource.Contains('Color.FromArgb(196, 32, 32)')) 'prominent safety color'
Test-FmtCondition 'Quick action button order' ($mainSource.Contains('MainMenu.Items.Insert(quickActionIndex, MenuFmtArmDisarm);') -and $mainSource.Contains('MainMenu.Items.Insert(quickActionIndex + 1, MenuFmtAirspeedZero);')) 'Arm/Disarm is left of airspeed zero'
Test-FmtCondition 'Arm action links existing handler' ($flightDataSource.Contains('BUT_ARM_Click(BUT_ARM, EventArgs.Empty);')) 'existing safety confirmation and force flow retained'
Test-FmtCondition 'Airspeed-only calibration parameter' ($flightDataSource.Contains('0, 0, 0, 0, 0, 2, 0);')) 'MAV_CMD_PREFLIGHT_CALIBRATION param6=2'
Test-FmtCondition 'Airspeed zero blocked while armed' ($flightDataSource.Contains('if (MainV2.comPort.MAV.cs.armed)')) 'preflight-only safety guard'
Test-FmtCondition 'GPS toolbar host compiled' ($mainSource.Contains('MenuFmtGpsStatus = new ToolStripControlHost')) 'two-line GPS status is hosted in the top toolbar'
Test-FmtCondition 'GPS toolbar is right aligned' ($mainSource.Contains('Name = "MenuFmtGpsStatus"') -and $mainSource.Contains('Alignment = ToolStripItemAlignment.Right')) 'GPS status is placed next to the FMT logo'
$fixLabelMethod = $mainType.GetMethod('GetFmtGpsFixLabel', $binding)
Test-FmtCondition 'RTK Fixed label mapping' ($fixLabelMethod.Invoke($null, @([single]6)) -eq 'RTK Fixed') 'GPS fix type 6 has the requested label'
Test-FmtCondition 'VDOP state exists' ($currentStateSource.Contains('public float gpsvdop { get; set; }')) 'vertical dilution is retained in current state'
Test-FmtCondition 'VDOP telemetry source' ($currentStateSource.Contains('gpsvdop = (float)Math.Round(gps.epv / 100.0, 2);')) 'GPS_RAW_INT epv updates VDOP'

$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($binaryPath)
Test-FmtCondition 'Windows file version' ($fileVersion.FileVersion -eq '1.0.3.0') "FileVersion=$($fileVersion.FileVersion)"
Test-FmtCondition 'Windows product version' ($fileVersion.ProductVersion -eq '1.0.3.0') "ProductVersion=$($fileVersion.ProductVersion)"

$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
Test-FmtCondition 'Development version' ($version -eq '1.0.3') "FMT/VERSION=$version"

$results | Format-Table -AutoSize
[PSCustomObject]@{
    Passed = @($results | Where-Object Passed).Count
    Failed = @($results | Where-Object { -not $_.Passed }).Count
}
