param([string]$Directory='bin/CaaOffline123/net461', [switch]$Online)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.FMT.TaiwanCaaAirspace',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$parse=$type.GetMethod('ParseSnapshot',$flags)
$stream=$assembly.GetManifestResourceStream('MissionPlanner.FMT.TaiwanCAA.gz')
if(!$stream) { throw 'Offline resource missing' }
$snapshot=$parse.Invoke($null,[object[]]@($stream,$true))
$zones=$snapshot.GetType().GetField('Zones',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($snapshot)
if($zones.Count -lt 5000) { throw 'Incomplete bundled snapshot' }
$type.GetField('currentSnapshot',$flags).SetValue($null,$snapshot)
$method=$type.GetMethod('LoadNearbyAsync',$flags)
$pointType=$method.GetParameters()[0].ParameterType
foreach($coords in @(@(25.03,121.56),@(23.46,120.3),@(22.63,120.3),@(23.57,119.57))) {
    $point=[Activator]::CreateInstance($pointType,[object[]]$coords)
    $task=$method.Invoke($null,[object[]]@($point))
    $result=$task.GetAwaiter().GetResult()
    if($result.Count -eq 0) { throw "Offline region empty: $coords" }
    Write-Output "Offline region $coords : $($result.Count) zones"
}
$invalid=[IO.MemoryStream]::new([byte[]]@(1,2,3))
$displayMethod=$type.GetMethod('LoadDisplayRadiusAsync',$flags)
if($displayMethod) {
    $center=[Activator]::CreateInstance($pointType,[object[]]@(25.03,121.56))
    $small=$displayMethod.Invoke($null,[object[]]@($center,[double]5000)).GetAwaiter().GetResult()
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $large=$displayMethod.Invoke($null,[object[]]@($center,[double]500000)).GetAwaiter().GetResult()
    if($large.Count -le $small.Count -or $large.Count -lt 4000) { throw '500 km still limited to local cell' }
    Write-Output "PASS: Taipei 5 km=$($small.Count), 500 km=$($large.Count); background filtering $($timer.ElapsedMilliseconds) ms (not map render time)"
}
$rejected=$false
try { $parse.Invoke($null,[object[]]@($invalid,$false)) | Out-Null } catch { $rejected=$true }
if(!$rejected) { throw 'Corrupt snapshot accepted' }
$cancel=[Threading.CancellationTokenSource]::new(); $cancel.Cancel()
$cancelled=$false
try { $type.GetMethod('UpdateOfflineAsync',$flags).Invoke($null,[object[]]@($null,$cancel.Token)).GetAwaiter().GetResult() } catch { $cancelled=$true }
finally { $cancel.Dispose() }
if(!$cancelled -or $type.GetField('updating',$flags).GetValue($null) -ne 0) { throw 'Cancelled update did not release gate' }
if($type.GetField('currentSnapshot',$flags).GetValue($null) -ne $snapshot) { throw 'Cancelled update replaced good snapshot' }
if($Online) {
    $utilities=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Utilities.dll'))
    $settingsType=$utilities.GetType('MissionPlanner.Utilities.Settings',$true)
    $dataField=$settingsType.GetField('CustomUserDataDirectory')
    $previous=$dataField.GetValue($null)
    $isolated=Join-Path $binaryDirectory ('caa-update-test-'+[Guid]::NewGuid().ToString('N'))
    $dataField.SetValue($null,$isolated)
    try {
        $type.GetMethod('UpdateOfflineAsync',$flags).Invoke($null,[object[]]@($null,[Threading.CancellationToken]::None)).GetAwaiter().GetResult()
        $path=$type.GetProperty('SnapshotPath',$flags).GetValue($null)
        if(!$path.StartsWith($isolated) -or !(Test-Path $path)) { throw 'Isolated update did not save snapshot' }
        $type.GetField('currentSnapshot',$flags).SetValue($null,$null)
        $loaded=$type.GetMethod('GetSnapshotAsync',$flags).Invoke($null,@()).GetAwaiter().GetResult()
        if($loaded.GetType().GetField('Bundled',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($loaded)) { throw 'Saved update did not reload offline' }
        Write-Output "PASS: real online update and offline reload saved only in $isolated"
    } finally { $dataField.SetValue($null,$previous) }
}
'PASS: embedded full Taiwan snapshot, four offline regions, corrupt input rejected, pre-cancel preserves snapshot and unlocks update. Production user cache unchanged; network used only with -Online.'
