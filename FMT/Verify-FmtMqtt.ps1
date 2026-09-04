param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$artifactDirectory = Join-Path $ProjectRoot 'tmp\mqtt-tests'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $installation 'MSBuild\Current\Bin\Roslyn\csc.exe'
$json = Join-Path $ProjectRoot 'bin\Release\net461\Newtonsoft.Json.dll'
$testExe = Join-Path $artifactDirectory 'FmtMqttHarness.exe'
$sourceFiles = @('FMT\FmtVisualAssets.cs', 'FMT\FmtMqttTraffic.cs', 'FMT\FmtMqttTrafficIndicator.cs', 'FMT\FmtMapOptionsLayout.cs', 'FMT\FmtConnectionCloseGuard.cs', 'FMT\FmtMqttSettings.cs', 'FMT\FmtMqttClient.cs', 'FMT\FmtMqttBridge.cs', 'FMT\FmtMqttPanel.cs', 'MissionPlannerTests\FmtMqttHarness.cs') | ForEach-Object { Join-Path $ProjectRoot $_ }
$mqttImage = Join-Path $ProjectRoot 'FMT\Assets\mqtt-toolbar-transparent.png'
& $compiler /nologo /target:exe /langversion:7.3 "/out:$testExe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Security.dll "/reference:$json" "/resource:$mqttImage,MissionPlanner.FMT.Assets.mqtt-toolbar-transparent.png" $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'MQTT regression harness compile failed.' }
Copy-Item -LiteralPath $json -Destination $artifactDirectory -Force
& $testExe $artifactDirectory
if ($LASTEXITCODE -ne 0) { throw 'MQTT regression harness failed.' }

$flightData = Get-Content -LiteralPath (Join-Path $ProjectRoot 'GCSViews\FlightData.cs') -Raw
foreach ($required in @('Text = "MQTT 連線"', 'CB_tuning, chkFmt3DMap, chkFmtMqtt, chkFmtSafety', 'splitContainer1.Panel1.Controls.Add(fmtMqttPanel)', 'fmtMqttPanel.Visible = showMqtt', 'SetFmtEmbeddedPanelHeight();')) {
    if (!$flightData.Contains($required)) { throw "Missing embedded integration: $required" }
}
Write-Host 'PASS FlightData shared 3D/MQTT/tuning panel integration'
$mqttPanel = Get-Content -LiteralPath (Join-Path $ProjectRoot 'FMT\FmtMqttPanel.cs') -Raw
$mqttSettings = Get-Content -LiteralPath (Join-Path $ProjectRoot 'FMT\FmtMqttSettings.cs') -Raw
foreach ($required in @('FmtMqttSettings.LoadRemembered()', 'if (remember.Checked) settings.SaveRemembered(password);',
    'else FmtMqttSettings.ClearRemembered();')) {
    if (!$mqttPanel.Contains($required)) { throw "Missing opt-in MQTT persistence behavior: $required" }
}
if ($mqttPanel.Contains('settings.Save();') -or !$mqttSettings.Contains('remember-settings.optin') -or
    !$mqttSettings.Contains('if (!File.Exists(RememberConsentPath) || !File.Exists(SettingsPath)) return new FmtMqttSettings();')) {
    throw 'MQTT settings must remain blank unless the user explicitly opts in to remembering them.'
}
Write-Host 'PASS MQTT connection data is blank by default and persistence requires explicit opt-in'
if (!$flightData.Contains('new RowStyle(SizeType.Absolute, FmtMapOptionsLayout.RowHeight)') -or
    !$flightData.Contains('FmtMapOptionsLayout.Configure(panel1, coords1,') -or
    $flightData.Contains('tableMap.RowStyles[2].Height =')) { throw 'Map option row must remain fixed, independent of content resize.' }
Write-Host 'PASS map option row uses fixed height with no content-to-row feedback'

$main = Get-Content -LiteralPath (Join-Path $ProjectRoot 'MainV2.cs') -Raw
$timeIndex = $main.IndexOf('MainMenu.Items.Add(MenuFmtFlightTime);')
$mqttIndex = $main.IndexOf('MainMenu.Items.Add(MenuFmtMqttTraffic);')
$rpmIndex = $main.IndexOf('MainMenu.Items.Add(MenuFmtRotorRpm);')
if ($timeIndex -lt 0 -or $mqttIndex -le $timeIndex -or $rpmIndex -le $mqttIndex -or
    !$main.Contains('() => FlightData?.MqttTrafficSnapshot ?? default(FmtMqttTrafficSnapshot)') -or
    !$flightData.Contains('fmtMqttPanel?.TrafficSnapshot ?? default(FmtMqttTrafficSnapshot)') -or
    !$main.Contains('MenuFmtMqttTraffic?.Dispose();')) { throw 'MQTT toolbar order, lifetime, or snapshot integration missing.' }
Write-Host 'PASS main toolbar right-aligned order: RPM -> MQTT -> flight time; independent snapshot and cleanup'
$closeStart = $main.IndexOf('protected override void OnFormClosing(FormClosingEventArgs e)')
if ($closeStart -lt 0) { throw 'Missing main form closing override.' }
$guard = $main.IndexOf('fmtConnectionCloseGuard.ShouldCancel', $closeStart)
$cleanup = $main.IndexOf('LayoutChanged -= updateLayout;', $closeStart)
if ($guard -le $closeStart -or $cleanup -le $guard) { throw 'Close confirmation must precede connection cleanup.' }
$closing = $main.Substring($closeStart, $cleanup - $closeStart)
foreach ($required in @('if (e.Cancel) return;', 'e.Cancel = true;', 'MessageBoxButtons.YesNo', 'MessageBoxDefaultButton.Button2', 'FlightData?.HasRunningMqttBridge == true')) {
    if (!$closing.Contains($required)) { throw "Missing close guard behavior: $required" }
}
if (!$main.Contains('Comports.ToArray().Any(port => port?.BaseStream?.IsOpen == true)')) { throw 'Close guard must inspect all telemetry connections.' }
Write-Host 'PASS main window confirmation precedes cleanup, defaults to No, covers all telemetry and hidden MQTT'
