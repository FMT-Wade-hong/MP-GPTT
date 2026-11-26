using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using MissionPlanner;
using MissionPlanner.ArduPilot.Mavlink;
using MissionPlanner.Comms;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

internal static class StabilityLifecycleHarness
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); Interlocked.Increment(ref checks); }
    private static void PumpUntil(Func<bool> completed, int milliseconds = 6000)
    {
        var watch = Stopwatch.StartNew();
        while (!completed())
        {
            if (watch.ElapsedMilliseconds > milliseconds) throw new TimeoutException("Offline lifecycle operation did not finish");
            Application.DoEvents(); Thread.Sleep(5);
        }
        Application.DoEvents();
    }
    private static void Wait(Task task) { PumpUntil(() => task.IsCompleted); if (task.IsFaulted) Console.Error.WriteLine(task.Exception); task.GetAwaiter().GetResult(); }
    private static void ConfigureSettings(string root)
    {
        Settings.CustomUserDataDirectory = root;
        Settings.AppConfigName = "FMTStability";
        Settings.FileName = "stability-test.xml";
    }
    private static void VerifyXml(string path)
    {
        using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            var xml = new XmlDocument(); xml.Load(input);
            Check(xml.DocumentElement.Name == "Config", "Incomplete settings document");
            Check(xml.SelectSingleNode("/Config/writer").InnerText == xml.SelectSingleNode("/Config/copy").InnerText,
                "Settings from different snapshots mixed");
        }
    }
    private static void WriteSettings(string writer, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Settings.Instance["writer"] = writer + ":" + i;
            Settings.Instance["copy"] = writer + ":" + i;
            Settings.Instance.Save();
        }
    }
    private static void SettingsChecks(string root)
    {
        ConfigureSettings(root);
        WriteSettings("initial", 1);
        var path = Path.Combine(Settings.GetUserDataDirectory(), Settings.FileName);
        var children = Enumerable.Range(0, 4).Select(i => Process.Start(new ProcessStartInfo
        {
            FileName = Assembly.GetExecutingAssembly().Location,
            Arguments = "--settings-worker \"" + root + "\" " + i,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true
        })).ToArray();
        try
        {
            var watch = Stopwatch.StartNew();
            do
            {
                // Exercise the public reader, which coordinates with concurrent writers.
                Settings.Instance.Load();
                Check(Settings.Instance["writer"] != null && Settings.Instance["writer"] == Settings.Instance["copy"],
                    "Concurrent settings load observed a partial snapshot");
                Thread.Sleep(5);
                if (watch.ElapsedMilliseconds > 15000) throw new TimeoutException("Settings writers stalled");
            } while (children.Any(p => !p.HasExited));
            foreach (var child in children)
                Check(child.ExitCode == 0, "Settings child failed: " + child.StandardError.ReadToEnd());
        }
        finally { foreach (var child in children) { if (!child.HasExited) child.Kill(); child.Dispose(); } }
        VerifyXml(path);
        Task save;
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            save = Task.Run(() => Settings.Instance.Save());
            Thread.Sleep(200);
            Check(!save.IsCompleted, "Sharing conflict did not retry");
        }
        Wait(save); VerifyXml(path);
        var auth = typeof(MainV2).Assembly.GetType("MissionPlanner.FMT.FmtAuthentication", true);
        var defaults = auth.GetMethod("EnsureDefaults", BindingFlags.Static | BindingFlags.NonPublic);
        defaults.Invoke(null, null);
        var preserved = File.ReadAllBytes(path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Existing defaults need no disk write, even if another application is reading exclusively.
            defaults.Invoke(null, null); Check(true, "Default initialization wrote unnecessarily");
            bool rejected = false;
            try { Settings.Instance.Save(); } catch (IOException) { rejected = true; }
            Check(rejected, "Persistent lock falsely reported a saved configuration");
        }
        Check(preserved.SequenceEqual(File.ReadAllBytes(path)), "Failed save damaged previous configuration");
        Check(!Directory.GetFiles(Settings.GetUserDataDirectory(), "stability-test.xml.*.tmp").Any(), "Save temporary files leaked");
        Console.WriteLine("PASS: four simultaneous processes, atomic XML snapshots, transient lock retry, locked-file preservation and no redundant startup save.");
    }

    private sealed class ResizeMap : OpenGLtest2
    {
        // Resize/context lifetime is under test; prevent terrain downloads and waypoint interaction.
        protected override void OnPaint(PaintEventArgs e) { }
    }
    private sealed class FailedContext : RealProxy
    {
        private readonly OpenTK.Graphics.IGraphicsContext original;
        internal int Failures;
        internal FailedContext(OpenTK.Graphics.IGraphicsContext original) : base(typeof(OpenTK.Graphics.IGraphicsContext))
        { this.original = original; }
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            if (call.MethodName == "get_IsCurrent") return new ReturnMessage(false, null, 0, call.LogicalCallContext, call);
            if (call.MethodName == "MakeCurrent" && call.Args[0] != null)
            {
                Failures++;
                return new ReturnMessage(new OpenTK.Graphics.GraphicsContextException("Injected invalid window/context"), call);
            }
            try { return new ReturnMessage(((MethodInfo)call.MethodBase).Invoke(original, call.Args), null, 0, call.LogicalCallContext, call); }
            catch (TargetInvocationException ex) { return new ReturnMessage(ex.InnerException, call); }
        }
    }
    private static void MapChecks(int cycles = 10)
    {
        GMap.NET.GMaps.Instance.Mode = GMap.NET.AccessMode.CacheOnly;
        var load = typeof(OpenGLtest2).GetMethod("test_Load", Private);
        var resize = typeof(OpenGLtest2).GetMethod("test_Resize", Private);
        var worker = typeof(OpenGLtest2).GetField("_imageloaderThread", Private);
        for (int i = 0; i < cycles; i++)
        {
            using (var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new Point(-30000, -30000), ClientSize = new Size(400, 300) })
            using (var map = new ResizeMap { Size = new Size(400, 300), minzoom = 0, zoom = 0 })
            {
                Console.WriteLine("GL lifecycle cycle " + i);
                int failures = 0; map.GraphicsUnavailable += (s, e) => failures++;
                // Pre-handle and zero-sized layouts must not acquire a GL context.
                map.Size = new Size(0, 0); resize.Invoke(map, new object[] { null, null });
                map.Size = new Size(400, 300); form.Controls.Add(map); form.Show(); Application.DoEvents();
                Check(!map.GraphicsFailed, "3D map initialization failed in supported GL environment");
                var thread = (Thread)worker.GetValue(map);
                Check(thread != null, "3D loader never started");
                for (int n = 0; n < 10; n++)
                {
                    map.Visible = false; map.Visible = true;
                    load.Invoke(map, new object[] { null, null });
                    map.Size = new Size(320 + n, n == 0 ? 0 : 240 + n);
                    resize.Invoke(map, new object[] { null, null });
                    Check(ReferenceEquals(thread, worker.GetValue(map)), "Repeated load created another GL worker");
                }
                map.Size = new Size(400, 300);
                Check(!map.GraphicsFailed, "Unexpected GL failure before fault injection in cycle " + i);
                var contextField = typeof(OpenTK.GLControl).GetField("_context", Private);
                var original = map.Context;
                var failedContext = new FailedContext(original);
                contextField.SetValue(map, failedContext.GetTransparentProxy());
                try
                {
                    // A resize deliberately skips a busy renderer. Wait for an idle attempt before asserting injection.
                    PumpUntil(() =>
                    {
                        resize.Invoke(map, new object[] { null, null });
                        return failedContext.Failures != 0;
                    });
                }
                finally { contextField.SetValue(map, original); }
                Check(failedContext.Failures == 1, "GL fault injection did not reach MakeCurrent");
                PumpUntil(() => failures == 1);
                Check(map.GraphicsFailed, "Lost context was not reported for 2D fallback");
                resize.Invoke(map, new object[] { null, null }); Application.DoEvents();
                Check(failures == 1, "Repeated context errors were not coalesced");
                map.Dispose();
                Check(!thread.IsAlive && !ReferenceEquals(OpenGLtest2.instance, map), "Disposed 3D control retained worker or static reference");
            }
        }
        Console.WriteLine("PASS: " + cycles + " real GL controls, " + (cycles * 10) + " visibility/resize/reload cycles, zero size, forced context loss and worker disposal.");
    }

    private static void MapFallbackChecks()
    {
        Settings.Instance["FMT_ShowAirspace"] = "False";
        using (var flight = new MissionPlanner.GCSViews.FlightData())
        {
            // No host/FlightData.Load: exercise the real selector and fallback without starting telemetry workers.
            var type = flight.GetType();
            var select = (CheckBox)type.GetField("chkFmt3DMap", Private).GetValue(flight);
            var field = type.GetField("fmt3DMapControl", Private);
            var panel = (SplitContainer)type.GetField("splitContainer1", Private).GetValue(flight);
            var fail = typeof(OpenGLtest2).GetMethod("ReportGraphicsFailure", Private);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                select.Checked = true;
                var old = (OpenGLtest2)field.GetValue(flight);
                Check(old != null && !old.IsDisposed && !panel.Panel1Collapsed, "3D selector did not open map panel");
                var notificationHandle = old.Handle;
                Check(notificationHandle != IntPtr.Zero, "Embedded GL control did not create a notification handle");
                fail.Invoke(old, new object[] { new OpenTK.Graphics.GraphicsContextException("Injected map fallback") });
                PumpUntil(() => !select.Checked);
                Check(!select.Checked && panel.Panel1Collapsed, "Lost 3D context did not return selector/panel to 2D");
                select.Checked = true;
                var replacement = (OpenGLtest2)field.GetValue(flight);
                Check(old.IsDisposed && !ReferenceEquals(old, replacement) && !replacement.GraphicsFailed,
                    "Reopening 3D reused failed control");
                select.Checked = false;
            }
        }
        Check(OpenGLtest2.instance == null, "FlightData disposal retained embedded map");
        Console.WriteLine("PASS: 5 actual FlightData 3D fallback/reopen cycles; failed controls disposed and replaced; offline notification integration.");
    }

    private sealed class FtpVehicle : IDisposable
    {
        internal readonly MAVLinkInterface Link = new MAVLinkInterface();
        internal readonly CommsInjection Stream = new CommsInjection();
        internal volatile bool Hold;
        internal int Requests;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Task pump;
        private readonly MAVLink.MavlinkParse parser = new MAVLink.MavlinkParse();
        internal FtpVehicle()
        {
            Link.BaseStream = Stream; Link.sysidcurrent = 17; Link.compidcurrent = 1; MainV2.comPort = Link;
            Stream.WriteCallback += (sender, bytes) =>
            {
                MAVLink.MAVLinkMessage message;
                using (var input = new MemoryStream(bytes.ToArray())) message = parser.ReadPacket(input);
                Check(message.msgid == (uint)MAVLink.MAVLINK_MSG_ID.FILE_TRANSFER_PROTOCOL, "Unexpected vehicle command");
                var packet = message.ToStructure<MAVLink.mavlink_file_transfer_protocol_t>();
                MAVFtp.FTPPayloadHeader request = packet.payload;
                Interlocked.Increment(ref Requests);
                if (Hold) return;
                Check(request.opcode == MAVFtp.FTPOpcode.kCmdListDirectory, "FTP test attempted a file mutation");
                var response = new MAVFtp.FTPPayloadHeader { opcode = MAVFtp.FTPOpcode.kRspNak,
                    req_opcode = request.opcode, seq_number = (ushort)(request.seq_number + 1),
                    offset = request.offset, size = 1, data = new byte[] { 6 } }; // End of directory.
                if (request.offset == 0 && System.Text.Encoding.UTF8.GetString(request.data).TrimEnd('\0') == "/")
                {
                    response.opcode = MAVFtp.FTPOpcode.kRspAck;
                    response.data = System.Text.Encoding.UTF8.GetBytes("D@SYS\0DLOGS\0");
                    response.size = (byte)response.data.Length;
                }
                Stream.AppendBuffer(parser.GenerateMAVLinkPacket20(MAVLink.MAVLINK_MSG_ID.FILE_TRANSFER_PROTOCOL,
                    new MAVLink.mavlink_file_transfer_protocol_t { target_system = MAVLinkInterface.gcssysid,
                        target_component = 190, payload = response }, false, 17, 1));
            };
            pump = Task.Run(async () => { while (!stop.IsCancellationRequested)
                { if (Stream.BytesToRead > 0) await Link.readPacketAsync(); else await Task.Delay(2); } });
        }
        public void Dispose() { stop.Cancel(); Stream.Close(); Wait(pump); Link.Dispose(); stop.Dispose(); }
    }
    private static void FtpChecks()
    {
        var type = typeof(MavFTPUI);
        var refresh = type.GetMethod("PopulateTreeViewAsync", Private);
        var navigate = type.GetMethod("ShowDirectoryAsync", Private);
        for (int cycle = 0; cycle < 10; cycle++)
        using (var vehicle = new FtpVehicle())
        using (var view = new MavFTPUI(vehicle.Link))
        using (var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-30000, -30000), ClientSize = new Size(700, 500) })
        {
            Console.WriteLine("FTP lifecycle cycle " + cycle);
            view.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), view, type.GetMethod("MavFTPUI_Load", Private));
            form.Controls.Add(view); form.Show(); Application.DoEvents();
            // This harness pumps messages manually instead of Application.Run.
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            var tree = (TreeView)type.GetField("treeView1", Private).GetValue(view);
            Console.WriteLine("FTP refresh start");
            Wait((Task)refresh.Invoke(view, null));
            Console.WriteLine("FTP refresh done; requests=" + vehicle.Requests);
            PumpUntil(() => vehicle.Requests >= 3);
            // Let the selected folder's async-void event finish before the next request.
            for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(5); }
            Check(tree.Nodes.Count == 2 && tree.Enabled, "Normal directory refresh did not restore UI");
            Check(tree.Nodes[0].Nodes.Cast<TreeNode>().Any(node => node.Text == "LOGS"), "Root directory lost normal folders");
            Check(!tree.Nodes[0].Nodes.Cast<TreeNode>().Any(node => node.Text == "@SYS") &&
                tree.Nodes.Cast<TreeNode>().Count(node => node.Text == "@SYS") == 1,
                "System folder appeared twice in the directory tree");
            vehicle.Hold = true;
            int before = vehicle.Requests;
            var pending = cycle % 2 == 0 ? (Task)refresh.Invoke(view, null)
                : (Task)navigate.Invoke(view, new object[] { tree.Nodes[0] });
            PumpUntil(() => vehicle.Requests > before);
            if (cycle % 2 == 0)
                Check(((Task)refresh.Invoke(view, null)).IsCompleted, "Duplicate folder refresh did not coalesce");
            view.Dispose(); Wait(pending);
            Console.WriteLine("FTP dispose done");
            Check(view.IsDisposed && !pending.IsFaulted, "FTP completion accessed a disposed view");
        }
        Console.WriteLine("PASS: 10 FTP views, normal folder listing, single @SYS entry, duplicate refresh and disposal during root/folder reads; no mutations.");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--settings-worker")
            { ConfigureSettings(args[1]); WriteSettings(args[2], 80); return 0; }
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Control.CheckForIllegalCrossThreadCalls = true;
            log4net.Config.BasicConfigurator.Configure();
            ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root.Level = log4net.Core.Level.Warn;
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stability-lifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            if (args.Length > 0 && args[0] == "--ftp-only") { ConfigureSettings(root); FtpChecks(); return 0; }
            if (args.Length > 0 && args[0] == "--gl-only")
            {
                ConfigureSettings(root);
                int cycles = args.Length > 1 ? int.Parse(args[1]) : 30;
                if (cycles < 1 || cycles > 100) throw new ArgumentOutOfRangeException("cycles");
                MapChecks(cycles); MapFallbackChecks();
                Console.WriteLine("PASS: " + checks + " graphics lifecycle assertions."); return 0;
            }
            SettingsChecks(root); MapChecks(); MapFallbackChecks(); FtpChecks();
            Console.WriteLine("PASS: " + checks + " stability lifecycle assertions. Local temporary settings and in-memory MAVLink only.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
