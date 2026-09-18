param([string]$SourceDirectory = 'bin/Release117/net461', [string]$PackagePath = 'bin/Package/FMTPlanner-V1.1.7.zip')
$ErrorActionPreference = 'Stop'
if ((Get-Item "$SourceDirectory/FMTPlanner.exe").VersionInfo.FileVersion -ne '1.1.7.0') { throw 'Version mismatch' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $PackagePath))
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    foreach ($required in @('FMTPlanner-V1.1.7.exe','FMTPlanner-V1.1.7.exe.config','README-FMT.md','CHANGELOG-FMT.md','RELEASE-MANIFEST.txt','FMT/P400-AT-README.md','MissionPlanner.Controls.dll','MissionPlanner.ArduPilot.dll')) {
        if ($names -notcontains "FMTPlanner-V1.1.7/$required") { throw "Missing $required" }
    }
    $forbidden = @($names | Where-Object { $_ -match '(?i)(H420-source-full|TestReference|FrequencyReference|frequency-tests-|P400FrequencyPlans|preview\.png$|\.bak$|\.(pdb|so|dylib|pfx|p12|key|tlog|rlog|dmp|bin)$|/(config\.xml|settings\.json|password\.bin|\.env)$|/(logs|gmapcache|private-signing)/)' })
    if ($forbidden.Count) { throw ('Forbidden entries: '+($forbidden -join ', ')) }
    if (@($names | Group-Object | Where-Object Count -gt 1).Count) { throw 'Duplicate archive entries' }
    foreach ($file in @('FMTPlanner.exe','MissionPlanner.ArduPilot.dll')) {
        $entryName = if ($file -eq 'FMTPlanner.exe') { 'FMTPlanner-V1.1.7.exe' } else { $file }
        $entry = $zip.GetEntry("FMTPlanner-V1.1.7/$entryName")
        $hash = [Security.Cryptography.SHA256]::Create()
        $stream = $entry.Open()
        try { $embedded = ([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-','') }
        finally { $stream.Dispose(); $hash.Dispose() }
        if ($embedded -ne (Get-FileHash "$SourceDirectory/$file").Hash) { throw "Packaged binary mismatch: $file" }
    }
    "PASS: V1.1.7 version, dependencies, exclusion rules, unique entries and EXE/ArduPilot hashes ($($names.Count) entries)"
} finally { $zip.Dispose() }
