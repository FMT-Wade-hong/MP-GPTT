param([string]$Directory = 'bin/ArduPilot472Fix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArducopter',$true)
$page = [Activator]::CreateInstance($type)
$bind = $type.GetMethod('BindPositionControls',$flags)
$listType = $bind.GetParameters()[0].ParameterType
$paramType = $listType.BaseType.GetGenericArguments()[0]
$ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 } | Select-Object -First 1
$wire = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$startup = $type.GetField('startup',$flags)
$changes = $type.GetField('changes',$flags).GetValue($page)
$validate = [Windows.Forms.NumericUpDown].GetMethod('ValidateEditText',$flags)
$refresh = $type.GetMethod('GetBoundRefreshParameters',[Reflection.BindingFlags]'Static,NonPublic')
$count = 0
try {
    # Explicit expectations from the 4.7.2 AC_PosControl definitions. Each test
    # exercises the real editor's typed input and the page's pending-write event.
    $cases = @(
        @('HLD_LAT_P','PSC_NE_POS_P',1,1,1.25,1.25),
        @('HLD_LAT_P','Q_P_NE_POS_P',1,1,1.25,1.25),
        @('LOITER_LAT_IMAX','PSC_NE_VEL_IMAX',10,10,8.5,8.5),
        @('LOITER_LAT_IMAX','Q_P_NE_VEL_IMAX',10,10,8.5,8.5),
        @('THR_ACCEL_IMAX','PSC_D_ACC_IMAX',0.8,0.8,0.65,0.65),
        @('THR_ACCEL_IMAX','Q_P_D_ACC_IMAX',0.8,0.8,0.65,0.65)
    )
    foreach ($name in @('HLD_LAT_P','POS_XY_P','PSC_POSXY_P','Q_P_POSXY_P')) {
        $cases += ,@('HLD_LAT_P',$name,1,1,1.25,1.25)
    }
    foreach ($name in @('LOITER_LAT_IMAX','VEL_XY_IMAX','PSC_VELXY_IMAX','Q_P_VELXY_IMAX')) {
        $cases += ,@('LOITER_LAT_IMAX',$name,500,50,40,400)
    }
    foreach ($name in @('THR_ACCEL_IMAX','ACCEL_Z_IMAX','PSC_ACCZ_IMAX','Q_P_ACCZ_IMAX')) {
        $cases += ,@('THR_ACCEL_IMAX',$name,800,80,65,650)
    }
    foreach ($case in $cases) {
        $list = [Activator]::CreateInstance($listType)
        $list.Add($ctor.Invoke(@([string]$case[1],[double]$case[2],$wire,$null)))
        $startup.SetValue($page,$true); $changes.Clear()
        $bind.Invoke($page,(,$list)) | Out-Null
        $editor = $type.GetField($case[0],$flags).GetValue($page)
        if (!$editor.Enabled -or $editor.ParamName -ne $case[1] -or [Math]::Abs([double]$editor.Value - $case[3]) -gt 0.0001) { throw "Incorrect display: $($case[1]) = $($editor.Value)" }
        if ($changes.Count) { throw "Binding staged a write: $($case[1])" }
        $startup.SetValue($page,$false)
        $editor.Text = ([double]$case[4]).ToString('G',[Globalization.CultureInfo]::CurrentCulture)
        $validate.Invoke($editor,@()) | Out-Null
        $expectedCount = if ($case[1] -eq 'LOITER_LAT_IMAX') { 2 } else { 1 }
        if ($changes.Count -ne $expectedCount -or !$changes.ContainsKey($case[1]) -or [Math]::Abs([double]$changes[$case[1]] - $case[5]) -gt 0.0001) { throw "Wrong staged write: $($case[1]) = $($changes[$case[1]])" }
        if ($expectedCount -eq 2 -and $changes['LOITER_LON_IMAX'] -ne $case[5]) { throw 'Legacy LAT/LON pairing changed' }
        if ([Math]::Abs([double]$list[$case[1]].Value - $case[2]) -gt 0.0001) { throw 'Parameter cache mutated' }
        if (@($refresh.Invoke($null,@($page))) -notcontains $case[1]) { throw 'Refresh used an old parameter name' }
        $count++
    }
    $startup.SetValue($page,$true); $changes.Clear()
    $empty = [Activator]::CreateInstance($listType)
    $bind.Invoke($page,(,$empty)) | Out-Null
    foreach ($field in @('HLD_LAT_P','LOITER_LAT_IMAX','THR_ACCEL_IMAX')) {
        if ($type.GetField($field,$flags).GetValue($page).Enabled) { throw "Missing parameter enabled: $field" }
    }
    if ($changes.Count) { throw 'Reconnect staged writes' }
} finally { $page.Dispose() }

