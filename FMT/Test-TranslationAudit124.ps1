param([string]$Directory = 'bin/TranslationAudit124/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$telemetry = $assembly.GetType('MissionPlanner.FMT.FmtTelemetryLocalization', $true).GetMethod('Field', $flags)
$safety = $assembly.GetType('MissionPlanner.FMT.FmtSafetyParameterCatalog', $true).GetMethod('TranslateOption', $flags)
$stringsAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Strings.dll'))
$stringsType = $stringsAssembly.GetType('MissionPlanner.Strings', $true)
$manager = $stringsType.GetProperty('ResourceManager').GetValue($null)
[xml]$source = Get-Content "$PSScriptRoot/../ExtLibs/Strings/Strings.resx" -Raw -Encoding UTF8
$originalCulture = [Threading.Thread]::CurrentThread.CurrentUICulture
try {
    foreach ($cultureName in @('zh-TW','zh-Hant','zh-HK')) {
        $culture = [Globalization.CultureInfo]$cultureName
        [Threading.Thread]::CurrentThread.CurrentUICulture = $culture
        $count = 0
        foreach ($entry in $source.root.data) {
            $actual = $manager.GetString($entry.name, $culture)
            if ([string]::IsNullOrWhiteSpace($actual)) { throw "Missing shared resource: $cultureName $($entry.name)" }
            if ($entry.name -ne 'MAVFtp' -and $actual -notmatch '[\u3400-\u9fff]') { throw "English fallback: $cultureName $($entry.name)" }
            if ($actual.Contains('{')) { [void][string]::Format($actual, [object[]]@('A','B','C','D')) }
            $count++
        }
        if (!$manager.GetString('MissingDataPoints',$culture).Contains('仍要執行校準') -or
            $manager.GetString('MissingDataPoints',$culture).Contains('結束')) { throw 'Calibration action reversed' }
        if (!$manager.GetString('CannotGraphField',$culture).Contains('欄位')) { throw 'Field mistranslated as geographic area' }
        if (!$manager.GetString('UploadSucceededButVerifyFailed',$culture).Contains('預期 {0}，實際 {1}')) { throw 'Expected/actual values confused' }
        foreach ($pair in @(@('fenceb_count','越界次數'),@('fenceb_status','越界狀態'),@('fenceb_type','越界類型'),@('accel_air','空速變化率'),@('toh','預估抵達返航點'),@('tot','預估抵達目標'))) {
            if (!$telemetry.Invoke($null,@($pair[0],$null)).Contains($pair[1])) { throw "Wrong telemetry meaning: $($pair[0])" }
        }
        if (!$telemetry.Invoke($null,@('timeInAirMinSec','Time in Air (min.sec)')).Contains('分.秒')) { throw 'Decimal min.sec format mislabeled' }
        "PASS: $cultureName loads $count shared messages; formatting and telemetry meanings verified"
    }
    foreach ($newOption in @('SmartRTL','Landing with a future restriction','Disabled unless new condition','Future RTL or Hold behavior')) {
        if ($safety.Invoke($null,@($newOption)) -cne $newOption) { throw "Unknown firmware option corrupted: $newOption" }
    }
    if ($safety.Invoke($null,@('HeartbeatAndREMRSSI')) -cne '心跳或遙測接收失效') { throw 'Plane heartbeat OR REMRSSI condition changed' }
    if ($safety.Invoke($null,@('SmartRTL or RTL')) -cne 'SmartRTL，否則返航') { throw 'Reviewed fallback order lost' }
    'PASS: unknown safety options stay intact; reviewed conditions and fallback order retained'

    $raw = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams', $true)
    $tooltip = $raw.GetMethod('GetFmtTooltipDescription',$flags)
    foreach ($item in (Get-Content "$PSScriptRoot/TranslationAudit/parameter-additions.json" -Raw -Encoding UTF8 | ConvertFrom-Json)) {
        $text = $tooltip.Invoke($null,@([string]$item.ParameterIds[0],[string]$item.Source))
        if (!$text.Contains($item.Translation) -or !$text.Contains($item.Source)) { throw "New description/original lost: $($item.ParameterIds[0])" }
    }
    'PASS: all 18 new descriptions are source matched and retain the complete original'
    [Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]'en-US'
    if ($manager.GetString('MissingDataPoints',[Globalization.CultureInfo]'en-US') -cne 'You are missing data points. do you want to run the calibration anyway?') { throw 'English locale altered' }
    if ($telemetry.Invoke($null,@('toh','Time over Home (sec)')) -cne 'Time over Home (sec)') { throw 'English telemetry altered' }
    'PASS: English locale remains English'
} finally { [Threading.Thread]::CurrentThread.CurrentUICulture = $originalCulture }

# Exercise actual WinForms resource fallback and layout without a vehicle/transport.
foreach ($name in @('ConfigArduplane','ConfigTradHeli4','ConfigFailSafe')) {
    $type = $assembly.GetType("MissionPlanner.GCSViews.ConfigurationView.$name",$true)
    $page = [Activator]::CreateInstance($type)
    try {
        if ($name -eq 'ConfigFailSafe') {
            $fields = [Reflection.BindingFlags]'Instance,NonPublic'
            $tips = $type.GetField('toolTip1',$fields).GetValue($page)
            foreach ($controlName in @('mavlinkCheckBoxshort_fs','mavlinkCheckBoxlong_fs')) {
                $control = $type.GetField($controlName,$fields).GetValue($page)
                if ($control.Text -match '1 秒|20 秒') { throw 'Failsafe timeout still hard coded' }
                if (!$tips.GetToolTip($control).Contains('0 不代表全面停用')) { throw 'Failsafe zero misrepresented as disabled' }
            }
            $gps = $type.GetField('lbl_gpslock',$fields).GetValue($page)
            $paintGps = $type.GetMethod('lbl_gpslock_Paint',$fields)
            foreach ($pair in @(@('0','無 GPS'),@('1','未定位'),@('2','2D'),@('3','3D'),@('5','RTK'),@('6','RTK'))) {
                $gps.Text = $pair[0]
                [void]$paintGps.Invoke($page,@($gps,$null))
                if (!$gps.Text.Contains($pair[1])) { throw "Failsafe GPS label wrong: $($pair[0]) => $($gps.Text)" }
            }
            $armed = $type.GetField('lbl_armed',$fields).GetValue($page)
            $paintArmed = $type.GetMethod('lbl_armed_Paint',$fields)
            foreach ($pair in @(@('True','已解鎖'),@('False','已上鎖'))) {
                $armed.Text = $pair[0]
                [void]$paintArmed.Invoke($page,@($armed,$null))
                if ($armed.Text -cne $pair[1]) { throw 'Failsafe armed state mistranslated' }
            }
        }
        $page.CreateControl()
        $bitmap = New-Object Drawing.Bitmap $page.Width,$page.Height
        try { $page.DrawToBitmap($bitmap,$page.ClientRectangle); $bitmap.Save((Join-Path $binaryDirectory "$name-translation-preview.png")) }
        finally { $bitmap.Dispose() }
    } finally { $page.Dispose() }
}
'PASS: Plane/current Heli/failsafe pages rendered offline; failsafe timeout and zero-value wording verified'
