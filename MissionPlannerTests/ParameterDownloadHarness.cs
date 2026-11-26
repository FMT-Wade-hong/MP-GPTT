using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.ArduPilot.Mavlink;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;

internal static class ParameterDownloadHarness
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); Interlocked.Increment(ref checks); }
    private static async Task Reject<T>(Task task) where T : Exception
    {
        try { await task; } catch (T) { Check(true, "Expected failure"); return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static byte[] Pack(ushort count, ushort total, bool defaults = false, bool duplicate = false)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)(defaults ? 0x671c : 0x671b)); writer.Write(count); writer.Write(total);
            for (int i = 0; i < count; i++)
            {
                var name = Encoding.ASCII.GetBytes("P_" + (duplicate ? 0 : i).ToString("D4"));
                writer.Write((byte)(defaults ? 0x14 : 4)); writer.Write((byte)((name.Length - 1) << 4));
                writer.Write(name); writer.Write((float)(i + 0.25));
                if (defaults) writer.Write((float)(i - 0.5));
            }
            return stream.ToArray();
        }
    }
    private static void PackAndCacheChecks()
    {
        foreach (bool defaults in new[] { false, true })
        {
            var bytes = Pack(5, 5, defaults);
            var values = parampck.unpack(bytes);
            Check(values.Count == 5 && values.TotalReported == 5, "Packed total lost");
            Check(values["P_0003"].Value == 3.25, "Packed value changed");
            Check(values["P_0003"].default_value == (defaults ? (double?)2.5 : null), "Default value changed");
            for (int size = 0; size < bytes.Length; size++)
                Check(parampck.unpack(bytes.Take(size).ToArray()) == null, "Truncated pack accepted at " + size);
        }
        var partial = parampck.unpack(Pack(2, 5));
        Check(partial.Count == 2 && partial.TotalReported == 5, "Partial pack disguised as full table");
        Check(parampck.unpack(Pack(2, 2, duplicate: true)) == null, "Duplicate packed names accepted");
        Check(parampck.unpack(null) == null, "Null pack accepted");
        var badPrefix = Pack(2, 2); badPrefix[7] |= 15;
        Check(parampck.unpack(badPrefix) == null, "Invalid shared name prefix accepted");
        var good = parampck.unpack(Pack(3, 3, true));
        var cache = ParameterCacheSnapshot.Write(good);
        var loaded = ParameterCacheSnapshot.Read(cache);
        Check(loaded.Count == 3 && loaded.TotalReported == 3 && loaded["P_0002"].Value == 2.25, "Verified cache roundtrip failed");
        Check(ParameterCacheSnapshot.Read("[]") == null && ParameterCacheSnapshot.Read("bad json") == null, "Unverifiable legacy cache accepted");
        Check(ParameterCacheSnapshot.Read(cache.Replace("\"TotalReported\":3", "\"TotalReported\":4")) == null, "Partial cache accepted");
        bool rejected = false;
        try { ParameterCacheSnapshot.Write(partial); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Partial list saved as verified cache");
    }

    private sealed class Vehicle : IDisposable
    {
        internal readonly MAVLinkInterface Link = new MAVLinkInterface();
        internal readonly CommsInjection Stream = new CommsInjection();
        private readonly MAVLink.MavlinkParse parse = new MAVLink.MavlinkParse();
        internal Action<int> List;
        internal Action<short> Read;
        internal int ListRequests, MissingRequests;
        internal int OpenRequests;
        internal bool HoldFtpOpen;
        internal byte[] File;
        private readonly CancellationTokenSource pumpStop = new CancellationTokenSource();
        private Task pump;
        internal Vehicle()
        {
            Link.BaseStream = Stream; Link.sysidcurrent = 17; Link.compidcurrent = 1;
            Link.MAV.apname = MAVLink.MAV_AUTOPILOT.ARDUPILOTMEGA;
            MainV2.comPort = Link;
            Stream.WriteCallback += (sender, bytes) =>
            {
                MAVLink.MAVLinkMessage packet;
                using (var memory = new MemoryStream(bytes.ToArray())) packet = parse.ReadPacket(memory);
                Check(packet != null, "Bad outgoing packet");
                var id = (MAVLink.MAVLINK_MSG_ID)packet.msgid;
                if (id == MAVLink.MAVLINK_MSG_ID.PARAM_REQUEST_LIST)
                {
                    var request = packet.ToStructure<MAVLink.mavlink_param_request_list_t>();
                    Check(request.target_system == 17 && request.target_component == 1, "Wrong list target");
                    var requestNumber = Interlocked.Increment(ref ListRequests);
                    List?.Invoke(requestNumber);
                }
                else if (id == MAVLink.MAVLINK_MSG_ID.PARAM_REQUEST_READ)
                {
                    var request = packet.ToStructure<MAVLink.mavlink_param_request_read_t>();
                    Check(request.target_system == 17 && request.target_component == 1 && request.param_index >= 0, "Wrong missing-index request");
                    Interlocked.Increment(ref MissingRequests); Read?.Invoke(request.param_index);
                }
                else if (id == MAVLink.MAVLINK_MSG_ID.COMMAND_LONG)
                    Check(packet.ToStructure<MAVLink.mavlink_command_long_t>().command == (ushort)MAVLink.MAV_CMD.DO_SEND_BANNER, "Unexpected vehicle command");
                else if (id == MAVLink.MAVLINK_MSG_ID.FILE_TRANSFER_PROTOCOL)
                    Ftp(packet.ToStructure<MAVLink.mavlink_file_transfer_protocol_t>());
                else throw new Exception("Parameter download sent unexpected " + id);
            };
        }
        internal void Value(int index, int total, string name = null, byte system = 17, float? value = null, byte type = 9)
        {
            var id = new byte[16]; var text = Encoding.ASCII.GetBytes(name ?? "P_" + index.ToString("D4"));
            Array.Copy(text, id, Math.Min(id.Length, text.Length));
            Stream.AppendBuffer(parse.GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.PARAM_VALUE,
                new MAVLink.mavlink_param_value_t { param_id = id, param_index = (ushort)index, param_count = (ushort)total,
                    param_value = value ?? index + 0.25f, param_type = type }, false, system, 1));
        }
        internal void All(int total) { for (int i = 0; i < total; i++) Value(i, total); }
        internal void StartFtpPump(byte[] file)
        {
            File = file; Link.MAV.cs.capabilities |= (uint)MAVLink.MAV_PROTOCOL_CAPABILITY.FTP;
            pump = Task.Run(async () =>
            {
                while (!pumpStop.IsCancellationRequested)
                {
                    if (Stream.BytesToRead > 0) await Link.readPacketAsync();
                    else await Task.Delay(2);
                }
            });
        }
        private void Ftp(MAVLink.mavlink_file_transfer_protocol_t packet)
        {
            MAVFtp.FTPPayloadHeader request = packet.payload;
            if (request.opcode == MAVFtp.FTPOpcode.kCmdOpenFileRO)
            {
                Interlocked.Increment(ref OpenRequests);
                if (HoldFtpOpen) return;
            }
            byte[] data;
            if (request.opcode == MAVFtp.FTPOpcode.kCmdResetSessions) data = new byte[0];
            else if (request.opcode == MAVFtp.FTPOpcode.kCmdOpenFileRO) data = BitConverter.GetBytes(File.Length);
            else if (request.opcode == MAVFtp.FTPOpcode.kCmdBurstReadFile || request.opcode == MAVFtp.FTPOpcode.kCmdReadFile)
                data = File.Skip((int)request.offset).Take(request.size).ToArray();
            else throw new Exception("Unexpected FTP opcode");
            var response = new MAVFtp.FTPPayloadHeader { opcode = MAVFtp.FTPOpcode.kRspAck,
                req_opcode = request.opcode, seq_number = (ushort)(request.seq_number + 1),
                offset = request.offset, data = data, size = (byte)data.Length, burst_complete = 1 };
            Stream.AppendBuffer(parse.GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.FILE_TRANSFER_PROTOCOL,
                new MAVLink.mavlink_file_transfer_protocol_t { target_system = MAVLinkInterface.gcssysid, target_component = 190, payload = response }, false, 17, 1));
        }
        public void Dispose()
        {
            pumpStop.Cancel(); pump?.GetAwaiter().GetResult(); pumpStop.Dispose();
            var subscriptions = (IEnumerable)typeof(MAVLinkInterface).GetField("Subscriptions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Link);
            Check(!subscriptions.Cast<object>().Any(item =>
                new[] { MAVLink.MAVLINK_MSG_ID.PARAM_VALUE, MAVLink.MAVLINK_MSG_ID.STATUSTEXT, MAVLink.MAVLINK_MSG_ID.FILE_TRANSFER_PROTOCOL }
                .Contains((MAVLink.MAVLINK_MSG_ID)item.GetType().GetField("Item1").GetValue(item))), "Download leaked packet subscriptions");
            Check(!Link.IsParameterListLoading, "Download busy flag leaked");
            Link.Dispose();
        }
    }

    private static async Task StreamChecks()
    {
        using (var vehicle = new Vehicle())
        {
            Task producer = null;
            vehicle.List = n => producer = Task.Run(async () =>
            {
                for (int batch = 0; batch < 20; batch++)
                {
                    for (int item = 0; item < 25; item++) vehicle.Value(batch * 25 + item, 500);
                    await Task.Delay(80); // Slow producer/receiver scheduling during the first read.
                }
            });
            var result = await vehicle.Link.getParamListAsync(17, 1);
            await producer;
            Check(result.Count == 500 && vehicle.ListRequests == 1, "Slow first read lost parameters or restarted unnecessarily");
        }
        using (var vehicle = new Vehicle())
        {
            vehicle.List = n =>
            {
                vehicle.Value(0, 1, system: 18);
                vehicle.Value(65535, 1, "UNINDEXED");
                vehicle.Value(199, 200);
                for (int i = 0; i < 199; i++) if (i != 37 && i != 98) vehicle.Value(i, 200);
                vehicle.Value(5, 200); vehicle.Value(0, 0, "BAD_COUNT");
            };
            vehicle.Read = index => vehicle.Value(index, 200);
            var result = await vehicle.Link.getParamListAsync(17, 1);
            Check(result.Count == 200 && result.TotalReported == 200 && vehicle.MissingRequests >= 2, "Missing parameters not recovered");
            Check(!result.ContainsKey("UNINDEXED") && !result.ContainsKey("BAD_COUNT") && result["P_0098"].Value == 98.25, "Invalid packets affected completion/value");
            Check(vehicle.Link.LastParamListSucceeded, "Verified download not marked complete");
        }
        foreach (bool duplicate in new[] { false, true })
            using (var vehicle = new Vehicle())
            {
                vehicle.List = n =>
                {
                    if (n == 1) { vehicle.Value(0, 3, "SAME"); vehicle.Value(1, duplicate ? 3 : 4, "SAME"); }
                    else vehicle.All(4);
                };
                var result = await vehicle.Link.getParamListAsync(17, 1);
                Check(result.Count == 4 && vehicle.ListRequests == 2, "Changed/duplicate table did not restart cleanly");
            }
        using (var vehicle = new Vehicle())
        {
            vehicle.List = n => { vehicle.Value(0, 2, "SAME"); vehicle.Value(1, 2, "SAME"); };
            await Reject<InvalidDataException>(vehicle.Link.getParamListAsync(17, 1));
            Check(!vehicle.Link.LastParamListSucceeded && vehicle.ListRequests == 3, "Repeated malformed list reported success or retried forever");
        }
        using (var vehicle = new Vehicle())
        {
            vehicle.List = n => vehicle.Value(0, 5);
            var pending = Task.Run(async () => await vehicle.Link.getParamListAsync(17, 1));
            while (!vehicle.Link.IsParameterListLoading) await Task.Delay(5);
            await Reject<InvalidOperationException>(vehicle.Link.getParamListMavftpAsync(17, 1));
            vehicle.Stream.Close();
            await Reject<IOException>(pending);
            Check(!vehicle.Link.LastParamListSucceeded, "Interrupted download reported success");
        }
        using (var vehicle = new Vehicle())
        {
            vehicle.List = n => { vehicle.Value(0, 1, "INTEGER", value: -123, type: (byte)MAVLink.MAV_PARAM_TYPE.INT32); };
            var result = await vehicle.Link.getParamListAsync(17, 1);
            Check(result["INTEGER"].Value == -123 && result["INTEGER"].TypeAP == MAVLink.MAV_PARAM_TYPE.INT32 &&
                vehicle.Link.MAV.param_types["INTEGER"] == MAVLink.MAV_PARAM_TYPE.INT32, "AP integer type or value lost");
        }
    }
    private static async Task FtpChecks()
    {
        foreach (var file in new[] { Pack(5, 5, true), Pack(2, 5), Pack(5, 5).Take(20).ToArray(), Pack(2, 2, duplicate: true) })
            using (var vehicle = new Vehicle())
            {
                vehicle.List = n => vehicle.All(5);
                vehicle.Read = index => vehicle.Value(index, 5);
                vehicle.StartFtpPump(file);
                var result = await vehicle.Link.getParamListMavftpAsync(17, 1);
                Check(result.Count == 5 && result.TotalReported == 5 && vehicle.Link.LastParamListSucceeded, "FTP/fallback returned partial table");
                Check(vehicle.ListRequests == (file.Length == Pack(5, 5, true).Length ? 0 : 1), "Incomplete FTP did not fall back");
            }
    }
    private sealed class Reporter : IProgressReporterDialogue
    {
        public event DoWorkEventHandler DoWork;
        public ProgressWorkerEventArgs doWorkArgs { get; set; } = new ProgressWorkerEventArgs();
        public void Dispose() { }
        public void RunBackgroundOperationAsync() { DoWork?.Invoke(this); }
        public void UpdateProgressAndStatus(int progress, string status) { }
        public void BeginInvoke(Delegate method) { method.DynamicInvoke(); }
    }
    private static async Task CancellationChecks()
    {
        foreach (bool ftp in new[] { false, true })
            using (var vehicle = new Vehicle())
            {
                var reporter = new Reporter();
                typeof(MAVLinkInterface).GetField("frmProgressReporter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(vehicle.Link, reporter);
                if (ftp) { vehicle.HoldFtpOpen = true; vehicle.StartFtpPump(Pack(5, 5)); }
                var task = Task.Run(async () => ftp
                    ? await vehicle.Link.getParamListMavftpAsync(17, 1)
                    : await vehicle.Link.getParamListAsync(17, 1));
                var start = System.Diagnostics.Stopwatch.StartNew();
                while (ftp ? vehicle.OpenRequests == 0 : vehicle.ListRequests == 0)
                {
                    if (task.IsCompleted || start.ElapsedMilliseconds > 10000) throw new Exception("Cancellation fixture failed to start");
                    await Task.Delay(5);
                }
                reporter.doWorkArgs.CancelRequested = true;
                await Reject<OperationCanceledException>(task);
                Check(reporter.doWorkArgs.CancelAcknowledged && !vehicle.Link.LastParamListSucceeded, "Cancellation reported success");
            }
    }
    [STAThread]
    private static int Main()
    {
        try
        {
            Settings.CustomUserDataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "parameter-download-test-data");
            Settings.Instance["UseMavFtpParams"] = "True";
            PackAndCacheChecks();
            StreamChecks().GetAwaiter().GetResult();
            FtpChecks().GetAwaiter().GetResult();
            CancellationChecks().GetAwaiter().GetResult();
            Console.WriteLine("PASS: " + checks + " parameter download checks; malformed/partial FTP, missing/duplicate/reordered packets, restart, cache, disconnect, concurrency and type fidelity. No live vehicle.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
