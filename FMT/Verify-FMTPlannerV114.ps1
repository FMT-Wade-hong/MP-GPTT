param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$PackagePath = '',
    [string]$SourceDirectory = ''
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]
$releaseVersion = '1.1.4'
$releaseRoot = "FMTPlanner-V$releaseVersion"

function Test-Source([string]$Name, [string]$Path, [string]$Pattern) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $Path) -Raw -Encoding UTF8
    if ($content.Contains($Pattern)) { Write-Host "PASS $Name" -ForegroundColor Green }
    else { $failures.Add($Name); Write-Host "FAIL $Name" -ForegroundColor Red }
}

function Test-SourceAbsent([string]$Name, [string]$Path, [string]$Pattern) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $Path) -Raw -Encoding UTF8
    if (!$content.Contains($Pattern)) { Write-Host "PASS $Name" -ForegroundColor Green }
    else { $failures.Add($Name); Write-Host "FAIL $Name" -ForegroundColor Red }
}

function Test-File([string]$Name, [string]$Path) {
    if (Test-Path -LiteralPath (Join-Path $ProjectRoot $Path) -PathType Leaf) {
        Write-Host "PASS $Name" -ForegroundColor Green
    } else {
        $failures.Add($Name); Write-Host "FAIL $Name" -ForegroundColor Red
    }
}

Test-Source 'V1.1.4 product version' 'FMT\FmtAuthentication.cs' 'ProductVersion = "1.1.4"'
Test-Source 'V1.1.4 project version' 'MissionPlanner.csproj' '<Version>1.1.4</Version>'
Test-Source 'V1.1.4 assembly version' 'Properties\AssemblyInfo.cs' 'AssemblyFileVersion("1.1.4.0")'
Test-Source 'Splash resource' 'FMT\FmtVisualAssets.cs' 'fmt-splash-v111.png'
Test-Source 'Login resource' 'FMT\FmtVisualAssets.cs' 'fmt-login-v111.png'
Test-File 'Splash artwork exists' 'FMT\Assets\fmt-splash-v111.png'
Test-File 'Login artwork exists' 'FMT\Assets\fmt-login-v111.png'

Test-Source 'MQTT persistence requires consent' 'FMT\FmtMqttSettings.cs' 'RememberConsentPath'
Test-Source 'MQTT empty defaults without consent' 'FMT\FmtMqttSettings.cs' '!File.Exists(RememberConsentPath)'
Test-Source 'MQTT clears remembered state' 'FMT\FmtMqttSettings.cs' 'ClearRemembered()'
Test-Source 'Embedded safety settings action' 'GCSViews\FlightData.cs' 'Text = "安全設定"'
Test-Source 'Independent safety parameter cards' 'FMT\FmtSafetySettingsPanel.cs' 'Text = "套用"'
Test-Source 'Localized failsafe options' 'FMT\FmtSafetyParameterCatalog.cs' 'TranslateOption'
Test-Source 'Centered throttle mode' 'GCSViews\ConfigurationView\ConfigRadioInput.cs' 'Text = "中立回中油門"'
Test-Source 'Manual throttle mode' 'GCSViews\ConfigurationView\ConfigRadioInput.cs' 'Text = "手動油門（低位為零）"'
Test-Source 'Throttle parameter check' 'GCSViews\ConfigurationView\ConfigRadioInput.cs' 'PILOT_THR_BHV'

Test-Source 'Six-position accelerometer cards' 'GCSViews\ConfigurationView\ConfigAccelerometerCalibration.cs' 'Name = "FmtAccelPoseGrid"'
Test-Source 'Accelerometer completed state' 'GCSViews\ConfigurationView\ConfigAccelerometerCalibration.cs' 'Color.FromArgb(35, 170, 78)'
Test-Source 'Accelerometer current state' 'GCSViews\ConfigurationView\ConfigAccelerometerCalibration.cs' 'Color.FromArgb(245, 196, 24)'
Test-Source 'Accelerometer pending state' 'GCSViews\ConfigurationView\ConfigAccelerometerCalibration.cs' 'Color.FromArgb(105, 112, 118)'
Test-Source 'Accelerometer message does not cover cards' 'GCSViews\ConfigurationView\ConfigAccelerometerCalibration.cs' 'lbl_Accel_user.Dock = DockStyle.None'
Test-Source 'Compact compass layout' 'GCSViews\ConfigurationView\ConfigHWCompass2.cs' 'Size = new Size(760, 610)'

