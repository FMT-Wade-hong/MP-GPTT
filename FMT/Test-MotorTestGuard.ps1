param([string]$Directory = 'bin/MotorGuard124/net461')
$ErrorActionPreference = 'Stop'
$binary = (Resolve-Path $Directory).Path
$compiler = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$testExe = Join-Path $binary 'MotorTestGuardHarness.exe'
& $compiler /nologo /target:exe "/out:$testExe" /r:System.Core.dll "$PSScriptRoot/../MissionPlannerTests/MotorTestGuardHarness.cs"
if ($LASTEXITCODE) { throw 'Motor test harness compilation failed' }
& $testExe (Join-Path $binary 'FMTPlanner.exe')
if ($LASTEXITCODE) { throw 'Motor test runner checks failed' }

. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$static = [Reflection.BindingFlags]'Static,NonPublic'
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigMotorTest',$true)
$mav = [MissionPlanner.MainV2]::comPort.MAV
$oldType = $mav.aptype
$mav.aptype = [Enum]::Parse($oldType.GetType(),'GROUND_ROVER')
$page = [Activator]::CreateInstance($type)
$secondPage = [Activator]::CreateInstance($type)
$gate = $type.GetField('MotorTests',$static).GetValue($null)
$begin = $gate.GetType().GetMethod('TryBegin',$flags)
$update = $type.GetMethod('UpdateFmtMotorButtons',$flags)
$operation = $null
try {
    $page.Activate(); $secondPage.Activate()
    $operation = $begin.Invoke($gate,@())
    if (!$operation) { throw 'Motor gate unexpectedly busy' }
    $update.Invoke($page,@()) | Out-Null; $update.Invoke($secondPage,@()) | Out-Null
    foreach ($candidate in @($page,$secondPage)) {
        $rows = $type.GetField('fmtMotorRows',$flags).GetValue($candidate)
        $buttons = @($rows.Controls | ForEach-Object { $_.Controls } | Where-Object { $_ -is [Windows.Forms.Button] })
        if ($buttons.Count -ne 4 -or @($buttons | Where-Object { $_.Enabled }).Count) { throw 'Individual motor buttons remain enabled' }
        foreach ($name in @('fmtTestAllButton','fmtTestSequenceButton','NUM_thr_percent','NUM_duration','but_mot_spin_arm','but_mot_spin_min')) {
            if ($type.GetField($name,$flags).GetValue($candidate).Enabled) { throw "Busy control remains enabled: $name" }
        }
        if (!$type.GetField('fmtStopAllButton',$flags).GetValue($candidate).Enabled) { throw 'Stop is unavailable during a pending test' }
        for ($i=0;$i -lt 100;$i++) {
            $task = $type.GetMethod('RunMotorTestAsync',$flags).Invoke($candidate,@('single',1))
            if (!$task.IsCompleted -or $task.IsFaulted) { throw 'Repeated UI action queued work' }
        }
    }
    $type.GetMethod('but_StopAll',$flags).Invoke($secondPage,@($null,[EventArgs]::Empty)) | Out-Null
    $update.Invoke($page,@()) | Out-Null
    if ($type.GetField('fmtStopAllButton',$flags).GetValue($page).Enabled) { throw 'Repeated stop clicks not blocked' }
    $page.Activate()
    if ($type.GetField('fmtTestAllButton',$flags).GetValue($page).Enabled) { throw 'Reopening page bypassed busy gate' }
    $operation.Dispose(); $operation = $null
    $update.Invoke($page,@()) | Out-Null
    if (!$type.GetField('fmtTestAllButton',$flags).GetValue($page).Enabled -or !$type.GetField('fmtStopAllButton',$flags).GetValue($page).Enabled) { throw 'Buttons did not recover after completion' }
    foreach ($name in @('but_Click','but_TestAll','but_TestAllSeq','but_StopAll','RunMotorTestAsync')) {
        if (!$type.GetMethod($name,$flags).IsDefined([Runtime.CompilerServices.AsyncStateMachineAttribute],$false)) { throw "Blocking handler: $name" }
    }
} finally {
    if ($operation) { $operation.Dispose() }
    $page.Deactivate(); $secondPage.Deactivate(); $page.Dispose(); $secondPage.Dispose(); $mav.aptype=$oldType
}
$source = Get-Content "$PSScriptRoot/../GCSViews/ConfigurationView/ConfigMotorTest.cs" -Raw
$body = $source.Substring($source.IndexOf('private async Task RunMotorTestAsync'),$source.IndexOf('private void linkLabel1_LinkClicked')-$source.IndexOf('private async Task RunMotorTestAsync'))
foreach ($required in @('if (operation == null) return;', 'command => Task.Run(async () =>', 'await link.doCommandAsync',
    'operation.StopRequested || operation.Cancelled', 'finally { link.giveComport = false; }', 'operation.Dispose();',
    'ReferenceEquals(target, link.MAV)', 'ReferenceEquals(stream, link.BaseStream)', 'link.ReadOnly', 'link.logreadmode',
    'link.CommsClose += disconnected', 'link.CommsClose -= disconnected', 'Volatile.Read(ref connectionLost) == 0')) {
    if (!$body.Contains($required)) { throw "Missing motor command guard: $required" }
}
if ($body -match '\.doCommand\(|\.Wait\(|\.Result\b|Task.WhenAny') { throw 'Blocking call or premature timeout unlock in motor command flow' }
if ($body.Contains('target.cs.armed')) { throw 'Motor-test armed heartbeat must not cancel the remaining batch' }
'PASS: real pages share busy gate; 200 forced repeated clicks ignored; all start/settings controls disabled; stop available/coalesced; reactivation cannot bypass; buttons recover; async handlers and target/transport cleanup verified; no vehicle commands sent'
