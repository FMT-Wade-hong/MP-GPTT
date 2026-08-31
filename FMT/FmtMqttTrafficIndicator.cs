using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtMqttTrafficIndicator : ToolStripControlHost
    {
        private readonly Func<FmtMqttTrafficSnapshot> getSnapshot;
        private readonly FmtMqttTrafficRate rate = new FmtMqttTrafficRate();
        private readonly Timer refresh;
        private readonly Label received;
        private readonly Label sent;
        private readonly Image icon;
        private readonly Font trafficFont;
        private bool disposed;

        internal FmtMqttTrafficIndicator(Func<FmtMqttTrafficSnapshot> getSnapshot)
            : base(new Panel { Name = "FmtMqttTrafficPanel", BackColor = Color.FromArgb(24, 24, 24), Margin = Padding.Empty })
        {
            this.getSnapshot = getSnapshot ?? throw new ArgumentNullException(nameof(getSnapshot));
            Name = "MenuFmtMqttTraffic";
            Alignment = ToolStripItemAlignment.Right;
            AutoSize = false;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            BackColor = Control.BackColor;
            ToolTipText = "MQTT 即時流量：上方接收、下方傳送（位元組／秒）。非累計流量，也不代表飛控已連線。";

            trafficFont = new Font(SystemFonts.MenuFont.FontFamily, 8.25f, FontStyle.Bold);
            // One compact fixed-width pair of rows; speed changes must not make adjacent items jump.
            int textWidth = TextRenderer.MeasureText("接收 1023.9 MiB/s", trafficFont).Width + 2;
            Size = new Size(46 + textWidth + 2, 35);
            Control.Size = Size;
            icon = FmtVisualAssets.LoadBitmap(FmtVisualAssets.MqttTrafficIcon);
            Control.Controls.Add(new PictureBox
            {
                Name = "FmtMqttTrafficIcon", Location = new Point(2, 3), Size = new Size(42, 28),
                Image = icon, BackColor = Color.Transparent, SizeMode = PictureBoxSizeMode.Zoom, TabStop = false
            });
            received = MakeLabel("FmtMqttReceivedRate", "接收 0 B/s", 1, textWidth, Color.LightSkyBlue);
            sent = MakeLabel("FmtMqttSentRate", "傳送 0 B/s", 17, textWidth, Color.Gainsboro);
            Control.Controls.Add(received);
            Control.Controls.Add(sent);
            Available = false;
            // Independent of flight telemetry and of the visibility of the MQTT/map panel.
            refresh = new Timer { Interval = 500 };
            refresh.Tick += (sender, args) => RefreshTraffic();
            refresh.Start();
        }

        private Label MakeLabel(string name, string text, int y, int width, Color color) => new Label
        {
            Name = name, Text = text, Location = new Point(46, y), Size = new Size(width, 16),
            Font = trafficFont, ForeColor = color, BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft, AutoSize = false
        };

        internal void RefreshTraffic()
        {
            if (disposed) return;
            UpdateTraffic(getSnapshot(), (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        }

        internal void UpdateTraffic(FmtMqttTrafficSnapshot snapshot, double seconds)
        {
            rate.Sample(snapshot, seconds);
            received.Text = "接收 " + FmtMqttTrafficRate.Format(rate.ReceivedPerSecond);
            sent.Text = "傳送 " + FmtMqttTrafficRate.Format(rate.SentPerSecond);
            if (Available != snapshot.IsRunning)
                Available = snapshot.IsRunning; // ToolStrip reflows siblings, leaving no empty slot.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                refresh?.Dispose();
                icon?.Dispose();
                trafficFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
