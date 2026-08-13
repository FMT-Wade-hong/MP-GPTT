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

$binaryPath = Join-Path $BinaryDirectory 'FMTPlanner.exe'
Test-FmtCondition 'Release binary exists' (Test-Path -LiteralPath $binaryPath -PathType Leaf) $binaryPath

$BinaryDirectory = (Resolve-Path -LiteralPath $BinaryDirectory).Path
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
$parameterType = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigRawParams', $true)
$themeType = $assembly.GetType('MissionPlanner.Utilities.ThemeManager', $true)
$themeMethod = $parameterType.GetMethod('ApplyFmtReadableTheme', $binding)
Test-FmtCondition 'Parameter theme method compiled' ($null -ne $themeMethod) 'dynamic page has an explicit theme hook'

$parameterControl = [Activator]::CreateInstance($parameterType)
try {
    $themeMethod.Invoke($parameterControl, @())
    $grid = $parameterType.GetField('Params', $binding).GetValue($parameterControl)
    $tree = $parameterType.GetField('treeView1', $binding).GetValue($parameterControl)
    $controlBackground = $themeType.GetField('ControlBGColor', $binding).GetValue($null)
    $textColor = $themeType.GetField('TextColor', $binding).GetValue($null)
    $background = $themeType.GetField('BGColor', $binding).GetValue($null)

    Test-FmtCondition 'Parameter grid dark background' ($grid.DefaultCellStyle.BackColor.ToArgb() -eq $controlBackground.ToArgb()) $grid.DefaultCellStyle.BackColor.ToString()
    Test-FmtCondition 'Parameter grid readable text' ($grid.DefaultCellStyle.ForeColor.ToArgb() -eq $textColor.ToArgb()) $grid.DefaultCellStyle.ForeColor.ToString()
    Test-FmtCondition 'Alternating row theme' ($grid.AlternatingRowsDefaultCellStyle.BackColor.ToArgb() -eq $background.ToArgb()) $grid.AlternatingRowsDefaultCellStyle.BackColor.ToString()
    Test-FmtCondition 'Parameter header readable text' ($grid.ColumnHeadersDefaultCellStyle.ForeColor.ToArgb() -eq $textColor.ToArgb()) $grid.ColumnHeadersDefaultCellStyle.ForeColor.ToString()
    Test-FmtCondition 'Parameter tree dark background' ($tree.BackColor.ToArgb() -eq $controlBackground.ToArgb()) $tree.BackColor.ToString()
    Test-FmtCondition 'Parameter tree readable text' ($tree.ForeColor.ToArgb() -eq $textColor.ToArgb()) $tree.ForeColor.ToString()
}
finally {
    $parameterControl.Dispose()
}

$wrapperSource = Get-Content -LiteralPath (Join-Path $projectRoot 'FMT\FmtProtectedParameters.cs') -Raw -Encoding UTF8
Test-FmtCondition 'Cached page cleared before prompt' ($wrapperSource.Contains('Controls.Clear();') -and $wrapperSource.IndexOf('Controls.Clear();') -lt $wrapperSource.IndexOf('access.ShowDialog')) 'old white page is removed before password prompt'
Test-FmtCondition 'Control hidden during load' ($wrapperSource.Contains('parameterControl.Visible = false;') -and $wrapperSource.Contains('parameterControl.Visible = true;')) 'prevents an unthemed loading flash'
Test-FmtCondition 'Theme reapplied after activation' ($wrapperSource.IndexOf('parameterControl.ApplyFmtReadableTheme();', $wrapperSource.IndexOf('parameterControl.Activate();')) -gt $wrapperSource.IndexOf('parameterControl.Activate();')) 'rows are restyled after parameter population'

$results | Format-Table -AutoSize
[PSCustomObject]@{
    Passed = @($results | Where-Object Passed).Count
    Failed = @($results | Where-Object { -not $_.Passed }).Count
}
