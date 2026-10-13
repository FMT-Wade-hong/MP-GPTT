param([string]$Directory='bin/HomeLine123/net461')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$maps=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Maps.dll'))
$type=$maps.GetType('MissionPlanner.ArduPilot.WPOverlay',$true)
$overlay=[Activator]::CreateInstance($type)
$method=$type.GetMethod('RegenerateWPRoute',[Reflection.BindingFlags]'Instance,NonPublic')
$list=[Activator]::CreateInstance($method.GetParameters()[0].ParameterType)
$pointType=$method.GetParameters()[1].ParameterType
foreach($coords in @(@(23.0,120.0,0.0),@(23.001,120.01,0.0),@(22.999,120.01,0.0))) {
    $point=[Activator]::CreateInstance($pointType,[object[]]$coords)
    $list.Add($point)
}
$homePoint=$list[0]
$method.Invoke($overlay,[object[]]@($list,$homePoint,$true)) | Out-Null
$route=@($overlay.overlay.Routes | Where-Object Name -eq 'home route')[0]
if($route.Stroke.LineJoin -ne [Drawing.Drawing2D.LineJoin]::Bevel) { throw 'HOME join still has a spike' }
if($route.Points.Count -ne 3 -or $route.Points[1].Lat -ne 23 -or $route.Points[1].Lng -ne 120) { throw 'HOME geometry changed' }
$source=Get-Content "$PSScriptRoot/../GCSViews/FlightData.cs" -Raw
if(!$source.Contains('homeroute.Stroke.LineJoin = System.Drawing.Drawing2D.LineJoin.Bevel;')) { throw 'Legacy flight route missing fix' }
# Near-parallel return/departure legs reproduce the miter protrusion. Test
# solid and dashed styles: distant HOME routes deliberately use solid lines.
$points=[Drawing.PointF[]]@([Drawing.PointF]::new(180,45),[Drawing.PointF]::new(60,60),[Drawing.PointF]::new(180,75))
foreach($style in @([Drawing.Drawing2D.DashStyle]::Solid,[Drawing.Drawing2D.DashStyle]::Dash)) {
    foreach($join in @([Drawing.Drawing2D.LineJoin]::Miter,[Drawing.Drawing2D.LineJoin]::Bevel)) {
        $bitmap=[Drawing.Bitmap]::new(220,120)
        $g=[Drawing.Graphics]::FromImage($bitmap)
        $pen=$route.Stroke.Clone()
        try {
            $g.Clear([Drawing.Color]::Black)
            $pen.DashStyle=$style
            $pen.LineJoin=$join
            $g.DrawLines($pen,$points)
            $minX=220
            for($x=0;$x -lt 220;$x++) {
                for($y=0;$y -lt 120;$y++) {
                    if($bitmap.GetPixel($x,$y).R -gt 100) { $minX=[Math]::Min($minX,$x) }
                }
            }
            if($join -eq [Drawing.Drawing2D.LineJoin]::Bevel -and $minX -lt 60) { throw "Line extends beyond HOME: $style, x=$minX" }
            if($join -eq [Drawing.Drawing2D.LineJoin]::Miter -and $style -eq [Drawing.Drawing2D.DashStyle]::Solid -and $minX -ge 60) { throw 'Regression fixture did not reproduce original spike' }
            $bitmap.Save((Join-Path $binaryDirectory "home-join-$style-$join.png"))
            Write-Output "$style / $join : leftmost yellow=$minX; HOME=60"
        } finally { $pen.Dispose(); $g.Dispose(); $bitmap.Dispose() }
    }
}
'PASS: compiled HOME pen and coordinates; solid/dashed rendering bounded at HOME; flight route source guard. Offline only.'
