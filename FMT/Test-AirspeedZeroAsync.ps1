param([string]$Directory = 'bin/AirspeedFix122/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.GCSViews.FlightData',$true)
$method=$type.GetMethod('ExecuteFmtAirspeedZero',[Reflection.BindingFlags]'Instance,NonPublic')
if(!$method.IsDefined([Runtime.CompilerServices.AsyncStateMachineAttribute],$false)) { throw 'Airspeed handler must be asynchronous' }
$source=Get-Content "$PSScriptRoot/../GCSViews/FlightData.cs" -Raw
$start=$source.IndexOf('internal async void ExecuteFmtAirspeedZero()')
$end=$source.IndexOf('internal void ExecuteFmtQnh()', $start)
$body=$source.Substring($start,$end-$start)
foreach($required in @('if (IsFmtAirspeedZeroRunning) return;', 'await Task.Run(async () =>',
    'await link.doCommandAsync(sysid, compid', '0, 0, 0, 0, 0, 2, 0)',
    'link.IsParameterListLoading', 'link.IsLogDownloadActive', 'link.ReadOnly',
    'link.MAV.cs.armed', 'link.sysidcurrent != sysid', 'link.compidcurrent != compid',
    'finally { link.giveComport = false; }', 'finally { IsFmtAirspeedZeroRunning = false; }',
    'ex is TimeoutException', 'if (IsDisposed || Disposing) return;')) {
    if(!$body.Contains($required)) { throw "Missing calibration guard: $required" }
}
if($body -match '\.doCommand\(|\.Wait\(|\.Result\b|setParam\(|getParamList\(') { throw 'Blocking call or unrelated parameter operation in airspeed action' }
$toolbar=Get-Content "$PSScriptRoot/../MainV2.cs" -Raw
if(!$toolbar.Contains('FlightData?.IsFmtAirspeedZeroRunning != true')) { throw 'Toolbar permits repeat calibration' }
'PASS: compiled async handler; background transport, pinned target, busy/armed/read-only guards, timeout notice and cleanup verified structurally; no flight controller commands sent'
