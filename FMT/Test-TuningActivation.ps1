param([string]$Directory='bin/ParameterWriteFix/net472')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$mav=[MissionPlanner.MainV2]::comPort.MAV
$link=[MissionPlanner.MainV2]::comPort
$comms=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Comms.dll'))
$stream=[Activator]::CreateInstance($comms.GetType('MissionPlanner.Comms.CommsInjection',$true))
$link.BaseStream=$stream
$type=$assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArducopter',$true)
$page=[Activator]::CreateInstance($type)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$validate=[Windows.Forms.NumericUpDown].GetMethod('ValidateEditText',$flags)
$changes=$type.GetField('changes',$flags).GetValue($page)
$paramType=$mav.param.GetType().BaseType.GetGenericArguments()[0]
$ctor=$paramType.GetConstructors() | Where-Object {$_.GetParameters().Count -eq 4} | Select-Object -First 1
$wire=[Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$utility=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Utilities.dll'))
$metadata=$utility.GetType('MissionPlanner.Utilities.ParameterMetaDataRepository',$true)
$get=$metadata.GetMethod('GetParameterMetaData')
$count=0
try {
    foreach($name in @($null,'',' ')) {
        if($get.Invoke($null,@($name,'Description','Copter4.7.2')) -ne ''){throw 'Unbound metadata must be empty'}
    }
    foreach($case in @(@('ArduCopter2','4.6.3','QUADROTOR'),@('ArduCopter2','4.7.2','QUADROTOR'),
        @('ArduCopter2','4.7.2','HELICOPTER'),@('ArduPlane','4.7.2','VTOL_QUADROTOR'))) {
        $quad=$case[0] -eq 'ArduPlane'; $modern=$case[1] -eq '4.7.2'
        $mav.cs.firmware=[Enum]::Parse($mav.cs.firmware.GetType(),$case[0])
        $mav.VersionString=if($quad){'ArduPlane V'+$case[1]}else{'ArduCopter V'+$case[1]}
        $mav.aptype=[Enum]::Parse($mav.aptype.GetType(),$case[2])
        $mav.param.Clear()
        $prefix=if($quad){'Q_A_'}else{'ATC_'}
        $position=if($modern){if($quad){'Q_P_NE_POS_P'}else{'PSC_NE_POS_P'}}else{'PSC_POSXY_P'}
        $speed=if($quad){'Q_WP_SPD'}elseif($modern){'WP_SPD'}else{'WPNAV_SPEED'}
        $fields=@(@(($prefix+'ANG_RLL_P'),4,5,5),@(($prefix+'ANG_PIT_P'),3,4,4),
            @(($prefix+'RAT_RLL_P'),.028,.03,.03),@($position,1,1.2,1.2),
            @($speed,$(if($modern){10}else{1000}),9.5,$(if($modern){9.5}else{950})))
        if($quad){$mav.param.Add($ctor.Invoke(@('Q_ENABLE',[double]1,$wire,$null)))}
        foreach($field in $fields){$mav.param.Add($ctor.Invoke(@([string]$field[0],[double]$field[1],$wire,$null)))}
        $mav.param.Add($ctor.Invoke(@('INS_HNTCH_ENABLE',[double]1,$wire,$null)))
        $page.Activate()
        if($type.GetField('startup',$flags).GetValue($page) -or $changes.Count){throw 'Activation left startup/staged state'}
        foreach($field in $fields) {
            $editor=$page.Controls.Find($field[0],$true)[0]
            if(!$editor.Enabled){throw ('Supported editor disabled: '+$field[0])}
            $beforeEdit=[double]$mav.param[$field[0]].Value
            $editor.Text=([double]$field[2]).ToString('G',[Globalization.CultureInfo]::CurrentCulture)
            [void]$validate.Invoke($editor,@())
            if(!$changes.ContainsKey($field[0]) -or [Math]::Abs([double]$changes[$field[0]]-$field[3]) -gt .00001 -or $editor.BackColor -ne [Drawing.Color]::Green){throw ('Edit not recorded/highlighted: '+$field[0])}
            if([double]$mav.param[$field[0]].Value -ne $beforeEdit){throw 'Editing changed the FC cache'}
            $count++
        }
        $mav.param.Clear()
        if($quad){$mav.param.Add($ctor.Invoke(@('Q_ENABLE',[double]1,$wire,$null)))}
        foreach($field in $fields){$mav.param.Add($ctor.Invoke(@([string]$field[0],[double]$field[1],$wire,$null)))}
        $page.Activate()
        $missing=$type.GetField('INS_HNTCH_ENABLE',$flags).GetValue($page)
        if($missing.Enabled -or $null -ne $missing.ParamName -or $null -ne $missing.DataSource -or $changes.Count){throw 'Missing combo retained a stale binding or edit'}
        $page.Deactivate()
    }
} finally {$page.Dispose(); $stream.Dispose()}
"PASS: $count real typed/highlighted edits after complete page activation across 4.6/4.7 Copter, Heli and QuadPlane; unbound metadata; missing/rebound option; no FC cache mutation."
