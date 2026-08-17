using log4net;
using MissionPlanner.Controls;
using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigAccelerometerCalibration : MyUserControl, IActivate, IDeactivate
    {
        private const float DisabledOpacity = 0.2F;
        private const float EnabledOpacity = 1.0F;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private byte count;

        bool _incalibrate = false;
        private MAVLink.ACCELCAL_VEHICLE_POS pos;
        private int sub1;
        private int sub2;
        private PictureBox fmtAccelPromptImage;
        private Label fmtAccelPromptTitle;

        public ConfigAccelerometerCalibration()
        {
            InitializeComponent();
            ApplyFmtResponsiveLayout();
        }

        private void ApplyFmtResponsiveLayout()
        {
            var chinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            AutoScroll = true;
            Font = new Font("Microsoft JhengHei UI", 10F);

            label5.Text = chinese ? "加速度計校準" : "Accelerometer Calibration";
            label4.Text = chinese
                ? "依畫面指示，將飛行器依序平放於各個面，完成三軸偏移量與比例校準。\r\n校準前請拆除螺旋槳，並將飛行器放在穩固平面。"
                : "Follow the prompts and place the vehicle on each side to calibrate 3-axis offsets and scale.";
            label1.Text = chinese
                ? "將飛行器放在水平且穩固的平面，重新設定姿態水平基準（單軸／AHRS 修整）。"
                : "Place the vehicle flat and level to set the attitude level reference.";
            label2.Text = chinese
                ? "僅在確認飛行器完全水平時使用，快速設定水平飛行所需的加速度比例。"
                : "Place the vehicle flat and level to set the simple accelerometer scale.";
            BUT_calib_accell.Text = chinese ? "開始三軸校準" : "Start 3-axis calibration";
            BUT_level.Text = chinese ? "校準水平姿態" : "Calibrate level";
            BUT_simpleAccelCal.Text = chinese ? "快速水平校準" : "Simple level calibration";
            lbl_Accel_user.Text = chinese ? "校準訊息會顯示在這裡。" : "Calibration messages appear here.";

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 6,
                MinimumSize = new Size(720, 0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            label5.AutoSize = true;
            label5.Font = new Font(Font, FontStyle.Bold);
            label5.Margin = new Padding(0, 0, 0, 6);
            lineSeparator2.Dock = DockStyle.Top;
            lineSeparator2.Height = 2;
            lineSeparator2.Margin = new Padding(0, 0, 0, 14);
            lbl_Accel_user.AutoSize = true;
            lbl_Accel_user.Dock = DockStyle.Fill;
            lbl_Accel_user.MaximumSize = new Size(760, 0);
            lbl_Accel_user.Margin = new Padding(0, 4, 0, 0);

            Controls.Clear();
            root.Controls.Add(label5, 0, 0);
            root.Controls.Add(lineSeparator2, 0, 1);
            root.Controls.Add(CreateCalibrationCard(chinese ? "完整三軸校準" : "Full 3-axis calibration",
                label4, BUT_calib_accell, 0), 0, 2);
            root.Controls.Add(CreateCalibrationCard(chinese ? "水平姿態校準" : "Level calibration",
                label1, BUT_level, 1), 0, 3);
            root.Controls.Add(CreateCalibrationCard(chinese ? "快速水平校準" : "Simple calibration",
                label2, BUT_simpleAccelCal, 2), 0, 4);
            root.Controls.Add(CreateFmtAccelPromptPanel(chinese), 0, 5);
            Controls.Add(root);
        }

        private Control CreateFmtAccelPromptPanel(bool chinese)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0, 2, 0, 0),
                Padding = new Padding(12),
                BackColor = Color.FromArgb(22, 43, 53),
                MinimumSize = new Size(680, 116)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            fmtAccelPromptImage = new PictureBox
            {
                Name = "FmtAccelPromptImage",
                Image = CreateAccelPromptImage("等待"),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 12, 0)
            };
            fmtAccelPromptTitle = new Label
            {
                Name = "FmtAccelPromptTitle",
                Text = chinese ? "校準姿態提示" : "Calibration pose prompt",
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(61, 196, 235),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(fmtAccelPromptImage, 0, 0);
            panel.SetRowSpan(fmtAccelPromptImage, 2);
            panel.Controls.Add(fmtAccelPromptTitle, 1, 0);
            panel.Controls.Add(lbl_Accel_user, 1, 1);
            return panel;
        }

        private Control CreateCalibrationCard(string titleText, Label description, Button button, int iconType)
        {
            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(12),
                BackColor = Color.FromArgb(22, 43, 53),
                MinimumSize = new Size(680, 92)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));

            var icon = new PictureBox
            {
                Image = CreateCalibrationIcon(iconType),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 10, 0)
            };
            var title = new Label
            {
                Text = titleText,
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.White,
                Margin = new Padding(0, 2, 0, 5)
            };
            description.AutoSize = true;
            description.Dock = DockStyle.Fill;
            description.MaximumSize = new Size(700, 0);
            description.Margin = Padding.Empty;
            button.Dock = DockStyle.Fill;
            button.MinimumSize = new Size(160, 40);
            button.Margin = new Padding(12, 8, 0, 8);

            card.Controls.Add(icon, 0, 0);
            card.SetRowSpan(icon, 2);
            card.Controls.Add(title, 1, 0);
            card.Controls.Add(description, 1, 1);
            card.Controls.Add(button, 2, 0);
            card.SetRowSpan(button, 2);
            return card;
        }

        private static Image CreateCalibrationIcon(int iconType)
        {
            var image = new Bitmap(44, 44);
            using (var g = Graphics.FromImage(image))
            using (var pen = new Pen(Color.FromArgb(61, 196, 235), 2.5F))
            using (var brush = new SolidBrush(Color.FromArgb(61, 196, 235)))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                if (iconType == 0)
                {
                    g.DrawEllipse(pen, 7, 7, 30, 30);
                    g.DrawLine(pen, 22, 3, 22, 41);
                    g.DrawLine(pen, 3, 22, 41, 22);
                    g.FillEllipse(brush, 18, 18, 8, 8);
                }
                else if (iconType == 1)
                {
                    g.DrawRectangle(pen, 5, 12, 34, 20);
                    g.DrawLine(pen, 9, 22, 17, 22);
                    g.DrawEllipse(pen, 17, 17, 10, 10);
                    g.DrawLine(pen, 27, 22, 35, 22);
                }
                else
                {
                    g.DrawArc(pen, 6, 6, 32, 32, 25, 290);
                    g.FillPolygon(brush, new[] { new Point(34, 5), new Point(40, 12), new Point(31, 13) });
                    g.DrawLine(pen, 14, 22, 30, 22);
                }
            }
            return image;
        }

        private static Image CreateAccelPromptImage(string pose)
        {
            var image = new Bitmap(138, 88);
            using (var g = Graphics.FromImage(image))
            using (var bodyPen = new Pen(Color.WhiteSmoke, 3F))
            using (var accentPen = new Pen(Color.FromArgb(61, 196, 235), 3F))
            using (var accentBrush = new SolidBrush(Color.FromArgb(61, 196, 235)))
            using (var textBrush = new SolidBrush(Color.WhiteSmoke))
            using (var font = new Font("Microsoft JhengHei UI", 8F, FontStyle.Bold))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var normalized = (pose ?? string.Empty).ToUpperInvariant();
                var angle = 0F;
                if (normalized.Contains("左")) angle = -90F;
                else if (normalized.Contains("右")) angle = 90F;
                else if (normalized.Contains("機頭向下")) angle = 180F;
                else if (normalized.Contains("機頭向上")) angle = 0F;
                else if (normalized.Contains("倒置") || normalized.Contains("背面")) angle = 180F;

                var state = g.Save();
                g.TranslateTransform(68F, 39F);
                g.RotateTransform(angle);
                g.DrawLine(bodyPen, 0, -22, 0, 20);
                g.DrawLine(bodyPen, -28, 4, 28, 4);
                g.DrawLine(bodyPen, -12, 18, 12, 18);
                g.DrawLine(bodyPen, -28, 4, -15, 13);
                g.DrawLine(bodyPen, 28, 4, 15, 13);
                g.DrawLine(accentPen, 0, -25, 0, -12);
                g.FillPolygon(accentBrush, new[]
                {
                    new Point(0, -29), new Point(-5, -20), new Point(5, -20)
                });
                g.Restore(state);

                g.DrawLine(accentPen, 12, 70, 126, 70);
                g.DrawString(GetAccelPoseShortText(pose), font, textBrush, 6, 72);
            }
            return image;
        }

        private static string GetAccelPoseShortText(string pose)
        {
            if (string.IsNullOrEmpty(pose) || pose == "等待") return "等待飛控提示";
            if (pose.Contains("水平")) return "平放於穩固水平面";
            if (pose.Contains("左")) return "左側朝下";
            if (pose.Contains("右")) return "右側朝下";
            if (pose.Contains("機頭向下")) return "機頭朝下";
            if (pose.Contains("機頭向上")) return "機頭朝上";
            if (pose.Contains("倒置") || pose.Contains("背面")) return "背面朝下";
            return pose.Length > 12 ? pose.Substring(0, 12) : pose;
        }

        private static string TranslateAccelMessage(string message, out string poseText)
        {
            poseText = string.Empty;
            var lower = (message ?? string.Empty).Trim('\0', ' ', '\r', '\n').ToLowerInvariant();
            if (lower.Contains("calibration successful") || lower.Contains("calibration complete"))
                return "加速度計校準完成。請重新啟動飛控，並確認姿態顯示正常。";
            if (lower.Contains("calibration failed"))
                return "加速度計校準失敗。請確認飛行器保持靜止、桌面穩固後重新校準。";
            if (lower.Contains("calibration cancelled") || lower.Contains("calibration canceled"))
                return "加速度計校準已取消。";

            if (lower.Contains("place vehicle") || lower.Contains("level") ||
                lower.Contains("left") || lower.Contains("right") ||
                lower.Contains("nose") || lower.Contains("back"))
            {
                if (lower.Contains("left")) poseText = "左側";
                else if (lower.Contains("right")) poseText = "右側";
                else if (lower.Contains("nose down") || lower.Contains("nosedown")) poseText = "機頭向下";
                else if (lower.Contains("nose up") || lower.Contains("noseup")) poseText = "機頭向上";
                else if (lower.Contains("back") || lower.Contains("upside")) poseText = "倒置";
                else poseText = "水平";
                return "請將飛行器「" + GetAccelPoseShortText(poseText) + "」，保持完全靜止後按下「確認此姿態」。";
            }

            if (lower.Contains("press any key") || lower.Contains("click when done"))
                return "姿態穩定後，請按下「確認此姿態」繼續下一步。";
            if (lower.Contains("calibration"))
                return "加速度計正在校準，請依姿態提示操作並保持飛行器靜止。";
            return "飛控回報校準狀態，請依提示完成操作。";
        }

        public void Activate()
        {
            BUT_calib_accell.Enabled = true;
            _incalibrate = false;
        }

        public void Deactivate()
        {
            MainV2.comPort.giveComport = false;
            _incalibrate = false;
        }

        private void BUT_calib_accell_Click(object sender, EventArgs e)
        {
            if (_incalibrate)
            {
                count++;
                try
                {
                    // old
                    //MainV2.comPort.sendPacket(new MAVLink.mavlink_command_ack_t { command = 1, result = count },
                        //MainV2.comPort.sysidcurrent, MainV2.comPort.compidcurrent);
                    // new
                    MainV2.comPort.sendPacket(new MAVLink.mavlink_command_long_t { param1 = (float)pos, command = (ushort)MAVLink.MAV_CMD.ACCELCAL_VEHICLE_POS },
                        MainV2.comPort.sysidcurrent, MainV2.comPort.compidcurrent);
                }
                catch
                {
                    CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
                    return;
                }

                return;
            }

            try
            {
                count = 0;

                Log.Info("Sending accel command (mavlink 1.0)");

                if (MainV2.comPort.doCommand((byte) MainV2.comPort.sysidcurrent, (byte) MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 1, 0, 0))
                {
                    _incalibrate = true;

                    sub1 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.STATUSTEXT, receivedPacket, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);
                    sub2 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.COMMAND_LONG, receivedPacket, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);

                    BUT_calib_accell.Text = CultureInfo.CurrentUICulture.Name.StartsWith("zh",
                        StringComparison.OrdinalIgnoreCase) ? "確認此姿態" : Strings.Click_when_Done;
                    UpdateUserMessage("Please place vehicle level");
                }
                else
                {
                    CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
                }
            }
            catch (Exception ex)
            {
                _incalibrate = false;
                Log.Error("Exception on level", ex);
                CustomMessageBox.Show("無法開始加速度計校準：" + ex.Message, "加速度計校準");
            }
        }

        private bool receivedPacket(MAVLink.MAVLinkMessage arg)
        {
            if (arg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.STATUSTEXT)
            {
                var message = Encoding.ASCII.GetString(arg.ToStructure<MAVLink.mavlink_statustext_t>().text);

                UpdateUserMessage(message);

                if (message.ToLower().Contains("calibration successful") ||
                 message.ToLower().Contains("calibration failed"))
                {
                    try
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            BUT_calib_accell.Text = CultureInfo.CurrentUICulture.Name.StartsWith("zh",
                                StringComparison.OrdinalIgnoreCase) ? "校準完成" : Strings.Done;
                            BUT_calib_accell.Enabled = false;
                        });

                        _incalibrate = false;
                        MainV2.comPort.UnSubscribeToPacketType(sub1);
                        MainV2.comPort.UnSubscribeToPacketType(sub2);
                    }
                    catch
                    {
                    }
                }
            }

            if (arg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.COMMAND_LONG)
            {
                var message = arg.ToStructure<MAVLink.mavlink_command_long_t>();
                if (message.command == (ushort)MAVLink.MAV_CMD.ACCELCAL_VEHICLE_POS)
                {
                    pos = (MAVLink.ACCELCAL_VEHICLE_POS)message.param1;

                    UpdateUserMessage("Please place vehicle " + pos.ToString());
                }
            }

            return true;
        }

        public void UpdateUserMessage(string message)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            Action update = delegate
            {
                string poseText;
                var translated = TranslateAccelMessage(message, out poseText);
                var chinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh",
                    StringComparison.OrdinalIgnoreCase);
                lbl_Accel_user.Text = chinese ? translated : (message ?? string.Empty).Trim('\0');
                if (!string.IsNullOrEmpty(poseText))
                {
                    fmtAccelPromptTitle.Text = chinese
                        ? "目前姿態：" + GetAccelPoseShortText(poseText)
                        : "Current pose: " + poseText;
                    var oldImage = fmtAccelPromptImage.Image;
                    fmtAccelPromptImage.Image = CreateAccelPromptImage(poseText);
                    if (oldImage != null)
                        oldImage.Dispose();
                }
            };

            if (InvokeRequired)
                BeginInvoke((MethodInvoker)delegate { update(); });
            else
                update();
        }

        private void BUT_level_Click(object sender, EventArgs e)
        {
            try
            {
                Log.Info("Sending level command (mavlink 1.0)");
                if (MainV2.comPort.doCommand((byte) MainV2.comPort.sysidcurrent, (byte) MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 2, 0, 0))
                {
                    BUT_level.Text = Strings.Completed;
                }
                else
                {
                    CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception on level", ex);
                CustomMessageBox.Show("水平姿態校準失敗：" + ex.Message, "加速度計校準");
            }
        }

        private void BUT_simpleAccelCal_Click(object sender, EventArgs e)
        {
            try
            {
                Log.Info("Sending simple accelerometer calibration command (mavlink 1.0)");
                if (MainV2.comPort.doCommand((byte) MainV2.comPort.sysidcurrent, (byte) MainV2.comPort.compidcurrent,
                    MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 4, 0, 0))
                {
                    BUT_simpleAccelCal.Text = Strings.Completed;
                }
                else
                {
                    CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception on simple accelerometer calibration", ex);
                CustomMessageBox.Show("快速水平校準失敗：" + ex.Message, "加速度計校準");
            }
        }
    }
}
