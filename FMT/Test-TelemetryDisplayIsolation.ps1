param([string]$Directory = 'bin/ComPortResponsivenessFix/net461')
$ErrorActionPreference = 'Stop'
$path = (Resolve-Path "$Directory/MissionPlanner.ArduPilot.dll").Path
[Reflection.Assembly]::LoadFrom($path) | Out-Null
$facade = Join-Path (Resolve-Path $Directory) 'netstandard.dll'
if (!(Test-Path -LiteralPath $facade)) {
    # Reference-only facade is supplied by the targeting pack, not necessarily copied to output.
    $referenceRoot = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework'
    $facade = Get-ChildItem -LiteralPath $referenceRoot -Filter netstandard.dll -Recurse -File |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (!$facade) { throw 'Install the .NET Framework targeting pack with a netstandard facade to run this test.' }
}
$runtimeDirectory = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
Add-Type -ReferencedAssemblies @($path, $facade, (Join-Path $runtimeDirectory 'mscorlib.dll'), (Join-Path $runtimeDirectory 'System.dll'), (Join-Path $runtimeDirectory 'System.Core.dll')) -TypeDefinition @'
using System;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
public static class DisplayIsolationTest {
    public static void Run() {
        var state = new CurrentState();
        var entered = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var holder = Task.Run(() => { lock(state) { entered.Set(); release.Wait(); } });
        try {
            if (!entered.Wait(2000)) throw new Exception("Test lock not acquired");
            int count = 0;
            var display = Task.Run(() => {
                for (int i = 0; i < 1000; i++)
                    state.BindCurrentState(s => { if (!object.ReferenceEquals(s, state)) throw new Exception(); count++; });
            });
            if (!display.Wait(2000)) throw new Exception("Display waits for telemetry lock");
            if (count != 1000) throw new Exception("Display updates lost");
            // No MAV parent/serial port is attached: successful calls cannot depend on either.
        } finally { release.Set(); holder.Wait(); entered.Dispose(); release.Dispose(); }
    }
}
'@
[DisplayIsolationTest]::Run()
'PASS: 1000 display bindings complete while telemetry lock remains held; no COM port attached'
$source = Get-Content (Join-Path $PSScriptRoot '../GCSViews/FlightData.cs') -Raw
$body = $source.Substring($source.IndexOf('private void updateBindingSourceWork()'))
$body = $body.Substring(0, $body.IndexOf('private void updateClearMissionRouteMarkers()'))
if ($body.Contains('.UpdateCurrentSettings(')) { throw 'Display still calls telemetry housekeeping' }
'PASS: all flight display tabs use display-only binding (source check)'
