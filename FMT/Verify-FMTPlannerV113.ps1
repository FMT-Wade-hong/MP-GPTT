param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$PackagePath = '',
    [string]$SourceDirectory = ''
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]

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

Test-Source 'V1.1.3 product version' 'FMT\FmtAuthentication.cs' 'ProductVersion = "1.1.3"'
Test-Source 'V1.1.3 project version' 'MissionPlanner.csproj' '<Version>1.1.3</Version>'
Test-Source 'V1.1.3 assembly version' 'Properties\AssemblyInfo.cs' 'AssemblyFileVersion("1.1.3.0")'
Test-Source 'Splash resource' 'FMT\FmtVisualAssets.cs' 'fmt-splash-v111.png'
Test-Source 'Login resource' 'FMT\FmtVisualAssets.cs' 'fmt-login-v111.png'
Test-File 'Splash artwork exists' 'FMT\Assets\fmt-splash-v111.png'
Test-File 'Login artwork exists' 'FMT\Assets\fmt-login-v111.png'

Test-Source 'Stable Mission column widths' 'FMT\FmtAutoMissionPanel.cs' 'missionColumnWidths'
Test-Source 'Mission Item terminology' 'FMT\FmtAutoMissionPanel.cs' 'Mission Item\r\n-- / --'
Test-Source 'Home-distance field' 'FMT\FmtAutoMissionPanel.cs' '離家距離\r\n--'
Test-Source 'Stable ETA formatter' 'FMT\FmtAutoMissionPanel.cs' 'FormatEtaValue'
Test-SourceAbsent 'Mission progress bar removed' 'FMT\FmtAutoMissionPanel.cs' 'new ProgressBar'
Test-Source 'AUTO jump action' 'FMT\FmtAutoMissionPanel.cs' 'ExecuteButtonClick'

Test-Source 'ArduPilot toolbar logo removed' 'MainV2.cs' 'MainMenu.Items.Remove(MenuArduPilot);'
Test-Source 'Preflight action' 'MainV2.cs' 'IsFmtTraditionalChineseUi ? "飛行前檢查" : "PREFLIGHT"'
Test-Source 'Rotor RPM telemetry' 'MainV2.cs' 'RPM1：--'
Test-Source 'Default Taiwan map anchor' 'GCSViews\FlightData.cs' 'new PointLatLng(23.8456499, 120.9759521);'
Test-Source 'Embedded 3D map action' 'GCSViews\FlightData.cs' 'Text = "3D 地圖"'
Test-Source 'Helicopter Brake mode' 'FMT\FmtFlightModeBar.cs' 'Mode("BRAKE", "煞車")'
Test-Source 'Helicopter RPM minimum' 'GCSViews\ConfigurationView\ConfigTradHeli4.cs' 'RPM1 轉速下限必須小於上限。'
Test-Source 'RTK setup entry' 'GCSViews\InitialSetup.cs' 'ConfigSerialInjectGPS'

Test-Source 'Fence type options' 'GCSViews\ConfigurationView\ConfigAC_Fence.cs' 'GetFenceTypeOptions()'
Test-Source 'Fence dropdown width' 'GCSViews\ConfigurationView\ConfigAC_Fence.cs' 'DropDownWidth = Math.Min(760, Math.Max(560, width))'
Test-Source 'Chinese parameter tooltips' 'GCSViews\ConfigurationView\ConfigRawParams.cs' 'GetFmtTooltipDescription'
Test-Source 'Plugin resolver registered once' 'Plugin\PluginLoader.cs' 'Interlocked.CompareExchange(ref assemblyResolverRegistered, 1, 0)'
Test-Source 'Plugin assembly cache' 'Plugin\PluginLoader.cs' 'filecache.TryGetValue(folderPath, out cached)'

Test-Source 'Terrain collision warning' 'Controls\ElevationProfile.cs' 'Color.FromArgb(183, 28, 28)'
Test-Source 'Terrain low-clearance warning' 'Controls\ElevationProfile.cs' 'Color.FromArgb(255, 196, 0)'
Test-Source 'Terrain safe state' 'Controls\ElevationProfile.cs' 'Color.FromArgb(0, 120, 70)'
Test-Source 'Airspace prohibited high contrast' 'GCSViews\FlightPlanner.cs' 'Color.FromArgb(183, 28, 28)'
Test-Source 'Airspace restricted high contrast' 'GCSViews\FlightPlanner.cs' 'Color.FromArgb(255, 193, 7)'
Test-Source 'QNH hPa entry' 'GCSViews\FlightData.cs' 'qnhHectopascals * 100.0'
Test-Source 'Privacy-safe GitHub report' 'FMT\FmtCrashReportForm.cs' 'OpenGitHub_Click'

if (!$SourceDirectory) { $SourceDirectory = Join-Path $ProjectRoot 'bin\Release\net461' }
$exe = Join-Path $SourceDirectory 'FMTPlanner.exe'
if (Test-Path -LiteralPath $exe -PathType Leaf) {
    $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($version -eq '1.1.3.0') { Write-Host 'PASS executable version' -ForegroundColor Green }
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
                'FMTPlanner-V1.1.3/FMTPlanner-V1.1.3.exe',
                'FMTPlanner-V1.1.3/FMTPlanner-V1.1.3.exe.config',
                'FMTPlanner-V1.1.3/README-FMT.md',
                'FMTPlanner-V1.1.3/CHANGELOG-FMT.md',
                'FMTPlanner-V1.1.3/RELEASE-MANIFEST.txt',
                'FMTPlanner-V1.1.3/FMT/MQTT-EMBEDDED.md',
                'FMTPlanner-V1.1.3/FMT/SIK-SETTINGS.md',
                'FMTPlanner-V1.1.3/FMT/Assets/mqtt-toolbar-transparent.png')) {
                if ($entries -notcontains $required) { $failures.Add('package entry ' + $required) }
            }
            foreach ($obsolete in @(
                'FMTPlanner-V1.1.3/FMTPlanner.exe',
                'FMTPlanner-V1.1.3/FMTPlanner.exe.config')) {
                if ($entries -contains $obsolete) { $failures.Add('obsolete package entry ' + $obsolete) }
            }
            $forbidden = @($entries | Where-Object {
                $_ -match '(?i)\.(pdb|so|dylib|pfx|p12|key|tlog|rlog|dmp)$' -or $_ -match '(?i)/plugins/example.*\.cs$' -or
                $_ -match '(?i)/(private-signing|tmp|logs|gmapcache|mqtt)/' -or
                $_ -match '(?i)/(config\.xml|settings\.json|password\.bin|\.env|\.git)$'
            })
            if ($forbidden.Count -eq 0) { Write-Host 'PASS package exclusion policy' -ForegroundColor Green }
            else { $failures.Add('package exclusion policy') }
        } finally {
            $archive.Dispose()
        }
    }
}

if ($failures.Count -gt 0) {
    throw ('V1.1.3 verification failed: ' + ($failures -join ', '))
}

Write-Host 'FMTPlanner V1.1.3 verification passed.' -ForegroundColor Cyan
