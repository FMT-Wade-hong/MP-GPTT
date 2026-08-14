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

function Test-SourceAbsent([string]$name, [string]$path, [string]$pattern) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $path) -Raw -Encoding UTF8
    if (!$content.Contains($pattern)) { Write-Host "PASS $name" -ForegroundColor Green }
    else { $failures.Add($name); Write-Host "FAIL $name" -ForegroundColor Red }
}

Test-Source 'Version constant' 'FMT\FmtAuthentication.cs' 'ProductVersion = "1.0.8"'
Test-Source 'Explicit mode bar layout' 'GCSViews\FlightData.cs' 'LayoutFmtFlightModeArea()'
Test-Source 'Normalized mode matching' 'FMT\FmtFlightModeBar.cs' 'NormalizeModeName'
Test-Source 'Mode group indicator' 'FMT\FmtFlightModeBar.cs' 'groupLabel.ForeColor = groupActive'
Test-Source 'Traditional Chinese force-arm warning' 'GCSViews\FlightData.cs' '強制" + localizedAction + "會略過飛控安全檢查'
Test-Source 'Traditional Chinese force-arm buttons' 'GCSViews\FlightData.cs' 'var cancelButtonText = IsFmtTraditionalChineseUi ? "取消" : "Cancel"'
Test-Source 'Flight time toolbar' 'MainV2.cs' 'MenuFmtFlightTime'
Test-Source 'Controller flight time parameter' 'MainV2.cs' 'STAT_FLTTIME'
Test-Source 'Motor test mandatory page' 'GCSViews\InitialSetup.cs' 'typeof(ConfigMotorTest)'
Test-Source 'Enter unlock' 'FMT\FmtParameterAccessForm.cs' 'password.KeyDown += Password_KeyDown'
Test-Source 'Full parameter editing enabled' 'FMT\FmtProtectedParameters.cs' 'EnableFmtEditing()'
Test-SourceAbsent 'No nested full parameter password prompt' 'FMT\FmtProtectedParameters.cs' 'new FmtParameterAccessForm()'
Test-Source 'Parameter settings top page' 'MainV2.cs' 'MenuFmtParameterSettings'
Test-Source 'Parameter settings screen registration' 'MainV2.cs' 'new MainSwitcher.Screen("FMTParameterSettings", typeof(FMT.FmtParameterSettings), false)'
Test-Source 'Password settings button on parameter page' 'FMT\FmtParameterSettings.cs' 'Text = "設定參數密碼"'
Test-SourceAbsent 'Full parameter removed from configuration sidebar' 'GCSViews\SoftwareConfig.cs' 'typeof(FmtProtectedParameters)'
Test-Source 'Waypoint drag throttle' 'GCSViews\FlightPlanner.cs' 'fmtLastWaypointDragRenderTick'
Test-Source 'Localized mission planner title' 'MainV2.cs' 'MenuFlightPlanner.Text ='
Test-Source 'Localized mission choices' 'GCSViews\FlightPlanner.cs' 'cmb_missiontype.DisplayMember = "Value"'
Test-Source 'Visible waypoint inputs' 'GCSViews\FlightPlanner.cs' 'EnsureFmtWaypointInputsVisible()'
Test-Source 'Unlocked parameter cell editing' 'GCSViews\ConfigurationView\ConfigRawParams.cs' 'Params.BeginEdit(true)'
Test-Source 'Aircraft-centred 50 km airspace' 'GCSViews\FlightData.cs' 'FmtAirspaceDisplayRadiusKm = 50.0'
Test-Source 'Airspace visibility switch' 'GCSViews\FlightData.cs' 'Text = "顯示限禁航區"'
Test-Source 'HUD throttle label above percentage' 'ExtLibs\Controls\HUD.cs' 'var throttleLabel = "油門"'
Test-Source 'HUD throttle percentage below label' 'ExtLibs\Controls\HUD.cs' 'throttleTop + throttleFontSize + 1'
Test-Source 'Prominent HUD waypoint distance badge' 'ExtLibs\Controls\HUD.cs' 'var waypointLabel = "WP 航點距離"'
Test-Source 'Waypoint badge between altitude and GPS status' 'ExtLibs\Controls\HUD.cs' 'var gpsStatusTop = this.Height - (fontsize + 13)'
Test-SourceAbsent 'Legacy lower-left waypoint distance text removed' 'ExtLibs\Controls\HUD.cs' 'drawstring(wpPrefix + newdist + newdistunit'
Test-Source 'QuadPlane map icon detection' 'Common.cs' 'MAV.param.ContainsKey("Q_ENABLE")'
Test-Source 'QuadPlane marker recreation' 'Common.cs' 'if (MAV.aptype == MAVLink.MAV_TYPE.FIXED_WING || isVtol)'
Test-Source 'Narrow aircraft glow' 'ExtLibs\Maps\GMapMarkerPlane.cs' 'const int glowSize = 78'
Test-Source 'Disarmed track suppression' 'GCSViews\FlightData.cs' 'add new route point only after arming'
Test-Source 'Scrollable check result' 'GCSViews\FlightPlanner.cs' 'ShowFmtScrollableCheckResult'
Test-Source 'Traditional Chinese elevation chart' 'Controls\ElevationProfile.cs' '飛行高度與地形剖面'
Test-Source 'No cursor distance balloon' 'GCSViews\FlightData.cs' 'do not attach the legacy "Dist to Home" balloon'
Test-Source 'FMT release update check' 'MainV2.cs' 'checkFmtUpdate'
Test-Source 'Altitude Angel sign-in banner suppressed' 'ExtLibs\AltitudeAngelWings\Clients\UserAuthenticationTokenProvider.cs' 'without showing its sign-in action banner over the map.'
Test-Source 'Traditional Chinese fixed-wing tuning page' 'GCSViews\ConfigurationView\ConfigArduplane.cs' '固定翼基本調校說明'
Test-Source 'Traditional Chinese tuning guidance' 'GCSViews\ConfigurationView\ConfigArduplane.cs' 'L1 反應週期：數值較小時轉向更積極'
Test-Source 'Traditional Chinese configuration menu' 'GCSViews\SoftwareConfig.cs' '飛控檔案管理（MAVFTP）'

$exe = Join-Path $ProjectRoot 'bin\Release\net461\FMTPlanner.exe'
if (Test-Path -LiteralPath $exe) {
    $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($version -eq '1.0.8.0') { Write-Host 'PASS executable version' -ForegroundColor Green }
    else { $failures.Add('executable version'); Write-Host "FAIL executable version: $version" -ForegroundColor Red }
} else { $failures.Add('executable exists') }

if ($PackagePath) {
    if (Test-Path -LiteralPath $PackagePath) { Write-Host 'PASS package exists' -ForegroundColor Green }
    else { $failures.Add('package exists') }
}

if ($failures.Count -gt 0) {
    throw ('V1.0.8 verification failed: ' + ($failures -join ', '))
}

Write-Host 'FMTPlanner V1.0.8 verification passed.' -ForegroundColor Cyan
