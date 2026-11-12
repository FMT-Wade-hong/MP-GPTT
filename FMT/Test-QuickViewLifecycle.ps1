param([string]$Directory='bin/GitHubIssueFix/net472')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
$flags=[Reflection.BindingFlags]'Instance,NonPublic,Public'
[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'GMap.NET.Core.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'MissionPlanner.Utilities.dll')) | Out-Null
[GMap.NET.GMaps]::Instance.Mode=[GMap.NET.AccessMode]::CacheOnly
[MissionPlanner.Utilities.Settings]::Instance['FMT_ShowAirspace']='False'
$type=$assembly.GetType('MissionPlanner.GCSViews.FlightData',$true)
$flight=[Activator]::CreateInstance($type)
# Do not start telemetry/map-update workers in an offline lifecycle test.
$flight.remove_Load([Delegate]::CreateDelegate([EventHandler],$flight,$type.GetMethod('FlightData_Load',$flags)))
$form=New-Object Windows.Forms.Form
$form.ShowInTaskbar=$false; $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point -30000,-30000
$form.Size=New-Object Drawing.Size 1200,800
$popup=$null
try {
    $hud=$type.GetField('hud1',$flags).GetValue($flight); $hud.opengl=$false
    $form.Controls.Add($flight); $flight.Dock='Fill'; $form.Show(); $flight.Activate()
    $menu=$type.GetField('contextMenuStripQuickView',$flags).GetValue($flight).Items['undockToolStripMenuItem']
    $quick=$type.GetField('tabQuick',$flags).GetValue($flight)
    $tabs=$type.GetField('tabControlactions',$flags).GetValue($flight)
    $panel=$type.GetField('tableLayoutPanelQuick',$flags).GetValue($flight)
    $undock=$type.GetMethod('undockDockToolStripMenuItem_Click',$flags)
    foreach($cycle in 1..20) {
        $undock.Invoke($flight,@($menu,[EventArgs]::Empty)) | Out-Null
        $popup=$quick.FindForm(); $popup.Location=New-Object Drawing.Point -30000,-30000
        if($popup -eq $form -or $tabs.TabPages.Contains($quick)){throw 'Quick tab did not detach'}
        $undock.Invoke($flight,@($menu,[EventArgs]::Empty)) | Out-Null
        if($quick.FindForm() -ne $popup){throw 'Repeated undock created another host'}
        foreach($returnCycle in 1..5) {
            [MissionPlanner.Utilities.Settings]::Instance['quickView1']=if($returnCycle%2){'battery_voltage'}else{'alt'}
            $flight.Deactivate(); $flight.Visible=$false; [Windows.Forms.Application]::DoEvents()
            $flight.Visible=$true; $flight.Activate(); [Windows.Forms.Application]::DoEvents()
            $view=$panel.Controls.Find('quickView1',$true)[0]
            if($view.DataBindings['number'].BindingMemberInfo.BindingField -ne [MissionPlanner.Utilities.Settings]::Instance['quickView1']){throw 'Detached telemetry binding not refreshed on return'}
            if($popup.IsDisposed -or $quick.IsDisposed -or $tabs.TabPages.Contains($quick)){throw 'Page return stole or disposed detached tab'}
        }
        $popup.Close(); [Windows.Forms.Application]::DoEvents()
        if(!$popup.IsDisposed -or $quick.IsDisposed -or !$tabs.TabPages.Contains($quick) -or $type.GetField('tabQuickDetached',$flags).GetValue($flight)){throw 'Close did not redock one live tab'}
    }
    $undock.Invoke($flight,@($menu,[EventArgs]::Empty)) | Out-Null
    $popup=$quick.FindForm(); $popup.Location=New-Object Drawing.Point -30000,-30000
    $flight.Dispose(); [Windows.Forms.Application]::DoEvents()
    if(!$popup.IsDisposed -or !$quick.IsDisposed){throw 'Detached popup/tab survived owner disposal'}
    $flight.Dispose()
    'PASS: 20 detach/redock cycles, 100 deactivate/return cycles with actual binding refresh, repeated-undock guard and owner disposal; offline actual FlightData controls.'
} finally {if($popup -and !$popup.IsDisposed){$popup.Dispose()}; $form.Dispose(); $flight.Dispose()}
