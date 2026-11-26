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
foreach($required in @('if (IsFmtAirspeedZeroRunning) return;', 'await Task.Run(() => SendFmtAirspeedZeroAsync(link, sysid, compid))',
    'await link.doCommandAsync(sysid, compid', '0, 0, 1, 0, 0, 0, 0)',
    'link.IsParameterListLoading', 'link.IsLogDownloadActive', 'link.ReadOnly',
    'link.MAV.cs.armed', 'link.sysidcurrent != sysid', 'link.compidcurrent != compid',
    'link.UnSubscribeToPacketType(subscription);', 'link.giveComport = false;', 'finally { IsFmtAirspeedZeroRunning = false; }',
    '同時更新氣壓計校正與空速計零點', 'link.logreadmode',
    'ex is TimeoutException', 'if (IsDisposed || Disposing) return;')) {
    if(!$body.Contains($required)) { throw "Missing calibration guard: $required" }
}
$uiBody=$body.Substring(0,$body.IndexOf('internal static async Task<'))
if($uiBody -match '\.doCommand\(|\.Wait\(|\.Result\b|setParam\(|getParamList\(') { throw 'Blocking call or unrelated parameter operation in airspeed action' }
$toolbar=Get-Content "$PSScriptRoot/../MainV2.cs" -Raw
if(!$toolbar.Contains('FlightData?.IsFmtAirspeedZeroRunning != true')) { throw 'Toolbar permits repeat calibration' }
'PASS: compiled async handler; background transport, pinned target, busy/armed/read-only guards, timeout notice and cleanup verified structurally; no flight controller commands sent'
