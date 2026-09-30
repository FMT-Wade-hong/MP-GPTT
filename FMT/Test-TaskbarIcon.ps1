param([string]$Directory = 'bin/TaskbarIconFix/net461')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-RoverChinese.ps1" -Directory $Directory
Add-Type -ReferencedAssemblies System.Windows.Forms,System.Drawing -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
public class IconTestForm : Form {
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    public void RebuildHandle() { RecreateHandle(); }
}
'@
$type = $assembly.GetType('MissionPlanner.FMT.FmtBranding',$true)
$apply = $type.GetMethod('ApplyApplicationIcon',[Reflection.BindingFlags]'Static,NonPublic')
$form = New-Object IconTestForm
try {
    $apply.Invoke($null,@($form.PSObject.BaseObject)) | Out-Null
    $handle = $form.Handle
    $small = [IconTestForm]::SendMessage($handle,0x7f,[IntPtr]::Zero,[IntPtr]::Zero)
    $large = [IconTestForm]::SendMessage($handle,0x7f,[IntPtr]1,[IntPtr]::Zero)
    if ($small -eq [IntPtr]::Zero -or $large -eq [IntPtr]::Zero) { throw 'Missing native window icon' }
    $form.RebuildHandle()
    if ([IconTestForm]::SendMessage($form.Handle,0x7f,[IntPtr]::Zero,[IntPtr]::Zero) -ne $small -or
        [IconTestForm]::SendMessage($form.Handle,0x7f,[IntPtr]1,[IntPtr]::Zero) -ne $large) {
        throw 'Icons not restored after handle recreation'
    }
    $apply.Invoke($null,@($form.PSObject.BaseObject)) | Out-Null
    if (!$form.ShowIcon) { throw 'Window icon hidden' }
    'PASS: native small/large icons present; restored after handle recreation; repeat branding safe'
} finally { $form.Dispose() }
