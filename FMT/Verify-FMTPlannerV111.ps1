param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$PackagePath = ''
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]

function Test-Source([string]$Name, [string]$Path, [string]$Pattern) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $Path) -Raw -Encoding UTF8
    if ($content.Contains($Pattern)) { Write-Host "PASS $Name" -ForegroundColor Green }
    else { $failures.Add($Name); Write-Host "FAIL $Name" -ForegroundColor Red }
}

function Test-File([string]$Name, [string]$Path) {
    if (Test-Path -LiteralPath (Join-Path $ProjectRoot $Path) -PathType Leaf) {
        Write-Host "PASS $Name" -ForegroundColor Green
    } else {
        $failures.Add($Name); Write-Host "FAIL $Name" -ForegroundColor Red
    }
}

Test-Source 'V1.1.1 product version' 'FMT\FmtAuthentication.cs' 'ProductVersion = "1.1.1"'
Test-Source 'V1.1.1 project version' 'MissionPlanner.csproj' '<Version>1.1.1</Version>'
Test-Source 'V1.1.1 splash resource' 'FMT\FmtVisualAssets.cs' 'fmt-splash-v111.png'
Test-Source 'V1.1.1 login resource' 'FMT\FmtVisualAssets.cs' 'fmt-login-v111.png'
Test-File 'Splash artwork exists' 'FMT\Assets\fmt-splash-v111.png'
Test-File 'Login artwork exists' 'FMT\Assets\fmt-login-v111.png'
Test-Source 'Terrain collision warning' 'Controls\ElevationProfile.cs' 'Color.FromArgb(183, 28, 28)'
Test-Source 'Terrain low-clearance warning' 'Controls\ElevationProfile.cs' 'Color.FromArgb(255, 196, 0)'
Test-Source 'Terrain safe state' 'Controls\ElevationProfile.cs' 'Color.FromArgb(0, 120, 70)'
Test-Source 'Airspace prohibited high contrast' 'GCSViews\FlightPlanner.cs' 'Color.FromArgb(183, 28, 28)'
Test-Source 'Airspace restricted high contrast' 'GCSViews\FlightPlanner.cs' 'Color.FromArgb(255, 193, 7)'
Test-Source 'QNH hPa entry' 'GCSViews\FlightData.cs' 'qnhHectopascals * 100.0'
Test-Source 'AUTO jump action' 'FMT\FmtAutoMissionPanel.cs' 'ExecuteButtonClick'
Test-Source 'Traditional Chinese context menus' 'GCSViews\FlightPlanner.cs' 'FmtTraditionalChineseContextMenus.Apply'
Test-Source 'Advanced tools layout' 'temp.cs' 'ApplyFmtAdvancedToolsLayout'
Test-Source 'Localized command display binding' 'GCSViews\FlightPlanner.cs' 'FmtMissionCommandOption.Display'
Test-Source 'Map click writes latitude' 'GCSViews\FlightPlanner.cs' 'Commands.Rows[selectedrow].Cells[Lat.Index] as DataGridViewTextBoxCell'
Test-Source 'Map click writes longitude' 'GCSViews\FlightPlanner.cs' 'Commands.Rows[selectedrow].Cells[Lon.Index] as DataGridViewTextBoxCell'
Test-Source 'Privacy-safe GitHub report' 'FMT\FmtCrashReportForm.cs' 'OpenGitHub_Click'

$exe = Join-Path $ProjectRoot 'bin\Release\net461\FMTPlanner.exe'
if (Test-Path -LiteralPath $exe -PathType Leaf) {
    $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($version -eq '1.1.1.0') { Write-Host 'PASS executable version' -ForegroundColor Green }
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
                'FMTPlanner-V1.1.1/FMTPlanner.exe',
                'FMTPlanner-V1.1.1/README-FMT.md',
                'FMTPlanner-V1.1.1/CHANGELOG-FMT.md',
                'FMTPlanner-V1.1.1/RELEASE-MANIFEST.txt')) {
                if ($entries -notcontains $required) { $failures.Add('package entry ' + $required) }
            }
            $forbidden = @($entries | Where-Object {
                $_ -match '(?i)\.(pdb|so|dylib)$' -or $_ -match '(?i)/plugins/example.*\.cs$'
            })
            if ($forbidden.Count -eq 0) { Write-Host 'PASS package exclusion policy' -ForegroundColor Green }
            else { $failures.Add('package exclusion policy') }
        } finally {
            $archive.Dispose()
        }
    }
}

if ($failures.Count -gt 0) {
    throw ('V1.1.1 verification failed: ' + ($failures -join ', '))
}

Write-Host 'FMTPlanner V1.1.1 verification passed.' -ForegroundColor Cyan
