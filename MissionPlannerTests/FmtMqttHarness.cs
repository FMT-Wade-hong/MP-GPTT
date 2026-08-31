// Standalone loopback regression harness. Run FMT/Verify-FmtMqtt.ps1.
using MissionPlanner.FMT;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class FmtMqttHarness
{
    private static int passed;
    private static string artifactDirectory;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            artifactDirectory = args[0];
            Directory.CreateDirectory(artifactDirectory);
            TestSettings();
            TestPanel();
            TestTrafficRates();
            TestTrafficIndicator();
            TestMapOptionsLayout();
            TestCloseConfirmation();
            TestPanelConnectionLifetime();
            Task.Run(() => TestRoundTrip()).GetAwaiter().GetResult();
            Task.Run(() => TestRejections()).GetAwaiter().GetResult();
            TestPortBusy();
            Console.WriteLine("PASS: " + passed + " checks; loopback only, no production credentials or broker used.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Check(bool success, string label)
    {
        if (!success) throw new Exception("FAIL " + label);
        passed++; Console.WriteLine("PASS " + label);
    }

    private static FmtMqttSettings Settings(int brokerPort, int tcpPort) => new FmtMqttSettings
    {
        Host = "127.0.0.1", Port = brokerPort, ClientId = "regression-mp", Username = "regression-user",
        InboundTopic = "test/fc_to_mp", OutboundTopic = "test/mp_to_fc",
        TcpAddress = "127.0.0.1", TcpPort = tcpPort, UseTls = false
    };

    private static void TestSettings()
    {
        var settings = Settings(1883, 8080);
        settings.Validate();
        string path = Path.Combine(artifactDirectory, "roundtrip.fmt");
        settings.Export(path);
        var restored = FmtMqttSettings.Import(path);
        Check(restored.Host == settings.Host && restored.InboundTopic == settings.InboundTopic && !restored.UseTls, ".fmt round trip");
        Check(File.ReadLines(path).First() == "FMT-UAV-BRIDGE-SETTINGS/1", "standalone bridge format header");
        Check(File.ReadAllText(path).IndexOf("password", StringComparison.OrdinalIgnoreCase) < 0, "no password in exported file");
        Check(new FmtMqttSettings().UseTls && new FmtMqttSettings().Host == "", "safe blank defaults with TLS");
        settings.OutboundTopic = settings.InboundTopic;
        bool rejected = false;
        try { settings.Validate(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "identical topics rejected");
        settings.OutboundTopic = "test/#";
        rejected = false;
        try { settings.Validate(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "wildcard topics rejected");
    }

    private static void TestPanel()
    {
        Application.EnableVisualStyles();
        using (var host = new Form { ClientSize = new Size(960, 390), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
        using (var panel = new FmtMqttPanel())
        {
            host.Controls.Add(panel);
            host.Show();
            Application.DoEvents();
            host.PerformLayout(); panel.PerformLayout();
            var password = (TextBox)panel.Controls.Find("mqttPassword", true).Single();
            Check(password.UseSystemPasswordChar, "password masking");
            Check(panel.Dock == DockStyle.Fill && panel.AutoScroll, "embedded fill and small-window scrolling");
            panel.Visible = false; panel.Visible = true;
            Check(!panel.IsDisposed, "hide/show preserves control lifetime");
            using (var bitmap = new Bitmap(960, 390))
            {
                panel.DrawToBitmap(bitmap, new Rectangle(0, 0, 960, 390));
                bitmap.Save(Path.Combine(artifactDirectory, "mqtt-panel.png"));
            }
            host.ClientSize = new Size(620, 230);
            host.PerformLayout(); panel.PerformLayout();
            Check(panel.VerticalScroll.Visible, "short embedded area scrolls instead of clipping controls");
        }
    }

    private static void TestTrafficRates()
    {
        var rate = new FmtMqttTrafficRate();
        var session = new object();
        rate.Sample(new FmtMqttTrafficSnapshot(session, 5000, 2000), 10);
        Check(rate.ReceivedPerSecond == 0 && rate.SentPerSecond == 0, "first sample is not cumulative speed");
        rate.Sample(new FmtMqttTrafficSnapshot(session, 6024, 2512), 10.5);
        Check(rate.ReceivedPerSecond == 2048 && rate.SentPerSecond == 1024, "RX and TX use actual half-second interval");
        rate.Sample(new FmtMqttTrafficSnapshot(session, 8072, 3024), 12.5);
        Check(rate.ReceivedPerSecond == 1024 && rate.SentPerSecond == 256, "delayed UI tick uses real elapsed time");
        rate.Sample(new FmtMqttTrafficSnapshot(session, 8072, 3024), 13);
        Check(rate.ReceivedPerSecond == 0 && rate.SentPerSecond == 0, "idle or retry interval displays zero traffic");
        rate.Sample(new FmtMqttTrafficSnapshot(new object(), 500000, 200000), 14);
        Check(rate.ReceivedPerSecond == 0 && rate.SentPerSecond == 0, "restart discards prior bridge baseline even with larger counters");
        rate.Sample(default(FmtMqttTrafficSnapshot), 15);
        Check(rate.ReceivedPerSecond == 0 && rate.SentPerSecond == 0, "stopping resets both rates");
        rate.Sample(new FmtMqttTrafficSnapshot(session, 1000, 500), 16);
        rate.Sample(new FmtMqttTrafficSnapshot(session, 0, 0), 17);
        Check(rate.ReceivedPerSecond == 0 && rate.SentPerSecond == 0, "counter reset never shows negative rates");
        rate.Sample(new FmtMqttTrafficSnapshot(session, 100, 100), 17);
        Check(rate.ReceivedPerSecond == 0 && rate.SentPerSecond == 0, "same timestamp cannot divide by zero");
        Check(FmtMqttTrafficRate.Format(0) == "0 B/s" && FmtMqttTrafficRate.Format(512) == "512 B/s" &&
            FmtMqttTrafficRate.Format(1536) == "1.5 KiB/s" && FmtMqttTrafficRate.Format(1048576) == "1.0 MiB/s",
            "byte-per-second units scale without confusing totals or bits");
    }

    private static ToolStripControlHost TrafficSibling(string text, int width)
    {
        var panel = new Panel { Size = new Size(width, 35), BackColor = Color.FromArgb(24, 24, 24), Margin = Padding.Empty };
        panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, ForeColor = Color.LightSkyBlue, TextAlign = ContentAlignment.MiddleLeft });
        return new ToolStripControlHost(panel) { AutoSize = false, Size = panel.Size, Alignment = ToolStripItemAlignment.Right,
            Margin = Padding.Empty, Padding = Padding.Empty };
    }

    private static void TestTrafficIndicator()
    {
        var snapshot = default(FmtMqttTrafficSnapshot);
        using (var form = new Form { ClientSize = new Size(1100, 72), ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.None, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
        using (var indicator = new FmtMqttTrafficIndicator(() => snapshot))
        {
            var strip = new MenuStrip { AutoSize = false, Height = 50, BackColor = Color.FromArgb(24, 24, 24), Padding = Padding.Empty };
            strip.Items.Add(new ToolStripLabel("FMT Planner   飛行資料   初始設置") { ForeColor = Color.White });
            var gps = TrafficSibling("衛星：--\r\nH: -- | V: --", 150);
            var time = TrafficSibling("飛行時間：00:00:00\r\n飛控總飛時：-- 小時", 164);
            var rpm = TrafficSibling("主旋翼\r\nRPM1：1350", 112);
            strip.Items.Add(gps); strip.Items.Add(time); strip.Items.Add(indicator); strip.Items.Add(rpm);
            form.Controls.Add(strip);
            form.Show(); Application.DoEvents();
            var timer = (System.Windows.Forms.Timer)typeof(FmtMqttTrafficIndicator).GetField("refresh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(indicator);
            timer.Stop();
            Check(!indicator.Available && rpm.Bounds.Right == time.Bounds.Left, "stopped MQTT has no reserved toolbar gap");
            var session = new object();
            snapshot = new FmtMqttTrafficSnapshot(session, 0, 0);
            indicator.UpdateTraffic(snapshot, 1);
            snapshot = new FmtMqttTrafficSnapshot(session, 1024, 256);
            indicator.UpdateTraffic(snapshot, 1.5);
            Application.DoEvents();
            var received = (Label)indicator.Control.Controls.Find("FmtMqttReceivedRate", true).Single();
            var sent = (Label)indicator.Control.Controls.Find("FmtMqttSentRate", true).Single();
            var icon = (PictureBox)indicator.Control.Controls.Find("FmtMqttTrafficIcon", true).Single();
            Check(indicator.Available && received.Text == "接收 2.0 KiB/s" && sent.Text == "傳送 512 B/s" && received.Top < sent.Top,
                "running MQTT displays RX above TX with live rates");
            Check(icon.Image != null && icon.Image.Width == 452 && icon.Image.Height == 353 && icon.SizeMode == PictureBoxSizeMode.Zoom,
                "background-removed original MQTT icon embedded with tight aspect-preserving framing");
            var mqttBitmap = (Bitmap)icon.Image;
            int transparent = 0, opaque = 0, partial = 0;
            for (int y = 0; y < mqttBitmap.Height; y++)
                for (int x = 0; x < mqttBitmap.Width; x++)
                {
                    int alpha = mqttBitmap.GetPixel(x, y).A;
                    if (alpha == 0) transparent++;
                    else if (alpha == 255) opaque++;
                    else partial++;
                }
            Check(transparent > mqttBitmap.Width * mqttBitmap.Height / 3 && opaque > 1000 && partial > 0,
                "PNG contains true zero alpha, opaque foreground and antialiased edges, not a baked checkerboard");
            Check(mqttBitmap.GetPixel(0, 0).A == 0 && mqttBitmap.GetPixel(451, 352).A == 0 && mqttBitmap.GetPixel(204, 281).A == 0,
                "outer background and Q interior are transparent");
            Check(icon.BackColor == Color.Transparent, "toolbar picture box does not add an opaque rectangle");
            Check(indicator.Margin == Padding.Empty && indicator.Padding == Padding.Empty && indicator.Height == 35 && indicator.Width < 180,
                "compact MQTT toolbar matches existing height and has no outer spacing");
            Check(received.Right <= indicator.Control.ClientSize.Width && sent.Bottom <= indicator.Control.ClientSize.Height && icon.Right <= received.Left,
                "icon and two rate rows fit without overlap or clipping");
            foreach (int width in new[] { 1100, 960, 1400, 1000 })
            {
                form.ClientSize = new Size(width, 72);
                foreach (bool rotor in new[] { false, true })
                {
                    rpm.Available = rotor;
                    Application.DoEvents();
                    if (indicator.Bounds.Right != time.Bounds.Left || (rotor && rpm.Bounds.Right != indicator.Bounds.Left) ||
                        indicator.Placement != ToolStripItemPlacement.Main || (rotor && rpm.Placement != ToolStripItemPlacement.Main))
                        throw new Exception("MQTT/RPM/time order or gap failed at " + width + ", RPM=" + rotor);
                }
            }
            Check(true, "four widths and both helicopter states keep RPM -> MQTT -> time tightly adjacent");
            using (var bitmap = new Bitmap(form.ClientSize.Width, strip.Height))
            {
                strip.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(artifactDirectory, "mqtt-traffic-toolbar.png"));
            }
            snapshot = default(FmtMqttTrafficSnapshot);
            indicator.RefreshTraffic(); Application.DoEvents();
            Check(!indicator.Available && rpm.Bounds.Right == time.Bounds.Left, "stop hides MQTT and closes the slot beside RPM immediately");
            snapshot = new FmtMqttTrafficSnapshot(new object(), 900000, 500000);
            indicator.RefreshTraffic(); Application.DoEvents();
            Check(indicator.Available && received.Text == "接收 0 B/s" && sent.Text == "傳送 0 B/s", "restart begins at zero with no old traffic spike");
            snapshot = default(FmtMqttTrafficSnapshot);
            timer.Start();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 650) { Application.DoEvents(); Thread.Sleep(10); }
            Check(!indicator.Available, "timer polls stop state without MP telemetry or visible MQTT settings");
            var hostedControl = indicator.Control;
            indicator.Dispose();
            Check(!timer.Enabled && hostedControl.IsDisposed, "indicator disposal stops UI timer and disposes hosted controls");
        }
    }

    private static void TestMapOptionsLayout()
    {
        using (var form = new Form { ClientSize = new Size(1200, 600), AutoScaleMode = AutoScaleMode.None,
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, FmtMapOptionsLayout.RowHeight));
            var map = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Color.SteelBlue };
            var bar = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, AutoSize = true };
            var coordinates = new Label { Text = "GEO    23.845650 120.975952 0.0m", Size = new Size(247, 21), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            var choices = new[] { "航機調整", "自動平移地圖", "顯示限禁航區", "3D 地圖", "MQTT 連線" }
                .Select(text => new CheckBox { Text = text }).ToArray();
            choices[4].Checked = true;
            var contents = FmtMapOptionsLayout.Configure(bar, coordinates, choices);
            table.Controls.Add(new Label { Text = "Mission strip", Dock = DockStyle.Fill }, 0, 0);
            table.Controls.Add(map, 0, 1);
            table.Controls.Add(bar, 0, 2);
            form.Controls.Add(table);
            form.Show(); Application.DoEvents();
            Check(bar.Height == 40 && !bar.AutoSize && !contents.WrapContents, "map options fixed single-row height");
            Check(map.Height == 505, "unused option space returns to map");
            Check(ReferenceEquals(choices[4].Parent, contents) && choices[4].Checked, "checkbox instances and checked state preserved");
            for (int cycle = 0; cycle < 12; cycle++)
            {
                form.ClientSize = new Size(cycle % 2 == 0 ? 460 : 1200, cycle % 3 == 0 ? 450 : 600);
                Application.DoEvents();
                if (bar.Height != 40 || map.Height != form.ClientSize.Height - 95)
                    throw new Exception("Map option bar grew during resize cycle " + cycle);
            }
            Check(true, "12 narrow/wide and short/tall resizes cannot grow option row");
            form.ClientSize = new Size(460, 600); Application.DoEvents();
            Check(bar.HorizontalScroll.Visible && !bar.VerticalScroll.Visible, "narrow map uses horizontal scrolling only");
            Check(choices.All(choice => choice.Top == choices[0].Top), "all options remain on one line");
            bar.ScrollControlIntoView(choices[4]); Application.DoEvents();
            var lastRight = bar.PointToClient(choices[4].PointToScreen(new Point(choices[4].Width, 0))).X;
            Check(lastRight <= bar.ClientSize.Width && bar.Height == 40, "last MQTT option reachable without consuming map height");
            form.ClientSize = new Size(1200, 600); Application.DoEvents();
            Check(!bar.HorizontalScroll.Visible && bar.Height == 40, "wide window restores single-row view without growth");
            using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                table.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(artifactDirectory, "map-options-fixed.png"));
            }
        }
    }

    private static void TestCloseConfirmation()
    {
        var guard = new FmtConnectionCloseGuard();
        int prompts = 0;
        Func<string, DialogResult> no = message => { prompts++; return DialogResult.No; };
        Check(!guard.ShouldCancel(CloseReason.UserClosing, false, false, no) && prompts == 0,
            "disconnected close needs no confirmation");
        Check(guard.ShouldCancel(CloseReason.UserClosing, true, false, no), "telemetry connection cancels close on No");
        Check(!guard.ShouldCancel(CloseReason.UserClosing, true, false, message => DialogResult.Yes), "explicit Yes permits close");
        Check(guard.ShouldCancel(CloseReason.UserClosing, false, true, no), "MQTT-only bridge also protects close");
        Check(guard.ShouldCancel(CloseReason.UserClosing, true, true, message => DialogResult.Cancel), "non-Yes dialog result keeps connections");
        Check(guard.ShouldCancel(CloseReason.ApplicationExitCall, true, false, no), "application exit also checks connections");
        int beforeShutdown = prompts;
        Check(!guard.ShouldCancel(CloseReason.WindowsShutDown, true, true, no) && prompts == beforeShutdown,
            "Windows shutdown is not blocked by a dialog");
        bool nestedCancelled = false;
        guard.ShouldCancel(CloseReason.UserClosing, true, false, message =>
        {
            nestedCancelled = guard.ShouldCancel(CloseReason.UserClosing, true, false,
                nested => { throw new Exception("Duplicate close dialog"); });
            return DialogResult.No;
        });
        Check(nestedCancelled, "reentrant close is cancelled without another dialog");

        using (var form = new CloseProbeForm())
        {
            form.Show();
            form.Close();
            Check(!form.IsDisposed && form.CleanupCount == 0, "FormClosing No leaves window and cleanup untouched");
            form.Close();
            Check(form.Prompts == 2 && form.CleanupCount == 0, "cancelled close can be attempted again");
            form.Response = DialogResult.Yes;
            form.Close();
            Check(form.IsDisposed && form.CleanupCount == 1, "FormClosing Yes executes cleanup exactly once");
        }
    }

    private sealed class CloseProbeForm : Form
    {
        private readonly FmtConnectionCloseGuard guard = new FmtConnectionCloseGuard();
        internal DialogResult Response = DialogResult.No;
        internal int CleanupCount, Prompts;
        internal CloseProbeForm()
        {
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-20000, -20000);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            if (guard.ShouldCancel(e.CloseReason, true, false, message => { Prompts++; return Response; }))
            {
                e.Cancel = true;
                return;
            }
            CleanupCount++;
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop(); return port;
    }

    private static void TestPanelConnectionLifetime()
    {
        var broker = new TcpListener(IPAddress.Loopback, 0);
        broker.Start();
        using (var panel = new FmtMqttPanel())
        using (var bridge = new FmtMqttBridge())
        {
            // Attach a loopback session without UI StartAsync: tests must never
            // overwrite the user's saved settings or invoke credentials dialogs.
            typeof(FmtMqttPanel).GetField("bridge", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, bridge);
            var settings = Settings(((IPEndPoint)broker.LocalEndpoint).Port, FreePort());
            var connected = Task.Run(async () =>
            {
                var client = await Within(broker.AcceptTcpClientAsync());
                var stream = client.GetStream();
                await ReadPacket(stream);
                await stream.WriteAsync(new byte[] { 0x20, 2, 0, 0 }, 0, 4);
                await ReadPacket(stream);
                await stream.WriteAsync(new byte[] { 0x90, 3, 0, 1, 0 }, 0, 5);
                await WaitFor(() => bridge.MqttStatus.Contains("已訂閱"));
                return client;
            });
            bridge.Start(settings, "synthetic-test-password");
            Check(panel.IsBridgeRunning, "pending MQTT connection is protected before CONNACK");
            using (var client = connected.GetAwaiter().GetResult())
            {
                panel.Visible = false;
                Check(bridge.MqttStatus.Contains("已訂閱"), "hiding panel preserves established MQTT session");
                Check(panel.TrafficSnapshot.IsRunning && ReferenceEquals(panel.TrafficSnapshot.Session, bridge) &&
                    panel.TrafficSnapshot.ReceivedBytes == bridge.ReceivedBytes && panel.TrafficSnapshot.SentBytes == bridge.SentBytes,
                    "hidden panel exposes current bridge session and interlocked counters to toolbar");
                Check(panel.IsBridgeRunning && new FmtConnectionCloseGuard().ShouldCancel(CloseReason.UserClosing,
                    false, panel.IsBridgeRunning, message => DialogResult.No), "hidden live bridge blocks accidental close");
                panel.Visible = true;
                Check(bridge.MqttStatus.Contains("已訂閱"), "showing panel reuses established MQTT session");
                panel.Dispose();
                Task.Run(() => Within(bridge.StopAsync())).GetAwaiter().GetResult();
                Check(bridge.MqttStatus == "MQTT 已停止", "disposing panel stops bridge");
                Check(!bridge.IsRunning && !panel.IsBridgeRunning, "stopped bridge no longer requires confirmation");
                Check(!panel.TrafficSnapshot.IsRunning, "disposed MQTT panel clears toolbar snapshot");
            }
        }
        broker.Stop();
    }

    private static async Task TestRoundTrip()
    {
        var broker = new TcpListener(IPAddress.Loopback, 0);
        broker.Start();
        var settings = Settings(((IPEndPoint)broker.LocalEndpoint).Port, FreePort());
        using (var bridge = new FmtMqttBridge())
        using (var mp = new TcpClient())
        {
            bridge.Start(settings, "synthetic-test-password");
            using (var remote = await Within(broker.AcceptTcpClientAsync()))
            {
                var stream = remote.GetStream();
                var connect = await ReadPacket(stream);
                Check(connect.Item1 == 0x10 && connect.Item2[7] == 0xC2, "MQTT 3.1.1 clean-session login");
                await stream.WriteAsync(new byte[] { 0x20, 2, 0, 0 }, 0, 4);
                var subscribe = await ReadPacket(stream);
                Check(subscribe.Item1 == 0x82 && Encoding.UTF8.GetString(subscribe.Item2, 4, subscribe.Item2.Length - 5) == settings.InboundTopic, "correct inbound subscription");
                Check(!bridge.MqttStatus.Contains("已訂閱"), "not ready until SUBACK");
                await stream.WriteAsync(new byte[] { 0x90, 3, 0, 1, 0 }, 0, 5);
                await WaitFor(() => bridge.MqttStatus.Contains("已訂閱"));
                await mp.ConnectAsync(IPAddress.Loopback, settings.TcpPort);
                await WaitFor(() => bridge.TcpStatus.Contains("已連線"));
                byte[] data = { 0xFD, 0, 1, 2, 255, 0, 0xFE, 128 };
                var packet = Publish(settings.InboundTopic, data, false);
                // Exercise fragmented TCP delivery.
                foreach (byte value in packet) await stream.WriteAsync(new[] { value }, 0, 1);
                var got = await ReadExact(mp.GetStream(), data.Length);
                Check(got.SequenceEqual(data), "MQTT to MP preserves binary bytes");
                await mp.GetStream().WriteAsync(data, 0, data.Length);
                var outgoing = await ReadPacket(stream);
                int length = (outgoing.Item2[0] << 8) | outgoing.Item2[1];
                Check(outgoing.Item1 == 0x30 && Encoding.UTF8.GetString(outgoing.Item2, 2, length) == settings.OutboundTopic, "outbound topic, QoS 0 and no retain");
                Check(outgoing.Item2.Skip(2 + length).SequenceEqual(data), "MP to MQTT preserves binary bytes");
                await WaitFor(() => bridge.SentBytes == data.Length);
                Check(bridge.ReceivedBytes == data.Length, "receive/send byte counters");
                var retained = Publish(settings.InboundTopic, new byte[] { 42 }, true);
                await stream.WriteAsync(retained, 0, retained.Length);
                // A following live message proves the retained one was processed and ignored.
                await stream.WriteAsync(packet, 0, packet.Length);
                got = await ReadExact(mp.GetStream(), data.Length);
                Check(got.SequenceEqual(data), "retained telemetry not replayed into live MP");
                remote.Close();
                await WaitFor(() => bridge.MqttStatus.Contains("未連線"));
            }
            using (var reconnected = await Within(broker.AcceptTcpClientAsync()))
            {
                await ReadPacket(reconnected.GetStream()); // CONNECT is pending CONNACK.
                Check(true, "broker disconnect triggers reconnect");
                await Within(bridge.StopAsync());
                Check(bridge.MqttStatus == "MQTT 已停止", "stop cancels stalled CONNACK read");
            }
            // Disposing twice is allowed; the local TCP port must be reusable.
            bridge.Dispose();
            var reused = new TcpListener(IPAddress.Loopback, settings.TcpPort);
            reused.Start(); reused.Stop();
            Check(true, "stop releases local TCP listener");
        }
        broker.Stop();
    }

    private static async Task TestRejections()
    {
        foreach (bool rejectSubscribe in new[] { false, true })
        {
            var broker = new TcpListener(IPAddress.Loopback, 0);
            broker.Start();
            using (var bridge = new FmtMqttBridge())
            {
                var settings = Settings(((IPEndPoint)broker.LocalEndpoint).Port, FreePort());
                bridge.Start(settings, "synthetic-test-password");
                using (var client = await Within(broker.AcceptTcpClientAsync()))
                {
                    var stream = client.GetStream();
                    await ReadPacket(stream);
                    var ack = new byte[] { 0x20, 2, 0, (byte)(rejectSubscribe ? 0 : 5) };
                    await stream.WriteAsync(ack, 0, ack.Length);
                    if (rejectSubscribe)
                    {
                        await ReadPacket(stream);
                        await stream.WriteAsync(new byte[] { 0x90, 3, 0, 1, 0x80 }, 0, 5);
                    }
                    await WaitFor(() => bridge.MqttStatus.Contains("未連線"));
                    Check(true, rejectSubscribe ? "SUBACK rejection never reports connected" : "bad credentials never report connected");
                    await Within(bridge.StopAsync());
                }
            }
            broker.Stop();
        }
    }

    private static void TestPortBusy()
    {
        var busy = new TcpListener(IPAddress.Loopback, 0);
        busy.Start();
        using (var bridge = new FmtMqttBridge())
        {
            bool rejected = false;
            try { bridge.Start(Settings(FreePort(), ((IPEndPoint)busy.LocalEndpoint).Port), "synthetic-test-password"); }
            catch (SocketException) { rejected = true; }
            Check(rejected, "busy TCP port fails before MQTT starts");
        }
        busy.Stop();
    }

    private static byte[] Publish(string topic, byte[] data, bool retained)
    {
        byte[] name = Encoding.UTF8.GetBytes(topic);
        return new[] { (byte)(retained ? 0x31 : 0x30), (byte)(2 + name.Length + data.Length), (byte)(name.Length >> 8), (byte)name.Length }
            .Concat(name).Concat(data).ToArray();
    }

    private static async Task<Tuple<int, byte[]>> ReadPacket(Stream stream)
    {
        int header = (await ReadExact(stream, 1))[0], remaining = 0, multiplier = 1, digit;
        do { digit = (await ReadExact(stream, 1))[0]; remaining += (digit & 127) * multiplier; multiplier *= 128; } while ((digit & 128) != 0);
        return Tuple.Create(header, await ReadExact(stream, remaining));
    }

    private static async Task<byte[]> ReadExact(Stream stream, int length)
    {
        var data = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int count = await Within(stream.ReadAsync(data, offset, length - offset));
            if (count == 0) throw new EndOfStreamException();
            offset += count;
        }
        return data;
    }

    private static async Task WaitFor(Func<bool> test)
    {
        var until = DateTime.UtcNow.AddSeconds(6);
        while (!test())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("Expected state did not arrive.");
            await Task.Delay(20);
        }
    }

    private static async Task<T> Within<T>(Task<T> task)
    {
        if (await Task.WhenAny(task, Task.Delay(6000)) != task) throw new TimeoutException();
        return await task;
    }

    private static async Task Within(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(6000)) != task) throw new TimeoutException();
        await task;
    }
}
