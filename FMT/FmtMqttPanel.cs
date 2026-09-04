using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtMqttPanel : UserControl
    {
        private readonly Dictionary<string, TextBox> fields = new Dictionary<string, TextBox>();
        private readonly CheckBox tls = new CheckBox { Text = "TLS/SSL", Checked = true, AutoSize = true };
        private readonly CheckBox remember = new CheckBox { Text = "安全記住密碼", AutoSize = true };
        private readonly Button start = new Button { Text = "啟動橋接", AutoSize = true };
        private readonly Button stop = new Button { Text = "停止橋接", AutoSize = true, Enabled = false };
        private readonly Button import = new Button { Text = "匯入 .fmt", AutoSize = true };
        private readonly Button export = new Button { Text = "匯出 .fmt", AutoSize = true };
        private readonly Label state = new Label { AutoSize = true, Text = "MQTT 已停止｜MP 未連線" };
        private readonly Label endpoint = new Label { AutoSize = true, Text = "先啟動橋接，再於 MP 選 TCP 連線。收合面板不會停止橋接。" };
        private readonly Label traffic = new Label { AutoSize = true, Text = "MQTT 接收 0 B｜送出 0 B（接收資料不代表飛控已就緒）" };
        private readonly TextBox log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Height = 66 };
        private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private readonly Timer refresh = new Timer { Interval = 300 };
        private FmtMqttBridge bridge;
        private bool stopping;
        internal bool IsBridgeRunning => !IsDisposed && bridge?.IsRunning == true;
        internal FmtMqttTrafficSnapshot TrafficSnapshot => IsBridgeRunning
            ? new FmtMqttTrafficSnapshot(bridge, bridge.ReceivedBytes, bridge.SentBytes)
            : default(FmtMqttTrafficSnapshot);

        internal FmtMqttPanel()
        {
            Name = "fmtEmbeddedMqtt";
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            AutoScroll = true;
            Font = new Font("Microsoft JhengHei UI", 9F);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(8), MinimumSize = new Size(560, 0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            AddRow(layout, "MQTT 主機", "Host", "MQTT Port", "Port");
            AddRow(layout, "Client ID", "ClientId", "TLS", null);
            AddRow(layout, "帳號", "Username", "密碼", "Password");
            AddRow(layout, "飛控 → MP", "InboundTopic", "MP → 飛控", "OutboundTopic");
            AddRow(layout, "TCP 監聽 IP", "TcpAddress", "TCP Port", "TcpPort");
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty };
            actions.Controls.AddRange(new Control[] { start, stop, import, export, remember,
                new Label { Text = ".fmt 不含密碼", AutoSize = true, Margin = new Padding(6, 8, 0, 0) } });
            AddFullRow(layout, actions);
            AddFullRow(layout, state);
            AddFullRow(layout, endpoint);
            AddFullRow(layout, traffic);
            AddFullRow(layout, log);
            Controls.Add(layout);
            start.Click += async (sender, args) => await StartAsync();
            stop.Click += async (sender, args) => await StopAsync();
            import.Click += (sender, args) => ImportSettings();
            export.Click += (sender, args) => ExportSettings();
            refresh.Tick += (sender, args) => RefreshStatus();
            // Timer polls bounded state; high-rate MAVLink never queues UI callbacks.
            refresh.Start();
            try
            {
                var settings = FmtMqttSettings.LoadRemembered();
                ApplySettings(settings);
                fields["Password"].Text = settings.LoadPassword();
                remember.Checked = fields["Password"].Text.Length > 0;
            }
            catch (Exception) { Enqueue("無法讀取本機設定，請重新輸入或匯入 .fmt。"); }
        }

        private void AddRow(TableLayoutPanel layout, string leftCaption, string leftKey, string rightCaption, string rightKey)
        {
            int row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(Caption(leftCaption), 0, row);
            layout.Controls.Add(Field(leftKey), 1, row);
            layout.Controls.Add(Caption(rightCaption), 2, row);
            layout.Controls.Add(rightKey == null ? (Control)tls : Field(rightKey), 3, row);
        }

        private static Label Caption(string text) => new Label
        {
            Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 4)
        };

        private TextBox Field(string key)
        {
            var box = new TextBox { Name = "mqtt" + key, Dock = DockStyle.Fill, UseSystemPasswordChar = key == "Password", Margin = new Padding(2, 2, 8, 2) };
            fields.Add(key, box);
            return box;
        }

        private static void AddFullRow(TableLayoutPanel layout, Control control)
        {
            int row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 4);
        }

        private void ApplySettings(FmtMqttSettings settings)
        {
            fields["Host"].Text = settings.Host;
            fields["Port"].Text = settings.Port == 0 ? "" : settings.Port.ToString();
            fields["ClientId"].Text = settings.ClientId;
            fields["Username"].Text = settings.Username;
            fields["InboundTopic"].Text = settings.InboundTopic;
            fields["OutboundTopic"].Text = settings.OutboundTopic;
            fields["TcpAddress"].Text = settings.TcpAddress;
            fields["TcpPort"].Text = settings.TcpPort == 0 ? "" : settings.TcpPort.ToString();
            tls.Checked = settings.UseTls;
        }

        private FmtMqttSettings ReadSettings()
        {
            int mqttPort, tcpPort;
            int.TryParse(fields["Port"].Text, out mqttPort);
            int.TryParse(fields["TcpPort"].Text, out tcpPort);
            var settings = new FmtMqttSettings
            {
                Host = fields["Host"].Text.Trim(), Port = mqttPort,
                ClientId = fields["ClientId"].Text.Trim(), Username = fields["Username"].Text.Trim(),
                InboundTopic = fields["InboundTopic"].Text.Trim(), OutboundTopic = fields["OutboundTopic"].Text.Trim(),
                TcpAddress = fields["TcpAddress"].Text.Trim(), TcpPort = tcpPort, UseTls = tls.Checked
            };
            settings.Validate();
            return settings;
        }

        private async Task StartAsync()
        {
            if (bridge != null || stopping) return;
            try
            {
                var settings = ReadSettings();
                var password = fields["Password"].Text;
                FmtMqttSettings.ValidateString(password, "MQTT 密碼");
                if (!settings.UseTls && MessageBox.Show(this,
                    "未使用 TLS：MQTT 帳密及飛行資料將以未加密方式傳送。確定僅在可信任的測試網路使用？",
                    "未加密連線", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                if (remember.Checked) settings.SaveRemembered(password);
                else FmtMqttSettings.ClearRemembered();
                bridge = new FmtMqttBridge();
                bridge.Log += Enqueue;
                bridge.Start(settings, password);
                var address = settings.TcpAddress == "0.0.0.0" ? "127.0.0.1" : settings.TcpAddress == "::" ? "::1" : settings.TcpAddress;
                endpoint.Text = "同機 MP：選 TCP → " + address + "，Port " + settings.TcpPort + "。收合面板不會停止橋接。";
                SetRunning(true);
                Enqueue("橋接已啟動。TCP 連線不等於飛控 MAVLink 已就緒。");
            }
            catch (Exception ex)
            {
                await StopAsync();
                if (!IsDisposed) MessageBox.Show(this, SafeMessage(ex), "無法啟動 MQTT 橋接", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task StopAsync()
        {
            if (stopping) return;
            stopping = true;
            stop.Enabled = false;
            var current = bridge;
            try
            {
                if (current != null)
                {
                    await current.StopAsync();
                    current.Log -= Enqueue;
                }
            }
            finally
            {
                bridge = null;
                stopping = false;
                if (!IsDisposed)
                {
                    SetRunning(false);
                    state.Text = "MQTT 已停止｜MP 未連線";
                    Enqueue("橋接已停止。");
                }
            }
        }

        private void SetRunning(bool running)
        {
            start.Enabled = !running;
            stop.Enabled = running;
            import.Enabled = export.Enabled = tls.Enabled = remember.Enabled = !running;
            foreach (var field in fields.Values) field.ReadOnly = running;
        }

        private void ImportSettings()
        {
            using (var dialog = new OpenFileDialog { Filter = "FMT 橋接設定 (*.fmt)|*.fmt" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var settings = FmtMqttSettings.Import(dialog.FileName);
                    ApplySettings(settings);
                    fields["Password"].Clear(); // Never carry a previous broker's password across imports.
                    remember.Checked = false;
                    Enqueue("設定已匯入至本次工作階段；.fmt 不包含密碼。若要下次載入，請勾選安全記住密碼。");
                }
                catch (Exception ex) { MessageBox.Show(this, SafeMessage(ex), "匯入失敗"); }
            }
        }

        private void ExportSettings()
        {
            try
            {
                var settings = ReadSettings();
                using (var dialog = new SaveFileDialog { Filter = "FMT 橋接設定 (*.fmt)|*.fmt", DefaultExt = "fmt", FileName = "mqtt-bridge.fmt" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    settings.Export(dialog.FileName);
                    Enqueue("設定已匯出（不含密碼）。");
                }
            }
            catch (Exception ex) { MessageBox.Show(this, SafeMessage(ex), "匯出失敗"); }
        }

        private string SafeMessage(Exception exception)
        {
            var password = fields["Password"].Text;
            return string.IsNullOrEmpty(password) ? exception.Message : exception.Message.Replace(password, "[已隱藏]");
        }

        private void Enqueue(string message)
        {
            if (IsDisposed) return;
            messages.Enqueue(DateTime.Now.ToString("HH:mm:ss") + " " + message);
            string discarded;
            while (messages.Count > 100) messages.TryDequeue(out discarded);
        }

        private void RefreshStatus()
        {
            if (bridge != null)
            {
                state.Text = bridge.MqttStatus + "｜" + bridge.TcpStatus;
                traffic.Text = "MQTT 接收 " + bridge.ReceivedBytes.ToString("N0") + " B｜送出 " + bridge.SentBytes.ToString("N0") + " B（接收量不代表飛控已就緒）";
            }
            string line;
            while (messages.TryDequeue(out line))
            {
                if (log.TextLength > 12000) log.Clear();
                log.AppendText(line + Environment.NewLine);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refresh.Stop(); refresh.Dispose();
                if (bridge != null) { bridge.Log -= Enqueue; bridge.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }
}
