param([string]$Directory = 'bin/AirspeedVisibilityFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$method = $assembly.GetType('MissionPlanner.MainV2').GetMethod('IsFmtAirspeedZeroAvailable',[Reflection.BindingFlags]'Static,NonPublic')
$listType = $method.GetParameters()[0].ParameterType
$paramType = $listType.BaseType.GetGenericArguments()[0]
$ctor = $paramType.GetConstructors() | Where-Object { $_.GetParameters().Count -eq 4 }
$wireType = [Enum]::ToObject($ctor.GetParameters()[2].ParameterType,9)
$cases = @(
    @{Values=@{}; Expected=$false},
    @{Values=@{ARSPD_USE=0;ARSPD_TYPE=1}; Expected=$true},
    @{Values=@{ARSPD_USE=1;ARSPD_TYPE=0}; Expected=$false},
    @{Values=@{ARSPD_USE=1;ARSPD_TYPE=1}; Expected=$true},
    @{Values=@{ARSPD_USE=2;ARSPD_TYPE=1}; Expected=$true},
    @{Values=@{ARSPD_USE=1;ARSPD_ENABLE=0}; Expected=$false},
    @{Values=@{ARSPD_USE=1;ARSPD_ENABLE=1}; Expected=$true},
    @{Values=@{ARSPD_USE=1;ARSPD_ENABLE=0;ARSPD_TYPE=1}; Expected=$false},
    @{Values=@{ARSPD_TYPE=1}; Expected=$true},
    @{Values=@{ARSPD_TYPE=0;ARSPD2_TYPE=1;ARSPD2_USE=0}; Expected=$true},
    @{Values=@{ARSPD_ENABLE=0;ARSPD2_TYPE=1}; Expected=$false},
    @{Values=@{ARSPD_TYPE=0;ARSPD2_TYPE=0}; Expected=$false}
)
foreach ($case in $cases) {
    $parameters = [Activator]::CreateInstance($listType)
    foreach ($pair in $case.Values.GetEnumerator()) {
        $parameters.Add($ctor.Invoke(@($pair.Key,[double]$pair.Value,$wireType,$null)))
    }
    $actual = $method.Invoke($null,[object[]]@(,$parameters))
    if ($actual -ne $case.Expected) { throw "Unexpected visibility for $($case.Values | ConvertTo-Json -Compress)" }
}
'PASS: airspeed off/on/auto, absent parameters, disabled type, legacy enable and global disable'
