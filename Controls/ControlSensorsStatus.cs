using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace MissionPlanner.Controls
{
    public partial class ControlSensorsStatus : UserControl
    {
        private static readonly IReadOnlyDictionary<string, string> TraditionalChineseSensorNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {"3D GYRO", "主陀螺儀"}, {"3D ACCEL", "主加速度計"},
                {"3D MAG", "主磁羅盤"}, {"ABSOLUTE PRESSURE", "絕對氣壓計"},
                {"DIFFERENTIAL PRESSURE", "差壓計"}, {"GPS", "衛星定位"},
                {"OPTICAL FLOW", "光流感測器"}, {"VISION POSITION", "視覺定位"},
                {"LASER POSITION", "雷射定位"}, {"EXTERNAL GROUND TRUTH", "外部定位基準"},
                {"ANGULAR RATE CONTROL", "角速度控制"}, {"ATTITUDE STABILIZATION", "姿態穩定"},
                {"YAW POSITION", "偏航控制"}, {"Z ALTITUDE CONTROL", "高度控制"},
                {"XY POSITION CONTROL", "水平位置控制"}, {"MOTOR OUTPUTS", "馬達輸出"},
                {"RC RECEIVER", "遙控接收器"}, {"3D GYRO2", "第二陀螺儀"},
                {"3D ACCEL2", "第二加速度計"}, {"3D MAG2", "第二磁羅盤"},
                {"GEOFENCE", "地理圍籬"}, {"AHRS", "姿態航向系統"},
                {"TERRAIN", "地形資料"}, {"REVERSE MOTOR", "馬達反轉"},
                {"LOGGING", "飛行記錄"}, {"BATTERY", "電池監測"},
                {"PROXIMITY", "近接感測器"}, {"SATCOM", "衛星通訊"},
                {"PREARM CHECK", "解鎖前檢查"},
                {"OBSTACLE AVOIDANCE", "障礙物迴避"}, {"PROPULSION", "推進系統"}
            };

        private static readonly Color StatusOkColor = Color.FromArgb(31, 122, 67);
        private static readonly Color StatusFaultColor = Color.FromArgb(198, 40, 40);
        private static readonly Color StatusUnavailableColor = Color.FromArgb(70, 80, 87);
        private TableLayoutPanel fixedHeader;

        private bool IsTraditionalChinese
        {
            get
            {
                return CultureInfo.CurrentUICulture.Name.StartsWith(
                    "zh", StringComparison.OrdinalIgnoreCase);
            }
        }

        public ControlSensorsStatus()
        {
            InitializeComponent();

            BuildFixedHeaderLayout();

            var names = Enum.GetNames(typeof (MAVLink.MAV_SYS_STATUS_SENSOR));
            ConfigureTable(names.Length);
        }

        private void BuildFixedHeaderLayout()
        {
            Controls.Remove(tableLayoutPanel1);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(22, 38, 47)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            fixedHeader = CreateFourColumnTable(false);
            fixedHeader.Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0);
            fixedHeader.Controls.Add(CreateHeaderLabel(IsTraditionalChinese ? "感測器" : "Sensor", false), 0, 0);
            fixedHeader.Controls.Add(CreateHeaderLabel(IsTraditionalChinese ? "啟用" : "On", true), 1, 0);
            fixedHeader.Controls.Add(CreateHeaderLabel(IsTraditionalChinese ? "存在" : "Present", true), 2, 0);
            fixedHeader.Controls.Add(CreateHeaderLabel(IsTraditionalChinese ? "狀態" : "Health", true), 3, 0);

            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Margin = Padding.Empty;
            tableLayoutPanel1.Padding = Padding.Empty;
            tableLayoutPanel1.BackColor = Color.FromArgb(22, 38, 47);

            layout.Controls.Add(fixedHeader, 0, 0);
            layout.Controls.Add(tableLayoutPanel1, 0, 1);
            Controls.Add(layout);
        }

        private TableLayoutPanel CreateFourColumnTable(bool scrollable)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                AutoScroll = scrollable,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            ApplyColumnWidths(table);
            return table;
        }

        private static void ApplyColumnWidths(TableLayoutPanel table)
        {
            table.ColumnStyles.Clear();
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            update();

            timer1.Start();
        }

        public void update()
        {
            var names = Enum.GetNames(typeof (MAVLink.MAV_SYS_STATUS_SENSOR));
            ConfigureTable(names.Length);

            var connected = MainV2.comPort != null && MainV2.comPort.BaseStream != null &&
                            MainV2.comPort.BaseStream.IsOpen;
            var statusReceived = connected && MainV2.comPort.MAV.cs.sensors_enabled.seen &&
                                 MainV2.comPort.MAV.cs.sensors_present.seen &&
                                 MainV2.comPort.MAV.cs.sensors_health.seen;

            var a = 0;
            foreach (var name in names)
            {
                var row = a;
                if (tableLayoutPanel1.GetControlFromPosition(0, row) != null)
                {
                    a++;
                    continue;
                }

                var displayName = name.Replace("MAV_SYS_STATUS_SENSOR_", "")
                    .Replace("MAV_SYS_STATUS_", "").Replace("_", " ").Trim();
                string translatedName;
                if (IsTraditionalChinese && TraditionalChineseSensorNames.TryGetValue(displayName, out translatedName))
                    displayName = translatedName;

                tableLayoutPanel1.Controls.Add(
                    CreateStatusLabel(displayName, false), 0, row);
                a++;
            }

            // enabled
            a = 0;
            var mask = 1;
            foreach (var name in names)
            {
                if (!statusReceived)
                {
                    updateLabel(1, a, IsTraditionalChinese ? "—" : "--", StatusUnavailableColor);
                }
                else if ((MainV2.comPort.MAV.cs.sensors_enabled.Value & mask) > 0)
                {
                    updateLabel(1, a, IsTraditionalChinese ? "啟用" : "On", StatusOkColor);
                }
                else
                {
                    updateLabel(1, a, IsTraditionalChinese ? "停用" : "Off", StatusUnavailableColor);
                }
                mask = mask << 1;
                a++;
            }

            // present
            a = 0;
            mask = 1;
            foreach (var name in names)
            {
                if (!statusReceived)
                {
                    updateLabel(2, a, IsTraditionalChinese ? "—" : "--", StatusUnavailableColor);
                }
                else if ((MainV2.comPort.MAV.cs.sensors_present.Value & mask) > 0)
                {
                    updateLabel(2, a, IsTraditionalChinese ? "有" : "Yes", StatusOkColor);
                }
                else
                {
                    updateLabel(2, a, IsTraditionalChinese ? "無" : "No", StatusUnavailableColor);
                }
                mask = mask << 1;
                a++;
            }

            // present
            a = 0;
            mask = 1;
            foreach (var name in names)
            {
                var present = statusReceived &&
                              (MainV2.comPort.MAV.cs.sensors_present.Value & mask) > 0;
                if (!statusReceived || !present)
                {
                    updateLabel(3, a, IsTraditionalChinese ? "—" : "--", StatusUnavailableColor);
                }
                else if ((MainV2.comPort.MAV.cs.sensors_health.Value & mask) > 0)
                {
                    updateLabel(3, a, IsTraditionalChinese ? "正常" : "OK", StatusOkColor);
                }
                else
                {
                    updateLabel(3, a, IsTraditionalChinese ? "異常" : "Bad", StatusFaultColor);
                }
                mask = mask << 1;
                a++;
            }
        }

        private void updateLabel(int coloum, int row, string text, Color color)
        {
            var ctl = tableLayoutPanel1.GetControlFromPosition(coloum, row);

            if (ctl == null)
            {
                ctl = CreateStatusLabel(text, true);
                tableLayoutPanel1.Controls.Add(ctl, coloum, row);
            }

            ctl.Text = text;
            ctl.BackColor = color;
            ctl.ForeColor = Color.White;
        }

        private void ConfigureTable(int sensorCount)
        {
            tableLayoutPanel1.SuspendLayout();
            tableLayoutPanel1.ColumnCount = 4;
            tableLayoutPanel1.RowCount = sensorCount;
            tableLayoutPanel1.AutoScroll = true;
            tableLayoutPanel1.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            ApplyColumnWidths(tableLayoutPanel1);
            tableLayoutPanel1.RowStyles.Clear();
            for (var row = 0; row < sensorCount; row++)
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            tableLayoutPanel1.ResumeLayout(true);
        }

        private Label CreateHeaderLabel(string text, bool centered)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font(Font.FontFamily, 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(43, 58, 66),
                ForeColor = Color.White,
                TextAlign = centered ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft,
                Margin = new Padding(1),
                Padding = new Padding(6, 2, 4, 2)
            };
        }

        private Label CreateStatusLabel(string text, bool centered)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font(Font.FontFamily, 9F, FontStyle.Regular),
                ForeColor = Color.White,
                TextAlign = centered ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft,
                Margin = new Padding(1),
                Padding = centered ? new Padding(2) : new Padding(6, 2, 3, 2),
                MinimumSize = new Size(0, 28),
                AutoEllipsis = true
            };
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            update();
        }
    }
}
