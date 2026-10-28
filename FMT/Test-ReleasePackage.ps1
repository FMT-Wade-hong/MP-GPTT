param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [string]$SourceDirectory='bin/Release/net461',
    [string]$Version=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Version){$Version=(Get-Content (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()}
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid release version'}
$source=(Resolve-Path -LiteralPath $SourceDirectory).Path
$info=(Get-Item -LiteralPath (Join-Path $source 'FMTPlanner.exe')).VersionInfo
if($info.FileVersion -ne "$Version.0" -or $info.ProductVersion -ne "V$Version"){throw 'Executable version mismatch'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
$prefix="FMTPlanner-V$Version/"
function Get-StreamHash([IO.Stream]$Stream) {
    $sha=[Security.Cryptography.SHA256]::Create()
    try {return ([BitConverter]::ToString($sha.ComputeHash($Stream))).Replace('-','')}
    finally {$sha.Dispose()}
}
try {
    $names=@($zip.Entries | ForEach-Object FullName)
    if(@($names | Select-Object -Unique).Count -ne $names.Count){throw 'Duplicate ZIP entries'}
    foreach($name in $names) {
        if(!$name.StartsWith($prefix) -or $name -match '(^|/)\.\.(/|$)'){throw "Invalid package path: $name"}
        if($name -match '(?i)H420-source-full|FlightLogViewer|Harness\.exe|crash-test-|/Diagnostics/|/mode-scale-|/screen-\d|/screen-size-|preview\.png$|\.(pfx|p12|key|tlog|rlog|dmp|log|pdb)$|/(config\.xml|settings\.json|password\.bin|\.env)$') {
            throw "Private or test artifact in package: $name"
        }
    }
    $files=@{
        'FMTPlanner.exe'="FMTPlanner-V$Version.exe"
        'FMTPlanner.exe.config'="FMTPlanner-V$Version.exe.config"
        'MissionPlanner.Controls.dll'='MissionPlanner.Controls.dll'
        'MissionPlanner.ArduPilot.dll'='MissionPlanner.ArduPilot.dll'
        'MissionPlanner.Utilities.dll'='MissionPlanner.Utilities.dll'
    }
    foreach($file in $files.Keys) {
        $entry=$zip.GetEntry($prefix+$files[$file])
        if(!$entry){throw "Missing runtime file: $file"}
        $stream=$entry.Open()
        try {$actual=Get-StreamHash $stream} finally {$stream.Dispose()}
        if($actual -ne (Get-FileHash -LiteralPath (Join-Path $source $file) -Algorithm SHA256).Hash){throw "Runtime hash mismatch: $file"}
    }
    foreach($file in @('README-FMT.md','CHANGELOG-FMT.md',"FMT/RELEASE-AUDIT-V$Version.md")) {
        $entry=$zip.GetEntry($prefix+$file)
        if(!$entry){throw "Missing documentation: $file"}
        $stream=$entry.Open()
        try {$actual=Get-StreamHash $stream} finally {$stream.Dispose()}
        if($actual -ne (Get-FileHash -LiteralPath (Join-Path $root $file) -Algorithm SHA256).Hash){throw "Documentation mismatch: $file"}
    }
    foreach($file in @('README-FIRST.txt','RELEASE-MANIFEST.txt')) {
        $entry=$zip.GetEntry($prefix+$file)
        if(!$entry){throw "Missing release instructions: $file"}
        $reader=[IO.StreamReader]::new($entry.Open())
        try {$content=$reader.ReadToEnd()} finally {$reader.Dispose()}
        if($content -match '(?i)local candidate'){throw 'Candidate package cannot be published as a release'}
        if(!$content.Contains($Version)){throw "Incorrect instructions version: $file"}
    }
    "PASS: V$Version package; $($names.Count) unique entries, runtime/document hashes, version, privacy and test-artifact exclusions."
} finally {$zip.Dispose()}
