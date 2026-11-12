using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Comms;
using MissionPlanner.Controls;
using MissionPlanner.GCSViews.ConfigurationView;
using MissionPlanner.Utilities;
using Port = MissionPlanner.Comms.SerialPort;

internal static class GitHubIssuesHarness
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
    static void Check(bool valid, string reason) { if (!valid) throw new Exception(reason); }
    static void Until(Func<bool> ready, string reason)
    {
        var timer = Stopwatch.StartNew();
        while (!ready()) { if (timer.ElapsedMilliseconds > 5000) throw new Exception(reason); Application.DoEvents(); Thread.Sleep(5); }
    }
    static void Pump(Task task) { Until(() => task.IsCompleted, "Task timed out"); task.GetAwaiter().GetResult(); }

    [STAThread]
    static int Main()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            CheckBitmask();
            CheckWirePrecision();
            CheckSerial();
            Console.WriteLine("PASS: GitHub bitmask precision, real raw-grid staging, wire encoding and serial-port regressions; no real vehicle/device connection.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void CheckBitmask()
    {
        const string name = "CAN_D1_UC_SRV_BM";
        var mav = MainV2.comPort.MAV;
        mav.cs.firmware = Firmwares.ArduCopter2;
        mav.VersionString = "ArduCopter V4.7.2";
        ParameterMetaDataRepository.VehicleTypeResolver = vehicle => "Copter4.7.2";
        mav.param.Clear();
        mav.param.Add(new MAVLink.MAVLinkParam(name, 16777217, MAVLink.MAV_PARAM_TYPE.INT32));
        using (var control = new MavlinkCheckBoxBitMask())
        {
            int events = 0; string emitted = null;
            control.ValueChanged += (sender, key, text) => { events++; emitted = text; };
            control.setup(name, mav.param);
            Check(control.Value == 16777217 && events == 0, "Initial high+low mask rounded or wrote during setup");
            for (int bit = 0; bit < 32; bit++)
            {
                var bits = (1u << bit) | 1u;
                var expected = unchecked((int)bits);
                control.Value = expected;
                Check(control.Value == expected && events == 0, "Mask setter rounded/wrote at bit " + bit);
            }
            foreach (var expected in new[] { -1, int.MinValue, int.MaxValue, 16777217, 33554433, 0 })
            {
                control.Value = expected;
                Check(control.Value == expected, "Signed mask mismatch " + expected);
            }
            control.Value = 16777217;
            var checks = (List<KeyValuePair<int, CheckBox>>)typeof(MavlinkCheckBoxBitMask).GetField("chklist", Hidden).GetValue(control);
            checks.Single(x => x.Key == 25).Value.Checked = true;
            Check(events == 1 && emitted == "50331649" && control.Value == 50331649, "Checkbox event lost low bits");
            var old = checks.Select(x => x.Value).ToArray();
            control.setup(name, mav.param);
            Check(old.All(x => x.IsDisposed) && control.Value == 16777217 && events == 1, "Rebinding leaked controls or emitted a write");
            mav.param[name] = new MAVLink.MAVLinkParam(name, 4294967295d, MAVLink.MAV_PARAM_TYPE.UINT32);
            control.setup(name, mav.param);
            Check(control.Value == 4294967295d, "UINT32 mask rounded");
        }
        mav.param[name] = new MAVLink.MAVLinkParam(name, 16777217, MAVLink.MAV_PARAM_TYPE.INT32);
        var rawType = typeof(ConfigRawParams);
        var startup = rawType.GetField("startup", StaticHidden);
        var previous = startup.GetValue(null);
        using (var raw = new ConfigRawParams())
        {
            try
            {
                startup.SetValue(null, true);
                rawType.GetMethod("processToScreen", Hidden).Invoke(raw, null);
                startup.SetValue(null, false);
                rawType.GetMethod("ShowBitmaskEditor", Hidden).Invoke(raw, new object[] { 0 });
                var popup = Application.OpenForms.Cast<Form>().Single(f => f.Controls.OfType<MavlinkCheckBoxBitMask>().Any());
                popup.Location = new Point(-30000, -30000);
                try
                {
                    var mask = popup.Controls.OfType<MavlinkCheckBoxBitMask>().Single();
                    var checks = (List<KeyValuePair<int, CheckBox>>)typeof(MavlinkCheckBoxBitMask).GetField("chklist", Hidden).GetValue(mask);
                    checks.Single(x => x.Key == 25).Value.Checked = true;
                    var changes = (Hashtable)rawType.GetField("_changes", Hidden).GetValue(raw);
                    var grid = (DataGridView)rawType.GetField("Params", Hidden).GetValue(raw);
                    Check((double)changes[name] == 50331649 && Convert.ToDouble(grid.Rows[0].Cells["Value"].Value) == 50331649,
                        "Real mask popup/grid staging rounded");
                    Check(mav.param[name].Value == 16777217, "Editing modified vehicle cache");
                }
                finally { popup.Close(); }
            }
            finally { startup.SetValue(null, previous); }
        }
        Console.WriteLine("PASS: all 32 mask bits, signed/unsigned extremes, exact checkbox events, rebind disposal and actual popup/grid staging.");
    }

    static void CheckWirePrecision()
    {
        using (var link = new MAVLinkInterface())
        {
            var stream = new CommsInjection(); link.BaseStream = stream;
            var mav = link.MAVlist[1, 1]; mav.apname = MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA;
            const string name = "FMT_MASK";
            mav.param[name] = new MAVLink.MAVLinkParam(name, BitConverter.GetBytes(0f), MAVLink.MAV_PARAM_TYPE.REAL32, MAVLink.MAV_PARAM_TYPE.INT32);
            mav.param_types[name] = MAVLink.MAV_PARAM_TYPE.INT32;
            var parser = new MAVLink.MavlinkParse(); int sets = 0;
            stream.WriteCallback += (sender, bytes) =>
            {
                MAVLink.MAVLinkMessage message;
                using (var input = new MemoryStream(bytes.ToArray())) message = parser.ReadPacket(input);
                if (message == null || message.msgid != (uint)MAVLink.MAVLINK_MSG_ID.PARAM_SET) return;
                sets++;
                var request = message.ToStructure<MAVLink.mavlink_param_set_t>();
                stream.AppendBuffer(parser.GenerateMAVLinkPacket10(MAVLink.MAVLINK_MSG_ID.PARAM_VALUE,
                    new MAVLink.mavlink_param_value_t { param_id=request.param_id, param_value=request.param_value,
                        param_type=request.param_type, param_count=1, param_index=0 }, 1, 1));
            };
            try { Pump(link.setParamAsync(1, 1, name, 16777217, true)); throw new Exception("Inexact integer was sent"); }
            catch (ArgumentOutOfRangeException) { }
            Check(sets == 0 && !link.giveComport && mav.param[name].Value == 0, "Precision rejection altered transport/cache");
            var accepted = link.setParamAsync(1, 1, name, 16777216, true); Pump(accepted);
            Check(accepted.Result && sets == 1 && mav.param[name].Value == 16777216,
                "Exact numeric integer rejected: success=" + accepted.Result + " sets=" + sets + " actual=" + mav.param[name].Value.ToString("R"));
            mav.apname = MAVLink.MAV_AUTOPILOT.GENERIC;
            mav.cs.capabilities = 0;
            mav.param[name] = new MAVLink.MAVLinkParam(name, 0, MAVLink.MAV_PARAM_TYPE.INT32);
            var encoded = link.setParamAsync(1, 1, name, 16777217, true); Pump(encoded);
            Check(encoded.Result && sets == 2 && mav.param[name].Value == 16777217, "Byte-wise integer encoding lost bits");
        }
        Console.WriteLine("PASS: inexact REAL32 integer write blocked before send; exact numeric and byte-wise 32-bit writes acknowledged in memory.");
    }

    static void CheckSerial()
    {
        var normalize = typeof(Port).GetMethod("NormalizePortNames", StaticHidden);
        var result = (string[])normalize.Invoke(null, new object[] { new[] { null, "", "COMabc", "COM10c", "COM1a2", "COM1234", "com1234", " COM4\0 ", "/dev/ttyACM0", "UDP", "bad\0name" } });
        Check(result.SequenceEqual(new[] { "COM10", "COM1", "COM1234", "COM4", "/dev/ttyACM0", "UDP" }), "Port normalization lost/truncated/merged names");
        var reader = typeof(Port).GetField("SystemPortReader", StaticHidden);
        var savedReader = reader.GetValue(null);
        var savedCustom = Port.GetCustomPorts;
        using (var gate = new ManualResetEventSlim())
        {
            int queries = 0;
            try
            {
                Port.GetCustomPorts = () => new List<string> { "TCP", null, "" };
                reader.SetValue(null, (Func<string[]>)(() => new[] { "COM1234", null, "COMabc" }));
                Check(Port.GetPortNames().SequenceEqual(new[] { "COM1234", "TCP" }), "Unsafe items reached dropdown list");
                reader.SetValue(null, (Func<string[]>)(() => { Interlocked.Increment(ref queries); gate.Wait(); return new[] { "COM99" }; }));
                var timer = Stopwatch.StartNew(); var cached = Port.GetPortNames();
                Check(timer.ElapsedMilliseconds < 1700 && cached.Contains("COM1234"), "Slow enumeration blocked or lost last good ports");
                Port.GetPortNames(); Check(queries == 1, "Repeated dropdown starts duplicate blocked workers");
                gate.Set();
                Until(() => ((Task)typeof(Port).GetField("portEnumeration", StaticHidden).GetValue(null)).IsCompleted, "Enumeration did not finish");
                Check(Port.GetPortNames().Contains("COM99"), "Late enumeration result discarded");
                reader.SetValue(null, (Func<string[]>)(() => { throw new IOException("test driver failure"); }));
                Check(Port.GetPortNames().Contains("COM99"), "Driver failure crashed or discarded cache");
            }
            finally { gate.Set(); reader.SetValue(null, savedReader); Port.GetCustomPorts = savedCustom; }
        }
        using (var gate = new ManualResetEventSlim())
        {
            int slowCalls = 0;
            Port.EventArgsDeviceName resolver = port =>
            {
                if (port == "FMT_SLOW") { Interlocked.Increment(ref slowCalls); gate.Wait(); return "slow Bluetooth"; }
                if (port == "FMT_FAIL") throw new IOException("test WMI failure");
                return "name:" + port;
            };
            Port.GetDeviceName += resolver;
            try
            {
                var timer = Stopwatch.StartNew();
                for (int i=0; i<1000; i++) Check(Port.GetNiceName("FMT_SLOW") == "", "Premature name");
                Check(timer.ElapsedMilliseconds < 500, "Drawing waits for slow WMI");
                Until(() => slowCalls > 0, "Lookup did not start"); Check(slowCalls == 1, "Lookup not deduplicated");
                Port.GetNiceName("FMT_FAST");
                Until(() => Port.GetNiceName("FMT_FAST") == "name:FMT_FAST", "Independent lookup blocked");
                gate.Set(); Until(() => Port.GetNiceName("FMT_SLOW") == "slow Bluetooth", "Slow lookup aborted or misattributed");
                Check(Port.GetNiceName("FMT_FAST") == "name:FMT_FAST", "Late result overwrote another port");
                Check(Port.GetNiceName(null) == "" && Port.GetNiceName("AUTO") == "", "Invalid/special port not safe");
                Port.GetNiceName("FMT_FAIL");
                var requests = (System.Collections.IDictionary)typeof(Port).GetField("nameRequests", StaticHidden).GetValue(null);
                Until(() => ((Lazy<Task<string>>)requests["FMT_FAIL"]).Value.IsCompleted, "Failing lookup hung");
                Check(Port.GetNiceName("FMT_FAIL") == "", "WMI exception escaped drawing");
            }
            finally { gate.Set(); Port.GetDeviceName -= resolver; }
        }
        Console.WriteLine("PASS: malformed/duplicate/null/4-digit ports, slow/failing enumeration, 1000 nonblocking lookups, no duplicate workers or cross-port late results.");
    }
}
