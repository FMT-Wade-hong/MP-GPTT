param([string]$SourceDirectory = 'bin/Release116/net461', [string]$PackagePath = 'bin/Package/FMTPlanner-V1.1.6.zip')
$ErrorActionPreference = 'Stop'
if ((Get-Item "$SourceDirectory/FMTPlanner.exe").VersionInfo.FileVersion -ne '1.1.6.0') { throw 'Version mismatch' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $PackagePath))
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    foreach ($required in @('FMTPlanner-V1.1.6.exe','FMTPlanner-V1.1.6.exe.config','README-FMT.md','CHANGELOG-FMT.md','RELEASE-MANIFEST.txt','FMT/P400-AT-README.md','MissionPlanner.Controls.dll','MissionPlanner.ArduPilot.dll')) {
        if ($names -notcontains "FMTPlanner-V1.1.6/$required") { throw "Missing $required" }
    }
    $forbidden = @($names | Where-Object { $_ -match '(?i)(TestReference|FrequencyReference|frequency-tests-|P400FrequencyPlans|preview\.png$|\.bak$|\.(pdb|so|dylib|pfx|p12|key|tlog|rlog|dmp)$|/(config\.xml|settings\.json|password\.bin|\.env)$|/(logs|gmapcache|private-signing)/)' })
    if ($forbidden.Count) { throw ('Forbidden entries: '+($forbidden -join ', ')) }
    if (@($names | Group-Object | Where-Object Count -gt 1).Count) { throw 'Duplicate archive entries' }
    $entry=$zip.GetEntry('FMTPlanner-V1.1.6/FMTPlanner-V1.1.6.exe')
    $hash=[Security.Cryptography.SHA256]::Create(); $stream=$entry.Open()
    try { $embedded=([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-','') }
    finally { $stream.Dispose(); $hash.Dispose() }
    if ($embedded -ne (Get-FileHash "$SourceDirectory/FMTPlanner.exe").Hash) { throw 'Packaged binary mismatch' }
    Write-Output "PASS: V1.1.6 binary, required files, archive privacy checks, unique entries and binary hash ($($names.Count) entries)"
} finally { $zip.Dispose() }
