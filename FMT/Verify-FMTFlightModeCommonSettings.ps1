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

Test-FmtCondition 'Common settings compiled' ($null -ne $type.GetMethod('CreateFmtCommonSettings', $binding)) 'dynamic panel builder is present'
Test-FmtCondition 'Common settings loader compiled' ($null -ne $type.GetMethod('LoadFmtCommonSettings', $binding)) 'parameter discovery is present'
Test-FmtCondition 'Common settings save compiled' ($null -ne $type.GetMethod('SaveFmtCommonSettings', $binding)) 'safe write handler is present'
Test-FmtCondition 'Legacy navigation speed converts to m/s' ([Math]::Abs([double]$fromRaw.Invoke($null, @('WPNAV_SPEED', 500.0)) - 5.0) -lt 0.001) '500 cm/s is displayed as 5.0 m/s'
Test-FmtCondition 'Legacy RTL speed converts to raw' ([Math]::Abs([double]$toRaw.Invoke($null, @('RTL_SPEED', 6.5)) - 650.0) -lt 0.001) '6.5 m/s is written as 650 cm/s'
Test-FmtCondition 'New navigation speed remains m/s' ([Math]::Abs([double]$fromRaw.Invoke($null, @('WP_SPD', 7.5)) - 7.5) -lt 0.001) 'WP_SPD uses m/s directly'
Test-FmtCondition 'New RTL speed remains m/s' ([Math]::Abs([double]$toRaw.Invoke($null, @('RTL_SPEED_MS', 8.5)) - 8.5) -lt 0.001) 'RTL_SPEED_MS uses m/s directly'

$yawMapping = $type.GetMethod('FmtYawSelectionsToBehavior', $binding)
Test-FmtCondition 'WP and RTL face targets mapping' ([int]$yawMapping.Invoke($null, @(1, 1)) -eq 1) 'WP next waypoint plus RTL home maps to behavior 1'
Test-FmtCondition 'WP face target and RTL hold mapping' ([int]$yawMapping.Invoke($null, @(1, 0)) -eq 2) 'WP next waypoint plus RTL hold maps to behavior 2'
Test-FmtCondition 'GPS course yaw mapping' ([int]$yawMapping.Invoke($null, @(3, 3)) -eq 3) 'WP and RTL GPS course maps to behavior 3'
Test-FmtCondition 'Unsupported independent yaw rejected' ([int]$yawMapping.Invoke($null, @(0, 1)) -eq -1) 'unsupported pair cannot be written to shared parameter'

$source = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\ConfigurationView\ConfigFlightModes.cs') -Raw -Encoding UTF8
Test-FmtCondition 'Panel is below flight modes' ($source.Contains('Location = new Point(0, tableLayoutPanel1.Bottom + 10)')) 'common settings follow the existing controls'
Test-FmtCondition 'Panel is Copter only' ($source.Contains('fmtCommonSettings.Visible = isCopter;')) 'unsupported vehicle types do not show Copter parameters'
Test-FmtCondition 'Navigation aliases supported' ($source.Contains('FindFmtParameter("WP_SPD", "WPNAV_SPEED")')) 'new and legacy navigation parameters are supported'
Test-FmtCondition 'GPS speed aliases supported' ($source.Contains('FindFmtParameter("LOIT_SPEED_MS", "WPNAV_LOIT_SPEED", "LOIT_SPEED")')) 'new and legacy position-control speed parameters are supported'
Test-FmtCondition 'RTL speed aliases supported' ($source.Contains('FindFmtParameter("RTL_SPEED_MS", "RTL_SPEED")')) 'new and legacy RTL parameters are supported'
Test-FmtCondition 'WP and RTL yaw selectors visible' ($source.Contains('CreateFmtYawCombo("FmtWpYaw"') -and $source.Contains('CreateFmtYawCombo("FmtRtlYaw"')) 'Chinese WP and RTL direction fields are separate'
Test-FmtCondition 'Yaw parameter mapped honestly' ($source.Contains('FindFmtParameter("WP_YAW_BEHAVIOR")') -and $source.Contains('FmtYawSelectionsToBehavior')) 'two fields map safely to the shared firmware parameter'
Test-FmtCondition 'All speed units are m/s' (([regex]::Matches($source, 'CreateFmtUnitLabel')).Count -ge 4 -and $source.Contains('return CreateFmtLabel("m/s"')) 'three speed rows share the m/s unit label'
Test-FmtCondition 'Writes blocked while armed' ($source.Contains('MainV2.comPort.MAV.cs.armed')) 'parameter writes have a disarmed safety gate'
Test-FmtCondition 'Read-only writes blocked' ($source.Contains('MainV2.comPort.ReadOnly')) 'read-only links cannot write parameters'

$results | Format-Table -AutoSize
[PSCustomObject]@{
    Passed = @($results | Where-Object Passed).Count
    Failed = @($results | Where-Object { -not $_.Passed }).Count
}
