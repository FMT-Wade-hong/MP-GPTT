using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigAC_Fence : MyUserControl, IActivate
    {
        private const int MinimumContentWidth = 700;

        public ConfigAC_Fence()
        {
            InitializeComponent();

            ApplyLocalizedDistanceLabels();
            ConfigureResponsiveLayout();
            Resize += (sender, args) => LayoutFencePage();
        }

        private void ApplyLocalizedDistanceLabels()
        {
            var unit = CurrentState.DistanceUnit;
            var isChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("zh", StringComparison.OrdinalIgnoreCase);

            if (isChinese)
            {
                label6maxalt.Text = $"最大高度（{unit}）";
                label8minalt.Text = $"最小高度（{unit}）";
                label7maxrad.Text = $"最大半徑（{unit}）";
                label2rtlalt.Text = $"返航最低高度（{unit}）";
                return;
            }

            label6maxalt.Text += $" [{unit}]";
            label8minalt.Text += $" [{unit}]";
            label7maxrad.Text += $" [{unit}]";
            label2rtlalt.Text += $" [{unit}]";
        }

        /// <summary>
        /// Localized resource files still contain the legacy six-row layout. Rebuild the
        /// table explicitly so all seven fence settings remain readable at high DPI and
        /// when the configuration navigation panel leaves only a narrow content area.
        /// </summary>
        private void ConfigureResponsiveLayout()
        {
            AutoScroll = true;
            AutoScrollMinSize = new Size(MinimumContentWidth + 40, 340);

            label1gftitle.AutoSize = true;
            label1gftitle.Location = new Point(20, 10);

            tableLayoutPanel1.SuspendLayout();
            tableLayoutPanel1.AutoSize = false;
            tableLayoutPanel1.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.RowCount = 7;
            tableLayoutPanel1.ColumnStyles.Clear();
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Clear();
            for (var row = 0; row < 7; row++)
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

            tableLayoutPanel1.SetCellPosition(label3enable, new TableLayoutPanelCellPosition(0, 0));
            tableLayoutPanel1.SetCellPosition(mavlinkCheckBox1, new TableLayoutPanelCellPosition(1, 0));
            tableLayoutPanel1.SetCellPosition(label4type, new TableLayoutPanelCellPosition(0, 1));
            tableLayoutPanel1.SetCellPosition(mavlinkComboBox1, new TableLayoutPanelCellPosition(1, 1));
            tableLayoutPanel1.SetCellPosition(label5action, new TableLayoutPanelCellPosition(0, 2));
            tableLayoutPanel1.SetCellPosition(mavlinkComboBox2, new TableLayoutPanelCellPosition(1, 2));
            tableLayoutPanel1.SetCellPosition(label6maxalt, new TableLayoutPanelCellPosition(0, 3));
            tableLayoutPanel1.SetCellPosition(mavlinkNumericUpDown1, new TableLayoutPanelCellPosition(1, 3));
            tableLayoutPanel1.SetCellPosition(label8minalt, new TableLayoutPanelCellPosition(0, 4));
            tableLayoutPanel1.SetCellPosition(mavlinkNumericUpDown4, new TableLayoutPanelCellPosition(1, 4));
            tableLayoutPanel1.SetCellPosition(label7maxrad, new TableLayoutPanelCellPosition(0, 5));
            tableLayoutPanel1.SetCellPosition(mavlinkNumericUpDown2, new TableLayoutPanelCellPosition(1, 5));
            tableLayoutPanel1.SetCellPosition(label2rtlalt, new TableLayoutPanelCellPosition(0, 6));
            tableLayoutPanel1.SetCellPosition(mavlinkNumericUpDown3, new TableLayoutPanelCellPosition(1, 6));

            var labels = new[]
            {
                label3enable, label4type, label5action, label6maxalt,
                label8minalt, label7maxrad, label2rtlalt
            };
            foreach (var label in labels)
            {
                label.AutoSize = false;
                label.Dock = DockStyle.Fill;
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.Margin = new Padding(3, 3, 8, 3);
            }

            mavlinkCheckBox1.Anchor = AnchorStyles.Left;
            ConfigureInput(mavlinkComboBox1, 520);
            ConfigureInput(mavlinkComboBox2, 240);
            ConfigureInput(mavlinkNumericUpDown1, 125);
            ConfigureInput(mavlinkNumericUpDown4, 125);
            ConfigureInput(mavlinkNumericUpDown2, 125);
            ConfigureInput(mavlinkNumericUpDown3, 125);

            tableLayoutPanel1.ResumeLayout(true);
            LayoutFencePage();
        }

        private static void ConfigureInput(Control control, int width)
        {
            control.Anchor = AnchorStyles.Left;
            control.Margin = new Padding(3, 6, 3, 6);
            control.Width = width;
        }

        private void LayoutFencePage()
        {
            // Do not let the responsive host squeeze the long FENCE_TYPE descriptions
            // underneath the combo-box arrow. A narrow host can scroll horizontally,
            // while a normal full-width configuration page uses all available space.
            var contentWidth = Math.Max(MinimumContentWidth, ClientSize.Width - 40);

            lineSeparator2.Location = new Point(20, label1gftitle.Bottom + 8);
            lineSeparator2.Width = contentWidth;

            tableLayoutPanel1.Location = new Point(20, lineSeparator2.Bottom + 10);
            tableLayoutPanel1.Size = new Size(contentWidth, 7 * 36);
        }

        public void Activate()
        {
            mavlinkCheckBox1.setup(1, 0, "FENCE_ENABLE", MainV2.comPort.MAV.param, null, () => { if (mavlinkCheckBox1.Checked) MainV2.comPort.getParamList(); });

            mavlinkComboBox1.setup(GetFenceTypeOptions(), "FENCE_TYPE", MainV2.comPort.MAV.param);
            // The fence type is a bit mask and its combined Chinese descriptions
            // are considerably longer than a normal parameter option. Keep both
            // the selected value and every expanded item fully readable.
            ConfigureFenceTypeDropDown();


            mavlinkComboBox2.setup(
                ParameterMetaDataRepository.GetParameterOptionsInt("FENCE_ACTION",
                    MainV2.comPort.MAV.cs.firmware.ToString()), "FENCE_ACTION", MainV2.comPort.MAV.param);


            // 3
            mavlinkNumericUpDown1.setup(10, 1000, (float)CurrentState.fromDistDisplayUnit(1), 1, "FENCE_ALT_MAX",
                MainV2.comPort.MAV.param);

            mavlinkNumericUpDown4.setup(-100, 100, (float)CurrentState.fromDistDisplayUnit(1), 1, "FENCE_ALT_MIN",
                MainV2.comPort.MAV.param);

            mavlinkNumericUpDown2.setup(30, 65536, (float)CurrentState.fromDistDisplayUnit(1), 1, "FENCE_RADIUS",
                MainV2.comPort.MAV.param);

            if (MainV2.comPort.MAV.param.ContainsKey("RTL_ALT_M"))
                mavlinkNumericUpDown3.setup(1, 500, (float)CurrentState.fromDistDisplayUnit(1), 1, "RTL_ALT_M",
                    MainV2.comPort.MAV.param);
            else
                mavlinkNumericUpDown3.setup(1, 500, (float)CurrentState.fromDistDisplayUnit(100), 1, "RTL_ALT",
                    MainV2.comPort.MAV.param);
        }

        private void ConfigureFenceTypeDropDown()
        {
            // WinForms can reset DropDownWidth while rebinding DataSource. Recalculate it
            // after setup and again immediately before opening so every combined option is
            // readable on the first click and after a vehicle/parameter refresh.
            Action updateWidth = () =>
            {
                var width = mavlinkComboBox1.Width;
                using (var graphics = mavlinkComboBox1.CreateGraphics())
                {
                    foreach (var item in mavlinkComboBox1.Items)
                    {
                        var text = mavlinkComboBox1.GetItemText(item);
                        width = Math.Max(width,
                            TextRenderer.MeasureText(graphics, text, mavlinkComboBox1.Font).Width + 44);
                    }
                }

                mavlinkComboBox1.DropDownWidth = Math.Min(760, Math.Max(560, width));
            };

            updateWidth();
            mavlinkComboBox1.DropDown -= FenceTypeComboBox_DropDown;
            mavlinkComboBox1.DropDown += FenceTypeComboBox_DropDown;
        }

        private void FenceTypeComboBox_DropDown(object sender, EventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null)
                return;

            var width = combo.Width;
            using (var graphics = combo.CreateGraphics())
            {
                foreach (var item in combo.Items)
                    width = Math.Max(width,
                        TextRenderer.MeasureText(graphics, combo.GetItemText(item), combo.Font).Width + 44);
            }

            combo.DropDownWidth = Math.Min(760, Math.Max(560, width));
        }

        /// <summary>
        /// FENCE_TYPE is a bit mask. Some bundled/older parameter metadata only contains
        /// values 0..7, while current ArduPilot versions also use bit 3 (minimum altitude),
        /// producing values 8..15. Binding an incomplete list makes a valid current value
        /// render as an empty ComboBox. Supply every supported combination explicitly.
        /// </summary>
        private static List<KeyValuePair<int, string>> GetFenceTypeOptions()
        {
            var isChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("zh", StringComparison.OrdinalIgnoreCase);

            var altitudeMax = isChinese ? "最大高度" : "Maximum altitude";
            var circle = isChinese ? "圓形範圍" : "Circle";
            var polygon = isChinese ? "多邊形範圍" : "Polygon";
            var altitudeMin = isChinese ? "最小高度" : "Minimum altitude";
            var none = isChinese ? "不限制" : "None";
            var separator = isChinese ? "＋" : " + ";

            var bitNames = new[] { altitudeMax, circle, polygon, altitudeMin };
            var options = new List<KeyValuePair<int, string>>(16);

            for (var value = 0; value <= 15; value++)
            {
                if (value == 0)
                {
                    options.Add(new KeyValuePair<int, string>(value, $"{value} - {none}"));
                    continue;
                }

                var names = new List<string>(4);
                for (var bit = 0; bit < bitNames.Length; bit++)
                {
                    if ((value & (1 << bit)) != 0)
                        names.Add(bitNames[bit]);
                }

                options.Add(new KeyValuePair<int, string>(value,
                    $"{value} - {string.Join(separator, names)}"));
            }

            return options;
        }
    }
}
