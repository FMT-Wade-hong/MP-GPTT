param([string]$Directory = 'bin/Metadata119Versioned/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$selector = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams', $true).GetMethod('GetFmtMetadataVehicle', $flags)
$utilities = [Reflection.Assembly]::LoadFrom((Join-Path (Resolve-Path $Directory) 'MissionPlanner.Utilities.dll'))
$repo = $utilities.GetType('MissionPlanner.Utilities.ParameterMetaDataRepository', $true)
if (!$repo) { throw 'Metadata repository assembly not loaded' }
$get = $repo.GetMethod('GetParameterMetaData')
foreach ($version in @('4.5.7','4.6.3','4.7.1')) {
    $key = $selector.Invoke($null, @('ArduCopter2', "ArduCopter V$version"))
    if ($key -cne "Copter$version") { throw "Wrong version key: $key" }
    [xml]$xml = Get-Content -LiteralPath (Join-Path $env:ProgramData "Mission Planner/$key.apm.pdef.xml") -Raw -Encoding UTF8
    $count = 0
    foreach ($node in $xml.SelectNodes('//param')) {
        $id = [string]$node.name
        if ($id -notmatch '^(ArduCopter:)?(WPNAV_|WP_|PSC_|ARMING_CHECK)') { continue }
        $id = $id -replace '^ArduCopter:', ''
        $actual = $get.Invoke($null, @($id, 'Description', $key))
        if ($actual -cne [string]$node.documentation) { throw "Description mismatch: $key $id" }
        $unitsNode = $node.SelectSingleNode('field[@name="Units"]')
        $expectedUnits = if ($unitsNode) { $unitsNode.InnerText } else { '' }
        if ($get.Invoke($null, @($id, 'Units', $key)) -cne $expectedUnits) { throw "Units mismatch: $key $id" }
        $count++
    }
    if ($count -lt 10) { throw "Insufficient coverage: $key" }
    "PASS: $key - $count navigation/controller descriptions and units match exact snapshot"
}
if ($get.Invoke($null, @('WPNAV_SPEED', 'Description', 'Copter4.6.99')) -ne '') { throw 'Missing version fell back to another release' }
'PASS: unavailable exact version does not mix generic or legacy metadata'
