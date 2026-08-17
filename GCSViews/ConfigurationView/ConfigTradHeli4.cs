using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigTradHeli4 : UserControl, IActivate, IDeactivate
    {
        private GroupBox fmtCommonSettings;
        private NumericUpDown fmtNavigationSpeed;
        private NumericUpDown fmtLoiterSpeed;
        private NumericUpDown fmtWpRadius;
        private NumericUpDown fmtRtlSpeed;
        private NumericUpDown fmtClimbSpeed;
        private NumericUpDown fmtDescentSpeed;
        private NumericUpDown fmtRtlAltitude;
        private ComboBox fmtYawBehavior;
        private NumericUpDown fmtRpmLower;
        private NumericUpDown fmtRpmUpper;
        private Label fmtCommonStatus;
        private Label fmtCommonHint;
        private Button fmtSaveCommonSettings;
        private string fmtNavigationSpeedParameter;
        private string fmtLoiterSpeedParameter;
        private string fmtWpRadiusParameter;
        private string fmtRtlSpeedParameter;
        private string fmtClimbSpeedParameter;
        private string fmtDescentSpeedParameter;
        private string fmtRtlAltitudeParameter;
        private string fmtYawBehaviorParameter;

        public ConfigTradHeli4()
        {
            InitializeComponent();
            ApplyFmtTraditionalChineseLayout();
            CreateFmtHelicopterCommonSettings();
        }

        private void ApplyFmtTraditionalChineseLayout()
        {
            Font = new Font("Microsoft JhengHei UI", 9F);
            groupBoxservo.Text = "舵機輸出與功能";
            groupBoxswash.Text = "十字盤設定";
            groupBoxthrot.Text = "旋翼轉速控制";
            groupBoxgover.Text = "調速器設定";
            groupBoxmisc.Text = "尾旋翼與其他設定";
            label1.Text = "舵機";
            label2.Text = "輸出功能";
            label3.Text = "最小";
            label4.Text = "最大";
            label5.Text = "中立";
            label6.Text = "反向";

            foreach (var table in new[] { tableLayoutPanel1, tableLayoutPanel2, tableLayoutPanel3, tableLayoutPanel5 })
            {
                table.ColumnStyles.Clear();
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
                table.MinimumSize = new Size(370, 0);
            }

            groupBoxswash.MinimumSize = new Size(382, 100);
            groupBoxthrot.MinimumSize = new Size(382, 100);
            groupBoxgover.MinimumSize = new Size(382, 100);
            groupBoxmisc.MinimumSize = new Size(382, 100);
        }

        public class ItemInfo
        {
            public string name { get; set; }
            public uitype type { get; set; }
        }

        private sealed class FmtYawOption
        {
            internal readonly int Value;
            private readonly string text;

            internal FmtYawOption(int value, string text)
            {
                Value = value;
                this.text = text;
            }

            public override string ToString()
            {
                return text;
            }
        }

        private void CreateFmtHelicopterCommonSettings()
        {
            fmtCommonSettings = new GroupBox
            {
                Name = "FmtHelicopterNavigationSettings",
                Text = "直升機常用導航與轉速警告",
                AutoSize = false,
                Size = new Size(790, 338),
                MinimumSize = new Size(790, 338),
                Padding = new Padding(12),
                Margin = new Padding(3, 8, 3, 8)
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 7,
                Padding = new Padding(4),
                Margin = Padding.Empty
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            fmtNavigationSpeed = CreateFmtNumeric("FmtHeliNavigationSpeed", 0.1m, 100m, 1);
            fmtLoiterSpeed = CreateFmtNumeric("FmtHeliLoiterSpeed", 0.1m, 100m, 1);
            fmtWpRadius = CreateFmtNumeric("FmtHeliWpRadius", 0.1m, 1000m, 1);
            fmtRtlSpeed = CreateFmtNumeric("FmtHeliRtlSpeed", 0m, 100m, 1);
            fmtClimbSpeed = CreateFmtNumeric("FmtHeliClimbSpeed", 0.1m, 30m, 1);
            fmtDescentSpeed = CreateFmtNumeric("FmtHeliDescentSpeed", 0.1m, 30m, 1);
            fmtRtlAltitude = CreateFmtNumeric("FmtHeliRtlAltitude", 0m, 10000m, 1);
            fmtYawBehavior = new ComboBox
            {
                Name = "FmtHeliYawBehavior",
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(3, 6, 3, 6)
            };
            fmtYawBehavior.Items.AddRange(new object[]
            {
                new FmtYawOption(0, "全程保持目前航向"),
                new FmtYawOption(1, "朝向下一航點（含返航）"),
                new FmtYawOption(2, "任務朝向航點，返航保持航向"),
                new FmtYawOption(3, "沿 GPS 航跡方向")
            });
            fmtRpmLower = CreateFmtNumeric("FmtHeliRpmLower", 0m, 9999m, 0);
            fmtRpmUpper = CreateFmtNumeric("FmtHeliRpmUpper", 1m, 9999m, 0);

            AddFmtSettingRow(table, 0, "任務導航速度", fmtNavigationSpeed, "m/s",
                "定點／任務飛行的水平速度", fmtLoiterSpeed, "m/s");
            AddFmtSettingRow(table, 1, "WP 接受半徑", fmtWpRadius, "m",
                "RTL 返航速度", fmtRtlSpeed, "m/s");
            AddFmtSettingRow(table, 2, "最大上升速度", fmtClimbSpeed, "m/s",
                "最大下降速度", fmtDescentSpeed, "m/s");
            AddFmtSettingRow(table, 3, "RTL 返航高度", fmtRtlAltitude, "m",
                "WP／RTL 航向", fmtYawBehavior, string.Empty);
            AddFmtSettingRow(table, 4, "RPM1 警告下限", fmtRpmLower, "RPM",
                "RPM1 警告上限", fmtRpmUpper, "RPM");

            fmtCommonStatus = new Label
            {
                Name = "FmtHeliCommonStatus",
                Dock = DockStyle.Fill,
                Text = "○ 連線後載入飛控實際參數",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gray,
                AutoEllipsis = true
            };
            fmtSaveCommonSettings = new Button
            {
                Name = "FmtSaveHeliCommonSettings",
                Text = "儲存直升機常用設定",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 171, 226),
                ForeColor = Color.White,
                Margin = new Padding(3, 5, 3, 5)
            };
            fmtSaveCommonSettings.FlatAppearance.BorderSize = 0;
            fmtSaveCommonSettings.Click += SaveFmtHelicopterCommonSettings;
            table.Controls.Add(fmtCommonStatus, 0, 5);
            table.SetColumnSpan(fmtCommonStatus, 4);
            table.Controls.Add(fmtSaveCommonSettings, 4, 5);
            table.SetColumnSpan(fmtSaveCommonSettings, 2);

            fmtCommonHint = new Label
            {
                Name = "FmtHeliCommonHint",
                Dock = DockStyle.Fill,
                Text = "說明：傳統直升機沿用 Copter 導航參數。RPM1 上下限只控制主畫面警告顏色，不會改變 H_RSC 旋翼控制。",
                ForeColor = Color.Silver,
                AutoEllipsis = true,
                Padding = new Padding(0, 5, 0, 0)
            };
            table.Controls.Add(fmtCommonHint, 0, 6);
            table.SetColumnSpan(fmtCommonHint, 6);

            fmtCommonSettings.Controls.Add(table);
            flowLayoutPanel1.Controls.Add(fmtCommonSettings);
            ThemeManager.ApplyThemeTo(fmtCommonSettings);
            foreach (Control input in new Control[]
                     {
                         fmtNavigationSpeed, fmtLoiterSpeed, fmtWpRadius, fmtRtlSpeed,
                         fmtClimbSpeed, fmtDescentSpeed, fmtRtlAltitude, fmtYawBehavior,
                         fmtRpmLower, fmtRpmUpper
                     })
            {
                input.BackColor = Color.FromArgb(67, 68, 69);
                input.ForeColor = Color.White;
            }
            fmtSaveCommonSettings.BackColor = Color.FromArgb(41, 171, 226);
            fmtSaveCommonSettings.ForeColor = Color.White;
        }

        private static NumericUpDown CreateFmtNumeric(string name, decimal minimum, decimal maximum,
            int decimals)
        {
            return new NumericUpDown
            {
                Name = name,
                Dock = DockStyle.Fill,
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = decimals,
                Increment = decimals == 0 ? 1m : 0.1m,
                TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(3, 6, 3, 6)
            };
        }

        private static Label CreateFmtTableLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(3)
            };
        }

        private static void AddFmtSettingRow(TableLayoutPanel table, int row, string leftLabel,
            Control leftControl, string leftUnit, string rightLabel, Control rightControl, string rightUnit)
        {
            table.Controls.Add(CreateFmtTableLabel(leftLabel), 0, row);
            table.Controls.Add(leftControl, 1, row);
            table.Controls.Add(CreateFmtTableLabel(leftUnit), 2, row);
            table.Controls.Add(CreateFmtTableLabel(rightLabel), 3, row);
            table.Controls.Add(rightControl, 4, row);
            table.Controls.Add(CreateFmtTableLabel(rightUnit), 5, row);
        }

        public void Activate()
        {
            this.Visible = false;

            setup(mavlinkCheckBoxrev1, mavlinkComboBoxfunc1, mavlinkNumericUpDownmin1, mavlinkNumericUpDowntrim1,
                mavlinkNumericUpDownmax1, 1);
            setup(mavlinkCheckBoxrev2, mavlinkComboBoxfunc2, mavlinkNumericUpDownmin2, mavlinkNumericUpDowntrim2,
                mavlinkNumericUpDownmax2, 2);
            setup(mavlinkCheckBoxrev3, mavlinkComboBoxfunc3, mavlinkNumericUpDownmin3, mavlinkNumericUpDowntrim3,
                mavlinkNumericUpDownmax3, 3);
            setup(mavlinkCheckBoxrev4, mavlinkComboBoxfunc4, mavlinkNumericUpDownmin4, mavlinkNumericUpDowntrim4,
                mavlinkNumericUpDownmax4, 4);
            setup(mavlinkCheckBoxrev5, mavlinkComboBoxfunc5, mavlinkNumericUpDownmin5, mavlinkNumericUpDowntrim5,
                mavlinkNumericUpDownmax5, 5);
            setup(mavlinkCheckBoxrev6, mavlinkComboBoxfunc6, mavlinkNumericUpDownmin6, mavlinkNumericUpDowntrim6,
                mavlinkNumericUpDownmax6, 6);
            setup(mavlinkCheckBoxrev7, mavlinkComboBoxfunc7, mavlinkNumericUpDownmin7, mavlinkNumericUpDowntrim7,
                mavlinkNumericUpDownmax7, 7);
            setup(mavlinkCheckBoxrev8, mavlinkComboBoxfunc8, mavlinkNumericUpDownmin8, mavlinkNumericUpDowntrim8,
                mavlinkNumericUpDownmax8, 8);

            TableLayoutPanel current = null;

            Func<ItemInfo, int, object> populatetable = (a, index) =>
            {
                var name = ParameterMetaDataRepository.GetParameterMetaData(a.name,
                    ParameterMetaDataConstants.DisplayName, MainV2.comPort.MAV.cs.firmware.ToString());
                var unit = ParameterMetaDataRepository.GetParameterMetaData(a.name,
                    ParameterMetaDataConstants.Units, MainV2.comPort.MAV.cs.firmware.ToString());
                var desc = ParameterMetaDataRepository.GetParameterMetaData(a.name,
                    ParameterMetaDataConstants.Description, MainV2.comPort.MAV.cs.firmware.ToString());

                var chineseName = GetFmtHeliDisplayName(a.name);
                var chineseDescription = GetFmtHeliDescription(a.name);
                if (!string.IsNullOrEmpty(chineseDescription))
                    desc = chineseDescription;

                var label = new Label()
                {
                    Text = (chineseName != "" ? chineseName : name != "" ? name : a.name) +
                           (unit != "" ? "（" + unit + "）" : ""),
                    AutoSize = false,
                    Size = new Size(214, 28),
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    Margin = new Padding(3)
                };
                current.Controls.Add(label, 0, index);
                if (a.type == uitype.Combo)
                {
                    var ctl = new MavlinkComboBox
                    {
                        Padding = new Padding(4),
                        Width = 144,
                        Margin = new Padding(3)
                    };
                    current.Controls.Add(ctl, 1, index);
                    ctl.setup(new[] { a.name }, MainV2.comPort.MAV.param);
                    toolTip1.SetToolTip(ctl, desc);
                }
                else if (a.type == uitype.Num)
                {
                    var ctl = new MavlinkNumericUpDown
                    {
                        Padding = new Padding(4),
                        Width = 144,
                        Margin = new Padding(3)
                    };
                    current.Controls.Add(ctl, 1, index);
                    ctl.setup(0, 0, 1, 1, new[] { a.name }, MainV2.comPort.MAV.param);
                    toolTip1.SetToolTip(ctl, desc);
                }

                toolTip1.SetToolTip(label, desc);

                return null;
            };

            {
                var swashplatelist = new[]
                {
                    new ItemInfo {name = "H_SV_MAN", type = uitype.Combo},
                    new ItemInfo {name = "H_SW_TYPE", type = uitype.Combo},
                    new ItemInfo {name = "H_SW_COL_DIR", type = uitype.Combo},
                    new ItemInfo {name = "H_SW_LIN_SVO", type = uitype.Combo},
                    new ItemInfo {name = "H_FLYBAR_MODE", type = uitype.Combo},
                    new ItemInfo {name = "H_CYC_MAX", type = uitype.Num},
                    new ItemInfo {name = "H_COL_MAX", type = uitype.Num},
                    new ItemInfo {name = "H_COL_MID", type = uitype.Num},
                    new ItemInfo {name = "H_COL_MIN", type = uitype.Num},
                    new ItemInfo {name = "H_COL_ANG_MIN", type = uitype.Num},
                    new ItemInfo {name = "H_COL_ANG_MAX", type = uitype.Num},
                    new ItemInfo {name = "H_COL_ZERO_THRST", type = uitype.Num},
                    new ItemInfo {name = "H_COL_LAND_MIN", type = uitype.Num},
                };

                current = tableLayoutPanel5;
                current.RowCount = swashplatelist.Length;
                swashplatelist.Select(populatetable).ToList();
            }

            {
                var throttlelist = new[]
                {
                    new ItemInfo {name = "H_RSC_MODE", type = uitype.Combo},
                    new ItemInfo {name = "H_RSC_CRITICAL", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_RAMP_TIME", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_RUNUP_TIME", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_CLDWN_TIME", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_SETPOINT", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_IDLE", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_THRCRV_0", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_THRCRV_25", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_THRCRV_50", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_THRCRV_75", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_THRCRV_100", type = uitype.Num},
                };

                current = tableLayoutPanel3;
                current.RowCount = throttlelist.Length;
                throttlelist.Select(populatetable).ToList();

            }
            {
                var governor = new[]
                {
                    new ItemInfo {name = "H_RSC_GOV_COMP", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_GOV_SETPNT", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_GOV_DISGAG", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_GOV_DROOP", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_GOV_FF", type = uitype.Num},                  
                    new ItemInfo {name = "H_RSC_GOV_TCGAIN", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_GOV_RANGE", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_GOV_RPM", type = uitype.Num},
                    new ItemInfo {name = "H_RSC_GOV_TORQUE", type = uitype.Num},
                };

                current = tableLayoutPanel2;
                current.RowCount = governor.Length;
                governor.Select(populatetable).ToList();
            }
            {
                var misc = new[]
                {
                    new ItemInfo {name = "IM_STB_COL_1", type = uitype.Num},
                    new ItemInfo {name = "IM_STB_COL_2", type = uitype.Num},
                    new ItemInfo {name = "IM_STB_COL_3", type = uitype.Num},
                    new ItemInfo {name = "IM_STB_COL_4", type = uitype.Num},
                
                    new ItemInfo {name = "H_TAIL_TYPE", type = uitype.Combo},
                    new ItemInfo {name = "H_TAIL_SPEED", type = uitype.Num},
                    new ItemInfo {name = "H_GYR_GAIN", type = uitype.Num},
                    new ItemInfo {name = "H_GYR_GAIN_ACRO", type = uitype.Num},
                    new ItemInfo {name = "H_COLYAW", type = uitype.Num},
                 
                };

                current = tableLayoutPanel1;
                current.RowCount = misc.Length;
                misc.Select(populatetable).ToList();
            }

            LoadFmtHelicopterCommonSettings();
            this.Visible = true;
        }

        private void LoadFmtHelicopterCommonSettings()
        {
            fmtNavigationSpeedParameter = FindFmtParameter("WP_SPD", "WPNAV_SPEED");
            fmtLoiterSpeedParameter = FindFmtParameter("LOIT_SPEED_MS", "WPNAV_LOIT_SPEED", "LOIT_SPEED");
            fmtWpRadiusParameter = FindFmtParameter("WP_RADIUS_M", "WPNAV_RADIUS");
            fmtRtlSpeedParameter = FindFmtParameter("RTL_SPEED_MS", "RTL_SPEED");
            fmtClimbSpeedParameter = FindFmtParameter("WPNAV_SPEED_UP");
            fmtDescentSpeedParameter = FindFmtParameter("WPNAV_SPEED_DN");
            fmtRtlAltitudeParameter = FindFmtParameter("RTL_ALT");
            fmtYawBehaviorParameter = FindFmtParameter("WP_YAW_BEHAVIOR");

            LoadFmtSpeed(fmtNavigationSpeed, fmtNavigationSpeedParameter);
            LoadFmtSpeed(fmtLoiterSpeed, fmtLoiterSpeedParameter);
            LoadFmtDistance(fmtWpRadius, fmtWpRadiusParameter);
            LoadFmtSpeed(fmtRtlSpeed, fmtRtlSpeedParameter);
            LoadFmtSpeed(fmtClimbSpeed, fmtClimbSpeedParameter);
            LoadFmtSpeed(fmtDescentSpeed, fmtDescentSpeedParameter);
            LoadFmtDistance(fmtRtlAltitude, fmtRtlAltitudeParameter);

            fmtYawBehavior.Enabled = fmtYawBehaviorParameter != null;
            fmtYawBehavior.SelectedIndex = -1;
            if (fmtYawBehaviorParameter != null)
            {
                var current = (int)Math.Round(MainV2.comPort.MAV.param[fmtYawBehaviorParameter].Value);
                for (var i = 0; i < fmtYawBehavior.Items.Count; i++)
                {
                    var option = fmtYawBehavior.Items[i] as FmtYawOption;
                    if (option != null && option.Value == current)
                    {
                        fmtYawBehavior.SelectedIndex = i;
                        break;
                    }
                }
            }

            var lower = Math.Max(0, Math.Min(9999,
                Settings.Instance.GetInt32("FMT_HeliRpmLower", 1000)));
            var upper = Math.Max(1, Math.Min(9999,
                Settings.Instance.GetInt32("FMT_HeliRpmUpper", 2500)));
            fmtRpmLower.Value = lower;
            fmtRpmUpper.Value = upper;

            var parameters = new List<string>();
            AddFmtParameter(parameters, fmtNavigationSpeedParameter);
            AddFmtParameter(parameters, fmtLoiterSpeedParameter);
            AddFmtParameter(parameters, fmtWpRadiusParameter);
            AddFmtParameter(parameters, fmtRtlSpeedParameter);
            AddFmtParameter(parameters, fmtClimbSpeedParameter);
            AddFmtParameter(parameters, fmtDescentSpeedParameter);
            AddFmtParameter(parameters, fmtRtlAltitudeParameter);
            AddFmtParameter(parameters, fmtYawBehaviorParameter);

            var connected = MainV2.comPort.BaseStream != null && MainV2.comPort.BaseStream.IsOpen;
            var helicopter = MainV2.comPort.MAV.param.ContainsKey("H_RSC_MODE") ||
                             MainV2.comPort.MAV.param.ContainsKey("H_COL_MIN") ||
                             MainV2.comPort.MAV.param.ContainsKey("H_SW_TYPE");
            fmtCommonStatus.Text = connected && helicopter
                ? "● 已辨識傳統直升機；可寫入共用導航參數"
                : connected
                    ? "○ 目前連線飛控未辨識為傳統直升機"
                    : "○ 尚未連線；可先儲存本機 RPM1 警告範圍";
            fmtCommonStatus.ForeColor = connected && helicopter ? Color.LimeGreen : Color.Gray;
            fmtCommonHint.Text = parameters.Count == 0
                ? "說明：連線後載入 WPNAV／RTL 共用參數。RPM1 上下限只控制主畫面警告，不改變 H_RSC 旋翼控制。"
                : "使用參數：" + string.Join("、", parameters) +
                  "。RPM1 上下限只控制主畫面警告，不改變 H_RSC 旋翼控制。";
        }

        private void SaveFmtHelicopterCommonSettings(object sender, EventArgs e)
        {
            var lower = (int)fmtRpmLower.Value;
            var upper = (int)fmtRpmUpper.Value;
            if (lower >= upper)
            {
                CustomMessageBox.Show("RPM1 轉速下限必須小於上限。", "直升機常用設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Settings.Instance["FMT_HeliRpmLower"] = lower.ToString();
            Settings.Instance["FMT_HeliRpmUpper"] = upper.ToString();

            var connected = MainV2.comPort.BaseStream != null && MainV2.comPort.BaseStream.IsOpen;
            if (!connected)
            {
                CustomMessageBox.Show("RPM1 轉速警告範圍已儲存在本機。\r\n連線飛控後才能寫入導航參數。",
                    "直升機常用設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MainV2.comPort.MAV.cs.armed)
            {
                CustomMessageBox.Show("請先將飛行器上鎖；解鎖狀態不允許修改常用參數。",
                    "直升機常用設定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MainV2.comPort.ReadOnly)
            {
                CustomMessageBox.Show("目前為唯讀連線，無法寫入飛控參數。",
                    "直升機常用設定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (CustomMessageBox.Show("確定要將直升機共用導航、返航、高度與航向設定寫入飛控嗎？",
                    "直升機常用設定", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) !=
                (int)DialogResult.Yes)
                return;

            try
            {
                var failures = new List<string>();
                SaveFmtSpeed(fmtNavigationSpeed, fmtNavigationSpeedParameter, failures);
                SaveFmtSpeed(fmtLoiterSpeed, fmtLoiterSpeedParameter, failures);
                SaveFmtDistance(fmtWpRadius, fmtWpRadiusParameter, failures);
                SaveFmtSpeed(fmtRtlSpeed, fmtRtlSpeedParameter, failures);
                SaveFmtSpeed(fmtClimbSpeed, fmtClimbSpeedParameter, failures);
                SaveFmtSpeed(fmtDescentSpeed, fmtDescentSpeedParameter, failures);
                SaveFmtDistance(fmtRtlAltitude, fmtRtlAltitudeParameter, failures);
                var yaw = fmtYawBehavior.SelectedItem as FmtYawOption;
                if (fmtYawBehaviorParameter != null && yaw != null &&
                    !SetFmtParameter(fmtYawBehaviorParameter, yaw.Value))
                    failures.Add(fmtYawBehaviorParameter);

                if (failures.Count > 0)
                {
                    CustomMessageBox.Show("RPM1 警告已儲存，但下列飛控參數寫入失敗：" +
                                          string.Join("、", failures), "直升機常用設定",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                CustomMessageBox.Show("直升機常用導航設定與 RPM1 警告範圍已儲存。",
                    "直升機常用設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadFmtHelicopterCommonSettings();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("寫入直升機常用設定失敗：" + ex.Message,
                    "直升機常用設定", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FindFmtParameter(params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (MainV2.comPort.MAV.param.ContainsKey(candidate))
                    return candidate;
            }
            return null;
        }

        private static void AddFmtParameter(ICollection<string> parameters, string parameterName)
        {
            if (!string.IsNullOrEmpty(parameterName))
                parameters.Add(parameterName);
        }

        private static void LoadFmtSpeed(NumericUpDown control, string parameterName)
        {
            control.Enabled = parameterName != null;
            if (parameterName == null)
                return;
            var value = ConfigFlightModes.FmtSpeedRawToMetersPerSecond(parameterName,
                MainV2.comPort.MAV.param[parameterName].Value);
            control.Value = ClampFmtNumeric(control, value);
        }

        private static void LoadFmtDistance(NumericUpDown control, string parameterName)
        {
            control.Enabled = parameterName != null;
            if (parameterName == null)
                return;
            var value = ConfigFlightModes.FmtDistanceRawToMeters(parameterName,
                MainV2.comPort.MAV.param[parameterName].Value);
            control.Value = ClampFmtNumeric(control, value);
        }

        private static decimal ClampFmtNumeric(NumericUpDown control, double value)
        {
            return (decimal)Math.Max((double)control.Minimum,
                Math.Min((double)control.Maximum, value));
        }

        private static void SaveFmtSpeed(NumericUpDown control, string parameterName,
            ICollection<string> failures)
        {
            if (parameterName == null || !control.Enabled)
                return;
            var raw = ConfigFlightModes.FmtSpeedMetersPerSecondToRaw(parameterName,
                (double)control.Value);
            if (!SetFmtParameter(parameterName, raw))
                failures.Add(parameterName);
        }

        private static void SaveFmtDistance(NumericUpDown control, string parameterName,
            ICollection<string> failures)
        {
            if (parameterName == null || !control.Enabled)
                return;
            var raw = ConfigFlightModes.FmtDistanceMetersToRaw(parameterName,
                (double)control.Value);
            if (!SetFmtParameter(parameterName, raw))
                failures.Add(parameterName);
        }

        private static bool SetFmtParameter(string parameterName, double value)
        {
            return MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent,
                (byte)MainV2.comPort.compidcurrent, parameterName, value);
        }

        private static string GetFmtHeliDisplayName(string parameterName)
        {
            var names = new Dictionary<string, string>
            {
                { "H_SV_MAN", "十字盤手動測試模式" },
                { "H_SW_TYPE", "十字盤類型" },
                { "H_SW_COL_DIR", "總距方向" },
                { "H_SW_LIN_SVO", "十字盤舵機線性化" },
                { "H_FLYBAR_MODE", "穩定翼模式" },
                { "H_CYC_MAX", "最大循環螺距" },
                { "H_COL_MAX", "總距最大 PWM" },
                { "H_COL_MID", "總距中點 PWM" },
                { "H_COL_MIN", "總距最小 PWM" },
                { "H_COL_ANG_MIN", "最小總距角" },
                { "H_COL_ANG_MAX", "最大總距角" },
                { "H_COL_ZERO_THRST", "零推力總距角" },
                { "H_COL_LAND_MIN", "著陸最小總距角" },
                { "H_RSC_MODE", "旋翼轉速控制模式" },
                { "H_RSC_CRITICAL", "臨界旋翼轉速" },
                { "H_RSC_RAMP_TIME", "油門漸升時間" },
                { "H_RSC_RUNUP_TIME", "旋翼加速完成時間" },
                { "H_RSC_CLDWN_TIME", "旋翼冷卻時間" },
                { "H_RSC_SETPOINT", "旋翼轉速目標" },
                { "H_RSC_IDLE", "怠速輸出" },
                { "H_RSC_THRCRV_0", "總距 0% 油門曲線" },
                { "H_RSC_THRCRV_25", "總距 25% 油門曲線" },
                { "H_RSC_THRCRV_50", "總距 50% 油門曲線" },
                { "H_RSC_THRCRV_75", "總距 75% 油門曲線" },
                { "H_RSC_THRCRV_100", "總距 100% 油門曲線" },
                { "H_RSC_GOV_COMP", "調速器扭矩補償" },
                { "H_RSC_GOV_SETPNT", "調速器轉速目標" },
                { "H_RSC_GOV_DISGAG", "調速器解除門檻" },
                { "H_RSC_GOV_DROOP", "調速器降速補償" },
                { "H_RSC_GOV_FF", "調速器前饋" },
                { "H_RSC_GOV_TCGAIN", "扭矩補償增益" },
                { "H_RSC_GOV_RANGE", "調速器運作範圍" },
                { "H_RSC_GOV_RPM", "旋翼 RPM 目標" },
                { "H_RSC_GOV_TORQUE", "調速器扭矩限制" },
                { "IM_STB_COL_1", "穩定模式最低總距" },
                { "IM_STB_COL_2", "穩定模式中低總距" },
                { "IM_STB_COL_3", "穩定模式中高總距" },
                { "IM_STB_COL_4", "穩定模式最高總距" },
                { "H_TAIL_TYPE", "尾旋翼類型" },
                { "H_TAIL_SPEED", "DDVP 尾旋翼速度" },
                { "H_GYR_GAIN", "外部陀螺儀增益" },
                { "H_GYR_GAIN_ACRO", "特技模式陀螺儀增益" },
                { "H_COLYAW", "總距對偏航補償" }
            };
            string value;
            return names.TryGetValue(parameterName, out value) ? value : string.Empty;
        }

        private static string GetFmtHeliDescription(string parameterName)
        {
            if (parameterName.StartsWith("H_RSC_GOV_", StringComparison.Ordinal))
                return "直升機旋翼調速器參數；調整前請確認轉速感測器、旋翼目標與動力系統設定。";
            if (parameterName.StartsWith("H_RSC_THRCRV_", StringComparison.Ordinal))
                return "設定對應總距位置的旋翼油門曲線輸出百分比。";
            if (parameterName.StartsWith("H_RSC_", StringComparison.Ordinal))
                return "旋翼轉速控制參數；請依動力系統與旋翼加速時間逐項設定。";
            if (parameterName.StartsWith("H_COL", StringComparison.Ordinal) ||
                parameterName.StartsWith("H_CYC", StringComparison.Ordinal) ||
                parameterName.StartsWith("H_SW", StringComparison.Ordinal))
                return "十字盤與總距幾何參數；調整前請拆除主旋翼或確保動力完全斷開。";
            if (parameterName.StartsWith("H_TAIL", StringComparison.Ordinal) ||
                parameterName.StartsWith("H_GYR", StringComparison.Ordinal))
                return "尾旋翼與外部陀螺儀控制參數。";
            if (parameterName.StartsWith("IM_STB_COL_", StringComparison.Ordinal))
                return "設定穩定模式中不同操縱位置對應的總距輸出百分比。";
            return string.Empty;
        }

        private void setup(MavlinkCheckBox rev1, MavlinkComboBox func1,
            MavlinkNumericUpDown min1, MavlinkNumericUpDown trim1, MavlinkNumericUpDown max1, int servono)
        {
            var servo = String.Format("SERVO{0}", servono);

            rev1.setup(1, 0, servo + "_REVERSED", MainV2.comPort.MAV.param);
            func1.setup(ParameterMetaDataRepository.GetParameterOptionsInt(servo + "_FUNCTION",
                    MainV2.comPort.MAV.cs.firmware.ToString()), servo + "_FUNCTION", MainV2.comPort.MAV.param);
            min1.setup(800, 2200, 1, 1, servo + "_MIN", MainV2.comPort.MAV.param);
            trim1.setup(800, 2200, 1, 1, servo + "_TRIM", MainV2.comPort.MAV.param);
            max1.setup(800, 2200, 1, 1, servo + "_MAX", MainV2.comPort.MAV.param);
        }

        public void Deactivate()
        {
            tableLayoutPanel1.Controls.Clear();
            tableLayoutPanel2.Controls.Clear();
            tableLayoutPanel3.Controls.Clear();
            tableLayoutPanel5.Controls.Clear();
        }
    }
}
