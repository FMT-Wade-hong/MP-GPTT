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

function Test-SourceAbsent([string]$Name, [string]$Path, [string]$Pattern) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $Path) -Raw -Encoding UTF8
    if (!$content.Contains($Pattern)) { Write-Host "PASS $Name" -ForegroundColor Green }
    else { $failures.Add($Name); Write-Host "FAIL $Name" -ForegroundColor Red }
}

Test-Source 'V1.0.10 product version' 'FMT\FmtAuthentication.cs' 'ProductVersion = "1.0.10"'
Test-Source 'Assembly resolver one-shot guard' 'Plugin\PluginLoader.cs' 'assemblyResolverRegistered'
Test-Source 'Plugin load one-shot guard' 'Plugin\PluginLoader.cs' 'loadAllStarted'
Test-Source 'No empty C# compile task' 'Plugin\PluginLoader.cs' 'if (csFiles.Length > 0)'
Test-Source 'Conditional plugin runner' 'MainV2.cs' 'if (Plugin.PluginLoader.RequiresRunner)'
Test-Source 'Thread-safe plugin file cache' 'Plugin\PluginLoader.cs' 'lock (FileCacheLock)'
Test-Source 'Flight-mode unchanged-state guard' 'FMT\FmtFlightModeBar.cs' 'vehicleStateSignature'
Test-Source 'Previous MAVLink event detached' 'MainV2.cs' 'previousPort.MavChanged -= instance.comPort_MavChanged'
Test-Source 'Windows power event detached' 'MainV2.cs' 'SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged'
Test-Source 'Flight Data parameter event detached' 'GCSViews\FlightData.cs' 'ParamListChanged -= FlightData_ParentChanged'
Test-Source 'Camera duplicate callback prevention' 'GCSViews\FlightData.cs' 'MainV2.cam.camimage -= cam_camimage'
Test-Source 'Child form Theme cache' 'MainV2.cs' 'ConditionalWeakTable<Form, object>'
Test-Source 'Updater explicit UTF-8' 'MainV2.cs' 'client.Encoding = Encoding.UTF8'
Test-Source 'Typed update dialog result' 'MainV2.cs' 'System.Windows.Forms.DialogResult.Yes'
Test-Source 'Scrollable update notes' 'FMT\FmtUpdateForm.cs' 'ScrollBars = RichTextBoxScrollBars.Vertical'
Test-Source 'Update fallback for damaged text' 'FMT\FmtUpdateForm.cs' 'replacementCount > 2'
Test-SourceAbsent 'No legacy updater CustomMessageBox' 'MainV2.cs' 'FMTPlanner 版本更新", MessageBoxButtons.YesNo'
Test-Source 'Package strips PDB files' 'FMT\Build-FMTPlannerPackage.ps1' "`$extension -ieq '.pdb'"
Test-Source 'Package strips non-Windows native files' 'FMT\Build-FMTPlannerPackage.ps1' "`$extension -ieq '.so' -or `$extension -ieq '.dylib'"
Test-Source 'Package strips example plugin sources' 'FMT\Build-FMTPlannerPackage.ps1' 'plugins\example*.cs'

# Representative V1.0.9 FMT feature protection checks.
Test-Source 'FMT login retained' 'FMT\FmtAuthentication.cs' 'DefaultUserName = "FMT"'
Test-Source 'Parameter password page retained' 'MainV2.cs' 'MenuFmtParameterSettings'
Test-Source 'Titan/FMT flight mode bar retained' 'GCSViews\FlightData.cs' 'new FmtFlightModeBar()'
Test-Source 'Aircraft marker retained' 'GCSViews\FlightData.cs' 'Aircraft markers must not depend on the flown-track route.'
Test-Source 'Taiwan airspace switch retained' 'GCSViews\FlightData.cs' 'Text = "顯示限禁航區"'
Test-Source 'Airspeed zero retained' 'MainV2.cs' 'MenuFmtAirspeedZero'
Test-Source 'QNH retained' 'MainV2.cs' 'MenuFmtQnh'
Test-Source 'GPS toolbar retained' 'MainV2.cs' 'MenuFmtGpsStatus'
Test-Source 'Waypoint distance retained' 'ExtLibs\Controls\HUD.cs' 'WP 航點距離'
Test-Source 'Airspace check retained' 'GCSViews\FlightPlanner.cs' 'FMT Airspace Check'
Test-Source 'Altitude check retained' 'GCSViews\FlightPlanner.cs' 'FMT Height Check'
Test-Source 'Traditional Chinese updater retained' 'FMT\FmtUpdateForm.cs' '是否開啟飛貓科技版本下載頁？'

$exe = Join-Path $ProjectRoot 'bin\Release\net461\FMTPlanner.exe'
if (Test-Path -LiteralPath $exe) {
    $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($version -eq '1.0.10.0') { Write-Host 'PASS executable version' -ForegroundColor Green }
    else { $failures.Add('executable version'); Write-Host "FAIL executable version: $version" -ForegroundColor Red }
} else {
    $failures.Add('executable exists')
}

if ($PackagePath) {
    if (!(Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        $failures.Add('package exists')
    } else {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
        try {
            $entryNames = @($archive.Entries | ForEach-Object FullName)
            foreach ($required in @(
                'FMTPlanner-V1.0.10/FMTPlanner.exe',
                'FMTPlanner-V1.0.10/README-FMT.md',
                'FMTPlanner-V1.0.10/CHANGELOG-FMT.md',
                'FMTPlanner-V1.0.10/RELEASE-MANIFEST.txt',
                'FMTPlanner-V1.0.10/plugins/AltitudeAngelWings.Plugin.dll')) {
                if ($entryNames -notcontains $required) {
                    $failures.Add('package entry ' + $required)
                }
            }

            $forbidden = @($entryNames | Where-Object {
                $_ -match '(?i)\.(pdb|so|dylib)$' -or $_ -match '(?i)/plugins/example.*\.cs$'
            })
            if ($forbidden.Count -eq 0) { Write-Host 'PASS package exclusion policy' -ForegroundColor Green }
            else { $failures.Add('package exclusion policy') }
        }
        finally {
            $archive.Dispose()
        }
    }
}

if ($failures.Count -gt 0) {
    throw ('V1.0.10 verification failed: ' + ($failures -join ', '))
}

Write-Host 'FMTPlanner V1.0.10 verification passed.' -ForegroundColor Cyan
