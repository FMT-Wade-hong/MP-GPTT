using log4net;
using MissionPlanner.Controls;
using System;
using System.Collections.Generic;
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
        private static readonly string[] FmtAccelPoses =
        {
            "水平", "左側", "右側", "機頭向下", "機頭向上", "倒置"
        };
        private readonly Dictionary<string, Panel> fmtAccelPosePanels = new Dictionary<string, Panel>();
        private readonly Dictionary<string, Label> fmtAccelPoseLabels = new Dictionary<string, Label>();
        private readonly HashSet<string> fmtAccelCompletedPoses = new HashSet<string>();
        private string fmtAccelCurrentPose;

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
                Dock = DockStyle.None,
                Location = new Point(8, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 6,
                MinimumSize = new Size(800, 0),
                MaximumSize = new Size(800, 0)
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
            fmtAccelPosePanels.Clear();
            fmtAccelPoseLabels.Clear();
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Margin = new Padding(0, 2, 0, 0),
                BackColor = Color.FromArgb(22, 43, 53),
                Size = new Size(776, 246),
                MinimumSize = new Size(776, 246),
                MaximumSize = new Size(776, 246)
            };
            var promptTitle = new Label
            {
                Name = "FmtAccelPromptTitle",
                Text = chinese ? "校準姿態提示" : "Calibration pose prompt",
                Location = new Point(12, 8),
                Size = new Size(145, 32),
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(61, 196, 235),
                TextAlign = ContentAlignment.MiddleLeft
            };
            lbl_Accel_user.AutoSize = false;
            lbl_Accel_user.Dock = DockStyle.None;
            lbl_Accel_user.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            lbl_Accel_user.Location = new Point(158, 8);
            lbl_Accel_user.Size = new Size(602, 34);
            lbl_Accel_user.TextAlign = ContentAlignment.MiddleLeft;

            var grid = new TableLayoutPanel
            {
                Name = "FmtAccelPoseGrid",
                Location = new Point(8, 45),
                Size = new Size(760, 193),
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (var column = 0; column < 3; column++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            for (var index = 0; index < FmtAccelPoses.Length; index++)
            {
                var pose = FmtAccelPoses[index];
                var poseCard = new Panel
                {
                    Name = "FmtAccelPose" + index,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(4),
                    Padding = new Padding(3),
                    BackColor = Color.FromArgb(105, 112, 118)
                };
                var poseLabel = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 25,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.FromArgb(42, 47, 51),
                    ForeColor = Color.White,
                    Font = new Font("Microsoft JhengHei UI", 8.5F, FontStyle.Bold)
                };
                var poseImage = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = CreateAccelPoseImage(pose),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    BackColor = Color.FromArgb(42, 47, 51)
                };
                poseCard.Controls.Add(poseImage);
                poseCard.Controls.Add(poseLabel);
                poseLabel.BringToFront();
                fmtAccelPosePanels[pose] = poseCard;
                fmtAccelPoseLabels[pose] = poseLabel;
                grid.Controls.Add(poseCard, index % 3, index / 3);
            }

            panel.Controls.Add(grid);
            panel.Controls.Add(promptTitle);
            panel.Controls.Add(lbl_Accel_user);
            promptTitle.BringToFront();
            lbl_Accel_user.BringToFront();
            UpdateAccelPoseCards();
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
                MinimumSize = new Size(776, 82),
                MaximumSize = new Size(776, 0)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));

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
            description.MaximumSize = new Size(520, 0);
            description.Margin = Padding.Empty;
            button.Dock = DockStyle.Fill;
            button.MinimumSize = new Size(132, 34);
            button.Margin = new Padding(8, 6, 0, 6);

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

        private static Image CreateAccelPoseImage(string pose)
        {
            var image = new Bitmap(170, 58);
            using (var g = Graphics.FromImage(image))
            using (var bodyBrush = new SolidBrush(Color.WhiteSmoke))
            using (var bodyPen = new Pen(Color.FromArgb(105, 210, 235), 1.5F))
            using (var floorPen = new Pen(Color.FromArgb(87, 98, 105), 1F))
            using (var accentBrush = new SolidBrush(Color.FromArgb(61, 196, 235)))
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

                g.DrawPolygon(floorPen, new[]
                {
                    new Point(24, 42), new Point(65, 16), new Point(146, 16), new Point(112, 50)
                });
                g.DrawLine(floorPen, 24, 42, 112, 42);
                g.DrawLine(floorPen, 65, 16, 65, 42);

                var state = g.Save();
                g.TranslateTransform(85F, 29F);
                g.RotateTransform(angle);
                var aircraft = new[]
                {
                    new Point(0, -23), new Point(-4, -12), new Point(-5, -3),
                    new Point(-29, 7), new Point(-29, 12), new Point(-5, 8),
                    new Point(-4, 18), new Point(-13, 23), new Point(-13, 26),
                    new Point(0, 23), new Point(13, 26), new Point(13, 23),
                    new Point(4, 18), new Point(5, 8), new Point(29, 12),
                    new Point(29, 7), new Point(5, -3), new Point(4, -12)
                };
                g.FillPolygon(bodyBrush, aircraft);
                g.DrawPolygon(bodyPen, aircraft);
                g.FillPolygon(accentBrush, new[]
                {
                    new Point(0, -26), new Point(-5, -17), new Point(5, -17)
                });
                if (normalized.Contains("倒置") || normalized.Contains("背面"))
                    g.FillEllipse(accentBrush, -4, -3, 8, 8);
                g.Restore(state);
            }
            return image;
        }

        private void ResetAccelPoseCards()
        {
            fmtAccelCompletedPoses.Clear();
            fmtAccelCurrentPose = null;
            UpdateAccelPoseCards();
        }

        private void SetAccelCurrentPose(string pose)
        {
            if (!string.IsNullOrEmpty(fmtAccelCurrentPose) &&
                !string.Equals(fmtAccelCurrentPose, pose, StringComparison.Ordinal))
                fmtAccelCompletedPoses.Add(fmtAccelCurrentPose);

            fmtAccelCurrentPose = pose;
            UpdateAccelPoseCards();
        }

        private void CompleteAccelCalibration()
        {
            foreach (var pose in FmtAccelPoses)
                fmtAccelCompletedPoses.Add(pose);
            fmtAccelCurrentPose = null;
            UpdateAccelPoseCards();
        }

        private void UpdateAccelPoseCards()
        {
            var chinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            foreach (var pose in FmtAccelPoses)
            {
                Panel panel;
                Label label;
                if (!fmtAccelPosePanels.TryGetValue(pose, out panel) ||
                    !fmtAccelPoseLabels.TryGetValue(pose, out label))
                    continue;

                var completed = fmtAccelCompletedPoses.Contains(pose);
                var current = string.Equals(fmtAccelCurrentPose, pose, StringComparison.Ordinal);
                panel.BackColor = completed
                    ? Color.FromArgb(35, 170, 78)
                    : current
                        ? Color.FromArgb(245, 196, 24)
                        : Color.FromArgb(105, 112, 118);

                var status = completed
                    ? (chinese ? "已完成" : "Completed")
                    : current
                        ? (chinese ? "請擺放" : "Place now")
                        : (chinese ? "尚未校正" : "Not calibrated");
                label.Text = GetAccelPoseShortText(pose) + "｜" + status;
                label.ForeColor = current
                    ? Color.FromArgb(255, 221, 70)
                    : completed
                        ? Color.FromArgb(103, 235, 139)
                        : Color.Gainsboro;
            }
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
            ResetAccelPoseCards();
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
                    ResetAccelPoseCards();

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
                var normalized = (message ?? string.Empty).Trim('\0', ' ', '\r', '\n').ToLowerInvariant();
                if (normalized.Contains("calibration successful") || normalized.Contains("calibration complete"))
                    CompleteAccelCalibration();
                else if (normalized.Contains("calibration failed") || normalized.Contains("calibration cancelled") ||
                         normalized.Contains("calibration canceled"))
                {
                    fmtAccelCurrentPose = null;
                    UpdateAccelPoseCards();
                }
                else if (!string.IsNullOrEmpty(poseText))
                    SetAccelCurrentPose(poseText);
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
