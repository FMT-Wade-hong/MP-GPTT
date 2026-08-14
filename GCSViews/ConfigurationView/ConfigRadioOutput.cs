using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigRadioOutput : MyUserControl, IActivate, IDeactivate
    {
        public ConfigRadioOutput()
        {
            InitializeComponent();
            ApplyFmtServoLayout();

            bindingSource1.DataSource = typeof(CurrentState);

            var num_servos = 16;

            // See if 32 servo support is enabled
            if (MainV2.comPort.MAV.param.ContainsKey("SERVO_32_ENABLE") &&
                    (MainV2.comPort.MAV.param["SERVO_32_ENABLE"].Value > 0))
            {
                num_servos = 32;
            }

            SuspendLayout();
            foreach (var i in Enumerable.Range(1, num_servos))
            {
                setup(i);
            }
            tableLayoutPanel1.Height = (num_servos + 1) * 34;

            ResumeLayout(true);
        }

        private void setup(int servono)
        {
            var servo = String.Format("SERVO{0}", servono);

            var label = new Label()
                {
                    Text = "CH " + servono,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(2)
                };
            var bAR1 = new HorizontalProgressBar2()
            {
                Minimum = 800, Maximum = 2200, Value = 1500, DrawLabel = true, Name = "BAR" + servono,
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(55, 57, 59),
                ValueColor = Color.FromArgb(255, 153, 45),
                BorderColor = Color.FromArgb(135, 140, 143),
                ForeColor = Color.White,
                Label = "PWM",
                Margin = new Padding(3, 4, 3, 4)
            };
            var rev1 = new MissionPlanner.Controls.MavlinkCheckBox()
                {Enabled = false, Dock = DockStyle.Fill, AutoSize = true, TextAlign = ContentAlignment.MiddleCenter};
            var func1 = new MavlinkComboBox()
            { Enabled = false, Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Width = 190, Margin = new Padding(3, 4, 3, 4) };
            var min1 = new MavlinkNumericUpDown() { Minimum = 800, Maximum = 2200, Value = 1500, Enabled = false, Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 4) };
            var trim1 = new MavlinkNumericUpDown() { Minimum = 800, Maximum = 2200, Value = 1500, Enabled = false, Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 4) };
            var max1 = new MavlinkNumericUpDown() { Minimum = 800, Maximum = 2200, Value = 1500, Enabled = false, Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 4) };

            while (tableLayoutPanel1.RowStyles.Count <= servono)
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            tableLayoutPanel1.RowStyles[servono] = new RowStyle(SizeType.Absolute, 34F);

            this.tableLayoutPanel1.Controls.Add(label, 0, servono);
            this.tableLayoutPanel1.Controls.Add(bAR1, 1, servono);
            this.tableLayoutPanel1.Controls.Add(rev1, 2, servono);
            this.tableLayoutPanel1.Controls.Add(func1, 3, servono);
            this.tableLayoutPanel1.Controls.Add(min1, 4, servono);
            this.tableLayoutPanel1.Controls.Add(trim1, 5, servono);
            this.tableLayoutPanel1.Controls.Add(max1, 6, servono);

            bAR1.DataBindings.Add("Value", bindingSource1, "ch" + servono + "out");
            rev1.setup(1, 0, servo + "_REVERSED", MainV2.comPort.MAV.param);
            func1.setup(ParameterMetaDataRepository.GetParameterOptionsInt(servo + "_FUNCTION",
                    MainV2.comPort.MAV.cs.firmware.ToString()), servo + "_FUNCTION", MainV2.comPort.MAV.param);
            min1.setup(800, 2200, 1, 1, servo + "_MIN", MainV2.comPort.MAV.param);
            trim1.setup(800, 2200, 1, 1, servo + "_TRIM", MainV2.comPort.MAV.param);
            max1.setup(800, 2200, 1, 1, servo + "_MAX", MainV2.comPort.MAV.param);
        }

        public void Activate()
        {
            ApplyFmtServoColors();
            timer1.Start();
        }

        private void ApplyFmtServoLayout()
        {
            BackColor = Color.FromArgb(18, 28, 35);
            flowLayoutPanel1.AutoSize = false;
            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.WrapContents = false;
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Padding = new Padding(8);
            flowLayoutPanel1.BackColor = Color.FromArgb(18, 28, 35);

            var header = new Panel
            {
                Name = "fmtServoHeader",
                Width = Math.Max(840, flowLayoutPanel1.ClientSize.Width - 28),
                Height = 55,
                BackColor = Color.FromArgb(27, 39, 47),
                Margin = new Padding(0, 0, 0, 8)
            };
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(14, 7),
                Text = "Servo 輸出與功能  Servo Output",
                ForeColor = Color.White,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12F, FontStyle.Bold)
            });
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(15, 31),
                Text = "即時 PWM、反向、輸出功能與端點設定集中顯示；修改前請先解除馬達電源。",
                ForeColor = Color.FromArgb(255, 174, 72)
            });
            flowLayoutPanel1.Controls.Add(header);
            flowLayoutPanel1.Controls.SetChildIndex(header, 0);

            tableLayoutPanel1.AutoSize = false;
            tableLayoutPanel1.Width = Math.Max(840, flowLayoutPanel1.ClientSize.Width - 28);
            tableLayoutPanel1.Height = Math.Max(60, tableLayoutPanel1.RowCount * 34);
            tableLayoutPanel1.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            tableLayoutPanel1.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tableLayoutPanel1.BackColor = Color.FromArgb(28, 39, 46);
            tableLayoutPanel1.ColumnStyles.Clear();
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
            tableLayoutPanel1.RowStyles[0] = new RowStyle(SizeType.Absolute, 32F);

            label15.Text = "通道";
            label1.Text = "即時輸出 PWM";
            label2.Text = "反向";
            label3.Text = "輸出功能";
            label4.Text = "最小";
            label5.Text = "中立";
            label6.Text = "最大";
            foreach (var label in new[] { label15, label1, label2, label3, label4, label5, label6 })
            {
                label.Dock = DockStyle.Fill;
                label.AutoSize = false;
                label.TextAlign = ContentAlignment.MiddleCenter;
                label.ForeColor = Color.White;
                label.BackColor = Color.FromArgb(33, 48, 58);
                label.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, FontStyle.Bold);
            }

            flowLayoutPanel1.SizeChanged += (sender, args) =>
            {
                var width = Math.Max(840, flowLayoutPanel1.ClientSize.Width - 28);
                header.Width = width;
                tableLayoutPanel1.Width = width;
            };
        }

        private void ApplyFmtServoColors()
        {
            BackColor = Color.FromArgb(18, 28, 35);
            flowLayoutPanel1.BackColor = Color.FromArgb(18, 28, 35);
            tableLayoutPanel1.BackColor = Color.FromArgb(28, 39, 46);
            foreach (Control control in tableLayoutPanel1.Controls)
            {
                var bar = control as HorizontalProgressBar2;
                if (bar != null)
                {
                    bar.BackgroundColor = Color.FromArgb(55, 57, 59);
                    bar.ValueColor = Color.FromArgb(255, 153, 45);
                    bar.BorderColor = Color.FromArgb(135, 140, 143);
                    bar.ForeColor = Color.White;
                }
            }
        }

        public void Deactivate()
        {
            timer1.Stop();
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.MAV.cs.UpdateCurrentSettings(bindingSource1.UpdateDataSource(MainV2.comPort.MAV.cs));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
