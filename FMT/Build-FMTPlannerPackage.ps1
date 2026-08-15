param(
    [string]$SourceDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Release\net461'),
    [string]$ReleaseVersion = '',
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$versionFile = Join-Path $PSScriptRoot 'VERSION'
if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) {
    if (-not [string]::IsNullOrWhiteSpace($env:FMTPLANNER_VERSION)) {
        $ReleaseVersion = $env:FMTPLANNER_VERSION
    }
    elseif (Test-Path -LiteralPath $versionFile -PathType Leaf) {
        $ReleaseVersion = (Get-Content -LiteralPath $versionFile -Raw).Trim()
    }
}
if ($ReleaseVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "ReleaseVersion must use X.X.X format. Received '$ReleaseVersion'."
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $projectRoot ("bin\Package\FMTPlanner-V{0}.zip" -f $ReleaseVersion)
}

$sourcePath = (Resolve-Path -LiteralPath $SourceDirectory).Path
$applicationPath = Join-Path $sourcePath 'FMTPlanner.exe'
if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
    throw "FMTPlanner.exe was not found in $sourcePath. Build Release first."
}

$outputFullPath = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path $outputFullPath -Parent
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$temporaryOutput = Join-Path $outputDirectory ('.fmtplanner-' + [Guid]::NewGuid().ToString('N') + '.zip')
$rootFolder = "FMTPlanner-V$ReleaseVersion"
$documentationFiles = @(
    [PSCustomObject]@{
        Source = Join-Path $projectRoot 'README-FMT.md'
        Entry = "$rootFolder/README-FMT.md"
    },
    [PSCustomObject]@{
        Source = Join-Path $projectRoot 'CHANGELOG-FMT.md'
        Entry = "$rootFolder/CHANGELOG-FMT.md"
    }
)
$documentationAssetRoot = Join-Path $projectRoot 'FMT'
foreach ($assetDirectory in 'Assets', 'ManualImages') {
    $assetPath = Join-Path $documentationAssetRoot $assetDirectory
    if (Test-Path -LiteralPath $assetPath -PathType Container) {
        foreach ($asset in Get-ChildItem -LiteralPath $assetPath -Recurse -File) {
            $relativeAsset = $asset.FullName.Substring($projectRoot.Length).TrimStart('\', '/')
            $documentationFiles += [PSCustomObject]@{
                Source = $asset.FullName
                Entry = ($rootFolder + '/' + $relativeAsset.Replace('\', '/'))
            }
        }
    }
}
$staleMissionPlannerOutputs = @(
    'MissionPlanner.exe',
    'MissionPlanner.exe.config',
    'MissionPlanner.pdb'
)
$excludedFiles = @()
$excludedBytes = 0L

function Get-ReleaseExclusionReason([string]$RelativePath) {
    $extension = [IO.Path]::GetExtension($RelativePath)
    if ($staleMissionPlannerOutputs -contains $RelativePath) {
        return 'stale Mission Planner output'
    }
    if ($extension -ieq '.pdb') {
        return 'debug symbols'
    }
    if ($extension -ieq '.so' -or $extension -ieq '.dylib') {
        return 'non-Windows native library'
    }
    if ($RelativePath -like 'plugins\example*.cs') {
        return 'developer example plugin source'
    }
    return $null
}

try {
    $stream = [IO.File]::Open($temporaryOutput, [IO.FileMode]::CreateNew)
    try {
        $archive = [IO.Compression.ZipArchive]::new(
            $stream, [IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            $files = @()
            foreach ($candidate in Get-ChildItem -LiteralPath $sourcePath -Recurse -File) {
                $relative = $candidate.FullName.Substring($sourcePath.Length).TrimStart('\', '/')
                $reason = Get-ReleaseExclusionReason $relative
                if ($null -ne $reason) {
                    $excludedFiles += [PSCustomObject]@{
                        Path = $relative
                        Reason = $reason
                        Bytes = $candidate.Length
                    }
                    $excludedBytes += $candidate.Length
                    continue
                }
                $files += $candidate
            }

            foreach ($file in $files) {
                $relativePath = $file.FullName.Substring($sourcePath.Length).TrimStart('\', '/')
                $entryName = ($rootFolder + '/' + $relativePath.Replace('\', '/'))
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $file.FullName,
                    $entryName,
                    [IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }

            foreach ($document in $documentationFiles) {
                if (-not (Test-Path -LiteralPath $document.Source -PathType Leaf)) {
                    throw "Release documentation file was not found: $($document.Source)"
                }
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $document.Source,
                    $document.Entry,
                    [IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }

            $readmeEntry = $archive.CreateEntry(
                "$rootFolder/README-FIRST.txt",
                [IO.Compression.CompressionLevel]::Optimal)
            $writer = [IO.StreamWriter]::new($readmeEntry.Open(), [Text.UTF8Encoding]::new($true))
            try {
                $writer.WriteLine('FMTPlanner V' + $ReleaseVersion)
                $writer.WriteLine('')
                $writer.WriteLine('1. Extract this ZIP to a normal local folder.')
                $writer.WriteLine('2. Run FMTPlanner.exe.')
                $writer.WriteLine('3. Do not run files directly from inside the ZIP viewer.')
                $writer.WriteLine('4. Open README-FMT.md for the Traditional Chinese illustrated user manual.')
                $writer.WriteLine('')
                $writer.WriteLine('Publisher: FMT飛貓科技')
                $writer.WriteLine('Website: https://www.feimaotec.com')
                $writer.WriteLine('')
                $writer.WriteLine('This portable ZIP avoids self-extracting launchers that can trigger antivirus heuristics.')
            }
            finally {
                $writer.Dispose()
            }

            $manifestEntry = $archive.CreateEntry(
                "$rootFolder/RELEASE-MANIFEST.txt",
                [IO.Compression.CompressionLevel]::Optimal)
            $manifestWriter = [IO.StreamWriter]::new($manifestEntry.Open(), [Text.UTF8Encoding]::new($true))
            try {
                $manifestWriter.WriteLine('FMTPlanner Windows stable package manifest')
                $manifestWriter.WriteLine('Version: ' + $ReleaseVersion)
                $manifestWriter.WriteLine('Runtime files: ' + $files.Count)
                $manifestWriter.WriteLine('Excluded non-runtime/developer files: ' + $excludedFiles.Count)
                $manifestWriter.WriteLine('Excluded bytes: ' + $excludedBytes)
                $manifestWriter.WriteLine('')
                $manifestWriter.WriteLine('Exclusion policy:')
                $manifestWriter.WriteLine('- PDB debug symbols are retained in build output, not in the public ZIP.')
                $manifestWriter.WriteLine('- macOS/Linux .dylib/.so files are not included in the Windows ZIP.')
                $manifestWriter.WriteLine('- plugins/example*.cs developer samples remain in source control, not in the stable ZIP.')
                $manifestWriter.WriteLine('- Operational DLL plugins, drivers, maps, languages and scripts remain bundled.')
            }
            finally {
                $manifestWriter.Dispose()
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    Move-Item -LiteralPath $temporaryOutput -Destination $outputFullPath -Force
    $outputFile = Get-Item -LiteralPath $outputFullPath
    [PSCustomObject]@{
        Output = $outputFile.FullName
        Packaging = 'Portable ZIP (no self-extracting launcher)'
        FilesBundled = $files.Count + $documentationFiles.Count + 2
        FilesExcluded = $excludedFiles.Count
        BytesExcluded = $excludedBytes
        SizeBytes = $outputFile.Length
        SHA256 = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryOutput -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryOutput -Force -ErrorAction SilentlyContinue
    }
}
