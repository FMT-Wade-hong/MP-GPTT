param([string]$Directory = 'bin/ParameterChineseFullTest/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams')
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$describe = $type.GetMethod('GetFmtTooltipDescription', $flags)
$options = $type.GetMethod('LocalizeFmtOptions', $flags)
$culture = [Threading.Thread]::CurrentThread.CurrentUICulture
try {
    [Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::GetCultureInfo('zh-TW')
    $english = 'Capacity of the battery in mAh when full'
    $translated = $describe.Invoke($null, @('BATT_CAPACITY', $english))
    if ($translated -eq $english -or !$translated.Contains($english)) { throw 'Description must contain translation and original' }
    $unknown = 'An unfamiliar firmware restriction: do not exceed 12.'
    if (!$describe.Invoke($null, @('NEW_PARAM', $unknown)).Contains($unknown)) { throw 'Unknown restriction lost' }
    $source = '-1:None,0:Disabled,1:Enabled,0.15:Default,99:Unrecognized firmware option'
    $result = $options.Invoke($null, @($source))
    $oldKeys = ($source.Split(',') | ForEach-Object { $_.Split(':')[0] }) -join ','
    $newKeys = ($result.Split(',') | ForEach-Object { $_.Split(':')[0] }) -join ','
    if ($oldKeys -ne $newKeys -or !$result.Contains('99:Unrecognized firmware option') -or $result -eq $source) { throw 'Option translation changed keys or unknown text' }
    'PASS: Chinese descriptions preserve original; unknown descriptions and numeric option keys preserved'
    [xml]$metadata = Get-Content ParameterMetaDataBackup.xml -Raw -Encoding UTF8
    $sources = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($node in $metadata.SelectNodes('//Description')) { $sources.Add($node.InnerText) | Out-Null }
    $cachedPath = 'C:/ProgramData/Mission Planner/ArduCopter.apm.pdef.xml'
    $cached = $null
    if (Test-Path $cachedPath) {
        [xml]$cached = Get-Content $cachedPath -Raw -Encoding UTF8
        foreach ($node in $cached.SelectNodes('//param')) { $sources.Add([string]$node.documentation) | Out-Null }
    }
    $translations = $type.GetField('FmtDescriptionTranslations', $flags).GetValue($null)
    foreach ($key in $translations.Keys) {
        if ($cached -and !$sources.Contains($key)) { throw "Missing metadata source: $key" }
    }
    if ($cached) { "PASS: all $($translations.Count) translations match bundled or cached source metadata" }
    $display = $type.GetMethod('GetFmtDisplayDescription', $flags)
    $option = $type.GetMethod('LocalizeFmtOption', $flags)
    $ek3 = @($metadata.Params.ArduCopter2.ChildNodes | Where-Object Name -like 'EK3_*')
    foreach ($node in $ek3) {
        $sourceDescription = [string]$node.Description
        $text = $display.Invoke($null, @([string]$node.Name, $sourceDescription))
        if ($text -eq $sourceDescription -or $text.Contains($sourceDescription) -or $text -notmatch '[\u4e00-\u9fff]') { throw "Untranslated or duplicated English: $($node.Name)" }
        foreach ($list in @([string]$node.Values, [string]$node.Bitmask)) {
            foreach ($entry in $list.Split(',')) {
                $colon = $entry.IndexOf(':')
                if ($colon -lt 0) { continue }
                $label = $entry.Substring($colon + 1).Trim()
                $local = $option.Invoke($null, @($label))
                if ($local -eq $label) { throw "Untranslated EK3 option: $label" }
            }
        }
    }
    "PASS: all $($ek3.Count) bundled EK3 descriptions, enum labels and bitmask labels translated; table excludes English source"
    if ($cached) {
        $cachedEk3 = $cached.SelectNodes('//param[starts-with(@name,"EK3_")]')
        foreach ($node in $cachedEk3) {
            $sourceDescription = [string]$node.documentation
            if ($display.Invoke($null, @([string]$node.name, $sourceDescription)) -eq $sourceDescription) { throw "Untranslated cached parameter: $($node.name)" }
            $labels = @($node.values.value | ForEach-Object { $_.InnerText })
            foreach ($field in @($node.field | Where-Object name -eq 'Bitmask')) {
                $labels += @($field.InnerText.Split(',') | ForEach-Object { $_.Substring($_.IndexOf(':') + 1).Trim() })
            }
            foreach ($label in $labels) {
                if ($label -and $option.Invoke($null, @([string]$label)) -eq $label) { throw "Untranslated cached option: $label" }
            }
        }
        "PASS: all $($cachedEk3.Count) cached EK3 descriptions and option labels translated"
        $allParameters = $cached.SelectNodes('//param')
        $draftCount = 0
        $humanCount = 0
        foreach ($node in $allParameters) {
            $sourceDescription = [string]$node.documentation
            if ([string]::IsNullOrWhiteSpace($sourceDescription)) { continue }
            $local = $display.Invoke($null, @([string]$node.name, $sourceDescription))
            if ($local -eq $sourceDescription) { throw "Missing full-table translation: $($node.name)" }
            if ($local.StartsWith([string][char]0x3014)) { $draftCount++ } else { $humanCount++ }
        }
        "PASS: full table has $humanCount human-dictionary and $draftCount explicitly marked draft descriptions"
    }
    $draftType = $assembly.GetType('MissionPlanner.FMT.FmtParameterDrafts')
    $draftTranslate = $draftType.GetMethod('Translate', $flags)
    foreach ($literal in @('0','115200','1.5MBaud','10Hz','-1','0.15','50%')) {
        if ($draftTranslate.Invoke($null, @($literal)) -ne $literal) { throw "Numeric or unit label changed: $literal" }
    }
    'PASS: numeric and unit-only labels preserved exactly'
    [Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::GetCultureInfo('en-US')
    if ($describe.Invoke($null, @('BATT_CAPACITY', $english)) -ne $english -or $options.Invoke($null, @($source)) -ne $source) { throw 'English locale changed' }
    'PASS: English locale unchanged'
} finally { [Threading.Thread]::CurrentThread.CurrentUICulture = $culture }
