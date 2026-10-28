param([string]$Directory = 'bin/Local124/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$mav = [MissionPlanner.MainV2]::comPort.MAV
$utilities = [Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Utilities.dll'))
$repository = $utilities.GetType('MissionPlanner.Utilities.ParameterMetaDataRepository',$true)
$get = $repository.GetMethod('GetParameterMetaData')
$paramType = $mav.param.GetType().BaseType.GetGenericArguments()[0]
$ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 } | Select-Object -First 1
$wire = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$statics = [Reflection.BindingFlags]'Static,NonPublic'
$originalVersion = $mav.VersionString
$originalFirmware = $mav.cs.firmware
$originalAircraftType = $mav.aptype
try {
    $mav.cs.firmware = [Enum]::Parse($originalFirmware.GetType(),'ArduCopter2')
    foreach ($version in @('4.6.3','4.7.2','4.6.1','4.7.1')) {
        $mav.VersionString = 'ArduCopter V' + $version
        $name = if ($version.StartsWith('4.6')) { 'WPNAV_SPEED' } else { 'WP_SPD' }
        $expected = if ($version.StartsWith('4.6')) { 'cm/s' } else { 'm/s' }
        foreach ($family in @('ArduCopter2','ArduCopter','Heli')) {
            if ($get.Invoke($null,@($name,'Units',$family)) -cne $expected) { throw "Host resolver used stale units: $version $family" }
        }
        if ($get.Invoke($null,@('WP_SPD','Units','Copter4.7.2')) -cne 'm/s') { throw 'Explicit snapshot changed with active vehicle' }
    }
    $mav.VersionString = 'ArduCopter V4.7.2'
    foreach ($frame in @(@('HELICOPTER','0.0 0.35'),@('QUADROTOR','0.01 0.5'),@('HELICOPTER','0.0 0.35'))) {
        $mav.aptype = [Enum]::Parse($mav.aptype.GetType(),$frame[0])
        if ($get.Invoke($null,@('ATC_RAT_RLL_P','Range','ArduCopter2')) -cne $frame[1]) { throw 'Active frame selected wrong PID range' }
        if ($get.Invoke($null,@('ATC_RAT_RLL_P','Range','Copter4.7.2')) -cne '0.01 0.5') { throw 'Active frame replaced explicit multirotor metadata' }
    }
    $mav.aptype = $originalAircraftType
    $heliType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigTradHeli4',$true)
    $heli = [Activator]::CreateInstance($heliType)
    try {
        $load = $heliType.GetMethod('LoadFmtHelicopterCommonSettings',$flags)
        foreach ($modern in @($false,$true)) {
            $mav.VersionString = if ($modern) { 'ArduCopter V4.7.2' } else { 'ArduCopter V4.6.3' }
            $mav.param.Clear()
            $names = if ($modern) { @('WP_SPD_UP','WP_SPD_DN','RTL_ALT_M') } else { @('WPNAV_SPEED_UP','WPNAV_SPEED_DN','RTL_ALT') }
            $raw = if ($modern) { @(2.5,1.5,15) } else { @(250,150,1500) }
            for ($i=0;$i -lt 3;$i++) { $mav.param.Add($ctor.Invoke(@($names[$i],[double]$raw[$i],$wire,$null))) }
            $load.Invoke($heli,@()) | Out-Null
            $fields = @('fmtClimbSpeed','fmtDescentSpeed','fmtRtlAltitude')
            $display = @(2.5,1.5,15)
            for ($i=0;$i -lt 3;$i++) {
                $control = $heliType.GetField($fields[$i],$flags).GetValue($heli)
                $key = $heliType.GetField(($fields[$i]+'Parameter'),$flags).GetValue($heli)
                if (!$control.Enabled -or $key -ne $names[$i] -or [Math]::Abs([double]$control.Value-$display[$i]) -gt 0.0001) { throw 'Heli name/display mismatch' }
                $convert = if ($i -eq 2) { 'FmtDistanceMetersToRaw' } else { 'FmtSpeedMetersPerSecondToRaw' }
                $method = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigFlightModes').GetMethod($convert,$statics)
                $wireValue = $method.Invoke($null,@($key,[double]$control.Value))
                if ([Math]::Abs($wireValue-$raw[$i]) -gt 0.0001) { throw 'Heli staged-unit conversion mismatch' }
            }
        }
        $mav.param.Clear(); $load.Invoke($heli,@()) | Out-Null
        if ($heliType.GetField('fmtClimbSpeed',$flags).GetValue($heli).Enabled) { throw 'Absent heli parameter enabled' }
    } finally { $heli.Dispose() }

    $osdType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigHWOSD',$true)
    $plan = $osdType.GetMethod('GetRateParameters',$statics)
    $legacy = @(); $modern = @()
    foreach ($index in @(0,1,3)) {
        foreach ($suffix in @('EXT_STAT','EXTRA1','EXTRA2','EXTRA3','POSITION','RAW_CTRL','RAW_SENS','RC_CHAN')) {
            $legacy += 'SR'+$index+'_'+$suffix
            $modern += 'MAV'+($index+1)+'_'+$suffix
        }
    }
    foreach ($pair in @(@($legacy,'Copter4.6.3'),@($modern,'Copter4.7.2'))) {
        $names = [string[]]$pair[0]
        $actual = @($plan.Invoke($null,(,$names)))
        if ($actual.Count -ne 24 -or (Compare-Object $actual $names)) { throw 'Wrong OSD stream instance mapping' }
        foreach ($name in $actual) {
            if (!$get.Invoke($null,@($name,'Description',$pair[1]))) { throw "OSD name not in release: $name" }
        }
    }
    $both = [string[]]($legacy+$modern+@('MAV3_EXTRA1','SR2_EXTRA1','UNKNOWN_PARAMETER'))
    $actual = @($plan.Invoke($null,(,$both)))
    if ($actual.Count -ne 24 -or (Compare-Object $actual $modern)) { throw 'OSD duplicates, unknown names or new port introduced' }
    $empty = [string[]]@()
    if (@($plan.Invoke($null,(,$empty))).Count) { throw 'OSD synthesized absent parameters' }
    $osd = [Activator]::CreateInstance($osdType)
    try { $osd.Activate(); if ($osd.Enabled) { throw 'Disconnected OSD writing enabled' } }
    finally { $osd.Dispose() }
    'PASS: actual host metadata resolver; all Copter aliases; 4.6/4.7 reconnect isolation; Heli display/write units; 48 OSD release keys; no synthetic writes'
} finally {
    $mav.param.Clear(); $mav.VersionString=$originalVersion; $mav.cs.firmware=$originalFirmware; $mav.aptype=$originalAircraftType
}
