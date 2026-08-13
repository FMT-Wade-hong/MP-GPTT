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

$BinaryDirectory = (Resolve-Path -LiteralPath $BinaryDirectory).Path
$binaryPath = Join-Path $BinaryDirectory 'FMTPlanner.exe'
Test-FmtCondition 'Release binary exists' (Test-Path -LiteralPath $binaryPath -PathType Leaf) $binaryPath

$dependencyResolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $dependencyName = ([Reflection.AssemblyName]::new($eventArgs.Name)).Name + '.dll'
    $dependencyPath = Join-Path $BinaryDirectory $dependencyName
    if (Test-Path -LiteralPath $dependencyPath -PathType Leaf) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($dependencyResolver)

$assembly = [Reflection.Assembly]::LoadFrom($binaryPath)
$binding = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigFlightModes', $true)
$fromRaw = $type.GetMethod('FmtSpeedRawToMetersPerSecond', $binding)
$toRaw = $type.GetMethod('FmtSpeedMetersPerSecondToRaw', $binding)
$distanceFromRaw = $type.GetMethod('FmtDistanceRawToMeters', $binding)
$distanceToRaw = $type.GetMethod('FmtDistanceMetersToRaw', $binding)

Test-FmtCondition 'Common settings compiled' ($null -ne $type.GetMethod('CreateFmtCommonSettings', $binding)) 'dynamic panel builder is present'
Test-FmtCondition 'Common settings loader compiled' ($null -ne $type.GetMethod('LoadFmtCommonSettings', $binding)) 'parameter discovery is present'
Test-FmtCondition 'Common settings save compiled' ($null -ne $type.GetMethod('SaveFmtCommonSettings', $binding)) 'safe write handler is present'
Test-FmtCondition 'Legacy navigation speed converts to m/s' ([Math]::Abs([double]$fromRaw.Invoke($null, @('WPNAV_SPEED', 500.0)) - 5.0) -lt 0.001) '500 cm/s is displayed as 5.0 m/s'
Test-FmtCondition 'Legacy RTL speed converts to raw' ([Math]::Abs([double]$toRaw.Invoke($null, @('RTL_SPEED', 6.5)) - 650.0) -lt 0.001) '6.5 m/s is written as 650 cm/s'
Test-FmtCondition 'New navigation speed remains m/s' ([Math]::Abs([double]$fromRaw.Invoke($null, @('WP_SPD', 7.5)) - 7.5) -lt 0.001) 'WP_SPD uses m/s directly'
Test-FmtCondition 'New RTL speed remains m/s' ([Math]::Abs([double]$toRaw.Invoke($null, @('RTL_SPEED_MS', 8.5)) - 8.5) -lt 0.001) 'RTL_SPEED_MS uses m/s directly'
Test-FmtCondition 'Legacy Plane airspeed converts to m/s' ([Math]::Abs([double]$fromRaw.Invoke($null, @('TRIM_ARSPD_CM', 1500.0)) - 15.0) -lt 0.001) 'legacy Plane cruise speed is displayed in m/s'
Test-FmtCondition 'Legacy Plane GPS speed converts to raw' ([Math]::Abs([double]$toRaw.Invoke($null, @('MIN_GNDSPD_CM', 8.0)) - 800.0) -lt 0.001) 'legacy Plane minimum ground speed is written in cm/s'
Test-FmtCondition 'Legacy VTOL navigation converts to m/s' ([Math]::Abs([double]$fromRaw.Invoke($null, @('Q_WP_SPEED', 600.0)) - 6.0) -lt 0.001) 'legacy QuadPlane waypoint speed is displayed in m/s'
Test-FmtCondition 'Legacy VTOL GPS speed converts to raw' ([Math]::Abs([double]$toRaw.Invoke($null, @('Q_LOIT_SPEED', 5.5)) - 550.0) -lt 0.001) 'legacy QuadPlane loiter speed is written in cm/s'
Test-FmtCondition 'Legacy Copter WP radius converts to m' ([Math]::Abs([double]$distanceFromRaw.Invoke($null, @('WPNAV_RADIUS', 200.0)) - 2.0) -lt 0.001) 'legacy Copter WP radius is displayed in meters'
Test-FmtCondition 'Legacy VTOL WP radius converts to raw' ([Math]::Abs([double]$distanceToRaw.Invoke($null, @('Q_WP_RADIUS', 3.5)) - 350.0) -lt 0.001) 'legacy QuadPlane WP radius is written in centimeters'
Test-FmtCondition 'Plane WP radius remains meters' ([Math]::Abs([double]$distanceToRaw.Invoke($null, @('WP_RADIUS', 25.0)) - 25.0) -lt 0.001) 'Plane WP radius uses meters directly'

$yawMapping = $type.GetMethod('FmtYawSelectionsToBehavior', $binding)
Test-FmtCondition 'WP and RTL face targets mapping' ([int]$yawMapping.Invoke($null, @(1, 1)) -eq 1) 'WP next waypoint plus RTL home maps to behavior 1'
Test-FmtCondition 'WP face target and RTL hold mapping' ([int]$yawMapping.Invoke($null, @(1, 0)) -eq 2) 'WP next waypoint plus RTL hold maps to behavior 2'
Test-FmtCondition 'GPS course yaw mapping' ([int]$yawMapping.Invoke($null, @(3, 3)) -eq 3) 'WP and RTL GPS course maps to behavior 3'
Test-FmtCondition 'Unsupported independent yaw rejected' ([int]$yawMapping.Invoke($null, @(0, 1)) -eq -1) 'unsupported pair cannot be written to shared parameter'

