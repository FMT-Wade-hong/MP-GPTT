using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner;
using MissionPlanner.Comms;

internal static class MissionResetHarness
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Type ResetType = typeof(MainV2).Assembly.GetType("MissionPlanner.FMT.FmtMissionReset", true);
    private static readonly Type PanelType = typeof(MainV2).Assembly.GetType("MissionPlanner.FMT.FmtAutoMissionPanel", true);
    private static readonly Type RelayType = typeof(MainV2).Assembly.GetType("MissionPlanner.FMT.FmtRelayControlService", true);
    private static void SetStation(int active)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        // Isolated in-process authority fixture; do not bind UDP or change the user's station configuration.
        RelayType.GetField("stopping", flags).SetValue(null, true);
        RelayType.GetField("listenerStation", flags).SetValue(null, 1);
        RelayType.GetField("activeStation", flags).SetValue(null, active);
    }
    private static int checks;
    private static void Check(bool valid, string reason) { if (!valid) throw new Exception(reason); checks++; }
    private static object Request(MAVLinkInterface link) => Activator.CreateInstance(ResetType, Hidden, null, new object[] { link }, null);
    private static Task<MAVLink.MAV_RESULT> Execute(object request, CancellationToken cancel = default(CancellationToken))
        => (Task<MAVLink.MAV_RESULT>)ResetType.GetMethod("ExecuteAsync", Hidden).Invoke(request, new object[] { cancel });
    private static string Validate(object request) => (string)ResetType.GetMethod("Validate", Hidden).Invoke(request, null);
    private static int AckSubscriptions(MAVLinkInterface link)
        => ((IEnumerable)typeof(MAVLinkInterface).GetField("Subscriptions", Hidden).GetValue(link)).Cast<object>()
            .Count(item => (MAVLink.MAVLINK_MSG_ID)item.GetType().GetField("Item1").GetValue(item) == MAVLink.MAVLINK_MSG_ID.COMMAND_ACK);

    private sealed class Vehicle : IDisposable
    {
        internal readonly MAVLinkInterface Link = new MAVLinkInterface();
        internal readonly CommsInjection Stream = new CommsInjection();
        private readonly MAVLink.MavlinkParse parser = new MAVLink.MavlinkParse();
        internal Action Reply;
        internal int Writes;
        internal Vehicle()
        {
            SetStation(1);
            Link.BaseStream = Stream; Link.sysidcurrent = 17; Link.compidcurrent = 1;
            Link.MAV.apname = MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA;
            Link.MAV.cs.mode = "AUTO"; Link.MAV.cs.wpno = 3;
            for (int i = 0; i < 4; i++) Link.MAV.wps[i] = new MAVLink.mavlink_mission_item_int_t
                { seq = (ushort)i, command = (ushort)(i == 1 ? MAVLink.MAV_CMD.TAKEOFF : MAVLink.MAV_CMD.WAYPOINT),
                    mission_type = (byte)MAVLink.MAV_MISSION_TYPE.MISSION, z = 30, x = 240000000, y = 1210000000 };
            MainV2.comPort = Link;
            Heartbeat();
            Stream.WriteCallback += (sender, data) =>
            {
                Writes++;
                MAVLink.MAVLinkMessage message;
                using (var input = new MemoryStream(data.ToArray())) message = parser.ReadPacket(input);
                Check(message != null && message.msgid == (uint)MAVLink.MAVLINK_MSG_ID.COMMAND_LONG, "Unexpected message sent");
                var command = message.ToStructure<MAVLink.mavlink_command_long_t>();
                Check(command.command == (ushort)MAVLink.MAV_CMD.DO_SET_MISSION_CURRENT, "Must only send mission-current command");
                Check(command.target_system == 17 && command.target_component == 1, "Wrong target");
                Check(command.param1 == 0 && command.param2 == 0 && command.param3 == 0 && command.param4 == 0 &&
                      command.param5 == 0 && command.param6 == 0 && command.param7 == 0, "Incorrect rewind / resume semantics");
                Check(!Link.giveComport, "Reset must leave the normal telemetry reader running");
                Reply?.Invoke();
            };
        }
        internal void Heartbeat(bool armed = false, double age = 0)
        {
            var bytes = parser.GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.HEARTBEAT,
                new MAVLink.mavlink_heartbeat_t { autopilot = (byte)MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA,
                    base_mode = armed ? (byte)MAVLink.MAV_MODE_FLAG.SAFETY_ARMED : (byte)0 }, false, 17, 1);
            Link.MAV.addPacket(new MAVLink.MAVLinkMessage(bytes, DateTime.UtcNow.AddSeconds(-age)));
        }
        internal void Ack(MAVLink.MAV_RESULT result, byte sysid = 17, byte compid = 1,
            MAVLink.MAV_CMD command = MAVLink.MAV_CMD.DO_SET_MISSION_CURRENT, byte recipient = 0)
        {
            Stream.AppendBuffer(parser.GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.COMMAND_ACK,
                new MAVLink.mavlink_command_ack_t { command = (ushort)command, result = (byte)result,
                    target_system = recipient }, false, sysid, compid));
            // Represents the application's existing receive loop, not the reset operation.
            Task.Run(async () => { await Link.readPacketAsync(); });
        }
        internal void Landed(byte state)
        {
            var bytes = parser.GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.EXTENDED_SYS_STATE,
                new MAVLink.mavlink_extended_sys_state_t { landed_state = state }, false, 17, 1);
            Link.MAV.addPacket(new MAVLink.MAVLinkMessage(bytes, DateTime.UtcNow));
        }
        public void Dispose() { Link.Dispose(); }
    }

    private static async Task ProtocolTests()
    {
        foreach (var result in new[] { MAVLink.MAV_RESULT.ACCEPTED, MAVLink.MAV_RESULT.DENIED,
            MAVLink.MAV_RESULT.FAILED, MAVLink.MAV_RESULT.UNSUPPORTED, MAVLink.MAV_RESULT.TEMPORARILY_REJECTED })
        {
            using (var v = new Vehicle())
            {
                var before = v.Link.MAV.wps.ToArray();
                v.Reply = () => v.Ack(result);
                Check(await Execute(Request(v.Link)) == result, "Lost ACK result");
                Check(v.Writes == 1 && AckSubscriptions(v.Link) == 0, "Repeated command or subscription leak");
                Check(v.Link.MAV.wps.ToArray().SequenceEqual(before) && v.Link.MAV.cs.wpno == 3 &&
                    v.Link.MAV.cs.mode == "AUTO" && !v.Link.MAV.cs.armed, "Local mission/state overwritten without telemetry");
            }
        }
        using (var v = new Vehicle())
        {
            var reset = Execute(Request(v.Link));
            v.Ack(MAVLink.MAV_RESULT.ACCEPTED, 18);
            v.Ack(MAVLink.MAV_RESULT.ACCEPTED, 17, 2);
            v.Ack(MAVLink.MAV_RESULT.ACCEPTED, 17, 1, MAVLink.MAV_CMD.DO_SET_MODE);
            v.Ack(MAVLink.MAV_RESULT.ACCEPTED, 17, 1, MAVLink.MAV_CMD.DO_SET_MISSION_CURRENT, 42);
            v.Ack(MAVLink.MAV_RESULT.IN_PROGRESS);
            await Task.Delay(200);
            Check(!reset.IsCompleted, "Wrong/stale/progress reply completed reset");
            bool duplicateRejected = false;
            try { await Execute(Request(v.Link)); } catch (InvalidOperationException) { duplicateRejected = true; }
            Check(duplicateRejected && v.Writes == 1, "Duplicate reset sent");
            v.Ack(MAVLink.MAV_RESULT.ACCEPTED);
            Check(await reset == MAVLink.MAV_RESULT.ACCEPTED && AckSubscriptions(v.Link) == 0, "Final ACK/cleanup failed");
        }
        // The operator may reset while armed/in flight, without the GCS changing mode or arming state.
        foreach (string mode in new[] { "AUTO", "ALT_HOLD" })
        foreach (bool armed in new[] { false, true })
        foreach (byte landed in new byte[] { 0, 1, 2, 3, 4 })
        {
            using (var v = new Vehicle())
            {
                v.Link.MAV.cs.mode = mode;
                v.Link.MAV.cs.armed = armed;
                v.Heartbeat(armed); v.Landed(landed);
                var request = Request(v.Link);
                Check(Validate(request) == null, "Arming/landing state blocked reset");
                v.Reply = () => v.Ack(MAVLink.MAV_RESULT.ACCEPTED);
                Check(await Execute(request) == MAVLink.MAV_RESULT.ACCEPTED && v.Writes == 1,
                    "Reset not transmitted for " + mode + "/" + armed + "/" + landed);
                Check(v.Link.MAV.cs.mode == mode && v.Link.MAV.cs.armed == armed && AckSubscriptions(v.Link) == 0,
                    "Reset changed flight mode/arming or leaked its subscription");
            }
        }
        foreach (string state in new[] { "stale", "readonly", "replay", "busy",
            "closed", "nomission", "changedmission", "changedtarget", "changedstream", "changedconnection", "parameters", "logs", "unsupported", "nocontrol" })
        {
            using (var v = new Vehicle())
            using (var other = new MAVLinkInterface())
            {
                var request = Request(v.Link);
                switch (state)
                {
                    case "stale": v.Heartbeat(false, 10); break;
                    case "readonly": v.Link.ReadOnly = true; break;
                    case "replay": v.Link.logreadmode = true; break;
                    case "busy": v.Link.giveComport = true; break;
                    case "closed": v.Stream.Close(); break;
                    case "nomission": v.Link.MAV.wps.Clear(); break;
                    case "changedmission": var item = v.Link.MAV.wps[1]; item.z = 50; v.Link.MAV.wps[1] = item; break;
                    case "changedtarget": v.Link.sysidcurrent = 18; break;
                    case "changedstream": v.Link.BaseStream = null; break;
                    case "changedconnection": MainV2.comPort = other; break;
                    case "parameters": typeof(MAVLinkInterface).GetField("activeParamDownloads", Hidden).SetValue(v.Link, 1); break;
                    case "logs": typeof(MAVLinkInterface).GetField("activeLogDownloads", Hidden).SetValue(v.Link, 1); break;
                    case "unsupported": v.Link.MAV.apname = MAVLink.MAV_AUTOPILOT.PX4; break;
                    case "nocontrol": SetStation(2); break;
                }
                Check(Validate(request) != null, "Invalid state enabled reset: " + state);
                bool rejected = false;
                try { await Execute(request); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && v.Writes == 0 && AckSubscriptions(v.Link) == 0, "Guard sent command: " + state);
                if (state == "busy") Check(v.Link.giveComport, "Reset released another operation's port");
                v.Link.BaseStream = v.Stream;
            }
        }
        using (var v = new Vehicle())
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            bool cancelled = false;
            try { await Execute(Request(v.Link), cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && v.Writes == 0, "Cancelled request sent");
        }
        using (var v = new Vehicle())
        using (var cancel = new CancellationTokenSource())
        {
            var task = Execute(Request(v.Link), cancel.Token);
            cancel.Cancel();
            bool cancelled = false;
            try { await task; } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && v.Writes == 1 && AckSubscriptions(v.Link) == 0, "Pending cancellation leaked or resent");
        }
        using (var v = new Vehicle())
        {
            var task = Execute(Request(v.Link));
            v.Link.sysidcurrent = 18;
            v.Ack(MAVLink.MAV_RESULT.ACCEPTED);
            bool rejected = false;
            try { await task; } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && v.Writes == 1 && AckSubscriptions(v.Link) == 0, "Late ACK after target change became success");
        }
        using (var v = new Vehicle())
        using (var timer = new System.Threading.Timer(_ => v.Heartbeat(), null, 1000, 1000))
        {
            v.Link.MAV.cs.wpno = 1;
            bool timedOut = false;
            try { await Execute(Request(v.Link)); } catch (TimeoutException) { timedOut = true; }
            Check(timedOut && v.Writes == 1 && AckSubscriptions(v.Link) == 0, "Timeout must not retry/report success/leak");
        }
    }

    private static void LayoutTests(bool demo)
    {
        using (var v = new Vehicle())
        using (var form = new Form { Text = "FMTPlanner 任務重置 — 離線介面測試", ClientSize = new Size(1100, 95),
            StartPosition = FormStartPosition.CenterScreen, BackColor = Color.FromArgb(12, 27, 36) })
        using (var panel = (Control)Activator.CreateInstance(PanelType, true))
        {
            v.Link.MAV.cs.wpno = 1;
            panel.Dock = DockStyle.Top; panel.Height = 55;
            form.Controls.Add(panel);
            if (!demo) { form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); }
            form.Show();
            var update = PanelType.GetMethod("UpdateFromVehicle", Hidden);
            var reset = (Button)panel.Controls.Find("fmtMissionReset", true)[0];
            var jump = (Button)panel.Controls.Find("fmtMissionExecute", true)[0];
            form.ActiveControl = reset;
            update.Invoke(panel, new object[] { true });
            Check(reset.Text == "重置任務" && reset.Enabled, "Reset must work at current/selected first item too");
            foreach (int width in new[] { 640, 800, 1024, 1050, 1366, 1500, 1920, 2560 })
            {
                form.ClientSize = new Size(width, 95); Application.DoEvents();
                Check(reset.Left >= jump.Right && reset.Width == jump.Width && reset.Height == jump.Height,
                    "Reset/jump not adjacent/equally sized at " + width);
                var row = reset.Parent;
                Check(row.Controls.Cast<Control>().Where(c => c.Visible).All(c => c.Left >= 0 && c.Right <= row.ClientSize.Width),
                    "Mission row overflow at " + width);
                var before = reset.Bounds;
                update.Invoke(panel, new object[] { true });
                Check(reset.Bounds == before, "Telemetry moved buttons at " + width);
                if (width == 640)
                    using (var bitmap = new Bitmap(panel.Width, panel.Height))
                    {
                        panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                        bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mission-reset-640.png"));
                    }
            }
            v.Link.MAV.cs.armed = true; v.Heartbeat(true);
            foreach (byte landed in new byte[] { 0, 1, 2, 3, 4 })
            {
                v.Landed(landed); update.Invoke(panel, new object[] { true });
                Check(reset.Enabled, "Armed/in-flight vehicle disabled reset");
            }
            v.Link.MAV.cs.armed = false;
            v.Heartbeat();
            PanelType.GetMethod("SetBusy", Hidden).Invoke(panel, new object[] { true });
            Check(!reset.Enabled && !jump.Enabled, "Mission operations are not mutually exclusive");
            PanelType.GetMethod("SetBusy", Hidden).Invoke(panel, new object[] { false });
            form.ClientSize = new Size(1100, 95); update.Invoke(panel, new object[] { true });
            Application.DoEvents();
            using (var bitmap = new Bitmap(panel.Width, panel.Height))
            {
                panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mission-reset-preview.png"));
            }
            Check(v.Writes == 0, "Rendering/refresh sent vehicle commands");
            if (!demo)
            {
                v.Link.MAV.wps.Clear();
                v.Link.MAV.cs.mode = "ALT_HOLD";
                update.Invoke(panel, new object[] { true });
                using (var bitmap = new Bitmap(panel.Width, panel.Height))
                {
                    panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mission-not-read-preview.png"));
                }
            }
            if (!demo)
            {
                panel.Dispose();
                panel.Dispose();
                Check(AckSubscriptions(v.Link) == 0, "Disposed panel retained command subscriptions");
            }
            if (demo)
            {
                // Preview only: physical vehicle never connected; all command buttons disabled.
                v.Link.ReadOnly = true; reset.Enabled = false; jump.Enabled = false;
                form.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 32, ForeColor = Color.White,
                    Text = "離線示意：重置任務位於跳轉航點右側；按鈕已停用，不會操作飛控。" });
                Application.Run(form);
            }
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            if (!args.Contains("--demo")) ProtocolTests().GetAwaiter().GetResult();
            LayoutTests(args.Contains("--demo"));
            Console.WriteLine("PASS: " + checks + " mission reset checks; in-memory MAVLink and offline UI only.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
