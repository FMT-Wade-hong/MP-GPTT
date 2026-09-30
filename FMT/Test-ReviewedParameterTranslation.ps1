param([string]$Directory = 'bin/ReviewedTranslationFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams',$true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$option = $type.GetMethod('LocalizeFmtOption',$flags)
$options = $type.GetMethod('LocalizeFmtOptions',$flags)
$display = $type.GetMethod('GetFmtDisplayDescription',$flags)
$draft = $assembly.GetType('MissionPlanner.FMT.FmtParameterDrafts').GetMethod('Translate',$flags)
foreach ($term in @('Roll','Pitch','Yaw','YawD','PID','VFF','Rate D/Rate P(incl max gain)','Angle P','GPS','HDoP','AGL KF for optflow scaling')) {
    if ($option.Invoke($null,@($term)) -cne $term) { throw "Technical term changed: $term" }
}
$mask = '0:Roll,1:Pitch,2:Yaw,3:YawD'
if ($options.Invoke($null,@($mask)) -cne $mask) { throw 'Axis bit indices/names changed' }
$axis = $display.Invoke($null,@('AUTOTUNE_AXES','1-byte bitmap of axes to autotune'))
if (!$axis.Contains('bitmask') -or !$axis.Contains('軸向') -or $axis.Contains('機譯')) { throw 'Axis description incorrect' }
$frequency = $display.Invoke($null,@('AUTOTUNE_FRQ_MIN','Defines the start frequency for sweeps and dwells'))
if (!$frequency.Contains('定頻測試') -or $frequency.Contains('Hz')) { throw 'Dwell meaning or unit changed' }
$gain = $display.Invoke($null,@('AUTOTUNE_GN_MAX','Defines the response gain (output/input) to tune'))
if (!$gain.Contains('output/input')) { throw 'Response ratio lost' }
foreach ($source in @('Some new firmware restriction: do not exceed 12.','Yaw','Pitch','115200','0.15','1.5MBaud')) {
    if ($draft.Invoke($null,@($source)) -cne $source) { throw "Unreviewed/numeric text changed: $source" }
}
$metadataPath = Join-Path $env:ProgramData 'Mission Planner/ArduCopter.apm.pdef.xml'
if (Test-Path $metadataPath) {
    [xml]$metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8
    foreach ($name in @('AUTOTUNE_ACC_MAX','AUTOTUNE_AXES','AUTOTUNE_FRQ_MAX','AUTOTUNE_FRQ_MIN','AUTOTUNE_GN_MAX','AUTOTUNE_RAT_MAX','AUTOTUNE_SEQ','AUTOTUNE_VELXY_P')) {
        $nodes = $metadata.SelectNodes("//param[@name='$name']")
        if ($nodes.Count -eq 0) { throw "Missing official metadata: $name" }
        foreach ($node in $nodes) {
            $source = [string]$node.documentation
            $result = $display.Invoke($null,@($name,$source))
            if ($result -ceq $source -or $result.Contains('機譯') -or $result -notmatch '[\u3400-\u9fff]') { throw "Unreviewed screenshot description: $name" }
        }
    }
    'PASS: all eight screenshot descriptions match cached ArduPilot metadata (including duplicate vehicle variants)'
} else { throw "Official metadata unavailable: $metadataPath" }
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]'en-US'
if ($draft.Invoke($null,@('Autotune axis bitmask')) -cne 'Autotune axis bitmask') { throw 'English locale changed' }
'PASS: technical terms and bit indices unchanged; AutoTune meaning; unreviewed fallback; units; English locale'
