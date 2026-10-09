param([string]$Directory = 'bin/IceToolbar121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-AirspeedZeroVisibility.ps1" -Directory $Directory
$main = $assembly.GetType('MissionPlanner.MainV2')
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$enabled = $main.GetMethod('IsFmtIceEnabled',$flags)
foreach ($value in @(-1,0,1)) {
    $p = [Activator]::CreateInstance($listType)
    $p.Add($ctor.Invoke(@('ICE_ENABLE',[double]$value,$wireType,$null)))
    if ($enabled.Invoke($null,[object[]]@(,$p)) -ne ($value -gt 0)) { throw 'ICE_ENABLE mismatch' }
}
if ($enabled.Invoke($null,[object[]]@(,$null))) { throw 'Null params enabled ICE' }
$fresh = $main.GetMethod('IsFmtIcePacketFresh',$flags)
$now = [DateTime]::UtcNow
foreach ($age in @(-1,0,2,3,4,100)) {
    if ($fresh.Invoke($null,@($now.AddSeconds(-$age),$now)) -ne ($age -ge 0 -and $age -le 3)) {
        throw "Freshness mismatch: $age"
    }
}
$rotation = $main.GetMethod('GetFmtIceRotation',$flags)
$mavType = $rotation.GetParameters()[0].ParameterType
$mav = [Activator]::CreateInstance($mavType,[object[]]@($null,[byte]0,[byte]0))
$mav.param.Add($ctor.Invoke(@('ICE_RPM_CHAN',[double]2,$wireType,$null)))
if ($null -ne $rotation.Invoke($null,@($mav,$now))) { throw 'Missing telemetry treated as stopped' }
$mavAssembly = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetType('MAVLink+MavlinkParse') }
$parser = [Activator]::CreateInstance($mavAssembly.GetType('MAVLink+MavlinkParse'),[object[]]@($false))
$rpmType = $mavAssembly.GetType('MAVLink+mavlink_rpm_t')
$idType = $mavAssembly.GetType('MAVLink+MAVLINK_MSG_ID')
$messageType = $mavAssembly.GetType('MAVLink+MAVLinkMessage')
foreach ($case in @(@(0,2000,0,$true),@(2000,0,0,$false),@(0,2000,10,$null))) {
    $rpm = [Activator]::CreateInstance($rpmType)
    $rpm.rpm1 = [single]$case[0]
    $rpm.rpm2 = [single]$case[1]
    $bytes = $parser.GenerateMAVLinkPacket20([Enum]::Parse($idType,'RPM'),$rpm,$false,[byte]0,[byte]0,-1)
    $msg = [Activator]::CreateInstance($messageType,[object[]]@([byte[]]$bytes,$now.AddSeconds(-$case[2])))
    $mav.addPacket($msg)
    if ($rotation.Invoke($null,@($mav,$now)) -ne $case[3]) { throw 'RPM channel/freshness mismatch' }
}
$source = Get-Content (Join-Path $PSScriptRoot '../MainV2.cs') -Raw
foreach ($required in @(
    'MainMenu.Items.Insert(quickActionIndex + 3, MenuFmtIceEngine)',
    'MAVLink.MAV_CMD.DO_ENGINE_CONTROL, start ? 1 : 0, 0, 0, 0, 0, 0, 0',
    '!port.logreadmode', 'FmtRelayControlService.CanLocalStationTransmitControl',
    'if (start) FmtIceRequestedState = true;',
    'MessageBoxButtons.YesNo) != (int)DialogResult.Yes'
)) {
    if (!$source.Contains($required)) { throw "Missing safeguard: $required" }
}
'PASS: ICE enable, fresh/missing/stale RPM, channel selection, toolbar placement and command safeguards (offline)'
