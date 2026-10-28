param([string]$Directory='bin/Release124/net461', [ValidateRange(30,1800)][int]$Seconds=600,
    [string]$Report='tmp/release124-soak.json')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class FmtSoakResources {
    [DllImport("user32.dll")] public static extern int GetGuiResources(IntPtr process, int kind);
}
'@
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$controls=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Controls.dll'))
$hudType=$controls.GetType('MissionPlanner.Controls.HUD',$true)
$modeType=$assembly.GetType('MissionPlanner.FMT.FmtFlightModeBar',$true)
$iceType=$assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigIceEngine',$true)
$update=$modeType.GetMethod('UpdateVehicle',$flags)
$height=$modeType.GetMethod('SetAvailableHeight',$flags)
$firmwareType=$update.GetParameters()[0].ParameterType
$profiles=@(@('ArduCopter2',$false,$false),@('ArduPlane',$false,$false),@('ArduPlane',$true,$false),
    @('ArduCopter2',$false,$true),@('ArduRover',$false,$false),@('ArduSub',$false,$false))
$form=New-Object Windows.Forms.Form
$gdi=[Activator]::CreateInstance($hudType)
$gl=[Activator]::CreateInstance($hudType)
$bar=[Activator]::CreateInstance($modeType,$true)
$ice=$null
$fontProbe=New-Object Drawing.Bitmap 2,2
$graphics=[Drawing.Graphics]::FromImage($fontProbe)
$samples=New-Object Collections.Generic.List[object]
$frames=0; $pages=0; $nextSample=0
$process=[Diagnostics.Process]::GetCurrentProcess()
function Sample-Resources([double]$Elapsed) {
    [GC]::Collect(); [GC]::WaitForPendingFinalizers(); [GC]::Collect()
    $process.Refresh()
    [pscustomobject]@{Seconds=[Math]::Round($Elapsed,1); Frames=$frames; IcePages=$pages;
        PrivateMiB=[Math]::Round($process.PrivateMemorySize64/1MB,2);
        WorkingSetMiB=[Math]::Round($process.WorkingSet64/1MB,2); Handles=$process.HandleCount;
        GdiObjects=[FmtSoakResources]::GetGuiResources($process.Handle,0);
        UserObjects=[FmtSoakResources]::GetGuiResources($process.Handle,1)}
}
try {
    $form.ShowInTaskbar=$false; $form.StartPosition='Manual'; $form.Location=[Drawing.Point]::new(-30000,-30000)
    $form.ClientSize=[Drawing.Size]::new(1100,900)
    $gdi.opengl=$false; $gl.opengl=$true
    $gdi.streamjpgenable=$true; $gl.streamjpgenable=$true
    $gdi.Size=[Drawing.Size]::new(400,300); $gl.Size=$gdi.Size; $gl.Location=[Drawing.Point]::new(500,0)
    $bar.Location=[Drawing.Point]::new(0,350)
    $form.Controls.AddRange([Windows.Forms.Control[]]@($gdi,$gl,$bar))
    $form.Show(); [Windows.Forms.Application]::DoEvents()
    if(!$gl.opengl){throw 'OpenGL initialization failed; soak cannot validate the expected rendering path'}
    $clock=[Diagnostics.Stopwatch]::StartNew()
    while($clock.Elapsed.TotalSeconds -lt $Seconds) {
        $frames++
        foreach($hud in @($gdi,$gl)) {
            $hud.roll=[single](30*[Math]::Sin($frames/30)); $hud.pitch=[single](10*[Math]::Cos($frames/30))
            $hud.heading=[single]($frames%360); $hud.alt=[single]($frames%200)
            if(($frames%10) -eq 0){$hud.Size=[Drawing.Size]::new((360+($frames%80)),(270+[int](($frames%80)*.75)))}
            $hudType.GetField('starttime',$flags).SetValue($hud,[DateTime]::MinValue)
            $hud.Refresh()
            $snapshot=$hud.GetSnapshot()
            try {if($snapshot.Width -le 0 -or $hud.streamjpg.Length -lt 10){throw 'HUD capture did not render'}}
            finally {$snapshot.Dispose()}
        }
        if(!$gl.opengl){throw 'OpenGL fell back during sustained resize/capture'}
        if(($frames%10) -eq 0) {
            $profile=$profiles[([int]($frames/10)%$profiles.Count)]
            $bar.Width=@(280,420,620,900)[([int]($frames/10)%4)]
            [void]$height.Invoke($bar,@(600))
            [void]$update.Invoke($bar,@([Enum]::Parse($firmwareType,$profile[0]),[bool]$profile[1],[bool]$profile[2],$false,'',([string[]]@())))
            $panel=$bar.Controls.Find('fmtFlightModeGroups',$true)[0]
            foreach($control in $panel.Controls){[void]$graphics.MeasureString($control.Text,$control.Font)}
            if($panel.AutoScroll -or $panel.HorizontalScroll.Visible -or $panel.VerticalScroll.Visible){throw 'Mode scrolling returned'}
            $bar.Refresh()
        }
        if(($frames%100) -eq 1) {
            if($ice){$ice.Deactivate(); $ice.Dispose()}
            $ice=[Activator]::CreateInstance($iceType); $ice.Activate(); $ice.CreateControl(); $pages++
            if($iceType.GetField('write',$flags).GetValue($ice).Enabled){throw 'Offline ICE writing enabled'}
            $ice.Deactivate(); $ice.Activate()
        }
        [Windows.Forms.Application]::DoEvents()
        if($clock.Elapsed.TotalSeconds -ge $nextSample) {
            $sample=Sample-Resources $clock.Elapsed.TotalSeconds; $samples.Add($sample)
            Write-Output ('SAMPLE '+($sample | ConvertTo-Json -Compress))
            if($sample.PrivateMiB -gt 1000 -or $sample.GdiObjects -gt 8000){throw 'Excessive resource use'}
            $nextSample+=60
        }
        Start-Sleep -Milliseconds 40
    }
    $samples.Add((Sample-Resources $clock.Elapsed.TotalSeconds))
    $warm=if($samples.Count -gt 2){$samples[1]}else{$samples[0]}
    $last=$samples[$samples.Count-1]
    $growth=[pscustomobject]@{PrivateMiB=[Math]::Round(($last.PrivateMiB-$warm.PrivateMiB),2);
        Handles=($last.Handles-$warm.Handles); GdiObjects=($last.GdiObjects-$warm.GdiObjects);
        UserObjects=($last.UserObjects-$warm.UserObjects)}
    if($growth.PrivateMiB -gt 150 -or $growth.Handles -gt 150 -or $growth.GdiObjects -gt 100 -or $growth.UserObjects -gt 100){throw 'Retained resources kept growing after warm-up'}
    [pscustomobject]@{Passed=$true; DurationSeconds=[Math]::Round($clock.Elapsed.TotalSeconds,1);
        FramesPerHud=$frames; IcePages=$pages; OpenGL=$gl.opengl; Samples=$samples.ToArray();
        GrowthAfterWarmup=$growth; Note='Offline WinForms/GDI/OpenGL exercise; GC at samples measures retained resources; no vehicle or flight validation.'} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Report -Encoding UTF8
    Write-Output "PASS: $Seconds-second offline soak; $frames frames per HUD; $pages ICE lifecycles; repeated mode resizing; bounded retained resources."
} finally {
    if($ice){$ice.Deactivate();$ice.Dispose()}
    $form.Dispose(); $gdi.Dispose(); $gl.Dispose(); $bar.Dispose(); $graphics.Dispose(); $fontProbe.Dispose()
}
