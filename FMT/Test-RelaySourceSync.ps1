param([string]$Directory = 'bin/RelayControlFix121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$mainType = $assembly.GetType('MissionPlanner.MainV2', $true)
$decode = $mainType.GetMethod('GetFmtControlSourceState', [Reflection.BindingFlags]'Static,NonPublic')
foreach ($pair in @(@(32,0),@(33,2),@(34,1),@(35,0),@(8193,2),@(8194,1),@(8480,0),@(8482,1))) {
    if ($decode.Invoke($null,@([int]$pair[0])) -ne $pair[1]) { throw 'Control source bit interpretation reversed' }
}
$source = Get-Content "$PSScriptRoot/../MainV2.cs" -Raw -Encoding UTF8
$align = $source.Substring($source.IndexOf('private bool TryAlignFmtGcsSystemId'))
$align = $align.Substring(0,$align.IndexOf('private void SetFmtControlSource'))
if ($align -match 'gcssysid\s*=(?!=)|Settings.Instance\[.*\]\s*=') { throw 'GCS ID validation must not change identity or settings' }
$mav = Get-Content "$PSScriptRoot/../ExtLibs/ArduPilot/Mavlink/MAVLinkInterface.cs" -Raw
$open = $mav.Substring($mav.IndexOf('public void Open(bool getparams'))
$open = $open.Substring(0,$open.IndexOf('MAVlist.Clear();'))
if (!$open.Contains('lastparamset = DateTime.MinValue;')) { throw 'Reconnect must clear delayed storage command' }
$observe = $source.Substring($source.IndexOf('private void UpdateFmtControlSourceButtons(bool connected)'))
$observe = $observe.Substring(0,$observe.IndexOf('private static void ApplyFmtControlSourceButtonState'))
if ($observe -match '\.setParam\s*\(|SetFmtControlSource\s*\(') { throw 'Connection/UI refresh must never write control-source parameters' }
if ($source.Contains('FmtControlSourceStartupDefaultApplied') -or $source.Contains('var receiverOptions =')) { throw 'Startup parameter rewrite returned' }
$switch = $source.Substring($source.IndexOf('private void SetFmtControlSource(bool groundControl)'))
$switch = $switch.Substring(0,$switch.IndexOf('private void MaintainFmtRelayControlParameters'))
foreach ($required in @('if (FmtControlSourceTransition)','if (readback != updated)','FmtControlSourceReadbackFailed = true','FmtAircraftGroundControlConfirmed = false','finally')) {
    if (!$switch.Contains($required)) { throw "Missing transition protection: $required" }
}
if ($switch.IndexOf('FmtGroundControlInputEnabled = groundControl') -lt $switch.IndexOf('if (readback != updated)')) { throw 'Output enabled before readback' }
if ($switch.Contains('FmtGroundControlInputEnabled = previousGroundGate')) { throw 'Failed switch restores stale output gate' }
$prefetch = $source.Substring($source.IndexOf('private static void PrefetchFmtRelayControlParameters'))
$prefetch = $prefetch.Substring(0,$prefetch.IndexOf('private bool IsFmtTraditionalHelicopter'))
if ($prefetch -match '\.setParam\s*\(') { throw 'Background parameter acquisition must remain read-only' }
foreach ($required in @('"RC_OPTIONS"','"SYSID_MYGCS"','"MAV_GCS_SYSID"','"_MIN"','"_MAX"','"_TRIM"','port.MAV.sysid != sysid')) {
    if (!$prefetch.Contains($required)) { throw "Missing background acquisition: $required" }
}
if (!$source.Contains('MaintainFmtRelayControlParameters(connected);') -or !$source.Contains('Interlocked.CompareExchange(ref FmtRelayParameterReadPending')) { throw 'Missing independent single-flight acquisition' }
$forwarding = Get-Content "$PSScriptRoot/../Controls/SerialOutputPass.cs" -Raw
if ([regex]::Matches($forwarding,'MainV2.FmtAircraftGroundControlConfirmed &&').Count -ne 2) { throw 'Both relay forwarding paths must respect confirmed aircraft control source' }
'PASS: source bit decoding; transaction/readback and failure latch guards; page-independent parameter acquisition; both relay forwarding gates (structural checks, no flight validation)'
