param([string]$Directory='bin/Release126/net472')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$version=(Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
$sourceBinary=(Resolve-Path -LiteralPath (Join-Path $Directory 'FMTPlanner.exe')).Path
$fixture=Join-Path $projectRoot ('tmp/package-exclusion-tests/'+[Guid]::NewGuid().ToString('N'))
$source=Join-Path $fixture 'source'
[void][IO.Directory]::CreateDirectory($source)
Copy-Item -LiteralPath $sourceBinary -Destination (Join-Path $source 'FMTPlanner.exe')
$excluded=@('authkeys.xml','user/authkeys.xml','github-audit-fixture/authkeys.xml',
    'github-audit-fixture/plugins/Probe.dll','stability-lifecycle-fixture/stability-test.xml',
    'mission-clear-test-data/mission.dat','parameter-download-test-data/cache.xml',
    'GitHubAudit125Harness.exe','StabilityLifecycleHarness.exe.config')
$retained=@('aircraft.xml','normal-runtime/data.json','plugins/operational.dll')
foreach($name in @($excluded)+@($retained)) {
    $path=Join-Path $source $name
    [void][IO.Directory]::CreateDirectory((Split-Path $path -Parent))
    [IO.File]::WriteAllText($path,'Non-sensitive packaging test fixture: '+$name)
}
$package=Join-Path $fixture 'fixture-candidate.zip'
& "$PSScriptRoot/Build-FMTPlannerPackage.ps1" -SourceDirectory $source -ReleaseVersion $version -OutputPath $package -Candidate | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($package)
try {
    $prefix="FMTPlanner-V$version/"
    foreach($name in $excluded) {
        if($zip.GetEntry($prefix+$name)) { throw "Private/test artifact included: $name" }
    }
    foreach($name in $retained) {
        if(!$zip.GetEntry($prefix+$name)) { throw "Runtime file incorrectly excluded: $name" }
    }
    if(!$zip.GetEntry($prefix+"FMTPlanner-V$version.exe")) { throw 'Versioned executable missing' }
} finally {$zip.Dispose()}
"PASS: $($excluded.Count) signing/test fixtures excluded; $($retained.Count) runtime files retained; versioned executable preserved. Fixture package is not a runnable release."
