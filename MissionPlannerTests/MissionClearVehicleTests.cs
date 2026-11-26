using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.GCSViews;

internal static class MissionClearVehicleTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Type ClearType = typeof(MainV2).Assembly.GetType("MissionPlanner.FMT.FmtMissionClear", true);
    private static readonly Type RelayType = typeof(MainV2).Assembly.GetType("MissionPlanner.FMT.FmtRelayControlService", true);
    private static int checks;
    private static void Check(bool valid, string reason) { if (!valid) throw new Exception(reason); Interlocked.Increment(ref checks); }
    private static object Request(Vehicle vehicle) => Activator.CreateInstance(ClearType, Hidden, null, new object[] { vehicle.Link }, null);
    private static Task Execute(object request, CancellationToken cancel = default(CancellationToken))
        => (Task)ClearType.GetMethod("ExecuteAsync", Hidden).Invoke(request, new object[] { cancel });
    private static bool Active => (bool)ClearType.GetProperty("IsActive", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    private static int Subscriptions(Vehicle v) => ((IEnumerable)typeof(MAVLinkInterface).GetField("Subscriptions", Hidden).GetValue(v.Link)).Cast<object>()
        .Count(item => new[] { MAVLink.MAVLINK_MSG_ID.MISSION_ACK, MAVLink.MAVLINK_MSG_ID.MISSION_COUNT }
            .Contains((MAVLink.MAVLINK_MSG_ID)item.GetType().GetField("Item1").GetValue(item)));
    private static void Station(int active)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        RelayType.GetField("stopping", flags).SetValue(null, true);
        RelayType.GetField("listenerStation", flags).SetValue(null, 1);
        RelayType.GetField("activeStation", flags).SetValue(null, active);
    }
    private static async Task Fails<T>(Task operation) where T : Exception
    {
        try { await operation; }
        catch (T) { Check(true, "Expected failure"); return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static async Task Until(Func<bool> predicate)
    {
        var timer = Stopwatch.StartNew();
        while (!predicate()) { if (timer.ElapsedMilliseconds > 2000) throw new Exception("Test stage timed out"); await Task.Delay(10); }
    }

    private sealed class Vehicle : IDisposable
    {
        internal readonly MAVLinkInterface Link = new MAVLinkInterface();
        internal readonly CommsInjection Stream = new CommsInjection();
        internal readonly ConcurrentQueue<MAVLink.MAVLINK_MSG_ID> Sent = new ConcurrentQueue<MAVLink.MAVLINK_MSG_ID>();
        private readonly MAVLink.MavlinkParse parser = new MAVLink.MavlinkParse();
        private readonly SemaphoreSlim reader = new SemaphoreSlim(1);
        internal Action OnClear, OnCount;
        internal ushort StoredCount = 3;
        internal bool Downloading;
        internal Vehicle()
        {
            Station(1);
            Link.BaseStream = Stream; Link.sysidcurrent = 17; Link.compidcurrent = 1;
            Link.MAV.apname = MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA;
            Link.MAV.cs.mode = "ALT_HOLD";
            MainV2.comPort = Link;
            for (int i = 0; i < 3; i++) Link.MAV.wps[i] = new MAVLink.mavlink_mission_item_int_t
                { seq = (ushort)i, command = (ushort)MAVLink.MAV_CMD.WAYPOINT, x = 240000000, y = 1210000000, z = 30 };
            Link.MAV.fencepoints[1] = new MAVLink.mavlink_mission_item_int_t { seq = 1, mission_type = 1 };
            Link.MAV.rallypoints[1] = new MAVLink.mavlink_mission_item_int_t { seq = 1, mission_type = 2 };
            Heartbeat();
            OnClear = () => { StoredCount = 0; _ = Ack(); };
            OnCount = () => { _ = Count(StoredCount); };
            Stream.WriteCallback += (sender, bytes) =>
            {
                MAVLink.MAVLinkMessage message;
                using (var input = new MemoryStream(bytes.ToArray())) message = parser.ReadPacket(input);
                Check(message != null, "Invalid outgoing MAVLink packet");
                var id = (MAVLink.MAVLINK_MSG_ID)message.msgid;
                Sent.Enqueue(id);
                Check(Downloading || !Link.giveComport, "Clear took over the telemetry reader");
                if (id == MAVLink.MAVLINK_MSG_ID.MISSION_CLEAR_ALL)
                {
                    var command = message.ToStructure<MAVLink.mavlink_mission_clear_all_t>();
                    Check(command.target_system == 17 && command.target_component == 1 && command.mission_type == 0, "Wrong clear scope/target");
                    OnClear?.Invoke();
                }
                else if (id == MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST_LIST)
                {
                    var command = message.ToStructure<MAVLink.mavlink_mission_request_list_t>();
                    Check(command.target_system == 17 && command.target_component == 1 && command.mission_type == 0, "Wrong read-back scope/target");
                    OnCount?.Invoke();
                }
                else if (id == MAVLink.MAVLINK_MSG_ID.MISSION_ACK)
                {
                    var command = message.ToStructure<MAVLink.mavlink_mission_ack_t>();
                    Check(command.target_system == 17 && command.target_component == 1 && command.mission_type == 0 && command.type == 0,
                        "Invalid download completion ACK");
                }
                else throw new Exception("Unexpected command: " + id);
            };
        }
        internal void Heartbeat(double age = 0)
        {
            var packet = parser.GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.HEARTBEAT,
                new MAVLink.mavlink_heartbeat_t { autopilot = (byte)MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA }, false, 17, 1);
            Link.MAV.addPacket(new MAVLink.MAVLinkMessage(packet, DateTime.UtcNow.AddSeconds(-age)));
        }
        private async Task Receive(MAVLink.MAVLINK_MSG_ID id, object data, byte system, byte component)
        {
            await reader.WaitAsync();
            try
            {
                Stream.AppendBuffer(parser.GenerateMAVLinkPacket20(id, data, false, system, component));
                if (!Downloading) await Task.Run(async () => await Link.readPacketAsync());
            }
            finally { reader.Release(); }
        }
        internal Task Ack(byte result = 0, byte system = 17, byte component = 1, byte type = 0, byte recipient = 0, byte recipientComponent = 0)
            => Receive(MAVLink.MAVLINK_MSG_ID.MISSION_ACK, new MAVLink.mavlink_mission_ack_t
            { type = result, mission_type = type, target_system = recipient, target_component = recipientComponent }, system, component);
        internal Task Count(ushort count, byte system = 17, byte component = 1, byte type = 0, byte? recipient = null, byte recipientComponent = 190)
            => Receive(MAVLink.MAVLINK_MSG_ID.MISSION_COUNT, new MAVLink.mavlink_mission_count_t
            { count = count, mission_type = type, target_system = recipient ?? MAVLinkInterface.gcssysid, target_component = recipientComponent }, system, component);
        public void Dispose() { Check(Subscriptions(this) == 0 && !Active, "Mission clear subscriptions/lock leaked"); Link.Dispose(); }
    }

    internal static async Task Run()
    {
        using (var v = new Vehicle())
        {
            v.Link.MAV.cs.armed = true;
            await Execute(Request(v));
            Check(v.StoredCount == 0 && v.Link.MAV.wps.IsEmpty, "Mission not cleared from vehicle/cache");
            Check(v.Link.MAV.fencepoints.Count == 1 && v.Link.MAV.rallypoints.Count == 1, "Fence/rally cache changed");
            Check(v.Link.MAV.cs.armed && v.Link.MAV.cs.mode == "ALT_HOLD", "Arming/mode changed");
            Check(v.Sent.ToArray().SequenceEqual(new[] { MAVLink.MAVLINK_MSG_ID.MISSION_CLEAR_ALL,
                MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST_LIST, MAVLink.MAVLINK_MSG_ID.MISSION_ACK }), "Wrong protocol sequence");
            // Repeat the real mission download used by the planner after clear.
            v.Downloading = true;
            var downloaded = await MissionPlanner.ArduPilot.mav_mission.download(v.Link, 17, 1, MAVLink.MAV_MISSION_TYPE.MISSION, (p, s) => { });
            Check(downloaded.Count == 0, "Re-read restored old mission");
        }
        foreach (byte rejection in new byte[] { 1, 3, 4, 14 })
            using (var v = new Vehicle())
            {
                v.OnClear = () => { _ = v.Ack(rejection); };
                await Fails<InvalidOperationException>(Execute(Request(v)));
                Check(v.Link.MAV.wps.Count == 3 && v.Sent.Count == 1, "Rejected clear changed local mission or read it back");
            }
        using (var v = new Vehicle())
        {
            v.OnClear = () => { _ = v.Ack(); }; // ACK claims success but storage still has the old route.
            await Fails<InvalidOperationException>(Execute(Request(v)));
            Check(v.StoredCount == 3 && v.Sent.Count == 2, "Non-empty read-back accepted");
        }
        using (var v = new Vehicle())
        {
            v.OnClear = null; v.OnCount = null;
            var task = Execute(Request(v));
            await v.Ack(system: 18); await v.Ack(component: 2); await v.Ack(type: 1);
            await v.Ack(recipient: 42); await v.Ack(recipientComponent: 42);
            await Task.Delay(50);
            Check(!task.IsCompleted && v.Sent.Count == 1, "Wrong ACK accepted");
            await Fails<InvalidOperationException>(Execute(Request(v)));
            await v.Ack();
            await Until(() => v.Sent.Count == 2);
            await v.Count(0, system: 18); await v.Count(0, component: 2);
            await v.Count(0, type: 1); await v.Count(0, recipient: 42); await v.Count(0, recipientComponent: 42);
            await Task.Delay(50);
            Check(!task.IsCompleted, "Wrong mission count accepted");
            await v.Count(0); await task;
            Check(v.Sent.Count(id => id == MAVLink.MAVLINK_MSG_ID.MISSION_CLEAR_ALL) == 1, "Duplicate clear sent");
        }
        foreach (bool afterAck in new[] { false, true })
            using (var v = new Vehicle())
            {
                if (afterAck) v.OnCount = null; else v.OnClear = null;
                await Fails<TimeoutException>(Execute(Request(v)));
                Check(v.Sent.Count(id => id == MAVLink.MAVLINK_MSG_ID.MISSION_CLEAR_ALL) == 1, "Timeout resent deletion");
            }
        foreach (bool afterAck in new[] { false, true })
            using (var v = new Vehicle())
            using (var cancel = new CancellationTokenSource())
            {
                if (afterAck) v.OnCount = null; else v.OnClear = null;
                var task = Execute(Request(v), cancel.Token);
                await Until(() => v.Sent.Count == (afterAck ? 2 : 1));
                cancel.Cancel();
                await Fails<OperationCanceledException>(task);
            }
        foreach (Action<Vehicle> invalidate in new Action<Vehicle>[]
        { v => v.Link.ReadOnly = true, v => v.Link.logreadmode = true, v => v.Link.giveComport = true,
          v => v.Stream.Close(), v => v.Heartbeat(10), v => Station(2) })
            using (var v = new Vehicle())
            {
                var request = Request(v); invalidate(v);
                await Fails<InvalidOperationException>(Execute(request));
                Check(v.Sent.IsEmpty, "Invalid connection sent clear");
            }
        using (var v = new Vehicle())
        {
            var request = Request(v); v.Link.sysidcurrent = 18;
            await Fails<InvalidOperationException>(Execute(request));
            Check(v.Sent.IsEmpty, "Changed target received clear");
        }
        using (var v = new Vehicle())
        {
            v.OnClear = null;
            var task = Execute(Request(v));
            v.Link.sysidcurrent = 18;
            await v.Ack();
            await Fails<InvalidOperationException>(task);
            Check(v.Sent.Count == 1, "Target change sent read-back to wrong vehicle");
        }
        Console.WriteLine("PASS: " + checks + " vehicle-clear protocol checks, including actual re-download after clearing.");
    }

    private static Task ClearUi(FlightPlanner planner)
        => (Task)typeof(FlightPlanner).GetMethod("ClearMissionAsync", Hidden).Invoke(planner, null);
    private static void Pump(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timer.ElapsedMilliseconds > 8000) throw new Exception("UI clear timed out");
            Application.DoEvents(); Thread.Sleep(5);
        }
        task.GetAwaiter().GetResult();
    }
    internal static void UiTests()
    {
        int before = checks;
        string lastMessage = null;
        var confirmation = CustomMessageBox.DialogResult.No;
        CustomMessageBox.ShowDelegate dialog = (text, title, buttons, icon, yes, no) =>
        {
            lastMessage = text;
            return buttons == CustomMessageBox.MessageBoxButtons.YesNo ? confirmation : CustomMessageBox.DialogResult.OK;
        };
        CustomMessageBox.ShowEvent += dialog;
        try
        {
            using (var v = new Vehicle())
            using (var planner = new FlightPlanner())
            {
                planner.BindingContext = new BindingContext();
                ((ComboBox)planner.Controls.Find("cmb_missiontype", true).Single()).SelectedValue = MAVLink.MAV_MISSION_TYPE.MISSION;
                planner.CMB_altmode.SelectedValue = FlightPlanner.altmode.Relative;
                var home = new[] { "TXT_homelat", "TXT_homelng", "TXT_homealt" }.Select(n => planner.Controls.Find(n, true).Single()).ToArray();
                home[0].Text = "24"; home[1].Text = "121"; home[2].Text = "100";
                var homeBefore = home.Select(c => c.Text).ToArray();
                planner.clearMissionToolStripMenuItem_Click(null, EventArgs.Empty);
                planner.AddCommand(MAVLink.MAV_CMD.WAYPOINT, 0, 0, 0, 0, 24.001, 121.001, 30);
                Pump(ClearUi(planner));
                Check(v.Sent.IsEmpty && planner.Commands.Rows.Count == 1, "Cancelled UI clear changed plan/vehicle");
                confirmation = CustomMessageBox.DialogResult.Yes;
                v.OnClear = () => { _ = v.Ack(1); };
                Pump(ClearUi(planner));
                Check(planner.Commands.Rows.Count == 1 && lastMessage.Contains("拒絕") && planner.Enabled, "Rejected clear lost the plan or did not restore UI");
                v.OnClear = () => { _ = v.Ack(); }; // Fake success, but read-back is still three.
                Pump(ClearUi(planner));
                Check(planner.Commands.Rows.Count == 1 && lastMessage.Contains("仍有 3"), "False ACK erased the planning grid");
                // A blank planner must still be able to clear the mission on the vehicle.
                planner.clearMissionToolStripMenuItem_Click(null, EventArgs.Empty);
                v.OnClear = null;
                var pending = ClearUi(planner);
                Check(!planner.Enabled && Active, "Controls not disabled while clearing");
                int sent = v.Sent.Count;
                Pump(ClearUi(planner));
                planner.BUT_read_Click(null, EventArgs.Empty);
                planner.BUT_write_Click(null, EventArgs.Empty);
                planner.but_writewpfast_Click(null, EventArgs.Empty);
                Check(v.Sent.Count == sent, "Busy actions sent overlapping mission commands");
                v.StoredCount = 0; _ = v.Ack();
                Pump(pending);
                Check(planner.Commands.Rows.Count == 0 && planner.Enabled && lastMessage.Contains("讀回任務數量為 0"), "Verified clear not shown as success");
                Check(home.Select(c => c.Text).SequenceEqual(homeBefore), "Clear altered HOME");
                Check(v.Link.MAV.fencepoints.Count == 1 && v.Link.MAV.rallypoints.Count == 1, "Clear altered other plan types");
                v.Downloading = true;
                var downloaded = MissionPlanner.ArduPilot.mav_mission.download(v.Link, 17, 1, MAVLink.MAV_MISSION_TYPE.MISSION, (p, s) => { });
                Pump(downloaded);
                planner.WPtoScreen(downloaded.Result);
                Check(planner.Commands.Rows.Count == 0, "Reload resurrected cleared waypoints");
            }
            Console.WriteLine("PASS: " + (checks - before) + " online UI checks; clear then re-read stays empty.");
        }
        finally { CustomMessageBox.ShowEvent -= dialog; }
    }
}
