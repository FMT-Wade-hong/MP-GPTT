param([string]$Directory = 'bin/LogSaveLocationTest/net461')
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$method = $assembly.GetType('MissionPlanner.Log.LogDownloadMavLink').GetMethod('AvailableDownloadPath', [Reflection.BindingFlags]'Static,NonPublic')
$fixture = Join-Path (Resolve-Path 'bin').Path ('log-path-tests-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$requested = Join-Path $fixture 'flight.bin'
if ($method.Invoke($null, @([string]$fixture, 'flight.bin')) -ne $requested) { throw 'Wrong destination' }
[IO.File]::WriteAllText($requested, 'existing-log')
$second = $method.Invoke($null, @([string]$fixture, 'flight.bin'))
if ($second -ne (Join-Path $fixture 'flight (1).bin')) { throw 'Collision not handled' }
[IO.File]::WriteAllText($second, 'second-log')
if ($method.Invoke($null, @([string]$fixture, 'flight.bin')) -ne (Join-Path $fixture 'flight (2).bin')) { throw 'Repeated collision not handled' }
if ([IO.File]::ReadAllText($requested) -ne 'existing-log') { throw 'Existing log changed' }
'PASS: selected destination and non-overwriting numbered filenames'
$source = Get-Content (Join-Path $PSScriptRoot '../Log/LogDownloadMavLink.cs') -Raw
if ([regex]::Matches($source, 'if \(!TryChooseDownloadDirectory\(out destination\)\) return;').Count -ne 2) { throw 'Both buttons must honor dialog cancellation' }
$getLog = $source.Substring($source.IndexOf('async Task<string> GetLog('))
$getLog = $getLog.Substring(0, $getLog.IndexOf('return logfile;'))
if ($getLog.Contains('Settings.Instance.LogDir')) { throw 'Download still uses global directory' }
if (!$getLog.Contains('AvailableDownloadPath(destination,') -or !$source.Contains('GetLog(entry.id, fileName, destination)')) { throw 'Batch destination not propagated' }
'PASS: both buttons honor cancellation; download and GPS rename use chosen directory (source checks)'
