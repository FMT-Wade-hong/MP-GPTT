param([string]$Directory = 'bin/PowerModule119/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$source = Get-Content "$PSScriptRoot/../GCSViews/InitialSetup.cs" -Raw -Encoding UTF8
$gate = $source.IndexOf('if (ShowFmtOptionalHardware)')
foreach ($name in @('ConfigBatteryMonitoring','ConfigBatteryMonitoring2')) {
    $pattern = 'AddBackstageViewPage\(typeof\(' + $name + '\),[^\r\n]+'
    $matches = [regex]::Matches($source,$pattern)
    if ($matches.Count -ne 1 -or $matches[0].Index -ge $gate) { throw "$name missing, duplicated or hidden" }
    if (!$matches[0].Value.Contains('isConnected && gotAllParams, opt')) { throw 'Connection/parameter gate or parent changed' }
    $type = $assembly.GetType("MissionPlanner.GCSViews.ConfigurationView.$name",$true)
    $page = [Activator]::CreateInstance($type)
    try {
        $expectedControls = if ($name -eq 'ConfigBatteryMonitoring') { @('CMB_batmontype','TXT_battcapacity','TXT_divider_VOLT_MULT','TXT_AMP_PERVLT') } else { @('mavlinkComboBox1','TXT_battcapacity','TXT_divider','TXT_ampspervolt') }
        foreach ($controlName in $expectedControls) {
            if ($page.Controls.Find($controlName,$true).Count -ne 1) { throw "Missing battery control: $name/$controlName" }
        }
    } finally { $page.Dispose() }
}
'PASS: both battery pages restored outside optional-page hiding, existing connection gates and calibration controls retained; no vehicle writes'
