param([string]$Directory = 'bin/PageRefresh122/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$refreshType = $assembly.GetType('MissionPlanner.FmtPageParameterRefresh',$true)
$static = [Reflection.BindingFlags]'Static,NonPublic'
$bound = $refreshType.GetMethod('BoundNames',$static)
$root = New-Object Windows.Forms.Panel
try {
    foreach ($entry in @(@('MavlinkNumericUpDown','OLD_NAME','ATC_RAT_RLL_P'),@('MavlinkComboBox','OLD_OPTION','RC7_OPTION'),@('MavlinkCheckBox','CHECK','RC_OPTIONS'),@('MavlinkCheckBoxBitMask','MASK','RC_OPTIONS'))) {
        $editor = [Activator]::CreateInstance($assembly.GetType("MissionPlanner.Controls.$($entry[0])",$true))
        $editor.Name = $entry[1]; $editor.ParamName = $entry[2]; $root.Controls.Add($editor)
    }
    $actual = @($bound.Invoke($null,@($root.PSObject.BaseObject,$false)))
    if ($actual.Count -ne 3 -or $actual -contains 'OLD_NAME' -or $actual -notcontains 'RC7_OPTION') { throw "Incorrect bound targets: $actual" }
} finally { $root.Dispose() }

# Real WinForms row visibility: hidden/filter-excluded and scrolled-off rows must not be requested.
$form = New-Object Windows.Forms.Form
$grid = New-Object Windows.Forms.DataGridView
try {
    $form.StartPosition = 'Manual'; $form.Location = New-Object Drawing.Point -30000,-30000
    $form.Size = New-Object Drawing.Size 450,300
    $grid.Dock = 'Fill'; $grid.AllowUserToAddRows = $false
    [void]$grid.Columns.Add('Name','Name')
    foreach ($i in 0..199) { [void]$grid.Rows.Add("TEST_$i") }
    $grid.Rows[5].Visible = $false
    $form.Controls.Add($grid); $form.Show(); [Windows.Forms.Application]::DoEvents()
    $targets = @($refreshType.GetMethod('DisplayedRows',$static).Invoke($null,@($grid.PSObject.BaseObject,0)))
    if (!$targets.Count -or $targets.Count -ge 30 -or $targets -contains 'TEST_5' -or $targets -contains 'TEST_199') { throw "Grid scope failed: $targets" }
    $grid.FirstDisplayedScrollingRowIndex = 100
    [Windows.Forms.Application]::DoEvents()
    $targets = @($refreshType.GetMethod('DisplayedRows',$static).Invoke($null,@($grid.PSObject.BaseObject,0)))
    if ($targets -notcontains 'TEST_100' -or $targets -contains 'TEST_0') { throw 'Scrolled viewport not respected' }
} finally { $form.Dispose() }

# OSD page numbers can have gaps: selected tab 1 may actually be OSD3.
$osdAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'OSDConfigurator.dll'))
$osdType = $osdAssembly.GetType('OSDConfigurator.GUI.OSDUserControl',$true)
$settingInterface = $osdAssembly.GetType('OSDConfigurator.Models.IOSDSetting',$true)
$settingsType = [Collections.Generic.List``1].MakeGenericType(@($settingInterface))
$settings = [Activator]::CreateInstance($settingsType)
$settingType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigOSD+OSDSetting',$true)
foreach ($name in @('OSD_TYPE','OSD3_ENABLE','OSD6_ENABLE')) {
    $settings.Add([Activator]::CreateInstance($settingType,@($name,[double]1)))
}
$osd = [Activator]::CreateInstance($osdType)
try {
    $osd.ApplySettings($settings)
    $tabs = $osdType.GetField('tabControl',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($osd)
    $tabs.SelectedIndex = 1
    $names = @($osd.CurrentParameterNames)
    if ($names.Count -ne 1 -or $names[0] -ne 'OSD3_ENABLE') { throw "OSD tab scope wrong: $names" }
    $tabs.SelectedIndex = 2
    $osd.ApplySettings($settings)
    if ($tabs.SelectedIndex -ne 2 -or @($osd.CurrentParameterNames)[0] -ne 'OSD6_ENABLE') { throw 'OSD refresh lost selected screen' }
    $tabs.SelectedIndex = 0
    if (@($osd.CurrentParameterNames)[0] -ne 'OSD_TYPE') { throw 'OSD global settings leaked screen parameters' }
} finally { $osd.Dispose() }

# Run the production asynchronous batch loop with a fake reader: no port or vehicle required.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
public static class PageRefreshBatchProbe {
    public static void Run(Type type) {
        var method = type.GetMethod("ReadBatchAsync", BindingFlags.Static | BindingFlags.NonPublic);
        var seen = new List<string>();
        var clock = Stopwatch.StartNew();
        Func<string, Task> read = name => { seen.Add(name); return Task.FromResult(0); };
        Func<bool> current = () => true;
        var task = (Task)method.Invoke(null, new object[] { new[] { "A", "B", "C" }, read, current, CancellationToken.None, null });
        if (task.IsCompleted) throw new Exception("Batch blocked caller or skipped pacing");
        task.GetAwaiter().GetResult();
        if (seen.Count != 3 || clock.ElapsedMilliseconds < 850) throw new Exception("Sequential pacing failed");
        using (var cancel = new CancellationTokenSource()) {
            seen.Clear();
            read = name => { seen.Add(name); cancel.Cancel(); return Task.FromResult(0); };
            task = (Task)method.Invoke(null, new object[] { new[] { "A", "B" }, read, current, cancel.Token, null });
            try { task.GetAwaiter().GetResult(); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { }
            if (seen.Count != 1) throw new Exception("Read after cancellation");
        }
        seen.Clear(); bool same = true;
        read = name => { seen.Add(name); same = false; return Task.FromResult(0); };
        current = () => same;
        task = (Task)method.Invoke(null, new object[] { new[] { "A", "B" }, read, current, CancellationToken.None, null });
        try { task.GetAwaiter().GetResult(); throw new Exception("Target change ignored"); }
        catch (InvalidOperationException) { }
        if (seen.Count != 1) throw new Exception("Read wrong target");
        seen.Clear(); current = () => true;
        read = name => { seen.Add(name); throw new TimeoutException(); };
        task = (Task)method.Invoke(null, new object[] { new[] { "A", "B" }, read, current, CancellationToken.None, null });
        try { task.GetAwaiter().GetResult(); throw new Exception("Timeout ignored"); }
        catch (TimeoutException) { }
        if (seen.Count != 1) throw new Exception("Requests continued after failure");
    }
}
'@
[PageRefreshBatchProbe]::Run($refreshType)
$service = Get-Content (Join-Path $PSScriptRoot 'FmtPageParameterRefresh.cs') -Raw
if ($service -match '\.setParam\(') { throw 'Refresh must never write' }
$scoped = $service.Substring($service.IndexOf('internal static async Task RefreshAsync'))
if ($scoped -match 'getParamList\(') { throw 'Scoped refresh must not download full table' }
if ([regex]::Matches($service,'\.getParamList\(').Count -ne 1 -or !$service.Contains('link.LastParamListSucceeded')) { throw 'Explicit full reload/cancel guard missing' }
foreach ($required in @('Gate.Wait(0)','Gate.Release()','page.VisibleChanged += hidden','page.Disposed += disposed','vehicle.cs.armed','link.giveComport','link.IsLogDownloadActive','hasChanges','await Task.Run')) {
    if (!$service.Contains($required)) { throw "Missing refresh guard: $required" }
}
$configuration = Join-Path $PSScriptRoot '../GCSViews/ConfigurationView'
$fullDownloads = Get-ChildItem $configuration -Filter '*.cs' | Select-String -Pattern '\.getParamList\('
if ($fullDownloads) { throw "Configuration full downloads remain: $fullDownloads" }
$loading = Get-Content (Join-Path $configuration 'ConfigParamLoading.cs') -Raw
$raw = Get-Content (Join-Path $configuration 'ConfigRawParams.cs') -Raw
if (!$loading.Contains('FmtPageParameterRefresh.ReloadAll') -or !$raw.Contains('FmtPageParameterRefresh.ReloadAll')) { throw 'Loading/full parameter table must offer a real full reload' }
if (!$loading.Contains('TotalReported <= 0')) { throw 'Empty cache mistaken for completed download' }
$main = Get-Content (Join-Path $PSScriptRoot '../MainV2.cs') -Raw
if ($main -notmatch 'keyData == Keys.F5\)[\s\S]{0,160}FmtPageParameterRefresh.TryRefreshVisible') { throw 'F5 not scoped' }
$transport = Get-Content (Join-Path $PSScriptRoot '../ExtLibs/ArduPilot/Mavlink/MAVLinkInterface.cs') -Raw
if (!$transport.Contains('finally { Interlocked.Decrement(ref activeLogDownloads); }')) { throw 'Log busy state not cleared on completion/error' }
'PASS: bound-name dedup, visible rows and scrolling, OSD selected-tab isolation, asynchronous paced reads, cancellation, target change, timeout abort, read-only guards, no setup full downloads, scoped F5'
