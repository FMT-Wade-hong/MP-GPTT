param([string]$Directory='bin/Release123Final/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$controls=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Controls.dll'))
$hudType=$controls.GetType('MissionPlanner.Controls.HUD',$true)
if($hudType.GetNestedType('DisplayAngle',[Reflection.BindingFlags]'NonPublic')) { throw 'Experimental interpolation remains' }
if($hudType.GetMethod('SetAttitudeSource')) { throw 'Experimental feed remains' }
$source=Get-Content "$PSScriptRoot/../ExtLibs/Controls/HUD.cs" -Raw
if(!$source.Contains('TotalMilliseconds < 30 && (_bgimage == null)')) { throw 'Default redraw limit changed' }
$designer=Get-Content "$PSScriptRoot/../GCSViews/FlightData.Designer.cs" -Raw
foreach($property in @('roll','pitch','heading')) {
    if(!$designer.Contains('Binding("'+$property+'", this.bindingSourceHud')) { throw "Default binding absent: $property" }
}
if(Test-Path "$PSScriptRoot/../GCSViews/FlightData.AttitudeRefresh.cs") { throw 'Fast feed file remains' }
'PASS: experimental smoothing removed; original three-axis bindings and redraw cadence retained.'
