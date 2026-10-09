param([string]$Directory = 'bin/MapZoom121/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$type = $assembly.GetType('MissionPlanner.GCSViews.FlightData+FmtInitialMapZoom',$true)
$state = [Activator]::CreateInstance($type,$true)
$next = $type.GetMethod('Next',[Reflection.BindingFlags]'Instance,NonPublic')
$first = New-Object object
$second = New-Object object
$cases = @(
    @($first,$false,$false,8),
    @($first,$false,$false,$null),
    @($first,$true,$false,8),
    @($first,$true,$false,$null),
    @($first,$true,$true,16),
    @($first,$true,$true,$null),
    @($first,$true,$false,$null),
    @($first,$true,$true,$null),
    @($second,$true,$true,16),
    @($second,$true,$true,$null),
    @($second,$false,$false,8),
    @($second,$true,$false,8),
    @($second,$true,$true,16)
)
foreach ($case in $cases) {
    $actual = $next.Invoke($state,[object[]]@($case[0],$case[1],$case[2]))
    if ($actual -ne $case[3]) { throw "Expected $($case[3]), got $actual" }
}
$source = Get-Content (Join-Path $PSScriptRoot '../GCSViews/FlightData.cs') -Raw
if ($source.Contains('updateMapZoom(17)')) { throw 'Legacy track-triggered zoom remains' }
foreach ($guard in @('port.logreadmode','age <= 3','UpdateFmtInitialMapZoom();')) {
    if (!$source.Contains($guard)) { throw "Missing guard: $guard" }
}
'PASS: once-only 8/16; repeated updates preserve manual zoom; GPS loss/recovery does not retrigger; reconnect/target switch; replay and stale GPS guards'
