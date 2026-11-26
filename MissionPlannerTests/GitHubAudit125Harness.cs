using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Comms;
using MissionPlanner.Controls;
using MissionPlanner.GCSViews;
using MissionPlanner.GCSViews.ConfigurationView;
using MissionPlanner.Mavlink;
using MissionPlanner.Utilities;
using Can = DroneCAN.DroneCAN;

internal static class GitHubAudit125Harness
{
    const BindingFlags Hidden = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static string root;
    static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); checks++; }
    static void Near(double expected, double value, string reason) { Check(Math.Abs(expected - value) < 0.0001, reason + ": " + value); }
    static MAVLink.MAVLinkMessage Packet(MAVLink.MAVLINK_MSG_ID id, object data, byte system = 17, byte component = 1)
    { return new MAVLink.MAVLinkMessage(new MAVLink.MavlinkParse().GenerateMAVLinkPacket20(id, data, false, system, component), DateTime.UtcNow); }
    static void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(12);
        while (!task.IsCompleted) { if (DateTime.UtcNow > until) throw new TimeoutException("Test task stalled"); Application.DoEvents(); Thread.Sleep(5); }
        task.GetAwaiter().GetResult();
    }
    static void Osd()
    {
        using (var port = new MAVLinkInterface())
        {
            var wire = new CommsInjection(); port.BaseStream = wire; port.sysidcurrent = 17; port.compidcurrent = 1; MainV2.comPort = port;
            using (var provider = new OsdTuningSlotProvider())
            {
                string result = "unanswered"; int responses = 0; string expected = "";
                provider.OnParamShowResponce += value => { result = value.Name; responses++; Check(value.Screen == 5 && value.Index == 6, "OSD slot mismatch"); };
                var receive = typeof(OsdTuningSlotProvider).GetMethod("HandleParamShowResponse", Hidden);
                wire.WriteCallback += (sender, bytes) =>
                {
                    var outgoing = new MAVLink.MAVLinkMessage(bytes.ToArray(), DateTime.UtcNow);
                    if (outgoing.msgid == (uint)MAVLink.MAVLINK_MSG_ID.OSD_PARAM_CONFIG)
                    {
                        var set = outgoing.ToStructure<MAVLink.mavlink_osd_param_config_t>();
                        var ack = Packet(MAVLink.MAVLINK_MSG_ID.OSD_PARAM_CONFIG_REPLY,
                            new MAVLink.mavlink_osd_param_config_reply_t { request_id = set.request_id, result = 0 });
                        var accept = typeof(OsdTuningSlotProvider).GetMethod("HandleParamSetResponse", Hidden);
                        accept.Invoke(provider, new object[] { ack }); accept.Invoke(provider, new object[] { ack });
                        return;
                    }
                    var request = outgoing.ToStructure<MAVLink.mavlink_osd_param_show_config_t>();
                    var name = new byte[16]; Encoding.ASCII.GetBytes(expected).CopyTo(name, 0);
                    var response = Packet(MAVLink.MAVLINK_MSG_ID.OSD_PARAM_SHOW_CONFIG_REPLY,
                        new MAVLink.mavlink_osd_param_show_config_reply_t { request_id = request.request_id, result = 0, param_id = name });
                    receive.Invoke(provider, new object[] { response });
                    receive.Invoke(provider, new object[] { response }); // duplicate must not duplicate the result
                };
                foreach (string name in new[] { "WP_RADIUS", "PARAM_NAME_1234X", "", "ABCDEFGHIJKLMNOP" })
                {
                    expected = name; result = "unanswered"; int before = responses;
                    provider.ParamShow(5, 6);
                    Check(result == (name == "" ? null : name), "OSD full-width/empty parameter name lost");
                    Check(responses == before + 1, "OSD immediate reply lost or duplicated");
                }
                provider.ParamSet(5, 6, "WP_RADIUS", (MAVLink.OSD_PARAM_CONFIG_TYPE)0, 1, 100, 1);
                int setResponses = 0;
                provider.OnParamSetResponce += resultSet => { Check(resultSet.Success && resultSet.Screen == 5 && resultSet.Index == 6, "OSD set acknowledgement mismatch"); setResponses++; };
                provider.ParamSet(5, 6, "WP_RADIUS", (MAVLink.OSD_PARAM_CONFIG_TYPE)0, 1, 100, 1);
                Check(setResponses == 1, "OSD immediate set reply lost or duplicated");
            }
        }
        Console.WriteLine("PASS OSD: full 16-byte names, empty names, immediate and duplicate replies.");
    }
    sealed class InputStream : MemoryStream
    {
        internal InputStream(string text) : base(Encoding.ASCII.GetBytes(text)) { }
        public override int ReadTimeout { get; set; }
    }
    static string Sbs(int type, string altitude = "", string rate = "")
    {
        var fields = Enumerable.Repeat("", 22).ToArray();
        fields[0] = "MSG"; fields[1] = type.ToString(); fields[4] = "ABC123";
        fields[11] = altitude; fields[12] = "450"; fields[13] = "87";
        if (type == 3) { fields[14] = "23.5"; fields[15] = "121.5"; }
        fields[16] = rate; fields[17] = "1200"; fields[21] = "0";
        return string.Join(",", fields) + "\r\n";
    }
    static void Adsb()
    {
        var reports = new List<adsb.PointLatLngAltHdg>();
        EventHandler<adsb.PointLatLngAltHdg> listener = (sender, point) => reports.Add(point);
        adsb.UpdatePlanePosition += listener;
        var running = typeof(adsb).GetField("run", Hidden);
        ((Hashtable)typeof(adsb).GetField("Planes", Hidden).GetValue(null)).Clear();
        try
        {
            running.SetValue(null, true);
            using (var input = new InputStream(Sbs(4, rate: "-640") + Sbs(3, "35000") + Sbs(3, "-100") + Sbs(3, "1000", "600") + Sbs(3, "1000")))
                new adsb().ReadMessage(input);
            Check(reports.Count == 4, "SBS position count");
            Near(10668, reports[0].Alt, "SBS altitude converted twice");
            Near(-325.12, reports[0].VerticalSpeed, "SBS MSG4 vertical rate missing");
            Near(-30.48, reports[1].Alt, "Negative SBS altitude");
            Near(304.8, reports[2].VerticalSpeed, "SBS MSG3 vertical rate missing");
            Near(304.8, reports[3].VerticalSpeed, "Blank vertical rate erased last velocity");
            var main = (MainV2)FormatterServices.GetUninitializedObject(typeof(MainV2));
            GC.SuppressFinalize(main); // This isolated event-handler fixture never initialized Form resources.
            main.adsblock = new object(); main.adsbPlanes = new ConcurrentDictionary<string, adsb.PointLatLngAltHdg>();
            var previous = MainV2.instance; MainV2.instance = main;
            try
            {
                typeof(MainV2).GetMethod("adsb_UpdatePlanePosition", Hidden).Invoke(main, new object[] { null, reports[0] });
                Near(-325.12, main.adsbPlanes["ABC123"].VerticalSpeed, "First traffic update lost vertical speed");
            }
            finally { MainV2.instance = previous; }
        }
        finally { running.SetValue(null, false); adsb.UpdatePlanePosition -= listener; }
        Console.WriteLine("PASS ADS-B: SBS altitude/velocity, negative height, empty rate and first target insertion.");
    }
    static void ActionsAndNumbers()
    {
        var get = typeof(FlightData).GetMethod("GetFmtActionParameters", Hidden);
        foreach (string action in new[] { "Mission_Start", "Nav_Loiter_Unlim", "Nav_Return_To_Launch", "Do_Adsb_Out_Ident" })
            Check(((int[])get.Invoke(null, new object[] { action, Firmwares.ArduCopter2 })).All(v => v == 0), "Reserved action parameters must be zero");
        Check(((int[])get.Invoke(null, new object[] { "Do_Parachute", Firmwares.ArduPlane })).SequenceEqual(new[] { 2, 0, 0 }), "Parachute action disabled instead of released");
        Check(((int[])get.Invoke(null, new object[] { "Preflight_Calibration", Firmwares.ArduCopter2 })).SequenceEqual(new[] { 1, 0, 1 }), "Copter calibration changed");
        Check(((int[])get.Invoke(null, new object[] { "Preflight_Calibration", Firmwares.ArduPlane })).SequenceEqual(new[] { 0, 0, 1 }), "Plane calibration changed");
        Check(((int[])get.Invoke(null, new object[] { "Battery_Reset", Firmwares.ArduPlane })).SequenceEqual(new[] { 255, 100, 0 }), "Battery action changed");
        var parse = typeof(ConfigBatteryMonitoring).GetMethod("TryParseMeasurement", Hidden);
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "zh-TW", "en-US", "de-DE", "fr-FR" })
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (string input in new[] { "12.34", "12,34", " 12.34 ", "1.234e1" })
                { object[] args = { input, 0f }; Check((bool)parse.Invoke(null, args), "Decimal input rejected"); Near(12.34, (float)args[1], "Decimal separator multiplied value"); }
                foreach (string input in new[] { "", "NaN", "Infinity", "1,234.56", "1.234,56", "1.2.3", "1e99" })
                { object[] args = { input, 0f }; Check(!(bool)parse.Invoke(null, args), "Invalid/non-finite number accepted"); }
            }
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
        Console.WriteLine("PASS actions/decimal input: release=2, reserved=0, calibration preserved; four locales.");
    }
    static void Missions()
    {
        foreach (var replyType in new[] { MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST, MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST_INT })
        using (var port = new MAVLinkInterface())
        {
            var wire = new CommsInjection(); port.BaseStream = wire; port.sysidcurrent = 17; port.compidcurrent = 1;
            int writes = 0;
            wire.WriteCallback += (sender, bytes) =>
            {
                writes++;
                var sent = new MAVLink.MAVLinkMessage(bytes.ToArray(), DateTime.UtcNow);
                Check(sent.msgid == (uint)MAVLink.MAVLINK_MSG_ID.MISSION_ITEM_INT, "Unexpected mission message");
                foreach (byte target in new byte[] { 99, MAVLinkInterface.gcssysid })
                {
                    object reply = replyType == MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST
                        ? (object)new MAVLink.mavlink_mission_request_t { seq = 4, target_system = target, target_component = 190 }
                        : new MAVLink.mavlink_mission_request_int_t { seq = 4, target_system = target, target_component = 190 };
                    wire.AppendBuffer(Packet(replyType, reply).buffer);
                }
            };
            var task = port.setWPAsync(new MAVLink.mavlink_mission_item_int_t { seq = 3, target_system = 17, target_component = 1,
                command = (ushort)MAVLink.MAV_CMD.WAYPOINT, frame = 3, x = 235000000, y = 1215000000, z = 25 });
            Wait(task);
            Check(task.Result == MAVLink.MAV_MISSION_RESULT.MAV_MISSION_ACCEPTED && writes == 1 && !port.giveComport,
                "Mission upload did not accept the matching next-item request");
        }
        Console.WriteLine("PASS mission upload: legacy/INT next-item requests; wrong target ignored; no retry/port lock retained.");
    }
    sealed class CallbackStream : MemoryStream
    {
        internal Action<string> Written;
        public override void Write(byte[] buffer, int offset, int count) { Written?.Invoke(Encoding.ASCII.GetString(buffer, offset, count)); }
    }
    static void CanParameters()
    {
        var client = new Can(); var decoder = new Can(); var wire = new CallbackStream();
        typeof(Can).GetField("sr", Hidden).SetValue(client, wire);
        wire.Written = line => decoder.ReadMessageSLCAN(line.Trim());
        var indices = new List<ushort>(); var transfers = new List<byte>(); bool dropFirst = true, truncate = false;
        decoder.MessageReceived += (frame, value, tid) =>
        {
            var request = value as Can.uavcan_protocol_param_GetSet_req;
            if (request == null) return;
            indices.Add(request.index);
            transfers.Add((byte)(tid & 31));
            if (dropFirst) { dropFirst = false; return; }
            if (truncate && request.index >= 3) return;
            string name = request.index < 40 ? "PARAM" + request.index : "";
            var response = new Can.uavcan_protocol_param_GetSet_res { name = Encoding.ASCII.GetBytes(name), name_len = (byte)name.Length };
            var wrong = new DroneCAN.CANFrame(new byte[4]) { IsServiceMsg = true, SourceNode = 44, SvcDestinationNode = client.SourceNode };
            client.InvokeMessageReceived(wrong, new Can.uavcan_protocol_param_GetSet_res { name_len = 0 }, tid);
            var correct = new DroneCAN.CANFrame(new byte[4]) { IsServiceMsg = true, SourceNode = 42, SvcDestinationNode = client.SourceNode };
            client.InvokeMessageReceived(correct, response, (byte)((tid + 1) & 31));
            client.InvokeMessageReceived(correct, response, tid);
            client.InvokeMessageReceived(correct, response, tid); // duplicate transfer
        };
        var result = client.GetParameters(42);
        Check(result.Count == 40, "CAN parameter list incomplete or mixed with another node");
        Check(indices.SequenceEqual(new ushort[] { 0 }.Concat(Enumerable.Range(0, 41).Select(i => (ushort)i))), "CAN index skipped or duplicated unnecessarily");
        Check(transfers.Zip(transfers.Skip(1), (a, b) => ((a + 1) & 31) == b).All(v => v), "CAN retry reused transfer ID or failed to wrap");
        Check(typeof(Can).GetField("MessageReceived", Hidden).GetValue(client) == null, "CAN subscription leaked");
        truncate = true;
        bool rejected = false;
        try { client.GetParameters(42); } catch (TimeoutException) { rejected = true; }
        Check(rejected, "CAN timeout falsely returned a complete list");
        Check(((ICollection)typeof(Can).GetField("_paramlistcache", Hidden).GetValue(client)).Count == 40,
            "Incomplete CAN parameter download contaminated the complete cache");
        Check(typeof(Can).GetField("MessageReceived", Hidden).GetValue(client) == null, "CAN timeout leaked subscription");
        Console.WriteLine("PASS DroneCAN: lost reply retry, wrong node/TID, duplicates, all indices, EOF and incomplete-list rejection.");
    }
    static void Signing()
    {
        var keyPath = Path.Combine(Settings.GetUserDataDirectory(), "authkeys.xml");
        MAVAuthKeys.Keys.Clear(); MAVAuthKeys.AddKey("test-key", "offline test fixture seed"); MAVAuthKeys.Save();
        var saved = File.ReadAllBytes(keyPath);
        var load = typeof(MAVAuthKeys).GetMethod("Load", Hidden);
        MAVAuthKeys.Keys.Clear(); load.Invoke(null, null); Check(MAVAuthKeys.Keys.Count == 1, "Signing key round trip");
        // Simulate the old no-adapter key and a different current adapter without changing this computer.
        byte[] fixture;
        using (var plain = new MemoryStream())
        using (var oldCipher = new Crypto(new byte[0]))
        {
            new DataContractSerializer(typeof(MAVAuthKeys.AuthKeys), new[] { typeof(MAVAuthKeys.AuthKey) }).WriteObject(plain, MAVAuthKeys.Keys);
            fixture = oldCipher.EncryptBuffer(plain.ToArray());
        }
        File.WriteAllBytes(keyPath, fixture);
        var cipherField = typeof(MAVAuthKeys).GetField("Rij", Hidden);
        ((Crypto)cipherField.GetValue(null)).Dispose(); cipherField.SetValue(null, new Crypto(new byte[] { 1, 2, 3, 4, 5, 6 }));
        MAVAuthKeys.Keys.Clear(); load.Invoke(null, null);
        Check(MAVAuthKeys.Keys.Count == 1 && MAVAuthKeys.LoadError == null, "Signing adapter fallback lost keys");
        MAVAuthKeys.Save(); load.Invoke(null, null); Check(MAVAuthKeys.Keys.Count == 1, "Recovered signing file not readable");
        var corrupt = new byte[] { 1, 5, 9, 13, 17 }; File.WriteAllBytes(keyPath, corrupt); load.Invoke(null, null);
        Check(MAVAuthKeys.LoadError != null, "Invalid signing file was not reported");
        bool blocked = false; try { MAVAuthKeys.Save(); } catch (IOException) { blocked = true; }
        Check(blocked && File.ReadAllBytes(keyPath).SequenceEqual(corrupt), "Unreadable signing file overwritten");
        File.WriteAllBytes(keyPath, fixture); load.Invoke(null, null); Check(MAVAuthKeys.LoadError == null, "Valid backup could not recover signing store");
        using (var held = new FileStream(keyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        { blocked = false; try { MAVAuthKeys.Save(); } catch (IOException) { blocked = true; } Check(blocked, "Locked signing save reported success"); }
        Check(File.ReadAllBytes(keyPath).SequenceEqual(fixture), "Failed signing save damaged file");
        Check(!Directory.GetFiles(Settings.GetUserDataDirectory(), "authkeys.xml.*.tmp").Any(), "Signing temp files leaked");
        Console.WriteLine("PASS signing: isolated round trip, adapter fallback, corrupt-file preservation, locked save and temp cleanup.");
    }
    static void Plugins(string calculatorPath)
    {
        CodeGenRoslyn.CachePath = Path.Combine(root, "plugins"); Directory.CreateDirectory(CodeGenRoslyn.CachePath);
        var source = Path.Combine(root, "Probe.cs");
        File.WriteAllText(source, "public static class Probe { public static string Value() { return \"A\"; } }");
        var first = CodeGenRoslyn.BuildCode(source); Check(first != null, "Plugin initial compile: " + CodeGenRoslyn.lasterror);
        var files = Directory.GetFiles(CodeGenRoslyn.CachePath);
        var source2 = "public static class Probe { public static string Value() { return \"Ł\"; } }";
        File.WriteAllText(source, source2, Encoding.UTF8);
        // Plant an incompatible old-style source-only cache entry. The new build must ignore it.
        string legacy;
        using (var md5 = MD5.Create()) legacy = BitConverter.ToString(md5.ComputeHash(source2.Select(c => (byte)c).ToArray())).Replace("-", "").ToLowerInvariant();
        foreach (string file in files) File.Copy(file, Path.Combine(CodeGenRoslyn.CachePath, legacy + Path.GetExtension(file)), true);
        var second = CodeGenRoslyn.BuildCode(source);
        Check(second != null && (string)second.GetType("Probe").GetMethod("Value").Invoke(null, null) == "Ł", "Old plugin cache or lossy Unicode source reused");
        int count = Directory.GetFiles(CodeGenRoslyn.CachePath).Length;
        var again = CodeGenRoslyn.BuildCode(source);
        Check(again != null && Directory.GetFiles(CodeGenRoslyn.CachePath).Length == count, "Same-build plugin cache not reused");
        Check(CodeGenRoslyn.BuildCode(calculatorPath) != null, "Initial parameter calculator compile: " + CodeGenRoslyn.lasterror);
        Console.WriteLine("PASS plugins: old cache ignored, Unicode source distinguished and current cache reused.");
    }
    static void WebSocket()
    {
        using (var hud = new HUD { opengl = false })
        using (var vehicle = new MAVLinkInterface())
        {
            FlightData.myhud = hud; var wire = new CommsInjection(); vehicle.BaseStream = wire; MainV2.comPort = vehicle;
            var subscriptions = typeof(MAVLinkInterface).GetField("_OnPacketReceived", Hidden);
            var originalSubscriptions = subscriptions.GetValue(vehicle);
            int writes = 0; wire.WriteCallback += (sender, bytes) => Interlocked.Increment(ref writes);
            foreach (int scenario in new[] { 0, 1, 2 })
            {
                bool externalOrigin = scenario == 0;
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                using (var browser = new TcpClient())
                {
                    browser.Connect((IPEndPoint)listener.LocalEndpoint); var accepted = listener.AcceptTcpClient(); listener.Stop();
                    var server = new httpserver(); httpserver.run = true;
                    var work = Task.Run(() => server.ProcessClient(accepted));
                    var stream = browser.GetStream(); stream.ReadTimeout = 4000;
                    var request = Encoding.ASCII.GetBytes("GET /websocket/raw HTTP/1.1\r\nHost: localhost\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                        (externalOrigin ? "Origin: https://untrusted.invalid\r\n" : "") + "\r\n");
                    stream.Write(request, 0, request.Length);
                    var header = new StringBuilder();
                    while (!header.ToString().EndsWith("\r\n\r\n")) { int next = stream.ReadByte(); if (next < 0) break; header.Append((char)next); }
                    Check(header.ToString().Contains(externalOrigin ? "403 Forbidden" : "101 WebSocket"), "WebSocket origin/handshake policy");
                    if (scenario == 1)
                    {
                        // A benign MAVLink heartbeat sent only to this fake connection must never reach its stream.
                        var heartbeat = Packet(MAVLink.MAVLINK_MSG_ID.HEARTBEAT, new MAVLink.mavlink_heartbeat_t()).buffer;
                        var frame = new byte[heartbeat.Length + 6]; frame[0] = 0x82; frame[1] = (byte)(0x80 | heartbeat.Length);
                        Buffer.BlockCopy(heartbeat, 0, frame, 6, heartbeat.Length); stream.Write(frame, 0, frame.Length);
                    }
                    if (scenario == 2)
                    {
                        var until = DateTime.UtcNow.AddSeconds(4);
                        while (Equals(subscriptions.GetValue(vehicle), originalSubscriptions) && DateTime.UtcNow < until) Thread.Sleep(5);
                        var before = ((Delegate)originalSubscriptions)?.GetInvocationList() ?? new Delegate[0];
                        var added = ((Delegate)subscriptions.GetValue(vehicle)).GetInvocationList().Except(before).ToArray();
                        Check(added.Length == 1, "WebSocket telemetry sender missing");
                        var heartbeat = Packet(MAVLink.MAVLINK_MSG_ID.HEARTBEAT, new MAVLink.mavlink_heartbeat_t());
                        ((EventHandler<MAVLink.MAVLinkMessage>)added[0])(vehicle, heartbeat);
                        Check(stream.ReadByte() == 0x82 && stream.ReadByte() == heartbeat.buffer.Length, "WebSocket downlink frame header");
                        var payload = new byte[heartbeat.buffer.Length];
                        for (int received = 0; received < payload.Length;)
                        { int count = stream.Read(payload, received, payload.Length - received); if (count == 0) throw new EndOfStreamException(); received += count; }
                        Check(payload.SequenceEqual(heartbeat.buffer), "WebSocket downlink telemetry altered");
                        browser.Close();
                    }
                    Wait(work); GC.KeepAlive(server);
                    Check(writes == 0, "Unauthenticated WebSocket wrote to vehicle");
                    Check(Equals(subscriptions.GetValue(vehicle), originalSubscriptions), "WebSocket telemetry subscription leaked: scenario " + scenario);
                }
            }
            FlightData.myhud = null;
        }
        Console.WriteLine("PASS WebSocket: cross-site rejection, read-only input, no vehicle writes, subscription cleanup; loopback sockets only.");
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "github-audit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Settings.CustomUserDataDirectory = root; Settings.AppConfigName = "GitHubAudit"; Settings.FileName = "test.xml";
            Osd(); Adsb(); ActionsAndNumbers(); Missions(); CanParameters(); Signing(); Plugins(args[0]); WebSocket();
            Console.WriteLine("PASS: " + checks + " GitHub audit assertions. No real vehicle, user key file or external endpoint was used."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
