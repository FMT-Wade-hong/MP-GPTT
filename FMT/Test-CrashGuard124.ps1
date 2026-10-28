param([string]$Directory='bin/CrashGuard124/net461')
$ErrorActionPreference='Stop'
$binary=(Resolve-Path $Directory).Path
$compiler='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$refs=@('MissionPlanner.ArduPilot.dll','MissionPlanner.Comms.dll','MAVLink.dll','MissionPlanner.Utilities.dll','log4net.dll','Interfaces.dll') | ForEach-Object { '/r:'+(Join-Path $binary $_) }
$exe=Join-Path $binary 'CrashGuardHarness.exe'
& $compiler /nologo /target:exe "/out:$exe" /r:System.Core.dll '/r:C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.7.2/Facades/netstandard.dll' $refs "$PSScriptRoot/../MissionPlannerTests/CrashGuardHarness.cs"
if($LASTEXITCODE){throw 'Crash guard harness compilation failed'}
Copy-Item -LiteralPath (Join-Path $binary 'FMTPlanner.exe.config') -Destination ($exe+'.config') -Force
$testDir=Join-Path $binary ('crash-test-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testDir)
& $exe (Join-Path $binary 'FMTPlanner.exe') $testDir
if($LASTEXITCODE){throw 'Crash guard harness failed'}
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$controls=[Reflection.Assembly]::LoadFrom((Join-Path $binary 'MissionPlanner.Controls.dll'))
$type=$controls.GetType('MissionPlanner.Controls.HUD',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$paint=$type.GetMethod('doPaint',$flags)
function Assert-Released($bitmap,[string]$message) {
    $valid=$false
    try {$valid=$bitmap.Width -gt 0} catch {}
    if($valid){throw $message}
}
$borrowed=New-Object Drawing.Bitmap 32,32
try {
    foreach($cycle in 1..25) {
        $hud=[Activator]::CreateInstance($type)
        try {
            $hud.opengl=$false; $hud.bgimage=$borrowed; $hud.streamjpgenable=$true
            foreach($frame in 1..40) {
                $old=$hud.objBitmap
                $oldGraphics=$type.GetField('bitmapGraphics',$flags).GetValue($hud)
                $hud.Size=[Drawing.Size]::new((320+($frame%2)*80),(240+($frame%2)*60))
                [void]$paint.Invoke($hud,@())
                Assert-Released $old 'Old HUD bitmap retained after resize'
                $graphicsAlive=$false
                try {$graphicsAlive=$oldGraphics.DpiX -gt 0} catch {}
                if($graphicsAlive){throw 'Old HUD Graphics retained after resize'}
                $snapshot=$hud.GetSnapshot()
                try {if($snapshot.Width -ne $hud.Width){throw 'Snapshot has incorrect size'}} finally {$snapshot.Dispose()}
                $encoded=$hud.streamjpg.ToArray()
                if($encoded.Length -lt 10 -or $encoded[0] -ne 255 -or $encoded[1] -ne 216){throw 'Incomplete JPEG published'}
                $hud.hudcolor=[Drawing.Color]::FromArgb(200,220,($frame%256))
            }
            $old=$hud.objBitmap
            $hud.Dispose()
            Assert-Released $old 'HUD bitmap retained after Dispose'
            if($borrowed.Width -ne 32){throw 'Borrowed background disposed by HUD'}
        } finally {$hud.Dispose()}
    }
} finally {$borrowed.Dispose()}
# Exercise the actual OpenGL control and context when the local driver supports it.
$form=New-Object Windows.Forms.Form
$hud=[Activator]::CreateInstance($type)
try {
    $form.ShowInTaskbar=$false; $form.StartPosition='Manual'; $form.Location=[Drawing.Point]::new(-30000,-30000)
    $hud.opengl=$true; $hud.streamjpgenable=$true; $hud.Size=[Drawing.Size]::new(400,300)
    $form.ClientSize=$hud.Size; $form.Controls.Add($hud); $form.Show()
    [Windows.Forms.Application]::DoEvents(); $hud.Refresh()
    if($hud.opengl) {
        foreach($frame in 1..100) {
            $old=$hud.objBitmap
            $hud.Size=[Drawing.Size]::new((400+($frame%2)*80),(300+($frame%2)*60))
            $type.GetField('starttime',$flags).SetValue($hud,[DateTime]::MinValue)
            $hud.Refresh(); [Windows.Forms.Application]::DoEvents()
            if(!$hud.opengl){throw 'OpenGL failed during resize/capture stress'}
            Assert-Released $old 'Old GL screenshot retained after replacement'
        }
        $old=$hud.objBitmap
        $hud.Dispose()
        Assert-Released $old 'GL screenshot retained after disposal'
        'PASS: 100 actual OpenGL resize/capture cycles and context cleanup.'
    } else {'SKIP: Local driver could not initialize OpenGL; GDI resource tests still apply.'}
} finally {$form.Dispose(); $hud.Dispose()}
$ice=$assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigIceEngine',$true)
[Windows.Forms.Control]::CheckForIllegalCrossThreadCalls=$true
foreach($i in 1..30) {
    $page=[Activator]::CreateInstance($ice)
    try {$page.Activate(); $page.CreateControl(); $page.Deactivate(); $page.Activate(); $page.Deactivate()}
    finally {$page.Dispose()}
}
[Windows.Forms.Application]::DoEvents()
'PASS: 1000 HUD resize/paint/JPEG frames, 25 complete disposals, snapshot ownership, borrowed-background preservation; 30 ICE page lifecycle cycles; all offline.'