# Verify the actual takeoff grid, selected editor, unit/range and parameter key
# together, including switching vehicle profiles and returning to old firmware.
$takeoffType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigTakeoff',$true)
$baseType = $takeoffType.BaseType
$page = [Activator]::CreateInstance($takeoffType)
$mav = [MissionPlanner.MainV2]::comPort.MAV
try {
    $page.Activate()
    $grid = $baseType.GetField('grid',$flags).GetValue($page)
    $profile = $baseType.GetField('profile',$flags).GetValue($page)
    $proposed = $baseType.GetField('proposed',$flags).GetValue($page)
    $write = $baseType.GetField('write',$flags).GetValue($page)
    $reload = $baseType.GetMethod('LoadValues',$flags)
    $takeoffCases = @(
        @(6,'PILOT_TKOFF_ALT','PILOT_TKO_ALT_M','m',0,10),
        @(6,'PILOT_SPEED_UP','PILOT_SPD_UP','m/s',0.5,5),
        @(6,'PILOT_ACCEL_Z','PILOT_ACC_Z',('m/s'+[char]178),0.5,5),
        @(7,'PILOT_TKOFF_ALT','PILOT_TKO_ALT_M','m',0,10),
        @(7,'PILOT_SPEED_UP','PILOT_SPD_UP','m/s',0.5,5),
        @(7,'PILOT_ACCEL_Z','PILOT_ACC_Z',('m/s'+[char]178),0.5,5),
        @(8,'Q_VELZ_MAX','Q_PILOT_SPD_UP','m/s',0.5,5),
        @(8,'Q_ACCEL_Z','Q_PILOT_ACCEL_Z',('m/s'+[char]178),0.5,5),
        @(8,'Q_WP_SPEED_UP','Q_WP_SPD_UP','m/s',0.1,10)
    )
    foreach ($case in $takeoffCases) {
        $mav.param.Clear()
        $mav.param.Add($ctor.Invoke(@([string]$case[1],[double]200,$wire,$null)))
        $profile.SelectedIndex = $case[0]
        $reload.Invoke($page,@()) | Out-Null
        $legacy = @($grid.Rows | Where-Object { $_.Tag.Name -eq $case[1] })
        if ($legacy.Count -ne 1 -or $legacy[0].Cells[2].Value -ne '200' -or $legacy[0].Tag.Max -ne $case[5]*100) { throw "Legacy takeoff binding failed: $($case[1])" }
        # If both names are reported, prefer the new wire-unit definition.
        $mav.param.Add($ctor.Invoke(@([string]$case[2],[double]2,$wire,$null)))
        $reload.Invoke($page,@()) | Out-Null
        $row = @($grid.Rows | Where-Object { $_.Tag.Name -eq $case[2] })
        if ($row.Count -ne 1 -or @($grid.Rows | Where-Object { $_.Tag.Name -eq $case[1] }).Count) { throw 'Wrong parameter key or duplicate row' }
        $def = $row[0].Tag
        if ($row[0].Cells[2].Value -ne '2' -or $def.Unit -ne $case[3] -or $def.Min -ne $case[4] -or $def.Max -ne $case[5]) { throw "Metric definition mismatch: $($case[2])" }
        $accepts = $def.GetType().GetMethod('Accepts',$flags)
        foreach ($endpoint in @([double]$case[4],[double]$case[5],2.5)) {
            if (!$accepts.Invoke($def,@($endpoint))) { throw 'Valid metric value rejected' }
        }
        if ($accepts.Invoke($def,@([double]200))) { throw 'Old centimetre magnitude accepted as metres' }
        $grid.CurrentCell = $row[0].Cells[0]
        if ($proposed.Text -ne '2' -or $write.Enabled) { throw 'Wrong wire value or disconnected write enabled' }
        if ($mav.param[$case[1]].Value -ne 200 -or $mav.param[$case[2]].Value -ne 2) { throw 'Opening page migrated values' }
        $count++
    }
    $mav.param.Clear(); $reload.Invoke($page,@()) | Out-Null
    if ($write.Enabled -or $proposed.Text -ne '') { throw 'Missing parameter remained writable' }
    $page.Deactivate()
} finally { $mav.param.Clear(); $page.Dispose() }

$utilities = [Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Utilities.dll'))
$warning = $utilities.GetType('MissionPlanner.Utilities.ParamChanges47',$true).GetMethod('changedByNewParamWarning')
foreach ($name in @('PSC_D_ACC_IMAX','Q_P_D_ACC_IMAX')) {
    if ($warning.Invoke($null,@($name)) -notmatch '1000x') { throw 'Wrong IMAX migration advice' }
}
foreach ($name in @('ATC_ACC_R_MAX','ATC_ACC_P_MAX','ATC_ACC_Y_MAX','Q_A_ACC_R_MAX','Q_A_ACC_P_MAX','Q_A_ACC_Y_MAX')) {
    if ($warning.Invoke($null,@($name)) -notmatch 'cdeg/s/s to deg/s/s') { throw 'Wrong angular acceleration units' }
}
"PASS: $count old/new Copter, Heli and QuadPlane cases; typed PID writes; cache preservation; refresh keys; takeoff units/ranges; reconnect; offline write protection; migration warnings"