$source = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\ConfigurationView\ConfigFlightModes.cs') -Raw -Encoding UTF8
Test-FmtCondition 'Panels are below flight modes' ($source.Contains('Location = new Point(0, fmtVehicleNotice.Bottom + 4)')) 'vehicle panels follow the existing controls'
Test-FmtCondition 'Three vehicle panels exist' ($source.Contains('Name = "FmtCommonSettings"') -and $source.Contains('Name = "FmtFixedWingSettings"') -and $source.Contains('Name = "FmtVtolSettings"')) 'multirotor, fixed-wing and VTOL each have a frame'
Test-FmtCondition 'Connected vehicle panel selection' ($source.Contains('LoadFmtMultirotorSettings(isCopter, canWrite);') -and $source.Contains('LoadFmtFixedWingSettings(isFixedWing, canWrite);') -and $source.Contains('LoadFmtVtolSettings(isVtol, canWrite);')) 'only the detected vehicle type is writable'
Test-FmtCondition 'Navigation aliases supported' ($source.Contains('FindFmtParameter("WP_SPD", "WPNAV_SPEED")')) 'new and legacy navigation parameters are supported'
Test-FmtCondition 'GPS speed aliases supported' ($source.Contains('FindFmtParameter("LOIT_SPEED_MS", "WPNAV_LOIT_SPEED", "LOIT_SPEED")')) 'new and legacy position-control speed parameters are supported'
Test-FmtCondition 'RTL speed aliases supported' ($source.Contains('FindFmtParameter("RTL_SPEED_MS", "RTL_SPEED")')) 'new and legacy RTL parameters are supported'
Test-FmtCondition 'Multirotor WP radius aliases' ($source.Contains('FindFmtParameter("WP_RADIUS_M", "WPNAV_RADIUS")')) 'new and legacy Copter waypoint radius parameters are supported'
Test-FmtCondition 'Plane cruise aliases supported' ($source.Contains('FindFmtParameter("AIRSPEED_CRUISE", "TRIM_ARSPD_CM")')) 'new and legacy Plane cruise parameters are supported'
Test-FmtCondition 'Plane GPS aliases supported' ($source.Contains('FindFmtParameter("MIN_GROUNDSPEED", "MIN_GNDSPD_CM")')) 'new and legacy Plane ground speed parameters are supported'
Test-FmtCondition 'Plane WP radius supported' ($source.Contains('FindFmtParameter("WP_RADIUS")')) 'Plane waypoint radius is supported in meters'
Test-FmtCondition 'VTOL navigation aliases supported' ($source.Contains('FindFmtParameter("Q_WP_SPD", "Q_WP_SPEED")')) 'new and legacy QuadPlane navigation parameters are supported'
Test-FmtCondition 'VTOL GPS aliases supported' ($source.Contains('FindFmtParameter("Q_LOIT_SPEED_MS", "Q_LOIT_SPEED")')) 'new and legacy QuadPlane loiter parameters are supported'
Test-FmtCondition 'VTOL WP radius aliases' ($source.Contains('FindFmtParameter("Q_WP_RADIUS_M", "Q_WP_RADIUS")')) 'new and legacy QuadPlane waypoint radius parameters are supported'
Test-FmtCondition 'VTOL RTL mode supported' ($source.Contains('FindFmtParameter("Q_RTL_MODE")') -and $source.Contains('new FmtSelectionOption(3, "')) 'QuadPlane return behavior is selectable'
Test-FmtCondition 'WP and RTL yaw selectors visible' ($source.Contains('CreateFmtYawCombo("FmtWpYaw"') -and $source.Contains('CreateFmtYawCombo("FmtRtlYaw"')) 'Chinese WP and RTL direction fields are separate'
Test-FmtCondition 'Yaw parameter mapped honestly' ($source.Contains('FindFmtParameter("WP_YAW_BEHAVIOR")') -and $source.Contains('FmtYawSelectionsToBehavior')) 'two fields map safely to the shared firmware parameter'
Test-FmtCondition 'All speed units are m/s' (([regex]::Matches($source, 'CreateFmtUnitLabel')).Count -ge 9 -and $source.Contains('return CreateFmtLabel("m/s"')) 'vehicle speed rows share the m/s unit label'
Test-FmtCondition 'All waypoint radius units are m' (([regex]::Matches($source, 'CreateFmtDistanceUnitLabel')).Count -ge 4 -and $source.Contains('return CreateFmtLabel("m"')) 'vehicle waypoint radius rows share the meter unit label'
Test-FmtCondition 'Vehicle-specific explanations exist' (([regex]::Matches($source, 'CreateFmtDescriptionLabel')).Count -ge 4) 'each vehicle frame contains a Chinese parameter explanation'
Test-FmtCondition 'Writes blocked while armed' ($source.Contains('MainV2.comPort.MAV.cs.armed')) 'parameter writes have a disarmed safety gate'
Test-FmtCondition 'Read-only writes blocked' ($source.Contains('MainV2.comPort.ReadOnly')) 'read-only links cannot write parameters'
Test-FmtCondition 'Common input text is readable' ($source.Contains('control.BackColor = inputBackground;') -and $source.Contains('control.ForeColor = Color.White;')) 'numeric and yaw inputs use a dark background with white text'
Test-FmtCondition 'Save actions keep FMT colors' ($source.Contains('fmtSaveCommonSettings.BackColor = Color.FromArgb(41, 171, 226);') -and $source.Contains('fmtSavePlaneSettings.BackColor = Color.FromArgb(41, 171, 226);') -and $source.Contains('fmtSaveVtolSettings.BackColor = Color.FromArgb(41, 171, 226);')) 'all save actions remain sky blue after theme application'

$results | Format-Table -AutoSize
[PSCustomObject]@{
    Passed = @($results | Where-Object Passed).Count
    Failed = @($results | Where-Object { -not $_.Passed }).Count
}
