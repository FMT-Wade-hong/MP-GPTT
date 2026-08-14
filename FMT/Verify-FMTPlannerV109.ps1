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

Test-Source 'Version constant' 'FMT\FmtAuthentication.cs' 'ProductVersion = "1.0.9"'
Test-Source 'Protected parameter page layout' 'FMT\FmtParameterSettings.cs' 'RowStyles.Add(new RowStyle(SizeType.Absolute, 76F))'
Test-Source 'Safe parameter tree placeholder handling' 'GCSViews\ConfigurationView\ConfigRawParams.cs' 'row.IsNewRow'
Test-Source 'Safe parameter tree expansion' 'GCSViews\ConfigurationView\ConfigRawParams.cs' 'treeView1.TopNode?.Expand()'
Test-Source 'Visible fixed-wing parameter captions' 'GCSViews\ConfigurationView\ConfigArduplane.cs' 'private Label[] FmtParameterLabels()'
Test-Source 'Fixed-wing caption scaling guard' 'GCSViews\ConfigurationView\ConfigArduplane.cs' 'label.Height = 20'
Test-Source 'Traditional Chinese GCS failsafe' 'GCSViews\ConfigurationView\ConfigFailSafe.cs' '地面站失控保護（GCS）'
Test-Source 'Servo bottom-row height fix' 'GCSViews\ConfigurationView\ConfigRadioOutput.cs' '32 + num_servos * 34 + num_servos + 8'
Test-Source 'Aircraft marker independent of track' 'GCSViews\FlightData.cs' 'Aircraft markers must not depend on the flown-track route.'
Test-Source 'Auto-pan independent of track' 'GCSViews\FlightData.cs' 'Auto-pan follows the live aircraft even while disarmed.'
Test-Source 'Altitude Angel automatic sign-in disabled' 'ExtLibs\AltitudeAngelWings.Plugin\AltitudeAngelPlugin.cs' 'Do not start the optional'
Test-SourceAbsent 'No automatic Altitude Angel sign-in call' 'ExtLibs\AltitudeAngelWings.Plugin\AltitudeAngelPlugin.cs' 'service.SignInAsync();'

$exe = Join-Path $ProjectRoot 'bin\Release\net461\FMTPlanner.exe'
if (Test-Path -LiteralPath $exe) {
    $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
    if ($version -eq '1.0.9.0') { Write-Host 'PASS executable version' -ForegroundColor Green }
    else { $failures.Add('executable version'); Write-Host "FAIL executable version: $version" -ForegroundColor Red }
} else {
    $failures.Add('executable exists')
    Write-Host 'FAIL executable exists' -ForegroundColor Red
}

if ($PackagePath) {
    if (Test-Path -LiteralPath $PackagePath) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
        try {
            $entryNames = @($archive.Entries | ForEach-Object FullName)
            if ($entryNames -contains 'FMTPlanner-V1.0.9/FMTPlanner.exe') {
                Write-Host 'PASS package executable' -ForegroundColor Green
            } else {
                $failures.Add('package executable')
                Write-Host 'FAIL package executable' -ForegroundColor Red
            }
            if ($entryNames -contains 'FMTPlanner-V1.0.9/plugins/AltitudeAngelWings.Plugin.dll') {
                Write-Host 'PASS rebuilt Altitude Angel plugin' -ForegroundColor Green
            } else {
                $failures.Add('rebuilt Altitude Angel plugin')
                Write-Host 'FAIL rebuilt Altitude Angel plugin' -ForegroundColor Red
            }
        }
        finally {
            $archive.Dispose()
        }
    } else {
        $failures.Add('package exists')
        Write-Host 'FAIL package exists' -ForegroundColor Red
    }
}

if ($failures.Count -gt 0) {
    throw ('V1.0.9 verification failed: ' + ($failures -join ', '))
}

Write-Host 'FMTPlanner V1.0.9 verification passed.' -ForegroundColor Cyan
