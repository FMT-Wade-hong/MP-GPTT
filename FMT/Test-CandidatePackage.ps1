param(
    [string]$Package = 'bin/Package/FMTPlanner-V1.1.9-TranslationUpdate-Candidate.zip',
    [string]$Directory = 'bin/Release119TranslationUpdate/net461',
    [string]$Version = '1.1.9'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $Package))
try {
    $root = "FMTPlanner-V$Version/"
    $duplicates = @($zip.Entries | Group-Object FullName | Where-Object Count -gt 1)
    if ($duplicates.Count) { throw 'Duplicate ZIP entries' }
    foreach ($required in @("FMTPlanner-V$Version.exe", "FMTPlanner-V$Version.exe.config", 'MissionPlanner.Utilities.dll', 'Newtonsoft.Json.dll', 'zh-TW/FMTPlanner.resources.dll')) {
        if (!@($zip.Entries | Where-Object { $_.FullName -ieq ($root + $required) }).Count) { throw "Missing runtime file: $required" }
    }
    $checked = 0
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -match '(?i)(H420-source-full|preview\.png$|swarm-tab-.*\.png$|(?:resource|source)-icon\.png$|parameter-translation-audit\.json$|\.(log|tlog|rlog|pfx|p12|key|dmp)$|/(config\.xml|settings\.json|password\.bin|\.env)$)') { throw "Unwanted package entry: $($entry.FullName)" }
        if ($entry.FullName -notmatch '\.(exe|dll)$') { continue }
        $relative = $entry.FullName.Substring($root.Length)
        if ($relative -eq "FMTPlanner-V$Version.exe") { $relative = 'FMTPlanner.exe' }
        $source = Join-Path $Directory $relative
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($actual -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) { throw "Binary mismatch: $relative" }
        $checked++
    }
    $reader = [IO.StreamReader]::new($zip.GetEntry($root + 'README-FIRST.txt').Open())
    try { if (!$reader.ReadToEnd().Contains('LOCAL CANDIDATE')) { throw 'Candidate label missing' } }
    finally { $reader.Dispose() }
    "PASS: candidate ZIP, required runtime files, no duplicates/private artifacts, $checked EXE/DLL hashes match build"
}
finally { $zip.Dispose() }
