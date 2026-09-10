param([string]$Directory = 'bin/P400FrequencyTest/net461')
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path "$Directory/FMTPlanner.exe").Path
[Reflection.Assembly]::LoadFrom($exe) | Out-Null
Set-Location (Split-Path $exe)
Copy-Item -LiteralPath $exe -Destination 'P400FrequencyReference.dll' -Force
Add-Type -ReferencedAssemblies @((Join-Path (Get-Location) 'P400FrequencyReference.dll'), 'System.Core.dll') -TypeDefinition @'
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using MissionPlanner.FMT;
public class FrequencyWire : IP400Transport {
 public string Fault="", Mode="2", Hopping="1", Pending="";
 public List<string> Commands=new List<string>();
 public string[] Values=Enumerable.Repeat("420.300000",50).ToArray();
 public void Write(string s) {
  Commands.Add(s); string c=s.Trim(); string result="";
  if(c=="ATI1") result="P400";
  if(c=="ATS128?") result=Mode;
  if(c=="ATS238?") result=Hopping;
  if(c=="ATP0?" || c=="ATP1?") result="Ch Freq(MHz)\r\n"+string.Join("\r\n",Values.Select((f,i)=>(i+1)+" "+f));
  if(c.StartsWith("ATP0=") || c.StartsWith("ATP1=")) {
   if(Fault=="timeout") return;
   if(Fault=="reject") {Pending="ERROR\r\n";return;}
   var lines=s.Split(new[]{"\r","\n"},StringSplitOptions.RemoveEmptyEntries);
   if(lines.Length!=51) throw new Exception("Not exactly fifty frequency lines");
   if(Fault!="mismatch") Values=lines.Skip(1).ToArray();
  }
  Pending=result+"\r\nOK\r\n";
 }
 public string ReadExisting(){var r=Pending;Pending="";return r;}
 public void Dispose(){}
}
public static class FrequencyRadioTests {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
 public static void Run() {
  foreach(var fault in new[]{"","reject","mismatch","timeout","NB","pattern","blank","changed","cancel"}) {
   var w=new FrequencyWire(); var c=new P400AtClient(w,null); var token=CancellationToken.None;
   c.EnterAsync(true,token).GetAwaiter().GetResult();
   var original=P400FrequencyPlan.Parse(c.ReadFrequencyTableAsync(1,token).GetAwaiter().GetResult());
   var p=new P400FrequencyPlan{Frequencies=Enumerable.Repeat("430.300000",50).ToArray()};
   w.Fault=fault;
   if(fault=="NB") w.Mode="0";
   if(fault=="pattern") w.Hopping="0";
   if(fault=="blank") p.Frequencies[3]="";
   if(fault=="changed") w.Values[4]="425.300000";
   if(fault=="cancel") {var cs=new CancellationTokenSource();cs.Cancel();token=cs.Token;}
   bool failed=false;try{c.WriteFrequencyTableAsync(1,p,original,token).GetAwaiter().GetResult();}catch(Exception){failed=true;}
   bool sent=w.Commands.Any(s=>s.StartsWith("ATP1="));
   if(fault=="") Check(!failed&&sent&&!c.Unsaved&&c.Ready,"secondary table write and exact readback");
   else if(new[]{"reject","mismatch","timeout"}.Contains(fault)) Check(failed&&sent&&c.Unsaved&&!c.Ready,"quarantine after "+fault);
   else Check(failed&&!sent,"no write for "+fault);
   Check(!w.Commands.Any(s=>s.StartsWith("ATP0=")||s.StartsWith("AT&W")),"no other table or settings saved");
  }
 }
}
'@
[FrequencyRadioTests]::Run()
