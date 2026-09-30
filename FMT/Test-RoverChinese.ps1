param([string]$Directory = 'bin/RoverChineseFix/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$binaryDirectory = (Resolve-Path $Directory).Path
[Reflection.Assembly]::LoadFrom((Join-Path $binaryDirectory 'System.Resources.Extensions.dll')) | Out-Null
Add-Type -TypeDefinition @'
public static class RoverTestResolver {
    public static void Install(string directory) {
        System.AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
            var name = new System.Reflection.AssemblyName(e.Name).Name;
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == name) return a;
            var path = System.IO.Path.Combine(directory, name + ".dll");
            return System.IO.File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
        };
    }
}
'@
[RoverTestResolver]::Install($binaryDirectory)
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]'zh-TW'
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path "$Directory/FMTPlanner.exe"))
$type = $assembly.GetType('MissionPlanner.GCSViews.ConfigurationView.ConfigArdurover', $true)
$page = [Activator]::CreateInstance($type)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
try {
    $resources = New-Object System.ComponentModel.ComponentResourceManager($type)
    $count = 0
    foreach ($entry in $resources.GetResourceSet([Globalization.CultureInfo]::InvariantCulture,$true,$true)) {
        if ($entry.Key -notlike '*.Text') { continue }
        $name = $entry.Key.Substring(0,$entry.Key.Length-5)
        $controls = $page.Controls.Find($name,$true)
        if ($controls.Count -ne 1 -or $controls[0].Text -notmatch '[\u3400-\u9fff]') { throw "Untranslated control $name" }
        $count++
    }
    $format = $type.GetMethod('FormatChineseOption',$flags)
    foreach ($pair in @(@(0,'Do Nothing'),@(1,'Enable'),@(0,'Normal'))) {
        $item = New-Object 'System.Collections.Generic.KeyValuePair[int,string]' ([int]$pair[0]),([string]$pair[1])
        $args = New-Object Windows.Forms.ListControlConvertEventArgs $pair[1],([string]),$item
        $format.Invoke($null,@($null,$args.PSObject.BaseObject)) | Out-Null
        if ($args.Value -notmatch '[\u3400-\u9fff]' -or $args.ListItem.Key -ne $pair[0]) { throw 'Option text/value mismatch' }
    }
    $page.CreateControl()
    $bitmap = New-Object Drawing.Bitmap $page.Width,$page.Height
    try { $page.DrawToBitmap($bitmap,$page.ClientRectangle); $bitmap.Save((Join-Path (Resolve-Path $Directory) 'rover-zh-preview.png')) }
    finally { $bitmap.Dispose() }
    "PASS: $count static labels translated; option formatting preserves numeric keys; offline preview rendered"
} finally { $page.Dispose() }
