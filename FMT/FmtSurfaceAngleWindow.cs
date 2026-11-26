using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner.FMT
{
    internal sealed class FmtSurfaceGauge : UserControl
    {
        private readonly int kind;
        private readonly ComboBox channel = new ComboBox();
        private string signature;
        private FmtSurfaceCalibration calibration;
        private int? pwm;
        private bool live;
        internal FmtSurfaceChannel Selected => channel.SelectedItem as FmtSurfaceChannel;
        internal FmtSurfaceGauge(int kind)
        {
            this.kind = kind;
            Name = "fmtSurfaceGauge" + kind;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(24, 35, 42);
            ForeColor = Color.White;
            Dock = DockStyle.Fill;
            Margin = new Padding(3);
            channel.Name = "fmtSurfaceChannel" + kind;
            channel.DropDownStyle = ComboBoxStyle.DropDownList;
            channel.FlatStyle = FlatStyle.Flat;
            channel.BackColor = Color.FromArgb(39, 54, 64);
            channel.ForeColor = Color.White;
            channel.Width = 88;
            channel.Top = 8;
            channel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(channel);
            channel.SelectedIndexChanged += (s, e) => { pwm = null; calibration = null; Invalidate(); };
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (channel != null) channel.Left = Math.Max(90, Width - channel.Width - 10);
        }
        internal void SetChannels(IEnumerable<FmtSurfaceChannel> channels)
        {
            var choices = channels.Where(c => c.Kind == kind).ToArray();
            string next = string.Join("|", choices.Select(c => c.Signature));
            if (signature == next) return;
            int selected = Selected?.Channel ?? -1;
            signature = next;
            channel.Items.Clear();
            channel.Items.AddRange(choices);
            channel.Enabled = choices.Length > 0;
            if (choices.Length > 0) channel.SelectedIndex = Math.Max(0, Array.FindIndex(choices, c => c.Channel == selected));
            Invalidate();
        }
        internal void SetReading(int? value, FmtSurfaceCalibration angles, bool connected)
        {
            pwm = value; calibration = angles; live = connected; Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var c = Selected;
            double? fraction = c != null && pwm.HasValue ? FmtSurfaceAngles.Fraction(c, pwm.Value) : null;
            bool calibrated = c != null && calibration != null && calibration.ValidFor(c);
            double? value = calibrated && pwm.HasValue ? FmtSurfaceAngles.Estimate(c, pwm.Value, calibration) : fraction * 100;
            double lo = calibrated ? Math.Min(0, Math.Min(calibration.AtMin, calibration.AtMax)) : c != null && c.Min == c.Trim ? 0 : -100;
            double hi = calibrated ? Math.Max(0, Math.Max(calibration.AtMin, calibration.AtMax)) : c != null && c.Max == c.Trim ? 0 : 100;
            if (hi <= lo) { lo = -100; hi = 100; }
            var accent = Color.FromArgb(41, 215, 239);
            float scale = Math.Max(.65f, Math.Min(1.35f, Math.Min(Width / 300f, Height / 220f)));
            using (var border = new Pen(Color.FromArgb(91, 113, 123)))
            using (var ticks = new Pen(Color.Gainsboro, 1.2f))
            using (var needle = new Pen(accent, 2.3f))
            using (var white = new SolidBrush(ForeColor))
            using (var blue = new SolidBrush(accent))
            using (var muted = new SolidBrush(Color.FromArgb(169, 187, 196)))
            using (var amber = new SolidBrush(Color.FromArgb(255, 194, 92)))
            using (var titleFont = new Font(Font.FontFamily, 12 * scale, FontStyle.Bold))
            using (var small = new Font(Font.FontFamily, 9 * scale))
            using (var number = new Font(Font.FontFamily, 22 * scale, FontStyle.Bold))
            using (var status = new Font(Font.FontFamily, 12 * scale))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
                g.DrawString(FmtSurfaceAngles.Names[kind], titleFont, white, 10, 10);
                float cx = Width / 2f, cy = Height * .67f;
                float radius = Math.Max(10, Math.Min(Width * .36f, (cy - 43) * .90f));
                g.DrawArc(ticks, cx - radius, cy - radius, radius * 2, radius * 2, 210, 120);
                for (int i = 0; i <= 10; i++)
                {
                    double t = i / 10.0;
                    double angle = (210 + 120 * t) * Math.PI / 180;
                    float length = i % 2 == 0 ? 9 : 5;
                    g.DrawLine(ticks, Point(cx, cy, radius, angle), Point(cx, cy, radius - length, angle));
                }
                foreach (double tick in TickValues(lo, hi))
                {
                    double angle = (210 + 120 * (tick - lo) / (hi - lo)) * Math.PI / 180;
                    var p = Point(cx, cy, radius + 16 * scale, angle);
                    g.DrawString(tick.ToString("0.#", CultureInfo.CurrentCulture), small, white,
                        new RectangleF(p.X - 25, p.Y - 10, 50, 20), center);
                }
                // The zero mark moves to the end for a one-sided flap travel.
                double zero = (210 + 120 * (0 - lo) / (hi - lo)) * Math.PI / 180;
                using (var zeroPen = new Pen(accent, 2))
                    g.DrawLine(zeroPen, Point(cx, cy, radius + 2, zero), Point(cx, cy, radius - 12, zero));
                if (live && value.HasValue)
                {
                    double angle = (210 + 120 * (value.Value - lo) / (hi - lo)) * Math.PI / 180;
                    g.DrawLine(needle, new PointF(cx, cy), Point(cx, cy, radius - 3, angle));
                    g.DrawEllipse(needle, cx - 3, cy - 3, 6, 6);
                }
                string display = !live ? "未連線" : c == null ? "未配置通道" : !c.Valid ? "PWM 設定無效" :
                    !pwm.HasValue ? "等待／資料逾時" : !fraction.HasValue ? "PWM 超出行程" :
                    value.Value.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture) + (calibrated ? "°" : "%");
                bool good = live && value.HasValue;
                g.DrawString(display, good ? number : status, good ? blue : amber,
                    new RectangleF(4, Height * .70f, Width - 8, Height * .18f), center);
                string detail = "PWM " + (live && pwm.HasValue ? pwm.Value.ToString() : "--") +
                    "  |  TRIM " + (c == null || c.Trim == 0 ? "--" : c.Trim.ToString());
                g.DrawString(detail, small, muted, new RectangleF(2, Height - 27 * scale, Width - 4, 22 * scale), center);
            }
        }
        private static PointF Point(float cx, float cy, float radius, double angle) =>
            new PointF(cx + radius * (float)Math.Cos(angle), cy + radius * (float)Math.Sin(angle));
        private static IEnumerable<double> TickValues(double lo, double hi)
        {
            double rough = (hi - lo) / 6;
            if (rough <= 0) return new[] { lo, 0, hi }; // Subnormal user input must not produce a zero-step loop.
            double power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            if (power <= 0) return new[] { lo, 0, hi };
            double normalized = rough / power;
            double step = (normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10) * power;
            var values = new SortedSet<double> { lo, 0, hi };
            for (double n = Math.Ceiling(lo / step) * step; n < hi; n += step)
                if (n - lo >= step * .6 && hi - n >= step * .6) values.Add(Math.Round(n, 8));
            return values;
        }
    }

    internal sealed class FmtSurfaceAngleWindow : Form
    {
        private readonly Timer timer = new Timer { Interval = 200 };
        private readonly FmtSurfaceTelemetry telemetry = new FmtSurfaceTelemetry();
        private readonly FmtSurfaceAngleStore store = new FmtSurfaceAngleStore();
        private readonly FmtSurfaceGauge[] gauges = new FmtSurfaceGauge[4];
        private readonly Font uiFont = new Font("Microsoft JhengHei UI", 9f);
        private readonly Button settings = MakeButton("角度設定", 105);
        private readonly Label note = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        private List<FmtSurfaceChannel> channels = new List<FmtSurfaceChannel>();
        private MAVState target;
        private string vehicle;
        private bool live;

        internal FmtSurfaceAngleWindow()
        {
            Name = "fmtSurfaceAngleWindow";
            Text = "舵角指示器";
            Font = uiFont;
            ClientSize = new Size(620, 470);
            MinimumSize = new Size(500, 420);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(18, 34, 43); ForeColor = Color.White;
            var grid = new TableLayoutPanel { Name = "fmtSurfaceGaugeGrid", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(3) };
            for (int i = 0; i < 2; i++) { grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); }
            for (int i = 0; i < 4; i++) { gauges[i] = new FmtSurfaceGauge(i); grid.Controls.Add(gauges[i], i % 2, i / 2); }
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(8, 3, 6, 3) };
            footer.Controls.Add(note); footer.Controls.Add(settings);
            Controls.Add(grid); Controls.Add(footer);
            settings.Name = "fmtSurfaceAngleSettings"; settings.Dock = DockStyle.Right;
            settings.Click += (s, e) => EditAngles();
            timer.Tick += (s, e) => RefreshFromVehicle();
            Shown += (s, e) => { RefreshFromVehicle(); timer.Start(); };
            VisibleChanged += (s, e) => { if (Visible) timer.Start(); else timer.Stop(); };
        }

        internal void RefreshFromVehicle()
        {
            if (IsDisposed) return;
            var link = MainV2.comPort;
            var stream = link?.BaseStream;
            live = stream != null && stream.IsOpen && !link.logreadmode;
            target = live ? link.MAV : null;
            string nextVehicle = FmtSurfaceAngles.VehicleKey(target);
            bool changed = telemetry.SetSource(live ? link : null, target, live ? stream : null);
            if (changed || vehicle != nextVehicle) store.ClearSession();
            vehicle = nextVehicle;
            channels = FmtSurfaceAngles.ReadChannels(target);
            foreach (var gauge in gauges)
            {
                gauge.SetChannels(channels);
                var channel = gauge.Selected;
                gauge.SetReading(channel == null ? null : telemetry.Read(channel.Channel, DateTime.UtcNow),
                    channel == null ? null : store.Get(vehicle, channel), live);
            }
            settings.Enabled = live && channels.Any(c => c.Valid);
            note.Text = "PWM 估算 · TRIM = 0°" + (channels.Any(c => store.Get(vehicle, c) == null) ? " · 未校正顯示 %" : "");
        }

        private void EditAngles()
        {
            RefreshFromVehicle();
            if (!live || channels.Count == 0) return;
            var originalTarget = target;
            string originalVehicle = vehicle;
            int generation = telemetry.Generation;
            var originalChannels = channels.ToList();
            string originalSignature = string.Join("|", channels.Select(c => c.Signature));
            using (var dialog = CreateSettingsDialog(originalChannels, c => store.Get(vehicle, c), values =>
            {
                RefreshFromVehicle();
                if (!live || !ReferenceEquals(target, originalTarget) || vehicle != originalVehicle ||
                    generation != telemetry.Generation || originalSignature != string.Join("|", channels.Select(c => c.Signature)))
                    throw new InvalidOperationException("連線或舵機設定已變更，請關閉後重新開啟角度設定。");
                store.Save(vehicle, originalChannels, values);
            }, vehicle != null)) dialog.ShowDialog(this);
            RefreshFromVehicle();
        }

        internal static Form CreateSettingsDialog(IList<FmtSurfaceChannel> channels,
            Func<FmtSurfaceChannel, FmtSurfaceCalibration> get, Action<IList<FmtSurfaceCalibration>> save, bool persistent)
        {
            var dialog = new Form { Text = "舵角指示器 — 角度設定", Name = "fmtSurfaceAngleCalibration",
                ClientSize = new Size(780, 380), MinimumSize = new Size(680, 330), StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false, BackColor = Color.FromArgb(18, 34, 43), ForeColor = Color.White };
            var dialogFont = new Font("Microsoft JhengHei UI", 9f);
            dialog.Font = dialogFont; dialog.Disposed += (s, e) => dialogFont.Dispose();
            var help = new Label { Dock = DockStyle.Top, Height = 65, Padding = new Padding(10, 8, 10, 0),
                Text = "輸入各通道在 MIN／MAX PWM 時的舵面角度，TRIM 固定為 0°。\r\n正負號依舵面方向填寫；兩欄留白顯示偏移 %。" +
                    (persistent ? "設定儲存於本機，依飛控識別。" : "未取得飛控識別，設定僅保留此視窗／連線。") };
            var grid = new DataGridView { Name = "fmtSurfaceCalibrationGrid", Dock = DockStyle.Fill, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AllowUserToOrderColumns = false, RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.FromArgb(24, 35, 42),
                BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 42,
                RowTemplate = { Height = 32 } };
            grid.DefaultCellStyle.BackColor = Color.FromArgb(39, 54, 64); grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 105, 133);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(18, 34, 43); grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            foreach (var column in new[] { "舵面 / CH", "MIN PWM", "TRIM PWM", "MAX PWM", "MIN 時角度 °", "MAX 時角度 °" })
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = column, SortMode = DataGridViewColumnSortMode.NotSortable, ReadOnly = grid.Columns.Count < 4 });
            grid.Columns[0].FillWeight = 140;
            foreach (var c in channels)
            {
                var angles = get(c);
                int row = grid.Rows.Add(FmtSurfaceAngles.Names[c.Kind] + " / CH" + c.Channel, c.Min, c.Trim, c.Max,
                    angles?.AtMin.ToString(CultureInfo.CurrentCulture), angles?.AtMax.ToString(CultureInfo.CurrentCulture));
                if (c.Min == c.Trim) { grid.Rows[row].Cells[4].Value = "0"; grid.Rows[row].Cells[4].ReadOnly = true; }
                if (c.Max == c.Trim) { grid.Rows[row].Cells[5].Value = "0"; grid.Rows[row].Cells[5].ReadOnly = true; }
                if (!c.Valid) { grid.Rows[row].ReadOnly = true; grid.Rows[row].ErrorText = "PWM 範圍缺漏或無效"; }
            }
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(8) };
            var error = new Label { Name = "fmtSurfaceCalibrationError", Dock = DockStyle.Top, Height = 26, ForeColor = Color.FromArgb(255, 194, 92) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30, FlowDirection = FlowDirection.RightToLeft };
            var cancel = MakeButton("取消", 85); cancel.DialogResult = DialogResult.Cancel;
            var ok = MakeButton("儲存", 85); ok.Name = "fmtSurfaceCalibrationSave";
            buttons.Controls.Add(cancel); buttons.Controls.Add(ok); bottom.Controls.Add(error); bottom.Controls.Add(buttons);
            dialog.Controls.Add(grid); dialog.Controls.Add(help); dialog.Controls.Add(bottom);
            dialog.CancelButton = cancel; dialog.AcceptButton = ok;
            ok.Click += (s, e) =>
            {
                try
                {
                    if (!grid.EndEdit()) return;
                    var values = new List<FmtSurfaceCalibration>();
                    for (int i = 0; i < channels.Count; i++)
                    {
                        var c = channels[i];
                        string a = Convert.ToString(grid.Rows[i].Cells[4].Value).Trim();
                        string b = Convert.ToString(grid.Rows[i].Cells[5].Value).Trim();
                        if (!c.Valid || ((a.Length == 0 || c.Min == c.Trim) && (b.Length == 0 || c.Max == c.Trim))) { values.Add(null); continue; }
                        double lo, hi;
                        if (!TryAngle(a, out lo) || !TryAngle(b, out hi)) throw new ArgumentException("CH" + c.Channel + "：兩個角度都需填入數值，或同時留白。");
                        var calibration = new FmtSurfaceCalibration { AtMin = lo, AtMax = hi };
                        if (!calibration.ValidFor(c)) throw new ArgumentException("CH" + c.Channel + "：角度需介於 −180° 至 180°，不可兩端皆為 0°；TRIM 端點必須為 0°。");
                        values.Add(calibration);
                    }
                    save(values);
                    dialog.DialogResult = DialogResult.OK; dialog.Close();
                }
                catch (Exception ex) { error.Text = ex.Message; }
            };
            return dialog;
        }

        private static bool TryAngle(string text, out double value) =>
            (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
             double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && FmtSurfaceCalibration.Finite(value);

        private static Button MakeButton(string text, int width) => new Button { Text = text, Width = width,
            Height = 27, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(38, 127, 158), ForeColor = Color.White };

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Stop(); timer.Dispose(); telemetry.Dispose(); }
            base.Dispose(disposing);
            if (disposing) uiFont.Dispose();
        }
    }
}
