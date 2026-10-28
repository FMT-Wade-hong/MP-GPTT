using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    // Each receiver keeps its own count, fix and DOP values. No telemetry writes.
    internal sealed class FmtGpsStatusControl : Panel
    {
        private readonly Label primary;
        private readonly Label primaryDop;
        private readonly Label secondary;
        private readonly Label secondaryDop;

        internal FmtGpsStatusControl(Image icon, bool chinese)
        {
            Name = "FmtGpsStatusPanel";
            BackColor = Color.FromArgb(24, 24, 24);
            Size = new Size(150, 35);
            Margin = Padding.Empty;
            Controls.Add(new PictureBox
            {
                Name = "FmtGpsIcon", Location = new Point(2, 5), Size = new Size(25, 25),
                Image = icon, SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent, TabStop = false
            });
            primary = CreateLabel("FmtGpsPrimaryLabel", 1, true);
            primaryDop = CreateLabel("FmtGpsDopLabel", 17, false);
            secondary = CreateLabel("FmtGpsSecondaryLabel", 1, true);
            secondaryDop = CreateLabel("FmtGpsSecondaryDopLabel", 17, false);
            UpdateStatus(false, chinese, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        private Label CreateLabel(string name, int top, bool bold)
        {
            var label = new Label
            {
                Name = name, AutoSize = false, Location = new Point(29, top),
                Size = new Size(119, bold ? 17 : 16), BackColor = Color.Transparent,
                ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(SystemFonts.MenuFont.FontFamily, bold ? 8.25f : 8f,
                    bold ? FontStyle.Bold : FontStyle.Regular)
            };
            Controls.Add(label);
            return label;
        }

        internal void UpdateStatus(bool connected, bool chinese,
            float fix, float satellites, float hdop, float vdop,
            float fix2, float satellites2, float hdop2, float vdop2)
        {
            // ArduPilot can send GPS2_RAW with fix_type=0 for a configured but
            // absent receiver. A No Fix (1) receiver is present and must show.
            var showSecond = connected && fix2 > 0;
            SuspendLayout();
            try
            {
                primary.Text = connected
                    ? FormatStatus(chinese, showSecond ? "1" : "", fix, satellites)
                    : chinese ? "衛星: --  未連線" : "Sats: --  Disconnected";
                primaryDop.Text = connected ? FormatDopPair(hdop, vdop) : "H: --  |  V: --";
                primary.ForeColor = primaryDop.ForeColor = connected
                    ? GetReceiverColor(fix, Color.LimeGreen) : Color.Gray;

                secondary.Visible = secondaryDop.Visible = showSecond;
                secondary.Text = showSecond ? FormatStatus(chinese, "2", fix2, satellites2) : "";
                secondaryDop.Text = showSecond ? FormatDopPair(hdop2, vdop2) : "";
                secondary.ForeColor = secondaryDop.ForeColor = GetReceiverColor(fix2, Color.Gold);

                var firstWidth = MeasureColumn(primary, primaryDop);
                primary.Width = primaryDop.Width = firstWidth;
                secondary.Left = secondaryDop.Left = primary.Right + 8;
                var secondWidth = showSecond ? MeasureColumn(secondary, secondaryDop) : 0;
                secondary.Width = secondaryDop.Width = secondWidth;
                Width = (showSecond ? secondary.Right : primary.Right) + 2;
            }
            finally { ResumeLayout(); }
        }

        private static int MeasureColumn(Label status, Label dop)
        {
            return Math.Max(119, Math.Max(TextRenderer.MeasureText(status.Text, status.Font).Width,
                TextRenderer.MeasureText(dop.Text, dop.Font).Width));
        }

        private static string FormatStatus(bool chinese, string receiver, float fix, float satellites)
        {
            var count = satellites >= 0 && satellites < byte.MaxValue && !float.IsNaN(satellites)
                ? satellites.ToString("0", CultureInfo.InvariantCulture) : "--";
            return (chinese ? "衛星" : "Sats") + receiver + ": " + count + "  " + MainV2.GetFmtGpsFixLabel(fix, chinese);
        }

        private static Color GetReceiverColor(float fix, Color normalColor)
        {
            // Preserve the original fix-status warnings. Receiver identity
            // colors apply only after a 3D fix; no satellite-count threshold.
            if (fix >= 3) return normalColor;
            if (fix >= 2) return Color.Gold;
            return Color.OrangeRed;
        }

        private static string FormatDopPair(float horizontal, float vertical) =>
            "H: " + FormatDop(horizontal) + "  |  V: " + FormatDop(vertical);

        private static string FormatDop(float value) => value > 0 && value < ushort.MaxValue / 100f &&
            !float.IsNaN(value) && !float.IsInfinity(value) ? value.ToString("0.0", CultureInfo.InvariantCulture) : "--";
    }
}
