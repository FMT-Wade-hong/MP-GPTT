param(
    [string]$BinaryDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\Release\net461')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$results = [System.Collections.Generic.List[object]]::new()

function Test-FmtCondition {
    param([string]$Name, [bool]$Condition, [string]$Detail)
    $results.Add([PSCustomObject]@{ Name = $Name; Passed = $Condition; Detail = $Detail })
    if (-not $Condition) {
        throw "Verification failed: $Name - $Detail"
    }
}

$BinaryDirectory = (Resolve-Path -LiteralPath $BinaryDirectory).Path
$binaryPath = Join-Path $BinaryDirectory 'FMTPlanner.exe'
Test-FmtCondition 'Release binary exists' (Test-Path -LiteralPath $binaryPath -PathType Leaf) $binaryPath

$dependencyResolver = [ResolveEventHandler] {
    param($sender, $eventArgs)

    $dependencyName = ([Reflection.AssemblyName]::new($eventArgs.Name)).Name + '.dll'
    $dependencyPath = Join-Path $BinaryDirectory $dependencyName
    if (Test-Path -LiteralPath $dependencyPath -PathType Leaf) {
        return [Reflection.Assembly]::LoadFrom($dependencyPath)
    }

    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($dependencyResolver)

$assembly = [Reflection.Assembly]::LoadFrom($binaryPath)
$binding = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$mainType = $assembly.GetType('MissionPlanner.MainV2', $true)
$flightDataType = $assembly.GetType('MissionPlanner.GCSViews.FlightData', $true)
$parseMethod = $flightDataType.GetMethod('TryParseFmtQnhPascals', $binding)

Test-FmtCondition 'QNH toolbar handler compiled' ($null -ne $mainType.GetMethod('MenuFmtQnh_Click', $binding)) 'toolbar click handler is present'
Test-FmtCondition 'QNH action compiled' ($null -ne $flightDataType.GetMethod('ExecuteFmtQnh', $binding)) 'Flight Data QNH action is present'
Test-FmtCondition 'QNH validator compiled' ($null -ne $parseMethod) 'input validation helper is present'

$validArgs = [object[]]@('101325', 0.0)
$validResult = [bool]$parseMethod.Invoke($null, $validArgs)
Test-FmtCondition 'Standard QNH accepted' ($validResult -and [Math]::Abs(([double]$validArgs[1]) - 101325.0) -lt 0.01) '101325 Pa is valid'

$lowArgs = [object[]]@('79999', 0.0)
Test-FmtCondition 'Unsafe low QNH rejected' (-not [bool]$parseMethod.Invoke($null, $lowArgs)) 'values below 80000 Pa are rejected'

$invalidArgs = [object[]]@('not-a-number', 0.0)
Test-FmtCondition 'Non-numeric QNH rejected' (-not [bool]$parseMethod.Invoke($null, $invalidArgs)) 'invalid text is rejected'

$mainSource = Get-Content -LiteralPath (Join-Path $projectRoot 'MainV2.cs') -Raw -Encoding UTF8
$flightDataSource = Get-Content -LiteralPath (Join-Path $projectRoot 'GCSViews\FlightData.cs') -Raw -Encoding UTF8
$airspeedInsert = 'MainMenu.Items.Insert(quickActionIndex + 1, MenuFmtAirspeedZero);'
$qnhInsert = 'MainMenu.Items.Insert(quickActionIndex + 2, MenuFmtQnh);'

Test-FmtCondition 'QNH follows Airspeed Zero' ($mainSource.IndexOf($qnhInsert) -gt $mainSource.IndexOf($airspeedInsert)) 'toolbar order is Arm, Airspeed Zero, QNH'
Test-FmtCondition 'QNH disabled while armed' ($mainSource.Contains('MenuFmtQnh.Enabled = connected && !armed && !comPort.ReadOnly;')) 'visible quick action has a disarmed safety gate'
Test-FmtCondition 'Legacy pressure parameter supported' ($flightDataSource.Contains('"GND_ABS_PRESS"')) 'older firmware parameter is supported'
Test-FmtCondition 'Current pressure parameter supported' ($flightDataSource.Contains('"BARO1_GND_PRESS"')) 'newer firmware parameter is supported'
Test-FmtCondition 'QNH uses pascals' ($flightDataSource.Contains('101325 Pa = 1013.25 hPa')) 'dialog explains Pa and hPa conversion'
Test-FmtCondition 'Read-only rejection explained' ($flightDataSource.Contains('as read-only')) 'firmware rejection is reported clearly'

$results | Format-Table -AutoSize
[PSCustomObject]@{
    Passed = @($results | Where-Object Passed).Count
    Failed = @($results | Where-Object { -not $_.Passed }).Count
}
