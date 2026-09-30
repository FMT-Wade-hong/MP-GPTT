param([string]$Directory = 'bin/TaskbarIdentityFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$resources = $assembly.GetType('MissionPlanner.Properties.Resources',$true)
$icon = $resources.GetProperty('mpdesktop',[Reflection.BindingFlags]'Public,Static').GetValue($null)
$file = New-Object Drawing.Icon (Join-Path (Get-Location) 'mpdesktop.ico')
$bytes = [IO.File]::ReadAllBytes((Join-Path (Get-Location) 'mpdesktop.ico'))
$count = [BitConverter]::ToUInt16($bytes,4)
for ($i = 0; $i -lt $count; $i++) {
    $offset = [BitConverter]::ToUInt32($bytes,6 + 16 * $i + 12)
    if ([BitConverter]::ToUInt32($bytes,$offset) -ne 40) { throw 'ICO frame is not a compatible DIB' }
}
foreach ($size in @(16,24,32,48,64,128,256)) {
    $resourceFrame = New-Object Drawing.Icon $icon,$size,$size
    $sourceFrame = New-Object Drawing.Icon $file,$size,$size
    $actual = $resourceFrame.ToBitmap()
    $expected = $sourceFrame.ToBitmap()
    try {
        "Requested $size; resource $($actual.Size); source $($expected.Size)"
        if ($actual.Size -ne $expected.Size) { throw "Embedded icon size differs at $size" }
        for ($y=0; $y -lt $actual.Height; $y++) {
            for ($x=0; $x -lt $actual.Width; $x++) {
                if ($actual.GetPixel($x,$y).ToArgb() -ne $expected.GetPixel($x,$y).ToArgb()) {
                    throw "Embedded icon differs from source at size $size"
                }
            }
        }
    } finally { $actual.Dispose(); $expected.Dispose(); $resourceFrame.Dispose(); $sourceFrame.Dispose() }
}
'PASS: 7 DIB frames; embedded resource matches source pixels at every icon size'
foreach ($entry in @(@('resource',$icon),@('source',$file))) {
    $bitmap = $entry[1].ToBitmap()
    try { $bitmap.Save((Join-Path (Resolve-Path $Directory) ($entry[0] + '-icon.png'))) }
    finally { $bitmap.Dispose() }
    "$($entry[0]): $($entry[1].Size)"
}
$file.Dispose()
