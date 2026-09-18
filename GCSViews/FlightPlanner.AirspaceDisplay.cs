using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GMap.NET;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews
{
    public partial class FlightPlanner
    {
        private CheckBox fmtAirspaceVisible;
        private NumericUpDown fmtAirspaceRadius;
        private Timer fmtAirspaceDisplayTimer;
        private int fmtAirspaceRevision;
        private string fmtAirspaceLoadedKey;
        private string fmtAirspaceLoadingKey;

        private void ConfigureFmtAirspaceDisplay()
        {
            if (fmtAirspaceVisible != null) return;
            var y = TXT_homealt.Bottom + 8;
            fmtAirspaceVisible = new CheckBox
            {
                Name = "fmtAirspaceVisible", Text = "顯示限禁航區", AutoSize = true,
                Location = new Point(3, y),
                Checked = Settings.Instance.GetBoolean("fmt_airspace_visible", true)
            };
            var label = new Label
            {
                Text = "HOME 半徑（公里）", AutoSize = true,
                Location = new Point(3, y + fmtAirspaceVisible.PreferredSize.Height + 5)
            };
            fmtAirspaceRadius = new NumericUpDown
            {
                Name = "fmtAirspaceRadius", Minimum = 1, Maximum = 30,
                DecimalPlaces = 0, Increment = 1,
                Value = FMT.FmtAirspaceRadius.WholeKilometres(FMT.FmtAirspaceRadius.MetresToKilometres(Settings.Instance.GetInt32("fmt_airspace_radius_m", 5000))),
                Width = Math.Max(90, panel1.ClientSize.Width - 8),
                Location = new Point(3, label.Bottom + 4), ThousandsSeparator = true
            };
            panel1.Controls.Add(fmtAirspaceVisible);
            panel1.Controls.Add(label);
            panel1.Controls.Add(fmtAirspaceRadius);
            panel1.Height = fmtAirspaceRadius.Bottom + 8;
            flowLayoutPanel1.AutoScroll = true;
            toolTip1.SetToolTip(fmtAirspaceVisible, "僅在縮放 10～18 級顯示；只控制地圖顯示，不停用限禁航區航線檢查。");
            toolTip1.SetToolTip(fmtAirspaceRadius, "以起始位置 HOME 為中心，顯示與半徑相交的完整區域；不是裁切邊界。預設 5 公里，範圍 1～30 整數公里；既有設定四捨五入至整數公里。");
            fmtAirspaceDisplayTimer = new Timer(components) { Interval = 350 };
            fmtAirspaceDisplayTimer.Tick += async (sender, args) =>
            {
                fmtAirspaceDisplayTimer.Stop();
                if (!IsDisposed && !Disposing) await UpdateTaiwanCaaAirspace(PointLatLng.Empty);
            };
            fmtAirspaceVisible.CheckedChanged += FmtAirspaceDisplayChanged;
            fmtAirspaceRadius.ValueChanged += FmtAirspaceDisplayChanged;
            TXT_homelat.TextChanged += FmtAirspaceDisplayChanged;
            TXT_homelng.TextChanged += FmtAirspaceDisplayChanged;
            FmtAirspaceDisplayChanged(this, EventArgs.Empty);
        }

        private bool TryGetFmtAirspaceHome(out PointLatLng home)
        {
            double lat, lng;
            home = PointLatLng.Empty;
            if (!double.TryParse(TXT_homelat.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out lat) ||
                !double.TryParse(TXT_homelng.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out lng) ||
                double.IsNaN(lat) || double.IsNaN(lng) || Math.Abs(lat) > 90 || Math.Abs(lng) > 180 ||
                (lat == 0 && lng == 0)) return false;
            home = new PointLatLng(lat, lng);
            return true;
        }

        private void FmtAirspaceDisplayChanged(object sender, EventArgs args)
        {
            var wholeRadius = FMT.FmtAirspaceRadius.WholeKilometres(fmtAirspaceRadius.Value);
            if (fmtAirspaceRadius.Value != wholeRadius)
            {
                fmtAirspaceRadius.Value = wholeRadius;
                return;
            }
            fmtAirspaceRevision++;
            fmtAirspaceLoadedKey = null;
            fmtAirspaceLoadingKey = null;
            fmtAirspaceDisplayTimer.Stop();
            Settings.Instance["fmt_airspace_visible"] = fmtAirspaceVisible.Checked.ToString();
            Settings.Instance["fmt_airspace_radius_m"] = FMT.FmtAirspaceRadius.KilometresToMetres(fmtAirspaceRadius.Value).ToString(CultureInfo.InvariantCulture);
            ClearFmtAirspacePolygons();
            UpdateFmtAirspaceZoomVisibility();
        }

        private void UpdateFmtAirspaceZoomVisibility()
        {
            if (fmtAirspaceVisible == null) return;
            var visible = FMT.FmtAirspaceRadius.ShouldDisplay(fmtAirspaceVisible.Checked, MainMap.Zoom);
            taiwanCaaOverlay.IsVisibile = visible;
            fmtAirspaceDisplayTimer.Stop();
            if (visible && fmtAirspaceLoadedKey == null) fmtAirspaceDisplayTimer.Start();
            MainMap.Invalidate(false);
        }

        private void ClearFmtAirspacePolygons()
        {
            var polygons = taiwanCaaOverlay.Polygons.ToArray();
            taiwanCaaOverlay.Polygons.Clear();
            taiwanCaaZoneIds.Clear();
            foreach (var polygon in polygons)
            {
                polygon.Stroke.Dispose();
                polygon.Fill.Dispose();
                polygon.Dispose();
            }
        }
    }
}
