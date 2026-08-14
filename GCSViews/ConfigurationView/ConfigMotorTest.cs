using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using Newtonsoft.Json;
using System.IO;
using System.Drawing.Drawing2D;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigMotorTest : MyUserControl, IActivate
    {
        private FlowLayoutPanel fmtMotorRows;
        private Panel fmtMotorDiagram;
        private TableLayoutPanel fmtMotorRoot;

        private sealed class DoubleBufferedPanel : Panel
        {
            internal DoubleBufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        public ConfigMotorTest()
        {
            InitializeComponent();
            BuildFmtMotorLayout();
        }

        private int motormax = 0;

        private struct _motors
        {
            public int Number { get; set; }
            public int TestOrder { get; set; }
            public string Rotation { get; set; }
            public float Roll { get; set; }
            public float Pitch { get; set; }
        }
        struct _layouts
         {
            public int Class { get; set; }
            public int Type { get; set; }
            public _motors[] motors { get; set; }
        }
        private struct JSON_motors
        {
            public string Version { get; set; }
            public _layouts[] layouts { get; set; }
        }
        private _layouts motor_layout;

        public void Activate()
        {
            Enabled = true;
            motormax = this.get_motormax();
            fmtMotorRows.Controls.Clear();
            for (var a = 1; a <= motormax; a++)
                AddFmtMotorRow(a);

            Utilities.ThemeManager.ApplyThemeTo(this);
            ApplyFmtMotorColors();
            fmtMotorDiagram.Invalidate();
        }

        private void BuildFmtMotorLayout()
        {
            SuspendLayout();
            Controls.Clear();
            BackColor = Color.FromArgb(18, 28, 35);

            fmtMotorRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.FromArgb(18, 28, 35),
                Padding = new Padding(10)
            };
            fmtMotorRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            fmtMotorRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            fmtMotorRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
            fmtMotorRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 39, 47), Margin = new Padding(0, 0, 0, 8) };
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(14, 7),
                Text = "馬達測試與旋向  Motor Setup",
                ForeColor = Color.White,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12F, FontStyle.Bold)
            });
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(15, 32),
                Text = "⚠ 測試前必須移除所有槳葉，並確認周圍無人員與鬆動物品。",
                ForeColor = Color.FromArgb(255, 110, 80),
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, FontStyle.Bold)
            });

            var framePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 360,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 5, 8, 3),
                BackColor = Color.FromArgb(27, 39, 47)
            };
            FrameClass.AutoSize = true;
            FrameType.AutoSize = true;
            FrameClass.Text = "構型：--";
            FrameType.Text = "配置：--";
            framePanel.Controls.Add(FrameClass);
            framePanel.Controls.Add(FrameType);
            header.Controls.Add(framePanel);
            fmtMotorRoot.Controls.Add(header, 0, 0);
            fmtMotorRoot.SetColumnSpan(header, 2);

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.FromArgb(22, 32, 39),
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(8)
            };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));

            var settings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                Margin = Padding.Empty
            };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            settings.Controls.Add(CreateFmtLabel("測試油門 (%)"), 0, 0);
            settings.Controls.Add(NUM_thr_percent, 1, 0);
            settings.Controls.Add(CreateFmtLabel("持續時間 (秒)"), 2, 0);
            settings.Controls.Add(NUM_duration, 3, 0);
            settings.Controls.Add(CreateFmtLabel("解鎖旋轉值"), 0, 1);
            settings.Controls.Add(but_mot_spin_arm, 1, 1);
            settings.Controls.Add(CreateFmtLabel("最低旋轉值"), 2, 1);
            settings.Controls.Add(but_mot_spin_min, 3, 1);
            linkLabel1.Text = "ArduPilot 馬達順序與旋向文件";
            linkLabel1.AutoSize = true;
            linkLabel1.LinkColor = Color.FromArgb(68, 186, 235);
            settings.Controls.Add(linkLabel1, 0, 2);
            settings.SetColumnSpan(linkLabel1, 4);
            left.Controls.Add(settings, 0, 0);

            fmtMotorRows = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(19, 29, 36),
                Padding = new Padding(2)
            };
            fmtMotorRows.SizeChanged += (sender, args) => ResizeFmtMotorRows();
            left.Controls.Add(fmtMotorRows, 0, 1);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 7, 0, 0)
            };
            actions.Controls.Add(CreateFmtActionButton("全部測試", Color.FromArgb(255, 153, 45), but_TestAll));
            actions.Controls.Add(CreateFmtActionButton("全部停止", Color.FromArgb(198, 48, 48), but_StopAll));
            actions.Controls.Add(CreateFmtActionButton("依序測試", Color.FromArgb(45, 169, 220), but_TestAllSeq));
            left.Controls.Add(actions, 0, 2);

            fmtMotorDiagram = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 247, 248),
                Margin = new Padding(0),
                BorderStyle = BorderStyle.FixedSingle
            };
            fmtMotorDiagram.Paint += PaintFmtMotorDiagram;

            fmtMotorRoot.Controls.Add(left, 0, 1);
            fmtMotorRoot.Controls.Add(fmtMotorDiagram, 1, 1);
            Controls.Add(fmtMotorRoot);
            ResumeLayout(true);
        }

        private static Label CreateFmtLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gainsboro,
                AutoSize = false
            };
        }

        private MyButton CreateFmtActionButton(string text, Color color, EventHandler click)
        {
            var button = new MyButton
            {
                Text = text,
                Size = new Size(108, 38),
                Margin = new Padding(0, 0, 8, 0),
                BackColor = color,
                ForeColor = Color.White,
                UseVisualStyleBackColor = false
            };
            button.Click += click;
            return button;
        }

        private void AddFmtMotorRow(int testOrder)
        {
            var motorNumber = testOrder;
            var rotation = "?";
            if (motor_layout.motors != null)
            {
                foreach (var motor in motor_layout.motors)
                {
                    if (motor.TestOrder != testOrder)
                        continue;
                    motorNumber = motor.Number;
                    rotation = motor.Rotation;
                    break;
                }
            }

            var letter = (char)('A' + testOrder - 1);
            var row = new Panel
            {
                Height = 46,
                Width = Math.Max(320, fmtMotorRows.ClientSize.Width - 28),
                Margin = new Padding(2, 2, 2, 4),
                BackColor = Color.FromArgb(35, 47, 55),
                BorderStyle = BorderStyle.FixedSingle
            };
            var button = new MyButton
            {
                Text = "測試 " + letter,
                Tag = testOrder,
                Location = new Point(6, 6),
                Size = new Size(92, 32),
                BackColor = Color.FromArgb(255, 153, 45),
                ForeColor = Color.Black,
                UseVisualStyleBackColor = false
            };
            button.Click += but_Click;
            row.Controls.Add(button);
            row.Controls.Add(new Label
            {
                AutoSize = false,
                Location = new Point(112, 4),
                Size = new Size(Math.Max(180, row.Width - 120), 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F, FontStyle.Bold),
                Text = letter + "  |  馬達 " + motorNumber + "  |  旋向 " + rotation
            });
            fmtMotorRows.Controls.Add(row);
        }

        private void ResizeFmtMotorRows()
        {
            foreach (Control row in fmtMotorRows.Controls)
                row.Width = Math.Max(320, fmtMotorRows.ClientSize.Width - 28);
        }

        private void ApplyFmtMotorColors()
        {
            fmtMotorRoot.BackColor = Color.FromArgb(18, 28, 35);
            FrameClass.ForeColor = Color.White;
            FrameType.ForeColor = Color.FromArgb(68, 186, 235);
            NUM_thr_percent.BackColor = Color.FromArgb(45, 50, 54);
            NUM_duration.BackColor = Color.FromArgb(45, 50, 54);
            NUM_thr_percent.ForeColor = Color.White;
            NUM_duration.ForeColor = Color.White;
            but_mot_spin_arm.Text = "設定";
            but_mot_spin_min.Text = "設定";
        }

        private void PaintFmtMotorDiagram(object sender, PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.FromArgb(245, 247, 248));
            var width = fmtMotorDiagram.ClientSize.Width;
            var height = fmtMotorDiagram.ClientSize.Height;
            if (width < 80 || height < 80)
                return;

            using (var titleFont = new Font(SystemFonts.MessageBoxFont.FontFamily, 13F, FontStyle.Bold))
            using (var labelFont = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, FontStyle.Bold))
            using (var bodyPen = new Pen(Color.FromArgb(83, 66, 105), 5F))
            using (var bodyBrush = new SolidBrush(Color.FromArgb(225, 228, 232)))
            {
                graphics.DrawString("構型與馬達旋向", titleFont, Brushes.Black, 14, 12);
                graphics.DrawString("機頭方向 ↑", labelFont, Brushes.DarkRed, 14, 38);

                var center = new PointF(width / 2F, height / 2F + 18F);
                var span = Math.Min(width, height) * 0.72F;
                var positions = new PointF[Math.Max(0, motormax)];

                for (var index = 0; index < positions.Length; index++)
                {
                    var angle = -Math.PI / 2 + index * Math.PI * 2 / Math.Max(1, positions.Length);
                    positions[index] = new PointF(center.X + (float)Math.Cos(angle) * span * 0.42F,
                        center.Y + (float)Math.Sin(angle) * span * 0.42F);
                }

                if (motor_layout.motors != null)
                {
                    foreach (var motor in motor_layout.motors)
                    {
                        var index = Math.Max(0, Math.Min(positions.Length - 1, motor.TestOrder - 1));
                        if (positions.Length == 0)
                            break;
                        positions[index] = new PointF(center.X + motor.Roll * span,
                            center.Y + motor.Pitch * span);
                    }
                }

                foreach (var point in positions)
                    graphics.DrawLine(bodyPen, center, point);
                graphics.FillRectangle(bodyBrush, center.X - 22, center.Y - 32, 44, 64);
                graphics.DrawRectangle(bodyPen, center.X - 22, center.Y - 32, 44, 64);
                graphics.FillPolygon(Brushes.DarkRed, new[]
                {
                    new PointF(center.X, center.Y - 48), new PointF(center.X - 9, center.Y - 30),
                    new PointF(center.X + 9, center.Y - 30)
                });

                for (var index = 0; index < positions.Length; index++)
                {
                    var motorNumber = index + 1;
                    var rotation = "?";
                    if (motor_layout.motors != null)
                    {
                        foreach (var motor in motor_layout.motors)
                        {
                            if (motor.TestOrder != index + 1)
                                continue;
                            motorNumber = motor.Number;
                            rotation = motor.Rotation;
                            break;
                        }
                    }

                    var color = rotation == "CW" ? Color.FromArgb(52, 188, 83) :
                        rotation == "CCW" ? Color.FromArgb(30, 174, 218) : Color.FromArgb(255, 153, 45);
                    using (var motorBrush = new SolidBrush(color))
                    using (var motorPen = new Pen(color, 5F))
                    {
                        var point = positions[index];
                        graphics.DrawEllipse(motorPen, point.X - 25, point.Y - 25, 50, 50);
                        graphics.FillEllipse(motorBrush, point.X - 16, point.Y - 16, 32, 32);
                        var number = motorNumber.ToString();
                        var numberSize = graphics.MeasureString(number, labelFont);
                        graphics.DrawString(number, labelFont, Brushes.White,
                            point.X - numberSize.Width / 2, point.Y - numberSize.Height / 2);
                        graphics.DrawString(((char)('A' + index)) + "  " + rotation, labelFont, Brushes.Black,
                            point.X - 25, point.Y + 29);
                    }
                }
            }
        }

        private int get_motormax()
        {
            var motormax = 8;

            if (MainV2.comPort.MAV.aptype == MAVLink.MAV_TYPE.GROUND_ROVER || MainV2.comPort.MAV.aptype == MAVLink.MAV_TYPE.SURFACE_BOAT)
            {
                return 4;
            }

            var enable = MainV2.comPort.MAV.param.ContainsKey("FRAME") || MainV2.comPort.MAV.param.ContainsKey("Q_FRAME_TYPE") || MainV2.comPort.MAV.param.ContainsKey("FRAME_TYPE");

            if (!enable)
            {
                Enabled = false;
                return motormax;
            }

            if (set_frame_class_and_type("FRAME_CLASS", "FRAME_TYPE") ||
                set_frame_class_and_type("Q_FRAME_CLASS", "Q_FRAME_TYPE"))
            {
                if (motor_layout.motors != null)
                {
                    return motor_layout.motors.Length;
                }
            }

            MAVLink.MAV_TYPE type = MAVLink.MAV_TYPE.QUADROTOR;

            if (MainV2.comPort.MAV.param.ContainsKey("Q_FRAME_CLASS"))
            {
                var value = (int)MainV2.comPort.MAV.param["Q_FRAME_CLASS"].Value;
                switch (value)
                {
                    case 0:
                    case 1:
                        type = MAVLink.MAV_TYPE.QUADROTOR;
                        break;
                    case 2:
                    case 5:
                        type = MAVLink.MAV_TYPE.HEXAROTOR;
                        break;
                    case 3:
                    case 4:
                        type = MAVLink.MAV_TYPE.OCTOROTOR;
                        break;
                    case 6:
                        type = MAVLink.MAV_TYPE.HELICOPTER;
                        break;
                    case 7:
                        type = MAVLink.MAV_TYPE.TRICOPTER;
                        break;
                }

            }
            else if (MainV2.comPort.MAV.param.ContainsKey("FRAME"))
            {
                type = MainV2.comPort.MAV.aptype;
            }
            else if (MainV2.comPort.MAV.param.ContainsKey("FRAME_TYPE"))
            {
                type = MainV2.comPort.MAV.aptype;
            }

            if (type == MAVLink.MAV_TYPE.TRICOPTER)
            {
                motormax = 4;
            }
            else if (type == MAVLink.MAV_TYPE.QUADROTOR)
            {
                motormax = 4;
            }
            else if (type == MAVLink.MAV_TYPE.HEXAROTOR)
            {
                motormax = 6;
            }
            else if (type == MAVLink.MAV_TYPE.OCTOROTOR)
            {
                motormax = 8;
            }
            else if (type == MAVLink.MAV_TYPE.HELICOPTER)
            {
                motormax = 0;
            }
            else if (type == MAVLink.MAV_TYPE.DODECAROTOR)
            {
                motormax = 12;
            }

            return motormax;
        }

        private bool set_frame_class_and_type(string class_param_name, string type_param_name)
        {
            if (!MainV2.comPort.MAV.param.ContainsKey(class_param_name) || !MainV2.comPort.MAV.param.ContainsKey(type_param_name))
            {
                return false;
            }
            var frame_class = (int)MainV2.comPort.MAV.param[class_param_name].Value;
            var class_list = ParameterMetaDataRepository.GetParameterOptionsInt(class_param_name, MainV2.comPort.MAV.cs.firmware.ToString());
            foreach (var item in class_list)
            {
                if (item.Key == Convert.ToInt32(frame_class))
                {
                    FrameClass.Text = "構型 Class：" + item.Value;
                    break;
                }
            }

            var frame_type = (int)MainV2.comPort.MAV.param[type_param_name].Value;
            var type_list = ParameterMetaDataRepository.GetParameterOptionsInt(type_param_name, MainV2.comPort.MAV.cs.firmware.ToString());
            foreach (var item in type_list)
            {
                if (item.Key == Convert.ToInt32(frame_type))
                {
                    FrameType.Text = "配置 Type：" + item.Value;
                    break;
                }
            }

            lookup_frame_layout(frame_class, frame_type);

            return true;
        }


        private void lookup_frame_layout(int frame_class, int frame_type)
        {
            motor_layout = new _layouts();
            try
            {
                string file = Path.GetDirectoryName(Path.GetFullPath(Assembly.GetExecutingAssembly().Location)) + Path.DirectorySeparatorChar + "APMotorLayout.json";
                using (StreamReader r = new StreamReader(file))
                {
                    string json = r.ReadToEnd();
                    var all_layouts = JsonConvert.DeserializeObject<JSON_motors>(json);
                    if (all_layouts.Version == "AP_Motors library test ver 1.2")
                    {
                        foreach (var layout in all_layouts.layouts)
                        {
                            if ((layout.Class == frame_class) && (layout.Type == frame_type))
                            {
                                motor_layout = layout;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void but_TestAll(object sender, EventArgs e)
        {
            if (!ConfirmBulkMotorTest("全部馬達將同時轉動。"))
                return;

            int speed = (int)NUM_thr_percent.Value;
            int time = (int)NUM_duration.Value;

            for (int i = 1; i <= motormax; i++)
            {
                testMotor(i, speed, time);
            }
        }

        private void but_TestAllSeq(object sender, EventArgs e)
        {
            if (!ConfirmBulkMotorTest("馬達將依 A、B、C…順序轉動。"))
                return;

            int speed = (int)NUM_thr_percent.Value;
            int time = (int)NUM_duration.Value;

            testMotor(1, speed, time, motormax);
        }

        private bool ConfirmBulkMotorTest(string action)
        {
            return CustomMessageBox.Show(action + "\n\n請確認已移除所有槳葉，是否繼續？",
                       "FMT 馬達安全確認", MessageBoxButtons.YesNo) == (int)DialogResult.Yes;
        }

        private void but_StopAll(object sender, EventArgs e)
        {
            for (int i = 1; i <= motormax; i++)
            {
                testMotor(i, 0, 0);
            }
        }

        private void but_Click(object sender, EventArgs e)
        {
            int speed = (int)NUM_thr_percent.Value;
            int time = (int)NUM_duration.Value;
            try
            {
                var motor = (int)((MyButton)sender).Tag;
                this.testMotor(motor, speed, time);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Failed to test motor\n" + ex);
            }
        }

        private void testMotor(int motor, int speed, int time, int motorcount = 0)
        {
            try
            {
                if (!MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent,
                        (byte)MainV2.comPort.compidcurrent,
                        MAVLink.MAV_CMD.DO_MOTOR_TEST,
                        (float)motor,
                        (float)(byte)MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PERCENT,
                        (float)speed,
                        (float)time,
                        (float)motorcount,
                        0,
                        0))
                {
                    CustomMessageBox.Show("Command was denied by the autopilot");
                }
            }
            catch
            {
                CustomMessageBox.Show(Strings.ErrorCommunicating + "\nMotor: " + motor, Strings.ERROR);
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start("https://ardupilot.org/copter/docs/connect-escs-and-motors.html#motor-order-diagrams");
            }
            catch
            {
                CustomMessageBox.Show("Bad default system association", Strings.ERROR);
            }
        }

        private async void but_mot_spin_arm_Click(object sender, EventArgs e)
        {
            this.Enabled = false;

            if (!MainV2.comPort.MAV.param.ContainsKey("MOT_SPIN_ARM"))
            {
                CustomMessageBox.Show("param MOT_SPIN_ARM missing", Strings.ERROR);
                return;
            }

            if (NUM_thr_percent.Value < 20)
            {
                var value = (int)NUM_thr_percent.Value + 2;
                if (InputBox.Show(Strings.ChangeThrottle, "Enter arm throttle % (deadzone + 2%)", ref value) == DialogResult.OK)
                {
                    await MainV2.comPort.setParamAsync((byte)MainV2.comPort.sysidcurrent,
                        (byte)MainV2.comPort.compidcurrent, "MOT_SPIN_ARM",
                        (float)value / 100.0f).ConfigureAwait(true);
                }
            }
            else
            {
                CustomMessageBox.Show("Throttle percent above 20, too high", Strings.ERROR);
            }

            this.Enabled = true;
        }

        private async void but_mot_spin_min_Click(object sender, EventArgs e)
        {
            this.Enabled = false;

            if (!MainV2.comPort.MAV.param.ContainsKey("MOT_SPIN_MIN"))
            {
                CustomMessageBox.Show("param MOT_SPIN_MIN missing", Strings.ERROR);
                return;
            }

            if (NUM_thr_percent.Value < 20)
            {
                var value = (int)MainV2.comPort.MAV.param["MOT_SPIN_MIN"].Value + 3;
                if (InputBox.Show(Strings.ChangeThrottle, "Enter min spin throttle % (arm min + 3%)", ref value) ==
                    DialogResult.OK)
                {
                    await MainV2.comPort.setParamAsync((byte)MainV2.comPort.sysidcurrent,
                        (byte)MainV2.comPort.compidcurrent, "MOT_SPIN_MIN",
                        (float)value/100.0f).ConfigureAwait(true);
                }
            }
            else
            {
                CustomMessageBox.Show("Throttle percent above 20, too high", Strings.ERROR);
            }

            this.Enabled = true;
        }
    }
}
