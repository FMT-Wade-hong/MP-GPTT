using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Comms;
using MissionPlanner.Controls;
using MissionPlanner.Radio;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public sealed class ConfigSiKRadio : UserControl, IActivate, IDeactivate
    {
        private readonly Sikradio radio = new Sikradio(true);
        private readonly ComboBox ports = new ComboBox { Name = "sikPort", DropDownStyle = ComboBoxStyle.DropDownList, Width = 125 };
        private readonly ComboBox baud = new ComboBox { Name = "sikBaud", DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        private readonly Button refresh = new Button { Text = "重新整理", AutoSize = true };
        private readonly Button release = new Button { Text = "釋放序列埠", AutoSize = true, Enabled = false };
        private readonly Label status = new Label { AutoSize = true, Text = "尚未開啟序列埠；選擇 COM／鮑率後按「讀取設定」。" };
        private ICommsSerial ownedPort;

        public ConfigSiKRadio()
        {
            Name = "ConfigSiKRadio";
            Dock = DockStyle.Fill;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill,
                Text = "SIK 數傳設定｜本機與遠端參數\r\n請先中斷 MP 飛行連線；僅於地面安全狀態設定。頻段、功率及配對參數須符合設備與所在地規定。" }, 0, 0);
            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            toolbar.Controls.Add(new Label { Text = "序列埠", AutoSize = true, Margin = new Padding(0, 7, 4, 0) });
            toolbar.Controls.Add(ports);
            toolbar.Controls.Add(new Label { Text = "鮑率", AutoSize = true, Margin = new Padding(8, 7, 4, 0) });
            toolbar.Controls.Add(baud);
            toolbar.Controls.Add(refresh);
            toolbar.Controls.Add(release);
            layout.Controls.Add(toolbar, 0, 1);
            layout.Controls.Add(status, 0, 2);
            var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Name = "sikSettingsViewport" };
            // Keep both modem columns and their parameter controls intact. The
            // Chinese layout reserves caption space; smaller displays can scroll.
            radio.Dock = DockStyle.None;
            radio.Location = Point.Empty;
            viewport.Controls.Add(radio);
            layout.Controls.Add(viewport, 0, 3);
            Controls.Add(layout);
            baud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200", "230400" });
            baud.SelectedItem = "57600";
            radio.EmbeddedPortProvider = AcquirePort;
            radio.EmbeddedPortRelease = ReleasePort;
            radio.EmbeddedOperationChanged += () =>
            {
                if (!IsDisposed) release.Enabled = ownedPort != null && !radio.EmbeddedOperationActive;
            };
            radio.ClearEmbeddedSettings();
            refresh.Click += (sender, args) => RefreshPorts();
            release.Click += (sender, args) => DisconnectRadio();
            RefreshPorts();
        }

        public void Activate() { if (ownedPort == null) RefreshPorts(); }
        public void Deactivate() { DisconnectRadio(); }

        private void RefreshPorts()
        {
            if (ownedPort != null) return;
            var selected = ports.Text;
            ports.Items.Clear();
            ports.Items.AddRange(SerialPort.GetPortNames().OrderBy(p => p).Cast<object>().ToArray());
            if (ports.Items.Contains(selected)) ports.SelectedItem = selected;
            // No automatic port selection and no automatic connection.
        }

        private ICommsSerial AcquirePort()
        {
            if (HasActiveTelemetry())
                throw new InvalidOperationException("MP 尚有連線，請先按「斷線」再設定數傳；本頁不會自動中斷飛行連線。");
            if (ownedPort != null) return ownedPort;
            if (ports.SelectedItem == null)
                throw new InvalidOperationException("請先選擇數傳的 COM 序列埠。");
            var port = new SerialPort { PortName = ports.Text, BaudRate = int.Parse(baud.Text), ReadTimeout = 4000, WriteTimeout = 4000 };
            try { port.Open(); }
            catch { port.Dispose(); throw; }
            ownedPort = port;
            ports.Enabled = baud.Enabled = refresh.Enabled = false;
            release.Enabled = !radio.EmbeddedOperationActive;
            status.Text = "正在使用 " + port.PortName + " / " + port.BaudRate + " bps；返回飛行連線前請先釋放序列埠。";
            return ownedPort;
        }

        private static bool HasActiveTelemetry()
        {
            if (MainV2.comPort?.BaseStream?.IsOpen == true) return true;
            return MainV2.Comports != null && MainV2.Comports.ToArray().Any(p => p?.BaseStream?.IsOpen == true);
        }

        private void DisconnectRadio()
        {
            try { radio.RequestEmbeddedDisconnect(); }
            catch { ReleasePort(); }
        }

        private void ReleasePort()
        {
            try { ownedPort?.Dispose(); }
            finally
            {
                ownedPort = null;
                radio.ClearEmbeddedSettings();
                if (!IsDisposed)
                {
                    ports.Enabled = baud.Enabled = refresh.Enabled = true;
                    release.Enabled = false;
                    status.Text = "序列埠已釋放；可返回 MP 進行飛行連線。";
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisconnectRadio();
            base.Dispose(disposing);
        }
    }
}
