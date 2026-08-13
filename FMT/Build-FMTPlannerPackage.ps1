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
$staleMissionPlannerOutputs = @(
    'MissionPlanner.exe',
    'MissionPlanner.exe.config',
    'MissionPlanner.pdb'
)

try {
    $stream = [IO.File]::Open($temporaryOutput, [IO.FileMode]::CreateNew)
    try {
        $archive = [IO.Compression.ZipArchive]::new(
            $stream, [IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            $files = @(Get-ChildItem -LiteralPath $sourcePath -Recurse -File | Where-Object {
                $relative = $_.FullName.Substring($sourcePath.Length).TrimStart('\', '/')
                $staleMissionPlannerOutputs -notcontains $relative
            })

            foreach ($file in $files) {
                $relativePath = $file.FullName.Substring($sourcePath.Length).TrimStart('\', '/')
                $entryName = ($rootFolder + '/' + $relativePath.Replace('\', '/'))
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $file.FullName,
                    $entryName,
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
                $writer.WriteLine('')
                $writer.WriteLine('Publisher: FMT飛貓科技')
                $writer.WriteLine('Website: https://www.feimaotec.com')
                $writer.WriteLine('')
                $writer.WriteLine('This portable ZIP avoids self-extracting launchers that can trigger antivirus heuristics.')
            }
            finally {
                $writer.Dispose()
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
        FilesBundled = $files.Count + 1
        SizeBytes = $outputFile.Length
        SHA256 = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryOutput -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryOutput -Force -ErrorAction SilentlyContinue
    }
}
