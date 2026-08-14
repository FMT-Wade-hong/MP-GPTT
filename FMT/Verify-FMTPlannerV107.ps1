param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$PackagePath = ""
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]

function Test-Source([string]$name, [string]$path, [string]$pattern) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $path) -Raw -Encoding UTF8
    if ($content.Contains($pattern)) { Write-Host "PASS $name" -ForegroundColor Green }
    else { $failures.Add($name); Write-Host "FAIL $name" -ForegroundColor Red }
}

Test-Source 'Version constant' 'FMT\FmtAuthentication.cs' 'ProductVersion = "1.0.7"'
Test-Source 'Explicit mode bar layout' 'GCSViews\FlightData.cs' 'LayoutFmtFlightModeArea()'
Test-Source 'Normalized mode matching' 'FMT\FmtFlightModeBar.cs' 'NormalizeModeName'
Test-Source 'Mode group indicator' 'FMT\FmtFlightModeBar.cs' 'groupLabel.ForeColor = groupActive'
Test-Source 'Flight time toolbar' 'MainV2.cs' 'MenuFmtFlightTime'
Test-Source 'Controller flight time parameter' 'MainV2.cs' 'STAT_FLTTIME'
Test-Source 'Motor test mandatory page' 'GCSViews\InitialSetup.cs' 'typeof(ConfigMotorTest)'
Test-Source 'Enter unlock' 'FMT\FmtParameterAccessForm.cs' 'password.KeyDown += Password_KeyDown'
Test-Source 'First unlock editing' 'FMT\FmtProtectedParameters.cs' 'EnableFmtEditingAfterUnlock()'
Test-Source 'Waypoint drag throttle' 'GCSViews\FlightPlanner.cs' 'fmtLastWaypointDragRenderTick'
Test-Source 'Localized mission planner title' 'MainV2.cs' 'MenuFlightPlanner.Text ='
Test-Source 'Localized mission choices' 'GCSViews\FlightPlanner.cs' 'cmb_missiontype.DisplayMember = "Value"'
Test-Source 'Visible waypoint inputs' 'GCSViews\FlightPlanner.cs' 'EnsureFmtWaypointInputsVisible()'
Test-Source 'Unlocked parameter cell editing' 'GCSViews\ConfigurationView\ConfigRawParams.cs' 'Params.BeginEdit(true)'
Test-Source 'Aircraft-centred 50 km airspace' 'GCSViews\FlightData.cs' 'FmtAirspaceDisplayRadiusKm = 50.0'
Test-Source 'Airspace visibility switch' 'GCSViews\FlightData.cs' 'Text = "顯示限禁航區"'
Test-Source 'HUD throttle percentage' 'ExtLibs\Controls\HUD.cs' '"油門 " + _throttlePercent'
Test-Source 'QuadPlane map icon detection' 'Common.cs' 'MAV.param.ContainsKey("Q_ENABLE")'
Test-Source 'Disarmed track suppression' 'GCSViews\FlightData.cs' 'add new route point only after arming'
Test-Source 'Scrollable check result' 'GCSViews\FlightPlanner.cs' 'ShowFmtScrollableCheckResult'
Test-Source 'Traditional Chinese elevation chart' 'Controls\ElevationProfile.cs' '飛行高度與地形剖面'
Test-Source 'No cursor distance balloon' 'GCSViews\FlightData.cs' 'do not attach the legacy "Dist to Home" balloon'
Test-Source 'FMT release update check' 'MainV2.cs' 'checkFmtUpdate'

$exe = Join-Path $ProjectRoot 'bin\Release\net461\FMTPlanner.exe'
if (Test-Path -LiteralPath $exe) {
    $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($version -eq '1.0.7.0') { Write-Host 'PASS executable version' -ForegroundColor Green }
    else { $failures.Add('executable version'); Write-Host "FAIL executable version: $version" -ForegroundColor Red }
} else { $failures.Add('executable exists') }

if ($PackagePath) {
    if (Test-Path -LiteralPath $PackagePath) { Write-Host 'PASS package exists' -ForegroundColor Green }
    else { $failures.Add('package exists') }
}

if ($failures.Count -gt 0) {
    throw ('V1.0.7 verification failed: ' + ($failures -join ', '))
}

Write-Host 'FMTPlanner V1.0.7 verification passed.' -ForegroundColor Cyan

