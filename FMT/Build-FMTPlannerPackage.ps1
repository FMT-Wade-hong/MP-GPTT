param(
    [string]$SourceDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Release\net461'),
    [string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Package\FMTPlanner.exe')
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$sourcePath = (Resolve-Path -LiteralPath $SourceDirectory).Path
$applicationPath = Join-Path $sourcePath 'MissionPlanner.exe'
if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
    throw "MissionPlanner.exe was not found in $sourcePath. Build the Release configuration first."
}

$outputDirectory = Split-Path $OutputPath -Parent
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    'fmtplanner-package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $payloadPath = Join-Path $temporaryRoot 'FMTPlanner.Payload.zip'
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $sourcePath,
        $payloadPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    $frameworkRoots = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'))
    $compiler = $frameworkRoots |
        ForEach-Object { Join-Path $_ 'csc.exe' } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
    if (-not $compiler) {
        throw 'The .NET Framework C# compiler was not found.'
    }

    $frameworkDirectory = Split-Path $compiler -Parent
    $launcherSource = Join-Path $PSScriptRoot 'Packaging\FMTPlannerLauncher.cs'
    $iconPath = Join-Path $projectRoot 'mpdesktop.ico'
    $compilerArguments = @(
        '/nologo',
        '/target:winexe',
        '/platform:anycpu',
        '/optimize+',
        ('/out:' + $outputFullPath),
        ('/win32icon:' + $iconPath),
        ('/resource:' + $payloadPath + ',FMTPlanner.Payload.zip'),
        ('/reference:' + (Join-Path $frameworkDirectory 'System.dll')),
        ('/reference:' + (Join-Path $frameworkDirectory 'System.Core.dll')),
        ('/reference:' + (Join-Path $frameworkDirectory 'System.Windows.Forms.dll')),
        ('/reference:' + (Join-Path $frameworkDirectory 'System.Drawing.dll')),
        ('/reference:' + (Join-Path $frameworkDirectory 'System.IO.Compression.dll')),
        $launcherSource)

    & $compiler $compilerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "FMTPlanner launcher compilation failed with exit code $LASTEXITCODE."
    }

    $outputFile = Get-Item -LiteralPath $outputFullPath
    $hash = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [PSCustomObject]@{
        Output = $outputFile.FullName
        FilesBundled = @(Get-ChildItem -LiteralPath $sourcePath -Recurse -File).Count
        SizeBytes = $outputFile.Length
        SHA256 = $hash
    }
}
finally {
    $expectedPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
    if ($resolvedTemporaryRoot.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path $resolvedTemporaryRoot -Leaf).StartsWith('fmtplanner-package-')) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
