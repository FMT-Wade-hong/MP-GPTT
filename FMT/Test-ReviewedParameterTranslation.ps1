param([string]$Directory = 'bin/ReviewedTranslationFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams',$true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$option = $type.GetMethod('LocalizeFmtOption',$flags)
$options = $type.GetMethod('LocalizeFmtOptions',$flags)
$display = $type.GetMethod('GetFmtDisplayDescription',$flags)
$draft = $assembly.GetType('MissionPlanner.FMT.FmtParameterDrafts').GetMethod('Translate',$flags)
foreach ($pair in @(@('Servos to Neutral','控制舵面 servo 中立輸出'),@('Servos to Zero PWM','控制舵面 servo 輸出 0 PWM（非中立位置）'))) {
    $result = [string]$option.Invoke($null,@($pair[0]))
    if (!$result.Contains($pair[0]) -or !$result.Contains($pair[1])) { throw "Post-landing servo option meaning lost: $($pair[0])" }
}
foreach ($pair in @(@('Go to the last location where landing target was detected','返回最後偵測到降落目標時的機體位置'),@('Go towards the approximate location of the detected landing target','前往估計的降落目標位置'))) {
    $result = [string]$option.Invoke($null,@($pair[0]))
    if (!$result.Contains($pair[0]) -or !$result.Contains($pair[1])) { throw "Precision landing retry location confused: $($pair[0])" }
}
foreach ($source in @('This is the altitude the vehicle will move to as the final stage of Returning to Launch or after completing a mission.  Set to zero to land.', 'Altitude the vehicle will move to as the final stage of Returning to Launch or after completing a mission. Set to zero to land.')) {
    $result = [string]$display.Invoke($null,@('RTL_ALT_FINAL',$source))
    foreach ($term in @('以 Home 為高度基準','0 表示降落','RC failsafe','不是所有任務結束都會套用')) {
        if (!$result.Contains($term)) { throw "RTL final altitude semantics lost: $term" }
    }
}
foreach ($pair in @(@('RTL or Hold','先切入 RTL；無法切入時改用 Hold'),@('RTL or Land','先切入 RTL；無法切入時改用 Land'),@('Loiter or Hold','先切入 Loiter；無法切入時改用 Hold'),@('AUTOLAND or RTL','先切入 AUTOLAND；無法切入時改用 RTL'))) {
    $translatedOption = [string]$option.Invoke($null,@($pair[0]))
    if (!$translatedOption.Contains($pair[0]) -or !$translatedOption.Contains($pair[1])) { throw "Fallback order lost: $($pair[0])" }
}
foreach ($pair in @(@('Raw Voltage','未補償負載壓降'),@('Sag Compensated Voltage','估計電壓'),@('Sum Of Selected Monitors','電流加總，電壓預設取平均'),@('SumOfFollowing','電流加總，電壓預設取平均'),@('Terminate','緊急終止（非一般返航或降落）'))) {
    $translatedOption = [string]$option.Invoke($null,@($pair[0]))
    if (!$translatedOption.Contains($pair[0]) -or !$translatedOption.Contains($pair[1])) { throw "Safety option meaning lost: $($pair[0])" }
}
foreach ($pair in @(@('Portable','可攜式'),@('Pedestrian','步行'),@('Automotive','車輛'),@('Sea','海上'),@('Aviation','航空'),@('cylinder','圓柱形'),@('cone','圓錐形'),@('sphere','球形'))) {
    $translatedOption = [string]$option.Invoke($null,@($pair[0]))
    if (!$translatedOption.Contains($pair[0]) -or !$translatedOption.Contains($pair[1])) { throw "Option meaning/original lost: $($pair[0])" }
}
foreach ($term in @('Roll','Pitch','Yaw','YawD','PID','VFF','Rate D/Rate P(incl max gain)','Angle P','GPS','HDoP')) {
    if ($option.Invoke($null,@($term)) -cne $term) { throw "Technical term changed: $term" }
}
# Explanatory phrases may add Chinese, but acronyms and the full original stay intact.
$phrase = 'AGL KF for optflow scaling'
$phraseResult = [string]$option.Invoke($null,@($phrase))
if (!$phraseResult.Contains($phrase) -or !$phraseResult.Contains('AGL KF') -or $phraseResult -notmatch '[\u3400-\u9fff]') { throw 'AGL KF explanation or original missing' }
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
