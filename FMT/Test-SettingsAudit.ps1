param([string]$Directory = 'bin/SettingsAudit121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigPlanner', $true)
$page = [Activator]::CreateInstance($type)
$instance = [Reflection.BindingFlags]'Instance,NonPublic'
try {
    foreach ($name in @('CHK_speechArmedOnly','label8','label6','label1','label9','label10','label11','chk_norcreceiver','CHK_params_bg','chk_slowMachine','BUT_mapCacheDir')) {
        $control = $type.GetField($name,$instance).GetValue($page)
        if ($control.Text -notmatch '[\u3400-\u9fff]') { throw "Missing Chinese label: $name" }
    }
    $startup = $type.GetField('startup',$instance)
    $line = $type.GetField('num_linelength',$instance).GetValue($page)
    $startup.SetValue($page,$false)
    $line.Value = 725
    $settingsType = [AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetType('MissionPlanner.Utilities.Settings',$false) } | Where-Object { $_ } | Select-Object -First 1
    $settings = $settingsType.GetProperty('Instance').GetValue($null,$null)
    $indexer = $settingsType.GetProperties() | Where-Object { $_.Name -eq 'Item' -and $_.GetIndexParameters().Count -eq 1 -and $_.GetIndexParameters()[0].ParameterType -eq [string] } | Select-Object -First 1
    if ($indexer.GetValue($settings,@('GMapMarkerBase_length')) -ne '725') { throw 'Line length not persisted' }
    $startup.SetValue($page,$true)
    $line.Value = 500
    if ($indexer.GetValue($settings,@('GMapMarkerBase_length')) -ne '725') { throw 'Loading overwrote saved line length' }
    $page.CreateControl()
    $bitmap = New-Object Drawing.Bitmap $page.Width,$page.Height
    try { $page.DrawToBitmap($bitmap,$page.ClientRectangle); $bitmap.Save((Join-Path (Resolve-Path $Directory) 'settings-preview.png')) }
    finally { $bitmap.Dispose() }
} finally { $page.Dispose() }
$main = Get-Content (Join-Path $PSScriptRoot '../MainV2.cs') -Raw
foreach ($name in @('speechcustomtime','speechlowspeedtime')) {
    if (!$main.Contains("DateTime $name = DateTime.UtcNow;")) { throw "Non-UTC speech clock: $name" }
}
$help = Get-Content (Join-Path $PSScriptRoot '../GCSViews/Help.cs') -Raw
if ($help -match 'Utilities.Update\.(CheckForUpdate|DoUpdate|dobeta)') { throw 'Upstream updater still reachable from Help' }
$source = Get-Content (Join-Path $PSScriptRoot '../GCSViews/ConfigurationView/ConfigPlanner.cs') -Raw
foreach ($guard in @('CHK_beta.Visible = false','CHK_disttohomeflightdata.Visible = false','num_gcsid.Enabled = !MainV2.comPort.BaseStream.IsOpen','CHK_params_bg.Enabled = FMT.FmtRelayStationIdentity.StationNumber == 1')) {
    if (!$source.Contains($guard)) { throw "Missing settings guard: $guard" }
}
if ($source.Contains('"GMapMarkerBase_Length"')) { throw 'Inconsistent line length key' }
'PASS: settings labels, line length persistence/load guard, UTC speech clocks, update routing and settings safeguards'
