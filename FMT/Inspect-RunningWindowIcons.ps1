$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
public static class RunningIconDiagnostics {
    [StructLayout(LayoutKind.Sequential)] struct Key { public Guid fmt; public uint id; public Key(uint n) { fmt=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");id=n; } }
    [StructLayout(LayoutKind.Explicit,Size=24)] struct Variant { [FieldOffset(0)] public ushort type; [FieldOffset(8)] public IntPtr value; }
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface Store { uint GetCount(); void GetAt(uint i,out Key key); void GetValue(ref Key key,out Variant value); void SetValue(ref Key key,ref Variant value); void Commit(); }
    [DllImport("shell32.dll",PreserveSig=false)] static extern void SHGetPropertyStoreForWindow(IntPtr h,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out Store store);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref Variant value);
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder s,int max);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h,uint command);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
    public static void Read(int processId,string output) {
        EnumWindows((h,p) => {
            uint id; GetWindowThreadProcessId(h,out id);
            if(id != processId) return true;
            var title = new StringBuilder(512); GetWindowText(h,title,512);
            Console.WriteLine("Window="+h+" Visible="+IsWindowVisible(h)+" Owner="+GetWindow(h,4)+" Title="+title);
            if(IsWindowVisible(h)) {
                Store store=null;
                try { var iid=typeof(Store).GUID; SHGetPropertyStoreForWindow(h,ref iid,out store);
                    foreach(uint n in new uint[]{2,3,4,5}) { var key=new Key(n); Variant value; store.GetValue(ref key,out value);
                        try { Console.WriteLine("  AppProperty"+n+"="+(value.type==31?Marshal.PtrToStringUni(value.value):"type "+value.type)); }
                        finally { PropVariantClear(ref value); }
                    }
                } catch(Exception e) { Console.WriteLine(e.Message); }
                finally { if(store!=null) Marshal.ReleaseComObject(store); }
            }
            for(int kind=0;kind<3;kind++) {
                IntPtr icon;
                var ok=SendMessageTimeout(h,0x7f,new IntPtr(kind),IntPtr.Zero,2,1000,out icon);
                Console.WriteLine("  icon"+kind+"="+icon+" response="+ok);
                if(icon != IntPtr.Zero) {
                    try { using(var copy=(Icon)Icon.FromHandle(icon).Clone()) using(var bmp=copy.ToBitmap())
                        bmp.Save(System.IO.Path.Combine(output,h+"-"+kind+".png"),ImageFormat.Png); }
                    catch(Exception e) { Console.WriteLine("  Decode: "+e.Message); }
                }
            }
            return true;
        },IntPtr.Zero);
    }
}
'@
$outputDirectory = Join-Path $PSScriptRoot '../tmp/running-icon-diagnostics'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
foreach ($process in Get-Process FMTPlanner -ErrorAction SilentlyContinue) {
    "Process $($process.Id) $($process.Path)"
    [RunningIconDiagnostics]::Read($process.Id,$outputDirectory)
}
