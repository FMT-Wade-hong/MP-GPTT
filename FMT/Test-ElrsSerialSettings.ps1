param([string]$Directory = 'bin/ElrsDirectProfile/net461')
$ErrorActionPreference = 'Stop'
$source = Get-Content "$PSScriptRoot/FmtElrsSerialForm.cs" -Raw
if ($source -match 'ParameterMetaDataRepository|GetParameterBitMaskInt|GetParameterOptionsInt') {
    throw 'ELRS setup must not depend on local parameter metadata'
}
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.FMT.FmtElrsSerialForm',$true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$build = $type.GetMethod('BuildPlan',$flags)
$parameters = New-Object 'Collections.Generic.Dictionary[string,double]'
$parameters.Add('SERIAL6_PROTOCOL',23)
$parameters.Add('SERIAL6_BAUD',115)
$parameters.Add('RSSI_TYPE',0)
$parameters.Add('SERIAL0_PROTOCOL',2)
$parameters.Add('SERIAL1_PROTOCOL',2)
foreach ($index in 0..3) {
    foreach ($suffix in @('RAW_SENS','EXT_STAT','RC_CHAN','POSITION','EXTRA1','EXTRA2','EXTRA3','ADSB','PARAMS','RAW_CTRL')) {
        $parameters.Add("SR${index}_$suffix", 7 + $index)
    }
}
# Official MAVLink recipe works without any RC_OPTIONS/RC_PROTOCOLS metadata.
$mav = $build.Invoke($null,@('SERIAL6',$true,$parameters.PSObject.BaseObject))
if ($mav.Count -ne 13 -or $mav['SERIAL6_PROTOCOL'] -ne 2 -or $mav['SERIAL6_BAUD'] -ne 460 -or $mav['RSSI_TYPE'] -ne 5) { throw 'Incorrect official MAVLink profile' }
foreach ($suffix in @('RAW_SENS','EXT_STAT','RC_CHAN','POSITION','EXTRA1','EXTRA2','EXTRA3')) {
    if ($mav["SR2_$suffix"] -ne 1) { throw "Wrong stream rate $suffix" }
}
foreach ($suffix in @('ADSB','PARAMS','RAW_CTRL')) {
    if ($mav["SR2_$suffix"] -ne 0) { throw "Wrong excluded stream $suffix" }
}
foreach ($mask in @(0,1,2,8706,11010,12034,16386)) {
    $parameters['RC_OPTIONS'] = $mask
    $parameters['RC_PROTOCOLS'] = 8
    $mav = $build.Invoke($null,@('SERIAL6',$true,$parameters.PSObject.BaseObject))
    if ($mav['RC_OPTIONS'] -ne ($mask -band (-bnot 2))) { throw 'MAVLink must clear only Ignore MAVLink Overrides' }
    if ($mav.ContainsKey('RC_PROTOCOLS')) { throw 'MAVRadio RC backend must not be required' }
}
$parameters['RC_OPTIONS'] = 32
$parameters['RC_PROTOCOLS'] = 1
$crsf = $build.Invoke($null,@('SERIAL6',$false,$parameters.PSObject.BaseObject))
if ($crsf['RC_OPTIONS'] -ne 8736 -or $crsf['RSSI_TYPE'] -ne 3 -or $crsf['SERIAL6_PROTOCOL'] -ne 23) { throw 'CRSF regression' }
$parameters.Add('SERIAL7_PROTOCOL',2)
$shifted = $build.Invoke($null,@('SERIAL6',$true,$parameters.PSObject.BaseObject))
if ($shifted['SR2_EXTRA1'] -ne 1 -or $shifted['SR3_EXTRA1'] -ne 9) { throw 'Other stream rates not preserved' }
$parameters.Remove('SERIAL7_PROTOCOL') | Out-Null
$modern = New-Object 'Collections.Generic.Dictionary[string,double]'
foreach ($p in $parameters.GetEnumerator()) {
    if ($p.Key -match '^SR(\d+)_(.+)$') { $modern.Add(('MAV' + (1 + [int]$matches[1]) + '_' + $matches[2]),$p.Value) }
    else { $modern.Add($p.Key,$p.Value) }
}
$mav = $build.Invoke($null,@('SERIAL6',$true,$modern.PSObject.BaseObject))
if ($mav['MAV3_EXTRA1'] -ne 1 -or $mav['MAV3_PARAMS'] -ne 0) { throw 'Modern mapping incorrect' }
foreach ($portName in @('SERIAL0','UART6','SERIAL99',$null)) {
    $rejected = $false
    try { $build.Invoke($null,@($portName,$true,$parameters.PSObject.BaseObject)) | Out-Null } catch { $rejected = $true }
    if (!$rejected) { throw "Invalid port accepted: $portName" }
}
$form = [Activator]::CreateInstance($type,$true)
try {
    function Count-Combos($control) {
        $count = 0
        foreach ($child in $control.Controls) {
            if ($child -is [Windows.Forms.ComboBox]) { $count++ }
            $count += Count-Combos $child
        }
        return $count
    }
    if ((Count-Combos $form) -ne 2) { throw 'Expected only port and mode selectors' }
    $instanceFlags = [Reflection.BindingFlags]'Instance,NonPublic'
    $modeControl = $type.GetField('mode',$instanceFlags).GetValue($form)
    $modeControl.SelectedIndex = 1
    $instructions = $type.GetMethod('Instructions',$instanceFlags).Invoke($form,@())
    if ($instructions -notmatch 'RC_CHANNELS_OVERRIDE' -or $instructions -notmatch 'RC_OPTIONS bit 1') {
        throw 'MAVLink instructions must explain override input'
    }
    $form.ShowInTaskbar = $false
    $form.StartPosition = 'Manual'
    $form.Location = New-Object Drawing.Point -32000,-32000
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    $bitmap = New-Object Drawing.Bitmap $form.Width,$form.Height
    try { $form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle 0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path (Resolve-Path $Directory) 'elrs-preview.png')) }
    finally { $bitmap.Dispose() }
} finally { $form.Dispose() }
'PASS: CRSF/MAVLink plans, missing/USB ports, RC bit preservation and offline dialog construction; no hardware writes'
