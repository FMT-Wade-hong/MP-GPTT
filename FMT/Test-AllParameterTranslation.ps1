param(
    [string]$Directory = 'bin/ReviewedTranslationFix/net461',
    [string]$MetadataDirectory = 'C:/ProgramData/Mission Planner'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$display = $type.GetMethod('GetFmtDisplayDescription', $flags)
$option = $type.GetMethod('LocalizeFmtOption', $flags)
$localizeOptions = $type.GetMethod('LocalizeFmtOptions', $flags)
$translator = $assembly.GetType('MissionPlanner.FMT.FmtParameterDrafts', $true).GetMethod('Translate', $flags)
if ($assembly.GetManifestResourceNames() -contains 'FMT.Parameters.zh-TW.draft.json') { throw 'Unreviewed draft still embedded' }
if ($type.GetField('FmtTraditionalChineseParameterDescriptions', $flags)) { throw 'Unsafe ID-only translation dictionary remains' }
foreach ($id in @('AHRS_GPS_USE','ACRO_OPTIONS','AIRSPEED_MIN','AUTOTUNE_AXES')) {
    $unknown = 'Future firmware meaning: never use this value for normal flight.'
    if ($display.Invoke($null,@($id,$unknown)) -cne $unknown) { throw "Version warning overwritten: $id" }
}
$catalog = Get-Content "$PSScriptRoot/Localization/Parameters.zh-TW.reviewed.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$sources = New-Object 'System.Collections.Generic.HashSet[string]'
$pending = New-Object 'System.Collections.Generic.HashSet[string]'
$labels = New-Object 'System.Collections.Generic.HashSet[string]'
$reports = @()
$files = @(Get-ChildItem -LiteralPath $MetadataDirectory -Filter '*.apm.pdef.xml')
if (!$files.Count) { throw 'No official parameter metadata available' }
foreach ($file in $files) {
    [xml]$metadata = Get-Content $file.FullName -Raw -Encoding UTF8
    $reviewed = 0; $legacy = 0; $english = 0; $empty = 0
    foreach ($node in $metadata.SelectNodes('//param')) {
        $source = [string]$node.documentation
        if (!$source.Trim()) { $empty++; continue }
        $key = [regex]::Replace($source.Trim(),'\s+',' ')
        [void]$sources.Add($key)
        $result = $display.Invoke($null,@([string]$node.name,$source))
        if ($result.Contains('機譯') -or $result.Contains('分類參考')) { throw "Draft text displayed: $($node.name)" }
        if ($translator.Invoke($null,@($source)) -cne $source) { $reviewed++ }
        elseif ($result -cne $source) { $legacy++ }
        else { $english++; [void]$pending.Add($key) }
        foreach ($value in $node.SelectNodes('values/value')) { [void]$labels.Add($value.InnerText) }
        foreach ($mask in $node.SelectNodes('field[@name="Bitmask"]')) {
            $localized = $localizeOptions.Invoke($null,@($mask.InnerText))
            $before = @($mask.InnerText.Split(',') | ForEach-Object { ($_ -split ':',2)[0] })
            $after = @($localized.Split(',') | ForEach-Object { ($_ -split ':',2)[0] })
            if (($before -join ',') -cne ($after -join ',')) { throw "Bit indices changed: $($node.name)" }
            foreach ($entry in $mask.InnerText.Split(',')) {
                $colon = $entry.IndexOf(':')
                if ($colon -ge 0) { [void]$labels.Add($entry.Substring($colon+1)) }
            }
        }
    }
    $reports += [pscustomobject]@{File=$file.Name; SHA256=(Get-FileHash $file.FullName -Algorithm SHA256).Hash; ReviewedDescriptions=$reviewed; LegacyDictionaryDescriptions=$legacy; OriginalEnglishDescriptions=$english; EmptyDescriptions=$empty}
}
foreach ($entry in $catalog.psobject.Properties) {
    if (!$sources.Contains($entry.Name)) { throw "No official source found: $($entry.Name)" }
    if ($translator.Invoke($null,@($entry.Name)) -cne $entry.Value) { throw "Reviewed catalog not used: $($entry.Name)" }
    foreach ($reference in [regex]::Matches($entry.Name,'\b[A-Z][A-Z0-9]*_[A-Z0-9_]+\b')) {
        if (!$entry.Value.Contains($reference.Value)) { throw "Parameter reference lost: $($reference.Value)" }
    }
}
foreach ($label in $labels) {
    $translated = $option.Invoke($null,@($label))
    if (!$translated.Contains($label)) { throw "Original option/technical name lost: $label => $translated" }
}
$report = [ordered]@{
    Scope='All local public ArduPilot pdef metadata, not connected vehicle values'
    CompleteSemanticReview=$false
    ReviewedCatalogEntries=@($catalog.psobject.Properties).Count
    UniqueDescriptions=$sources.Count
    UntranslatedUniqueDescriptions=$pending.Count
    OptionLabelsChecked=$labels.Count
    Sources=$reports
    PendingDescriptions=@($pending | Sort-Object)
}
$reportPath = Join-Path (Resolve-Path $Directory) 'parameter-translation-audit.json'
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$reports | Where-Object File -in @('ArduCopter.apm.pdef.xml','ArduPlane.apm.pdef.xml','Rover.apm.pdef.xml','ArduSub.apm.pdef.xml') | Format-Table -AutoSize
"PASS: $($files.Count) metadata files, $($labels.Count) option labels retain original, bit indices unchanged, source/version checks passed"
"REVIEW INCOMPLETE: $($pending.Count) unique descriptions still original English. Audit: $reportPath"
