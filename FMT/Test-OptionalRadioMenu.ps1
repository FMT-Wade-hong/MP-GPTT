param([string]$Directory='bin/OptionalRadio123/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-PowerModuleMenu.ps1" -Directory $Directory
$source=Get-Content "$PSScriptRoot/../GCSViews/InitialSetup.cs" -Raw -Encoding UTF8
$parent='var opt = AddBackstageViewPage(typeof(ConfigOptional), rm.GetString("backstageViewPageopt.Text"));'
if(!$source.Contains($parent)) { throw 'Optional parent must be present even with battery pages hidden' }
$parentIndex=$source.IndexOf($parent)
$hiddenIndex=$source.IndexOf('if (ShowFmtOptionalHardware)')
foreach($name in @('ConfigSerialInjectGPS','ConfigSiKRadio','ConfigP400')) {
    $pattern='AddBackstageViewPage\(typeof\('+$name+'\),[\s\S]*?\);'
    $found=[regex]::Matches($source,$pattern)
    if($found.Count -ne 1) { throw "Missing or duplicate menu: $name" }
    if($found[0].Value -notmatch 'true, opt\);$') { throw "Wrong parent or offline gate: $name" }
    if($found[0].Index -le $parentIndex -or $found[0].Index -ge $hiddenIndex) { throw "Hidden menu: $name" }
    $pageType=$assembly.GetType("MissionPlanner.GCSViews.ConfigurationView.$name",$true)
    if(!$pageType.IsSubclassOf([Windows.Forms.UserControl])) { throw "Page unavailable: $name" }
}
if(!$source.Contains('if (MainV2.DisplayConfiguration.displayRTKInject)')) { throw 'RTK display preference lost' }
'PASS: three compiled page types; source registration unique, optional parent, offline visibility and RTK preference preserved; battery controls intact. No vehicle connection.'
