param([string]$Directory='bin/BugAudit122/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type=$assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigInitialParams',$true)
$round=$type.GetMethod('RoundTo',[Reflection.BindingFlags]'Static,NonPublic')
foreach($precision in @(-5,16)) {
    $rejected=$false
    try { $round.Invoke($null,@(123.0,[int]$precision)) | Out-Null } catch { $rejected=$true }
    if(!$rejected) { throw 'Invalid precision accepted' }
}
if($round.Invoke($null,@(123.45,1)) -ne 123.4 -or $round.Invoke($null,@(12345.0,-2)) -ne 12300) { throw 'Valid rounding changed' }
$source=Get-Content "$PSScriptRoot/../GCSViews/ConfigurationView/ConfigInitialParams.cs" -Raw -Encoding UTF8
foreach($text in @('PSC_D_ACC_P = 0[.]1 . MOT_THST_HOVER','PSC_D_ACC_I = 0[.]2 . MOT_THST_HOVER','PSC_ACCZ_P = MOT_THST_HOVER','PSC_ACCZ_I = 2 . MOT_THST_HOVER')) {
    if($source -notmatch $text) { throw "Incorrect version-specific advice: $text" }
}
if($source.Contains('Set PSC_ACCZ_P/PSC_D_ACC_P')) { throw 'Unsafe shared gain scaling returned' }
'PASS: initial tuning version-specific advice and rounding bounds; no vehicle writes'
