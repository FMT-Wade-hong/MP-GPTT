param([string]$Directory = 'bin/TimeSync122/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.FMT.FmtConnectionTimeSync',$true)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$create=$type.GetMethod('CreateMessage',$flags)
$utc=[DateTime]::SpecifyKind([DateTime]'2026-10-10T00:00:00',[DateTimeKind]::Utc)
$message=$create.Invoke($null,@($utc))
$epoch=[DateTime]::SpecifyKind([DateTime]'1970-01-01',[DateTimeKind]::Utc)
if($message.time_unix_usec -ne [uint64](($utc.Ticks-$epoch.Ticks)/10) -or $message.time_boot_ms -ne 0) { throw 'Incorrect UTC microseconds or boot time' }
$rejected=$false
try { $create.Invoke($null,@([DateTime]::SpecifyKind($utc,[DateTimeKind]::Unspecified))) | Out-Null } catch { $rejected=$true }
if(!$rejected) { throw 'Ambiguous non-UTC time accepted' }
$sessionType=$type.GetNestedType('Session',[Reflection.BindingFlags]::NonPublic)
$session=[Activator]::CreateInstance($sessionType,$true)
$instance=[Reflection.BindingFlags]'Instance,NonPublic'
$begin=$sessionType.GetMethod('Begin',$instance)
$claim=$sessionType.GetMethod('Claim',$instance)
$first=$begin.Invoke($session,@())
if(!$claim.Invoke($session,@($first)) -or $claim.Invoke($session,@($first))) { throw 'Session must schedule exactly once' }
$second=$begin.Invoke($session,@())
if($claim.Invoke($session,@($first))) { throw 'Old connection accepted' }
if(!$claim.Invoke($session,@($second)) -or $claim.Invoke($session,@($second))) { throw 'Reconnect must permit one new sync' }
$source=Get-Content "$PSScriptRoot/FmtConnectionTimeSync.cs" -Raw
foreach($guard in @('key.CommsClose +=','Task.Run(async () =>','link.logreadmode','link.ReadOnly','link.IsParameterListLoading','link.IsLogDownloadActive','link.compidcurrent != compid','link.sysidcurrent != sysid','time_boot_ms = 0')) {
    if(!$source.Contains($guard)) { throw "Missing guard $guard" }
}
if(([regex]::Matches($source,'link\.sendPacket\(')).Count -ne 1 -or $source -match 'setParam\(|doCommand\(|getParamList\(') { throw 'Unexpected connection write operation' }
'PASS: UTC microseconds, zero boot time, non-UTC rejection, once per connection, stale generation/reconnect and source guards; no packets sent'
