param([string]$Directory = 'bin/P400AtTest/net461')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$exe = (Resolve-Path "$Directory/FMTPlanner.exe").Path
[Reflection.Assembly]::LoadFrom($exe) | Out-Null
Set-Location (Split-Path $exe)
# Windows PowerShell Add-Type assumes DLL metadata references.
Copy-Item -LiteralPath $exe -Destination 'P400TestReference.dll' -Force
Add-Type -ReferencedAssemblies @((Join-Path (Get-Location) 'P400TestReference.dll'), 'System.Core.dll') -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Threading;
using MissionPlanner.FMT;
public class FakeP400 : IP400Transport {
    public List<string> Commands = new List<string>();
    public Dictionary<int,string> Values = new Dictionary<int,string>();
    public string Identity = "P400";
    public string Fault = "";
    string pending = "";
    public FakeP400() { foreach(int n in P400AtClient.Registers) Values[n]="1"; Values[128]="2"; Values[101]="2"; Values[108]="20"; }
    public void Write(string text) {
        string c=text.Trim(); Commands.Add(c);
        if(Fault=="timeout" && c=="AT") return;
        string result="";
        if(c=="ATI1") result=Identity;
        if(c.StartsWith("ATS")) {
            int end=c.IndexOfAny(new char[]{'?','='}); int n=int.Parse(c.Substring(3,end-3));
            if(c.Contains("=")) {
                if(Fault=="reject") {pending="ERROR\r\n"; return;}
                if(Fault!="mismatch") Values[n]=c.Substring(end+1);
            } else result=Values[n];
        }
        pending=c+"\r\n"+result+"\r\nOK\r\n";
    }
    public string ReadExisting(){string r=pending;pending="";return r;}
    public void Dispose(){}
}
public static class P400Tests {
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
    public static void Run(){
        Check(P400AtClient.IsValid(2,102,7)&&!P400AtClient.IsValid(2,102,15),"serial baud documented range");
        foreach(int reg in new[]{102,110,116,128,142,150,151,153,213,217}) {
            var wire=new FakeP400(); wire.Values[151]="200";
            var client=new P400AtClient(wire,null);client.EnterAsync(true,CancellationToken.None).GetAwaiter().GetResult();
            var entry=client.ReadSettingsAsync(CancellationToken.None).GetAwaiter().GetResult().Find(s=>s.Register==reg);
            entry.Proposed=reg==151?"300":reg==128?"0":"2";
            if(reg==153)entry.Proposed="0";
            client.WriteAndSaveAsync(new List<P400Setting>{entry},CancellationToken.None).GetAwaiter().GetResult();
            Check(entry.Editable&&wire.Commands.Contains("AT&W"),"documented editable S"+reg);
        }
        Check(P400AtClient.IsValid(0,125,2)&&P400AtClient.IsValid(0,131,63)&&P400AtClient.IsValid(0,132,0)&&!P400AtClient.IsValid(2,131,1),"NB-only options");
        var serialWire=new FakeP400();var serialClient=new P400AtClient(serialWire,null);
        serialClient.EnterAsync(false,CancellationToken.None).GetAwaiter().GetResult();
        var serialEntry=serialClient.ReadSettingsAsync(CancellationToken.None).GetAwaiter().GetResult().Find(s=>s.Register==102);serialEntry.Proposed="7";
        bool serialBlocked=false;try{serialClient.WriteAndSaveAsync(new List<P400Setting>{serialEntry},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){serialBlocked=true;}
        Check(serialBlocked&&!serialWire.Commands.Exists(s=>s.StartsWith("ATS102=")),"baud change requires forced command mode");
        Check(!P400AtClient.IsValid(0,104,100),"NB excludes FH network ID");
        Check(!P400AtClient.IsValid(2,108,33),"factory high power not assumed");
        Check(!P400AtClient.IsValid(2,103,7),"FH invalid link-rate rejected");
        Check(P400AtClient.IsValid(2,104,4000000000L),"network ID supports unsigned range");
        bool invalid=false;try{P400AtClient.NumericValue("1\rAT&W");}catch(FormatException){invalid=true;}
        Check(invalid,"command injection rejected");
        foreach(string fault in new[]{"", "reject", "mismatch"}) {
            var wire=new FakeP400();var c=new P400AtClient(wire,null);
            c.EnterAsync(true,CancellationToken.None).GetAwaiter().GetResult();
            var values=c.ReadSettingsAsync(CancellationToken.None).GetAwaiter().GetResult();
            var item=values.Find(s=>s.Register==104); item.Proposed="42";
            wire.Fault=fault;bool failed=false;
            try{c.WriteAndSaveAsync(new List<P400Setting>{item},CancellationToken.None).GetAwaiter().GetResult();}catch(Exception){failed=true;}
            Check(fault=="" ? !failed && wire.Commands.Contains("AT&W") && !c.Unsaved : failed && !wire.Commands.Contains("AT&W") && c.Unsaved,"write/save scenario: "+(fault==""?"success":fault));
            if(fault=="") {c.ReturnToDataAsync(CancellationToken.None).GetAwaiter().GetResult();Check(wire.Commands.Contains("ATA")&&!c.Ready,"explicit DATA exit");}
            c.Dispose();
        }
        var wrong=new FakeP400{Identity="P900"};var bad=new P400AtClient(wrong,null);bool blocked=false;
        try{bad.EnterAsync(true,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){blocked=true;}
        Check(blocked&&!bad.Ready&&!wrong.Commands.Contains("AT&W"),"wrong model blocks writes");
        var timeout=new FakeP400{Fault="timeout"};var tc=new P400AtClient(timeout,null);blocked=false;
        try{tc.EnterAsync(true,CancellationToken.None).GetAwaiter().GetResult();}catch(TimeoutException){blocked=true;}
        Check(blocked&&!tc.Ready,"bounded timeout");
        var cancel=new CancellationTokenSource();cancel.Cancel();var cw=new FakeP400();blocked=false;
        try{new P400AtClient(cw,null).EnterAsync(false,cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){blocked=true;}
        Check(blocked&&cw.Commands.Count==0,"cancel before escape sends nothing");
        var denied=new FakeP400();blocked=false;
        try{new P400AtClient(denied,()=>{throw new InvalidOperationException();}).EnterAsync(true,CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){blocked=true;}
        Check(blocked&&denied.Commands.Count==0,"telemetry guard sends nothing");
        foreach(string fault in new[]{"", "reject"}) {
            var pw=new FakeP400(); pw.Values[107]="****";
            var pc=new P400AtClient(pw,null);pc.EnterAsync(true,CancellationToken.None).GetAwaiter().GetResult();
            var pi=pc.ReadSettingsAsync(CancellationToken.None).GetAwaiter().GetResult().Find(s=>s.Register==107);
            Check(pi.Editable&&pi.Value=="****"&&pi.Proposed=="","masked password is editable without default write");
            pi.Proposed="Test_Key-123";pw.Fault=fault; string error="";
            try{pc.WriteAndSaveAsync(new List<P400Setting>{pi},CancellationToken.None).GetAwaiter().GetResult();}catch(Exception ex){error=ex.Message;}
            Check(fault=="" ? error==""&&pw.Commands.Contains("AT&W") : error!=""&&!error.Contains(pi.Proposed)&&!pw.Commands.Contains("AT&W"),"password acknowledgement or redacted rejection");
        }
        foreach(string value in new[]{"****","abc\rAT&W","abc;AT&W","",new string('a',17)}) {
            bool rejected=false;try{P400AtClient.ValidatePassword(value);}catch(FormatException){rejected=true;}
            Check(rejected,"unsafe or placeholder password rejected");
        }
    }
}
'@
[P400Tests]::Run()
$page = New-Object MissionPlanner.GCSViews.ConfigurationView.ConfigP400
if ($page.Controls.Count -eq 0) { throw 'Page did not load' }
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$wire = New-Object FakeP400
$wire.Values[107] = '****'
$client = New-Object MissionPlanner.FMT.P400AtClient($wire, $null)
$client.EnterAsync($true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
$page.GetType().GetField('client',$flags).SetValue($page,$client)
$cts = New-Object Threading.CancellationTokenSource
$page.GetType().GetField('cancellation',$flags).SetValue($page,$cts)
$page.GetType().GetMethod('ReadAsync',$flags).Invoke($page,@()).GetAwaiter().GetResult()
$left = $page.GetType().GetField('grid',$flags).GetValue($page)
$right = $page.GetType().GetField('rightGrid',$flags).GetValue($page)
if ($left.Rows.Count -ne 12 -or $right.Rows.Count -ne 15) { throw 'AT&V column counts' }
if ($left.Rows[0].Tag.Register -ne 101 -or $right.Rows[0].Tag.Register -ne 102 -or $right.Rows[11].Tag.Register -ne 128) { throw 'AT&V ordering' }
foreach ($table in @($left,$right)) {
    foreach ($row in $table.Rows) {
        if ($row.Cells['proposed'] -is [Windows.Forms.DataGridViewComboBoxCell]) {
            if ($row.Cells['proposed'].Value -notmatch '^\d+$') { throw 'Dropdown must retain numeric protocol code' }
            if (-not $row.Tag.Editable) { throw 'Read-only field became editable' }
        }
    }
}
'PASS: AT&V two-column order, dropdown codes, read-only preservation'
foreach ($table in @($left,$right)) {
    foreach ($row in $table.Rows) {
        if ([string]::IsNullOrWhiteSpace($row.Cells['default'].Value)) { throw 'Missing manual default annotation' }
        if ($row.Cells['default'].Value -ne [MissionPlanner.FMT.P400SettingInfo]::ManualDefault(2,$row.Tag.Register)) { throw 'Default replaced with live value' }
        if ($row.Tag.Register -eq 107 -and (!$row.Tag.Editable -or $row.Tag.Value -ne '****' -or $row.Tag.Proposed -ne '' -or $row.Cells['proposed'] -isnot [Windows.Forms.DataGridViewButtonCell])) { throw 'Password masked editor missing' }
    }
}
if ([MissionPlanner.FMT.P400SettingInfo]::ManualDefault(2,154) -match '\d') { throw 'Invented S154 default' }
if ([MissionPlanner.FMT.P400SettingInfo]::ManualDefault(0,158) -eq [MissionPlanner.FMT.P400SettingInfo]::ManualDefault(2,158)) { throw 'FEC modes mixed' }
'PASS: manual defaults independent of readback; S107 strings; unknown S154; NB/FH defaults'
$page.Size = New-Object Drawing.Size 1800,900
$page.CreateControl()
$page.PerformLayout()
$image = New-Object Drawing.Bitmap 1800,900
$page.DrawToBitmap($image, (New-Object Drawing.Rectangle 0,0,1800,900))
$image.Save((Join-Path (Get-Location) 'P400-settings-preview.png'))
$image.Dispose()
$page.Dispose()
$cts.Dispose()
'PASS: settings page constructs/disposes without hardware'
