using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.GCSViews;
using MissionPlanner.Utilities;

internal static class SurfaceAngleHarness
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly App = typeof(MainV2).Assembly;
    static object Store, Channels;
    static string Vehicle;
    static int Writes, Checks;
    static Type T(string name) => App.GetType("MissionPlanner.FMT." + name, true);
    static object New(string name) => Activator.CreateInstance(T(name), true);
    static object Call(object obj, string name, params object[] args) =>
        (obj as Type ?? obj.GetType()).GetMethod(name, All).Invoke(obj is Type ? null : obj, args);
    static object Field(object obj, string name) => obj.GetType().GetField(name, All).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, All).SetValue(obj, value);
    static void Check(bool ok, string text) { Checks++; if (!ok) throw new Exception(text); }
    static void Equal(object actual, double expected, string text) => Check(actual != null && Math.Abs(Convert.ToDouble(actual) - expected) < 1e-8, text);
    [DllImport("user32.dll")] static extern int GetGuiResources(IntPtr process, int flag);

    static object Calibration(double lo, double hi)
    {
        var c = New("FmtSurfaceCalibration");
        c.GetType().GetProperty("AtMin").SetValue(c, lo); c.GetType().GetProperty("AtMax").SetValue(c, hi); return c;
    }
    static object Channel(int min = 1000, int trim = 1500, int max = 2000)
    {
        var c = New("FmtSurfaceChannel");
        Set(c, "Channel", 1); Set(c, "Function", 4); Set(c, "Min", min); Set(c, "Trim", trim); Set(c, "Max", max); return c;
    }
    static void Parameters(MAVState target)
    {
        target.cs.uid = 987654321;
        foreach (var pair in new[] { new[] {1,4}, new[] {2,19}, new[] {4,21}, new[] {5,2}, new[] {7,4}, new[] {17,3}, new[] {3,70} })
        {
            string prefix = "SERVO" + pair[0] + "_";
            foreach (var value in new[] { new[] {"FUNCTION",pair[1].ToString()}, new[] {"MIN","1000"},
                new[] {"TRIM",pair[1] == 2 || pair[1] == 3 ? "1000" : "1500"}, new[] {"MAX","2000"}, new[] {"REVERSED","0"} })
                target.param[prefix + value[0]] = new MAVLink.MAVLinkParam(prefix + value[0], double.Parse(value[1]), MAVLink.MAV_PARAM_TYPE.REAL32);
        }
    }
    static void Send(MAVLinkInterface link, byte port = 0, byte sysid = 1, byte compid = 1, double? phase = null)
    {
        var raw = new MAVLink.mavlink_servo_output_raw_t { port = port, servo1_raw = 1600, servo2_raw = 1425,
            servo4_raw = 1650, servo5_raw = 1500, servo7_raw = 1400 };
        if (phase.HasValue)
        {
            raw.servo1_raw=(ushort)(1500+300*Math.Sin(phase.Value));
            raw.servo7_raw=(ushort)(3000-raw.servo1_raw);
            raw.servo2_raw=(ushort)(1500+200*Math.Sin(phase.Value*.7));
            raw.servo4_raw=(ushort)(1500+250*Math.Cos(phase.Value*.8));
            raw.servo5_raw=(ushort)(1000+500*(1+Math.Sin(phase.Value*.4)));
        }
        byte[] bytes = new MAVLink.MavlinkParse().GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.SERVO_OUTPUT_RAW, raw, false, sysid, compid);
        var handler = (EventHandler<MAVLink.MAVLinkMessage>)typeof(MAVLinkInterface).GetField("_OnPacketReceived", All).GetValue(link);
        handler?.Invoke(link, new MAVLink.MAVLinkMessage(bytes));
    }
    static TAngle ReadAngle<TChannel, TAngle>(TChannel c) => (TAngle)Call(Store, "Get", Vehicle, c);
    static void SaveAngles<TAngle>(IList<TAngle> values) => Call(Store, "Save", Vehicle, Channels, values);
    static Form Dialog()
    {
        Type ch = T("FmtSurfaceChannel"), angle = T("FmtSurfaceCalibration");
        var getter = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(ch, angle),
            typeof(SurfaceAngleHarness).GetMethod("ReadAngle", All).MakeGenericMethod(ch, angle));
        var saver = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(typeof(IList<>).MakeGenericType(angle)),
            typeof(SurfaceAngleHarness).GetMethod("SaveAngles", All).MakeGenericMethod(angle));
        return (Form)Call(T("FmtSurfaceAngleWindow"), "CreateSettingsDialog", Channels, getter, saver, true);
    }
    static void Input(DataGridView grid, int row, int column, string text)
    {
        grid.CurrentCell = grid.Rows[row].Cells[column]; grid.BeginEdit(true);
        ((DataGridViewTextBoxEditingControl)grid.EditingControl).Text = text;
        Check(grid.EndEdit(), "Cell did not commit user input");
    }
    static void Offscreen(Form form)
    {
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.ShowInTaskbar = false;
        form.Show(); Application.DoEvents();
    }
    static void MathChecks()
    {
        var model = T("FmtSurfaceAngles");
        var c = Channel(1100, 1450, 2000); var a = Calibration(-25, 40);
        Equal(Call(model,"Estimate",c,1100,a),-25,"MIN"); Equal(Call(model,"Estimate",c,1450,a),0,"TRIM");
        Equal(Call(model,"Estimate",c,2000,a),40,"MAX"); Equal(Call(model,"Estimate",c,1275,a),-12.5,"asymmetric negative");
        Equal(Call(model,"Estimate",c,1725,a),20,"asymmetric positive");
        Set(c,"Reversed",1); a=Calibration(25,-40); Equal(Call(model,"Estimate",c,1725,a),-20,"Do not reverse PWM twice");
        c=Channel(1000,1000,2000); a=Calibration(0,40); Equal(Call(model,"Estimate",c,1000,a),0,"flap neutral endpoint");
        Equal(Call(model,"Estimate",c,1500,a),20,"flap one-sided travel");
        Check(Call(model,"Estimate",c,999,a)==null,"Do not clamp out-of-range output");
        Check(Call(model,"Estimate",c,1500,Calibration(10,40))==null,"Neutral endpoint cannot be nonzero");
        Check(Call(model,"Estimate",c,1500,Calibration(0,double.NaN))==null,"Reject NaN");
        Check(Call(model,"Estimate",Channel(1500,1500,1500),1500,a)==null,"Reject zero PWM span");
        Check(Call(model,"Estimate",c,1500,null)==null,"Never invent degrees before calibration");
        c=Channel(1000,2000,2000); a=Calibration(-40,0); Equal(Call(model,"Estimate",c,1500,a),-20,"reverse single-sided flap");
        Console.WriteLine("PASS: asymmetric/one-sided/negative endpoints, neutral, invalid and out-of-range PWM, uncalibrated output.");
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-TW"); CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            Settings.CustomUserDataDirectory = Path.GetFullPath(args.Contains("--demo") ? "tmp/surface-demo" : "tmp/surface-test");
            Settings.AppConfigName = "SurfaceIndicatorTest"; Settings.FileName = "surface-test.config.xml";
            Settings.config.Clear(); Settings.Instance["FMT_ShowAirspace"] = "False";
            if (!args.Contains("--demo")) MathChecks();
            var link = new MAVLinkInterface(); var stream = new CommsInjection(); link.BaseStream = stream;
            link.sysidcurrent=1; link.compidcurrent=1; MainV2.comPort=link;
            stream.WriteCallback += (s,e) => Writes += e.Count();
            Parameters(link.MAV); Channels=Call(T("FmtSurfaceAngles"),"ReadChannels",link.MAV);
            Store=New("FmtSurfaceAngleStore"); Vehicle=(string)Call(T("FmtSurfaceAngles"),"VehicleKey",link.MAV);
            Check(((IList)Channels).Count==6,"Only selected four function families, including both flap functions");
            using (var dialog=Dialog())
            {
                Offscreen(dialog);
                var grid=(DataGridView)dialog.Controls.Find("fmtSurfaceCalibrationGrid",true)[0];
                var save=(Button)dialog.Controls.Find("fmtSurfaceCalibrationSave",true)[0];
                Input(grid,0,4,"NaN"); Input(grid,0,5,"30"); save.PerformClick();
                Check(dialog.DialogResult!=DialogResult.OK,"Invalid typed angle was saved");
                for(int i=0;i<grid.Rows.Count;i++)
                {
                    var ch=((IList)Channels)[i]; bool flap=(int)Field(ch,"Kind")==1; bool elev=(int)Field(ch,"Kind")==2;
                    if(!flap) Input(grid,i,4,elev?"-20":"-30");
                    Input(grid,i,5,flap?"40":elev?"20":"30");
                }
                save.PerformClick(); Check(dialog.DialogResult==DialogResult.OK,"Valid typed calibration not saved");
            }
            Store=New("FmtSurfaceAngleStore"); var first=((IList)Channels)[0];
            Check(Call(Store,"Get",Vehicle,first)!=null,"Reload calibration");
            Check(Call(Store,"Get","OTHER_VEHICLE",first)==null,"Calibration leaked between vehicles");
            Set(first,"Trim",1490); Check(Call(Store,"Get",Vehicle,first)==null,"Old calibration reused after trim changed"); Set(first,"Trim",1500);
            using (var window=(Form)New("FmtSurfaceAngleWindow"))
            {
                Offscreen(window); Call(window,"RefreshFromVehicle"); Send(link); Send(link,1); Call(window,"RefreshFromVehicle");
                var telemetry=Field(window,"telemetry");
                Equal(Call(telemetry,"Read",1,DateTime.UtcNow),1600,"Receive bank zero"); Equal(Call(telemetry,"Read",17,DateTime.UtcNow),1600,"Receive bank one");
                Check(Call(telemetry,"Read",3,DateTime.UtcNow)==null,"Zero PWM must not be neutral");
                Check(Call(telemetry,"Read",1,DateTime.UtcNow.AddSeconds(4))==null,"Stale data still shown");
                var times=(DateTime[])Field(telemetry,"received"); times[0]=DateTime.UtcNow.AddSeconds(-4);
                Send(link,0,2); Check(Call(telemetry,"Read",1,DateTime.UtcNow)==null,"Wrong aircraft refreshed telemetry");
                Send(link,0,1,2); Check(Call(telemetry,"Read",1,DateTime.UtcNow)==null,"Wrong component refreshed telemetry");
                Send(link,1); Check(Call(telemetry,"Read",1,DateTime.UtcNow)==null,"Bank one must not refresh bank zero age");
                Send(link); Call(window,"RefreshFromVehicle");
                var gauges=(Array)Field(window,"gauges");
                var selector=(ComboBox)((Control)gauges.GetValue(0)).Controls.Find("fmtSurfaceChannel0",true)[0];
                Check(selector.Items.Count==2,"Multiple aileron channels missing"); selector.SelectedIndex=1; Call(window,"RefreshFromVehicle");
                Equal(Field(gauges.GetValue(0),"pwm"),1400,"Aileron channel selector ignored"); selector.SelectedIndex=0;
                Directory.CreateDirectory("tmp/surface-preview");
                foreach(var size in new[]{new Size(500,420),new Size(620,470),new Size(900,650)})
                {
                    window.ClientSize=size; Send(link); Send(link,1); Call(window,"RefreshFromVehicle"); Application.DoEvents();
                    using(var bitmap=new Bitmap(window.Width,window.Height)) { window.DrawToBitmap(bitmap,new Rectangle(0,0,window.Width,window.Height)); bitmap.Save("tmp/surface-preview/indicator-"+size.Width+".png"); }
                }
                using (var dialog=Dialog())
                {
                    Offscreen(dialog);
                    using(var bitmap=new Bitmap(dialog.Width,dialog.Height)) { dialog.DrawToBitmap(bitmap,new Rectangle(0,0,dialog.Width,dialog.Height)); bitmap.Save("tmp/surface-preview/settings.png"); }
                    dialog.Close();
                }
                int before=GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0);
                for(int i=0;i<500;i++) { window.ClientSize=new Size(500+i%160,420+i%80); using(var b=new Bitmap(window.Width,window.Height)) window.DrawToBitmap(b,window.ClientRectangle); }
                GC.Collect(); GC.WaitForPendingFinalizers();
                int after=GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0); Check(after-before<12,"GDI resources leaked");
                stream.Close(); Call(window,"RefreshFromVehicle"); Check(Call(telemetry,"Read",1,DateTime.UtcNow)==null,"Disconnect retained live values");
                stream.Open(); Call(window,"RefreshFromVehicle"); Check(Call(telemetry,"Read",1,DateTime.UtcNow)==null,"Reconnect reused old PWM");
                Send(link); link.logreadmode=true; Call(window,"RefreshFromVehicle"); Check(Call(telemetry,"Read",1,DateTime.UtcNow)==null,"Replay shown as live"); link.logreadmode=false;
                Console.WriteLine("PASS: real MAVLink banks/target filter/timeout/disconnect/reconnect/replay, multiple channels, 500 redraws; GDI growth="+(after-before));
            }
            CheckIntegration(link);
            Check(Writes==0,"Indicator sent a vehicle command");
            Console.WriteLine("PASS: "+Checks+" surface indicator checks; typed input/persistence/UI/lifecycle; no vehicle writes.");
            if(args.Contains("--demo"))
            {
                using(var demo=(Form)New("FmtSurfaceAngleWindow"))
                using(var ticker=new Timer {Interval=200})
                {
                    demo.Text="舵角指示器 — 離線模擬測試"; demo.ShowInTaskbar=true;
                    double phase=0;
                    ticker.Tick+=(s,e)=>{ phase+=.08; Send(link,phase:phase); Send(link,1,phase:phase); };
                    demo.Shown+=(s,e)=>ticker.Start(); Application.Run(demo);
                }
            }
            stream.Dispose(); link.Dispose(); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void CheckIntegration(MAVLinkInterface link)
    {
        GMap.NET.GMaps.Instance.Mode=GMap.NET.AccessMode.CacheOnly;
        using(var flight=new FlightData())
        using(var owner=new Form {ClientSize=new Size(1300,800)})
        {
            flight.Load-=(EventHandler)Delegate.CreateDelegate(typeof(EventHandler),flight,typeof(FlightData).GetMethod("FlightData_Load",All));
            ((MissionPlanner.Controls.HUD)Field(flight,"hud1")).opengl=false;
            owner.Controls.Add(flight); flight.Dock=DockStyle.Fill; Offscreen(owner);
            var box=(CheckBox)flight.Controls.Find("CHK_fmtSurfaceAngles",true)[0];
            var safety=flight.Controls.Find("CHK_fmtSafety",true)[0];
            Check(box.Parent==safety.Parent && box.Parent.Controls.GetChildIndex(box)==box.Parent.Controls.GetChildIndex(safety)+1,"Indicator option must be immediately after Safety");
            for(int i=0;i<20;i++)
            {
                box.Checked=true; Application.DoEvents();
                var popup=(Form)Field(flight,"fmtSurfaceAngleWindow");
                Check(popup!=null && popup.Owner==owner,"No owned indicator window");
                Call(flight,"ToggleFmtSurfaceAngles"); Check(ReferenceEquals(popup,Field(flight,"fmtSurfaceAngleWindow")),"Duplicate popup");
                if(i%2==0) popup.Close(); else box.Checked=false;
                Check(popup.IsDisposed && !box.Checked && Field(flight,"fmtSurfaceAngleWindow")==null,"Window/checkbox not cleaned up");
            }
            box.Checked=true; var final=(Form)Field(flight,"fmtSurfaceAngleWindow"); flight.Dispose(); Check(final.IsDisposed,"Owner disposal left indicator alive");
        }
        Console.WriteLine("PASS: Safety option order, 20 open/close cycles, duplicate guard and owner disposal.");
    }
}
