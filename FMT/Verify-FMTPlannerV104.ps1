param(
    [string]$BinaryDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Release\net461'),
    [string]$PackagePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Package\FMTPlanner-V1.0.4.zip')
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

$bytes = [System.IO.File]::ReadAllBytes($binaryPath)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
$subsystem = [BitConverter]::ToUInt16($bytes, $peOffset + 24 + 68)
Test-FmtCondition 'Windows GUI subsystem' ($subsystem -eq 2) "PE subsystem=$subsystem (2 means no console window)"

$assembly = [Reflection.Assembly]::LoadFrom($binaryPath)
$resources = @($assembly.GetManifestResourceNames())
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
$brandingSource = Get-Content -LiteralPath (Join-Path $projectRoot 'FMT\FmtBranding.cs') -Raw -Encoding UTF8
$loginSource = Get-Content -LiteralPath (Join-Path $projectRoot 'FMT\FmtLoginForm.cs') -Raw -Encoding UTF8
$parameterAccessSource = Get-Content -LiteralPath (Join-Path $projectRoot 'FMT\FmtParameterAccessForm.cs') -Raw -Encoding UTF8
$plannerConfigSource = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\ConfigurationView\ConfigPlanner.cs') -Raw -Encoding UTF8
$flightDataSource = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\FlightData.cs') -Raw -Encoding UTF8
$currentStateSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ExtLibs\ArduPilot\CurrentState.cs') -Raw -Encoding UTF8
$altitudeAngelSource = Get-Content -LiteralPath (Join-Path $projectRoot 'ExtLibs\AltitudeAngelWings\Clients\UserAuthenticationTokenProvider.cs') -Raw -Encoding UTF8
Test-FmtCondition 'Takeoff warning label' ($source.Contains('Takeoff path Home')) 'warning identifies takeoff path'
Test-FmtCondition 'Return warning label' ($source.Contains('Return path WP')) 'warning identifies return path'
Test-FmtCondition 'Home required for complete check' ($source.Contains('Please set the Home location before checking takeoff and return paths.')) 'prevents incomplete clear result'
Test-FmtCondition 'FMT product title version' ($authenticationSource.Contains('ProductTitle = ProductName + " V" + ProductVersion')) 'title uses FMT release version'
Test-FmtCondition 'Splash hides upstream build version' ($programSource.Contains('Splash.Text = name;') -and -not $programSource.Contains('Application.ProductVersion + " build "')) 'only FMT title is displayed'
Test-FmtCondition 'Connected title remains FMT version' ($mainSource.Contains('this.Text = titlebar;') -and -not $mainSource.Contains('this.Text = titlebar + " " + comPort.MAV.VersionString')) 'flight-controller version is not appended'
Test-FmtCondition 'FMT executable icon configured' ((Get-Content -LiteralPath (Join-Path $projectRoot 'MissionPlanner.csproj') -Raw -Encoding UTF8).Contains('<ApplicationIcon>mpdesktop.ico</ApplicationIcon>')) 'Windows executable uses the generated FMT ICO'
Test-FmtCondition 'FMT runtime icon is fixed' ($brandingSource.Contains('Resources.mpdesktop.Clone()') -and -not $programSource.Contains('Settings.GetRunningDirectory() + "icon.png"')) 'external icon.png cannot replace FMT branding'
Test-FmtCondition 'FMT login icon applied' ($loginSource.Contains('FmtBranding.ApplyApplicationIcon(this);')) 'login and taskbar use the embedded FMT icon'
Test-FmtCondition 'FMT main window icon applied' ($mainSource.Contains('FMT.FmtBranding.ApplyApplicationIcon(this);')) 'main window uses the embedded FMT icon'
Test-FmtCondition 'FMT parameter dialog icons applied' (([regex]::Matches($parameterAccessSource, 'FmtBranding\.ApplyApplicationIcon\(this\);')).Count -eq 2) 'parameter access dialogs use the embedded FMT icon'
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
Test-FmtCondition 'GPS satellite icon embedded' ($resources -contains 'MissionPlanner.FMT.Assets.fmt-satellite-icon.png') 'satellite icon is available without an external file'
Test-FmtCondition 'GPS satellite icon is left of data' ($mainSource.Contains('Name = "FmtGpsIcon"') -and $mainSource.Contains('Location = new Point(3, 3)') -and $mainSource.Contains('Location = new Point(35, 1)')) 'icon precedes the two-line GPS text'
$fixLabelMethod = $mainType.GetMethod('GetFmtGpsFixLabel', $binding)
Test-FmtCondition 'RTK Fixed English label mapping' ($fixLabelMethod.Invoke($null, @([single]6, $false)) -eq 'RTK Fixed') 'GPS fix type 6 has the standard English label'
$rtkFixedChinese = 'RTK ' + [char]0x56FA + [char]0x5B9A + [char]0x89E3
Test-FmtCondition 'RTK Fixed Chinese label mapping' ($fixLabelMethod.Invoke($null, @([single]6, $true)) -eq $rtkFixedChinese) 'GPS fix type 6 is localized for the Chinese UI'
Test-FmtCondition 'VDOP state exists' ($currentStateSource.Contains('public float gpsvdop { get; set; }')) 'vertical dilution is retained in current state'
Test-FmtCondition 'VDOP telemetry source' ($currentStateSource.Contains('gpsvdop = (float)Math.Round(gps.epv / 100.0, 2);')) 'GPS_RAW_INT epv updates VDOP'
Test-FmtCondition 'Duplicate lower GPS values hidden' ($flightDataSource.Contains('lbl_hdop.Visible = false;') -and $flightDataSource.Contains('lbl_sats.Visible = false;')) 'GPS values are shown only in the top toolbar'
$currentHeadingChinese = [string]([char]0x76EE) + [char]0x524D + [char]0x822A + [char]0x5411
$gpsTrackChinese = 'GPS ' + [char]0x822A + [char]0x8DE1 + [char]0xFF08 + [char]0x9ED1 + [char]0x8272 + [char]0xFF09
Test-FmtCondition 'Lower heading legend localized' ($flightDataSource.Contains('label4.Text = "' + $currentHeadingChinese + '";') -and $flightDataSource.Contains('label6.Text = "' + $gpsTrackChinese + '";')) 'remaining map legend uses Traditional Chinese'
Test-FmtCondition 'Altitude Angel prompt localized' ($altitudeAngelSource.Contains('CultureInfo.CurrentUICulture.Name.StartsWith("zh"') -and $altitudeAngelSource.Contains('You need to sign into Altitude Angel.')) 'sign-in action selects a Chinese prompt for the Chinese UI'

$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($binaryPath)
Test-FmtCondition 'Windows file version' ($fileVersion.FileVersion -eq '1.0.4.0') "FileVersion=$($fileVersion.FileVersion)"
Test-FmtCondition 'Windows product version' ($fileVersion.ProductVersion -eq '1.0.4.0') "ProductVersion=$($fileVersion.ProductVersion)"

$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
Test-FmtCondition 'Development version' ($version -eq '1.0.4') "FMT/VERSION=$version"

Test-FmtCondition 'Versioned package exists' (Test-Path -LiteralPath $PackagePath -PathType Leaf) $PackagePath
Test-FmtCondition 'Versioned package filename' ((Split-Path $PackagePath -Leaf) -eq 'FMTPlanner-V1.0.4.zip') $PackagePath
Test-FmtCondition 'Versioned package is non-empty' ((Get-Item -LiteralPath $PackagePath).Length -gt 1MB) ((Get-Item -LiteralPath $PackagePath).Length.ToString())
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageArchive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    $entryNames = @($packageArchive.Entries | ForEach-Object FullName)
    Test-FmtCondition 'Portable package contains FMTPlanner.exe' ($entryNames -contains 'FMTPlanner-V1.0.4/FMTPlanner.exe') 'direct application executable is present'
    Test-FmtCondition 'Portable package contains startup guide' ($entryNames -contains 'FMTPlanner-V1.0.4/README-FIRST.txt') 'extraction and startup instructions are present'
    Test-FmtCondition 'Portable package contains full manual' ($entryNames -contains 'FMTPlanner-V1.0.4/README-FMT.md') 'Traditional Chinese illustrated manual is bundled'
    Test-FmtCondition 'Portable package contains release history' ($entryNames -contains 'FMTPlanner-V1.0.4/CHANGELOG-FMT.md') 'release history is bundled'
    Test-FmtCondition 'Portable package contains manual images' (($entryNames | Where-Object { $_ -like 'FMTPlanner-V1.0.4/FMT/ManualImages/*.png' }).Count -ge 4) 'manual images render after extraction'
    Test-FmtCondition 'Portable package excludes old executable' (-not ($entryNames -contains 'FMTPlanner-V1.0.4/MissionPlanner.exe')) 'upstream executable is not bundled'
    Test-FmtCondition 'Portable package excludes self-extracting launcher' (-not ($entryNames | Where-Object { $_ -like '*FMTPlanner.Start.exe' -or $_ -like '*FMTPlanner.Payload.zip' })) 'no extraction launcher or nested payload'
}
finally {
    $packageArchive.Dispose()
}

$results | Format-Table -AutoSize
[PSCustomObject]@{
    Passed = @($results | Where-Object Passed).Count
    Failed = @($results | Where-Object { -not $_.Passed }).Count
}
