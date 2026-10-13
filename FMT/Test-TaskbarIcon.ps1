param([string]$Directory = 'bin/TaskbarIconFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
Add-Type -ReferencedAssemblies System.Windows.Forms,System.Drawing -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
public class IconTestForm : Form {
    public static System.Reflection.MethodInfo Query;
    protected override void WndProc(ref Message m) {
        if (Query != null) {
            object[] args = {m};
            if ((bool)Query.Invoke(null, args)) { m = (Message)args[0]; return; }
        }
        base.WndProc(ref m);
    }
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    public void RebuildHandle() { RecreateHandle(); }
}
'@
$type = $assembly.GetType('MissionPlanner.FMT.FmtBranding',$true)
$apply = $type.GetMethod('ApplyApplicationIcon',[Reflection.BindingFlags]'Static,NonPublic')
[IconTestForm]::Query=$type.GetMethod('TryGetApplicationIcon',[Reflection.BindingFlags]'Static,NonPublic')
if($null -eq [IconTestForm]::Query) { throw 'Missing shell query handler' }
$form = New-Object IconTestForm
try {
    $apply.Invoke($null,@($form.PSObject.BaseObject)) | Out-Null
    $handle = $form.Handle
    $small = [IconTestForm]::SendMessage($handle,0x7f,[IntPtr]::Zero,[IntPtr]::Zero)
    $large = [IconTestForm]::SendMessage($handle,0x7f,[IntPtr]1,[IntPtr]::Zero)
    if ($small -eq [IntPtr]::Zero -or $large -eq [IntPtr]::Zero) { throw 'Missing native window icon' }
    $form.ShowInTaskbar=$false
    $form.StartPosition='Manual'
    $form.Location=New-Object Drawing.Point -30000,-30000
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    if ([IconTestForm]::SendMessage($form.Handle,0x7f,[IntPtr]::Zero,[IntPtr]::Zero) -ne $small -or
        [IconTestForm]::SendMessage($form.Handle,0x7f,[IntPtr]1,[IntPtr]::Zero) -ne $large) {
        throw 'Icons overwritten after full window startup'
    }
    $form.RebuildHandle()
    [Windows.Forms.Application]::DoEvents()
    if ([IconTestForm]::SendMessage($form.Handle,0x7f,[IntPtr]::Zero,[IntPtr]::Zero) -ne $small -or
        [IconTestForm]::SendMessage($form.Handle,0x7f,[IntPtr]1,[IntPtr]::Zero) -ne $large) {
        throw 'Icons not restored after handle recreation'
    }
    $apply.Invoke($null,@($form.PSObject.BaseObject)) | Out-Null
    if (!$form.ShowIcon) { throw 'Window icon hidden' }
    [IconTestForm]::Query=$type.GetMethod('TryGetApplicationIcon',[Reflection.BindingFlags]'Static,NonPublic')
    if($null -eq [IconTestForm]::Query) { throw 'Missing shell query handler' }
    [IconTestForm]::SendMessage($form.Handle,0x80,[IntPtr]0,[IntPtr]::Zero) | Out-Null
    [IconTestForm]::SendMessage($form.Handle,0x80,[IntPtr]1,[IntPtr]::Zero) | Out-Null
    foreach($kind in @(0,1,2)) {
        $expected=$small; if($kind -eq 1) { $expected=$large }
        if([IconTestForm]::SendMessage($form.Handle,0x7f,[IntPtr]$kind,[IntPtr]::Zero) -ne $expected) { throw 'Shell query lost branding after native icon overwrite' }
    }
    $mainSource=Get-Content "$PSScriptRoot/../MainV2.cs" -Raw
    if(!$mainSource.Contains('FMT.FmtBranding.TryGetApplicationIcon(ref m)')) { throw 'Main window must handle shell icon queries' }
    'PASS: shown/recreated window icons; all shell icon sizes survive simulated overwrite; MainV2 integration checked'
} finally { $form.Dispose() }