Test-Source 'Stable Mission column widths' 'FMT\FmtAutoMissionPanel.cs' 'missionColumnWidths'
Test-SourceAbsent 'Mission progress bar removed' 'FMT\FmtAutoMissionPanel.cs' 'new ProgressBar'
Test-Source 'AUTO jump action' 'FMT\FmtAutoMissionPanel.cs' 'ExecuteButtonClick'
Test-Source 'Preflight action' 'MainV2.cs' 'IsFmtTraditionalChineseUi ? "飛行前檢查" : "PREFLIGHT"'
Test-Source 'Rotor RPM telemetry' 'MainV2.cs' 'RPM1：--'
Test-Source 'Default Taiwan map anchor' 'GCSViews\FlightData.cs' 'new PointLatLng(23.8456499, 120.9759521);'
Test-Source 'Embedded 3D map action' 'GCSViews\FlightData.cs' 'Text = "3D 地圖"'
Test-Source 'Helicopter Brake mode' 'FMT\FmtFlightModeBar.cs' 'Mode("BRAKE", "煞車")'
Test-Source 'RTK setup entry' 'GCSViews\InitialSetup.cs' 'ConfigSerialInjectGPS'
Test-Source 'Fence type options' 'GCSViews\ConfigurationView\ConfigAC_Fence.cs' 'GetFenceTypeOptions()'
Test-Source 'Chinese parameter tooltips' 'GCSViews\ConfigurationView\ConfigRawParams.cs' 'GetFmtTooltipDescription'
Test-Source 'Terrain collision warning' 'Controls\ElevationProfile.cs' 'Color.FromArgb(183, 28, 28)'
Test-Source 'QNH hPa entry' 'GCSViews\FlightData.cs' 'qnhHectopascals * 100.0'
Test-Source 'Privacy-safe GitHub report' 'FMT\FmtCrashReportForm.cs' 'OpenGitHub_Click'

if (!$SourceDirectory) { $SourceDirectory = Join-Path $ProjectRoot 'bin\Release\net461' }
$exe = Join-Path $SourceDirectory 'FMTPlanner.exe'
if (Test-Path -LiteralPath $exe -PathType Leaf) {
    $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($version -eq '1.1.4.0') { Write-Host 'PASS executable version' -ForegroundColor Green }
    else { $failures.Add('executable version'); Write-Host "FAIL executable version: $version" -ForegroundColor Red }
} else {
    $failures.Add('executable exists'); Write-Host 'FAIL executable exists' -ForegroundColor Red
}

if ($PackagePath) {
    if (!(Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        $failures.Add('package exists')
    } else {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
        try {
            $entries = @($archive.Entries | ForEach-Object FullName)
            foreach ($required in @(
                "$releaseRoot/FMTPlanner-V$releaseVersion.exe",
                "$releaseRoot/FMTPlanner-V$releaseVersion.exe.config",
                "$releaseRoot/README-FMT.md",
                "$releaseRoot/CHANGELOG-FMT.md",
                "$releaseRoot/RELEASE-MANIFEST.txt",
                "$releaseRoot/FMT/MQTT-EMBEDDED.md",
                "$releaseRoot/FMT/SIK-SETTINGS.md",
                "$releaseRoot/FMT/Assets/mqtt-toolbar-transparent.png")) {
                if ($entries -notcontains $required) { $failures.Add('package entry ' + $required) }
            }
            foreach ($obsolete in @(
                "$releaseRoot/FMTPlanner.exe",
                "$releaseRoot/FMTPlanner.exe.config")) {
                if ($entries -contains $obsolete) { $failures.Add('obsolete package entry ' + $obsolete) }
            }
            $forbidden = @($entries | Where-Object {
                $_ -match '(?i)\.(pdb|so|dylib|pfx|p12|key|tlog|rlog|dmp)$' -or
                $_ -match '(?i)/plugins/example.*\.cs$' -or
                $_ -match '(?i)/(private-signing|tmp|logs|gmapcache|mqtt)/' -or
                $_ -match '(?i)/(config\.xml|settings\.json|password\.bin|remember-settings\.optin|\.env|\.git)$'
            })
            if ($forbidden.Count -eq 0) { Write-Host 'PASS package exclusion policy' -ForegroundColor Green }
            else { $failures.Add('package exclusion policy') }
        } finally {
            $archive.Dispose()
        }
    }
}

if ($failures.Count -gt 0) {
    throw ('V1.1.4 verification failed: ' + ($failures -join ', '))
}

Write-Host 'FMTPlanner V1.1.4 verification passed.' -ForegroundColor Cyan
