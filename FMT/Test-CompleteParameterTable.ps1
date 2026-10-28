param([string]$Directory = 'bin/Local124/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
Add-Type -AssemblyName System.IO.Compression
$mav = [MissionPlanner.MainV2]::comPort.MAV
$paramType = $mav.param.GetType().BaseType.GetGenericArguments()[0]
$ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 } | Select-Object -First 1
$wire = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams',$true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$statics = [Reflection.BindingFlags]'Static,NonPublic'
$process = $type.GetMethod('processToScreen',$flags)
$page = [Activator]::CreateInstance($type)
$total = 0
try {
    foreach ($case in @(@('Copter4.6.3','ArduCopter2'),@('Copter4.7.2','ArduCopter2'),@('Plane4.7.2','ArduPlane'),@('Rover4.7.2','ArduRover'),@('Sub4.7.2','ArduSub'),@('Tracker4.7.2','ArduTracker'))) {
        $input = [IO.File]::OpenRead((Join-Path $PSScriptRoot ('../ExtLibs/Utilities/ParameterMetadata/'+$case[0]+'.apm.pdef.xml.gz')))
        $gzip = New-Object IO.Compression.GZipStream $input,([IO.Compression.CompressionMode]::Decompress)
        $reader = New-Object IO.StreamReader $gzip
        try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose(); $input.Dispose() }
        $expected = @{}
        foreach ($node in $xml.SelectNodes('//param')) {
            $name = ([string]$node.name -split ':')[-1]
            if (!$expected.ContainsKey($name)) { $expected[$name] = $node }
        }
        $mav.param.Clear()
        foreach ($name in $expected.Keys) { $mav.param.Add($ctor.Invoke(@($name,[double]0,$wire,$null))) }
        $mav.VersionString = 'ArduPilot V' + ($case[0] -replace '^[A-Za-z]+','')
        $mav.cs.firmware = [Enum]::Parse($mav.cs.firmware.GetType(),$case[1])
        $type.GetField('fmtMetadataVehicle',$flags).SetValue($page,$case[0])
        $type.GetField('startup',$statics).SetValue($null,$true)
        $process.Invoke($page,@()) | Out-Null
        $grid = $type.GetField('Params',$flags).GetValue($page)
        $rows = @($grid.Rows | Where-Object { !$_.IsNewRow })
        if ($rows.Count -ne $expected.Count) { throw "Table omitted parameters: $($case[0]) $($rows.Count)/$($expected.Count)" }
        $nameIndex = $type.GetField('Command',$flags).GetValue($page).Index
        $valueIndex = $type.GetField('Value',$flags).GetValue($page).Index
        $unitIndex = $type.GetField('Units',$flags).GetValue($page).Index
        foreach ($row in $rows) {
            $name = [string]$row.Cells[$nameIndex].Value
            if (!$expected.ContainsKey($name) -or [double]$row.Cells[$valueIndex].Value -ne 0) { throw 'Table synthesized a name or rewrote a value' }
            $unitNode = $expected[$name].SelectSingleNode('field[@name="Units"]')
            $unit = if ($unitNode) { $unitNode.InnerText } else { '' }
            if ([string]$row.Cells[$unitIndex].Value -cne $unit) { throw "Wrong table units: $($case[0]) $name" }
        }
        if ($type.GetField('_changes',$flags).GetValue($page).Count) { throw 'Rendering staged writes' }
        $total += $rows.Count
        "PASS: $($case[0]) $($rows.Count) actual grid rows, values and units"
    }
} finally { $page.Dispose(); $mav.param.Clear() }
"PASS: $total complete-table rows across old/new firmware and five vehicle families; no dropped keys, unit mismatches or staged writes"
