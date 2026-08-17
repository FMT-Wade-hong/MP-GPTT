using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using ZedGraph;

namespace MissionPlanner.Swarm.WaypointLeader
{
    public partial class WPControl : Form
    {
        DroneGroup DG = new DroneGroup();
        bool threadrun;
        readonly bool useTraditionalChinese = CultureInfo.CurrentUICulture.Name.StartsWith(
            "zh", StringComparison.OrdinalIgnoreCase);

        public WPControl()
        {
            InitializeComponent();
            ApplyFmtTraditionalChinese();
            ApplyFmtResponsiveLayout();

            zedGraphControl1.GraphPane.AddCurve(useTraditionalChinese ? "航徑" : "Path",
                DG.path_to_fly, Color.Red, SymbolType.None);
            zedGraphControl1.GraphPane.Title.Text = useTraditionalChinese ? "編隊航徑預覽" : "Path preview";
            zedGraphControl1.GraphPane.XAxis.Title.Text = useTraditionalChinese ? "航程距離（公尺）" : "Distance";
            zedGraphControl1.GraphPane.YAxis.Title.Text = useTraditionalChinese ? "高度（公尺）" : "Altitude";

            DG.Drones.Clear();
        }

        private void ApplyFmtResponsiveLayout()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            MinimumSize = new Size(1000, 700);
            ClientSize = new Size(Math.Max(ClientSize.Width, 1120), Math.Max(ClientSize.Height, 720));
            StartPosition = FormStartPosition.CenterParent;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10),
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 245F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8)
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var settingsGroup = new GroupBox
            {
                Text = useTraditionalChinese ? "編隊參數" : "Formation settings",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 8, 0)
            };
            var settings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Margin = Padding.Empty,
                Padding = new Padding(2)
            };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            for (var row = 0; row < 5; row++)
                settings.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            AddSettingRow(settings, 0, label1, numericUpDown1);
            AddSettingRow(settings, 1, label2, numericUpDown2);
            AddSettingRow(settings, 2, label3, num_useroffline);
            AddSettingRow(settings, 3, label4, num_rtl_alt);
            AddSettingRow(settings, 4, label5, num_wpnav_accel);
            settingsGroup.Controls.Add(settings);

            var actionsGroup = new GroupBox
            {
                Text = useTraditionalChinese ? "編隊操作" : "Formation actions",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 8, 0)
            };
            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Margin = Padding.Empty,
                Padding = new Padding(2)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

            txt_mode.AutoSize = false;
            txt_mode.Dock = DockStyle.Fill;
            txt_mode.TextAlign = ContentAlignment.MiddleCenter;
            txt_mode.Font = new Font(Font, FontStyle.Bold);
            txt_mode.Margin = new Padding(4, 0, 4, 3);
            actions.Controls.Add(txt_mode, 0, 0);
            actions.SetColumnSpan(txt_mode, 2);

            ConfigureActionButton(but_master);
            ConfigureActionButton(but_airmaster);
            ConfigureActionButton(but_start);
            ConfigureActionButton(but_resetmode);
            ConfigureActionButton(but_rth);
            ConfigureActionButton(but_setmoderltland);
            actions.Controls.Add(but_master, 0, 1);
            actions.Controls.Add(but_airmaster, 1, 1);
            actions.Controls.Add(but_start, 0, 2);
            actions.Controls.Add(but_resetmode, 1, 2);
            actions.Controls.Add(but_rth, 0, 3);
            actions.Controls.Add(but_setmoderltland, 1, 3);

            chk_V.AutoSize = false;
            chk_V.Dock = DockStyle.Fill;
            chk_V.Margin = new Padding(6, 4, 4, 2);
            chk_alt_interleave.AutoSize = false;
            chk_alt_interleave.Dock = DockStyle.Fill;
            chk_alt_interleave.Margin = new Padding(6, 4, 4, 2);
            actions.Controls.Add(chk_V, 0, 4);
            actions.Controls.Add(chk_alt_interleave, 1, 4);
            actionsGroup.Controls.Add(actions);

            var instructionsGroup = new GroupBox
            {
                Text = useTraditionalChinese ? "操作說明" : "Instructions",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Margin = Padding.Empty
            };
            textBox1.Dock = DockStyle.Fill;
            textBox1.Margin = Padding.Empty;
            textBox1.Multiline = true;
            textBox1.WordWrap = true;
            textBox1.ScrollBars = ScrollBars.Vertical;
            textBox1.Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            instructionsGroup.Controls.Add(textBox1);

            top.Controls.Add(settingsGroup, 0, 0);
            top.Controls.Add(actionsGroup, 1, 0);
            top.Controls.Add(instructionsGroup, 2, 0);

            var statusGroup = new GroupBox
            {
                Text = useTraditionalChinese ? "飛行器狀態" : "Vehicle status",
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                Margin = new Padding(0, 0, 0, 8)
            };
            PNL_status.Dock = DockStyle.Fill;
            PNL_status.Margin = Padding.Empty;
            PNL_status.AutoScroll = true;
            PNL_status.WrapContents = true;
            statusGroup.Controls.Add(PNL_status);

            zedGraphControl1.Dock = DockStyle.Fill;
            zedGraphControl1.Margin = Padding.Empty;

            root.Controls.Add(top, 0, 0);
            root.Controls.Add(statusGroup, 0, 1);
            root.Controls.Add(zedGraphControl1, 0, 2);

            Controls.Clear();
            Controls.Add(root);
            ResumeLayout(true);
        }

        private static void AddSettingRow(TableLayoutPanel layout, int row, System.Windows.Forms.Label label,
            NumericUpDown value)
        {
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(4, 3, 8, 3);
            value.Dock = DockStyle.Fill;
            value.Margin = new Padding(4, 7, 4, 7);
            value.MinimumSize = new Size(90, 0);
            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(value, 1, row);
        }

        private static void ConfigureActionButton(Control button)
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4);
            button.MinimumSize = new Size(0, 34);
        }

        private void ApplyFmtTraditionalChinese()
        {
            if (!useTraditionalChinese)
                return;

            Text = "航點編隊控制";
            but_master.Text = "設定地面主機";
            but_airmaster.Text = "設定空中領航機";
            but_start.Text = "開始";
            but_resetmode.Text = "重設模式";
            but_rth.Text = "切換返航";
            but_setmoderltland.Text = "返航並降落\r\n（放棄任務）";
            label1.Text = "隊形間距（公尺）";
            label2.Text = "領航提前量（公尺）";
            label3.Text = "離線觸發距離（公尺）";
            label4.Text = "高度間距（公尺）";
            label5.Text = "航點加速度（m/s²）";
            chk_V.Text = "V 字隊形";
            chk_alt_interleave.Text = "高度交錯排列";
            txt_mode.Text = "待命";
            textBox1.Text =
                "操作方式：\r\n" +
                "1. 先連線所有飛行器（包含地面主機與空中領航機）。\r\n" +
                "2. 選取地面載具，按下「設定地面主機」。\r\n" +
                "3. 選取空中載具，按下「設定空中領航機」。\r\n" +
                "4. 將任務航徑上傳至空中領航機。\r\n" +
                "5. 設定隊形間距、領航提前量及高度交錯等選項。\r\n" +
                "6. 等待所有飛行器取得 GPS 定位並完成校正。\r\n" +
                "7. 確認地面主機已就緒，並等待航徑開始。\r\n" +
                "8. 按下「開始」。";

            toolTip1.SetToolTip(numericUpDown1, "飛行器在空中的橫向間距。");
            toolTip1.SetToolTip(numericUpDown2, "空中領航機相對地面主機的提前距離。");
            toolTip1.SetToolTip(num_useroffline, "超過此偏離距離時，觸發編隊返航保護。");
            toolTip1.SetToolTip(num_rtl_alt, "起飛及降落時各飛行器的高度間距。");
            toolTip1.SetToolTip(num_wpnav_accel, "編隊使用的航點導航加速度。");
            toolTip1.SetToolTip(PNL_status, "顯示所有已連線飛行器的狀態。");
            toolTip1.SetToolTip(but_master, "將目前選取的載具設為地面主機。");
            toolTip1.SetToolTip(but_airmaster, "將目前選取的載具設為空中領航機。");
            toolTip1.SetToolTip(but_start, "開始或停止向編隊傳送控制命令。");
            toolTip1.SetToolTip(but_resetmode, "清除內部狀態並回到待命模式。");
            toolTip1.SetToolTip(but_rth, "命令編隊切換為返航模式。");
            toolTip1.SetToolTip(but_setmoderltland, "放棄目前任務，命令編隊返航並降落。");
        }

        private void but_master_Click(object sender, EventArgs e)
        {
            DG.groundmaster = MainV2.comPort.MAV;

            DG.Drones.Clear();

            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    DG.Drones.Add(new Drone() { MavState = MAV });
                }
            }
        }

        private void but_arm_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    MAV.parent.doARM(MAV.sysid, MAV.compid, true);
                }
            }
        }

        private void but_takeoff_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    MAV.parent.setMode(MAV.sysid, MAV.compid, "GUIDED");

                    MAV.parent.doCommand(MAV.sysid, MAV.compid, MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, 5);
                }
            }
        }

        private void but_auto_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    MAV.parent.setMode(MAV.sysid, MAV.compid, "AUTO");
                }
            }
        }

        private void but_start_Click(object sender, EventArgs e)
        {
            if (threadrun == true)
            {
                threadrun = false;
                but_start.Text = useTraditionalChinese ? "開始" : Strings.Start;
                return;
            }

            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    if (MAV.cs.armed && MAV.cs.alt > 1)
                    {
                        var result = CustomMessageBox.Show(useTraditionalChinese
                                ? "偵測到目前有飛行器正在空中，確定要繼續嗎？"
                                : "There appears to be a drone in the air at the moment. Are you sure you want to continue?",
                            useTraditionalChinese ? "確認操作" : "continue", MessageBoxButtons.YesNo);
                        if (result == (int)DialogResult.Yes)
                            break;
                        return;
                    }
                }
            }

            zedGraphControl1.AxisChange();

            //if (SwarmInterface != null)
            {
                new System.Threading.Thread(mainloop) { IsBackground = true }.Start();
                but_start.Text = useTraditionalChinese ? "停止" : Strings.Stop;
            }
        }

        private void mainloop()
        {
            threadrun = true;

            while (threadrun)
            {
                DG.UpdatePositions();

                System.Threading.Thread.Sleep(100);
            }
        }

        private void but_guided_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    MAV.parent.setMode(MAV.sysid, MAV.compid, "GUIDED");
                }
            }
        }

        private void but_navguided_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    MAV.parent.doCommand(MAV.sysid, MAV.compid, MAVLink.MAV_CMD.GUIDED_ENABLE, 1, 0, 0, 0, 0, 0, 0);
                }
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            DG.Seperation = (double)numericUpDown1.Value;
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            DG.Lead = (double)numericUpDown2.Value;
        }

        private void but_airmaster_Click(object sender, EventArgs e)
        {
            DG.airmaster = MainV2.comPort.MAV;

            DG.Drones.Clear();

            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    DG.Drones.Add(new Drone() { MavState = MAV });
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            txt_mode.Text = useTraditionalChinese ? GetTraditionalModeName(DG.CurrentMode) : DG.CurrentMode.ToString();

            // clean up old
            foreach (Control ctl in PNL_status.Controls)
            {
                var found = false;
                foreach (var port in MainV2.Comports)
                {
                    foreach (var MAV in port.MAVlist)
                    {
                        if (ctl.Tag == MAV)
                        {
                            found = true;
                        }
                    }
                }

                if (!found)
                    ctl.Dispose();
            }

            // setup new
            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    bool exists = false;
                    foreach (Control ctl in PNL_status.Controls)
                    {
                        if (ctl is Status && ctl.Tag == MAV)
                        {
                            exists = true;
                            if (MAV.cs.gpsstatus < 3)
                            {
                                ((Status)ctl).GPS.Text = useTraditionalChinese ? "定位不良" : "Bad";
                            }
                            else if (MAV.cs.gpsstatus >= 3)
                            {
                                ((Status)ctl).GPS.Text = "OK " + Math.Max(MAV.cs.gpsstatus, MAV.cs.gpsstatus2);
                            }
                            ((Status)ctl).Armed.Text = useTraditionalChinese
                                ? (MAV.cs.armed ? "已解鎖" : "已上鎖")
                                : MAV.cs.armed.ToString();
                            ((Status)ctl).Mode.Text = MAV.cs.mode;
                            ((Status)ctl).MAV.Text = String.Format("MAV {0}-{1}", MAV.sysid, MAV.compid);
                            ((Status)ctl).Guided.Text = MAV.GuidedMode.x / 1e7 + "," + MAV.GuidedMode.y / 1e7 + "," +
                                                         MAV.GuidedMode.z;
                            ((Status)ctl).Location1.Text = MAV.cs.lat.ToString("0.00000") + "," + MAV.cs.lng.ToString("0.00000") + "," +
                                                      MAV.cs.alt;
                            ((Status)ctl).Speed.Text = MAV.cs.groundspeed.ToString("0.00");

                            if (MAV == DG.airmaster)
                                ((Status)ctl).MAV.Text = String.Format(useTraditionalChinese
                                    ? "MAV {0}-{1} 空中領航機" : "MAV {0}-{1} airmaster", MAV.sysid, MAV.compid);

                            if (MAV == DG.groundmaster)
                                ((Status)ctl).MAV.Text = String.Format(useTraditionalChinese
                                    ? "MAV {0}-{1} 地面主機" : "MAV {0}-{1} groundmaster", MAV.sysid, MAV.compid);

                            if (MAV == DG.airmaster || MAV == DG.groundmaster)
                            {
                                ((Status)ctl).ForeColor = Color.Red;
                            }
                            else
                            {
                                ((Status)ctl).ForeColor = Color.Black;
                            }
                        }
                    }

                    if (!exists)
                    {
                        Status newstatus = new Status();
                        if (useTraditionalChinese)
                            newstatus.ApplyTraditionalChinese();
                        newstatus.Tag = MAV;
                        PNL_status.Controls.Add(newstatus);
                    }
                }
            }

            foreach (var drone in DG.Drones)
            {
                // check if curve exists
                if (zedGraphControl1.GraphPane.CurveList["MAV " + drone.MavState.sysid.ToString()] == null)
                {
                    if (drone.Location != null)
                    {
                        // create the curve
                        zedGraphControl1.GraphPane.CurveList.Add(
                            new LineItem("MAV " + drone.MavState.sysid.ToString(),
                                new PointPairList(new[] { (double)drone.PathIndex }, new[] { drone.Location.Alt }),
                                colours[zedGraphControl1.GraphPane.CurveList.Count % colours.Length],
                                SymbolType.Triangle));

                        zedGraphControl1.ZoomOutAll(zedGraphControl1.GraphPane);
                    }
                }
                else
                {
                    // update the curve
                    var curve = zedGraphControl1.GraphPane.CurveList["MAV " + drone.MavState.sysid.ToString()];
                    curve.Clear();
                    try
                    {
                        curve.AddPoint((double)(drone.PathIndex * 0.1), drone.Location.Alt);
                    }
                    catch { }
                }
            }

            zedGraphControl1.Invalidate();
        }

        Color[] colours = new Color[]
        {
            Color.Red,
            Color.Green,
            Color.Blue,
            Color.Orange,
            Color.Yellow,
            Color.Violet,
            Color.Pink,
            Color.Teal,
            Color.Wheat,
            Color.Silver,
            Color.Purple,
            Color.Aqua,
            Color.Brown,
            Color.WhiteSmoke
        };

        private void but_resetmode_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                foreach (var MAV in port.MAVlist)
                {
                    if (MAV.cs.armed && MAV.cs.alt > 1)
                    {
                        var result = CustomMessageBox.Show(useTraditionalChinese
                                ? "偵測到目前有飛行器正在空中，確定要重設模式嗎？"
                                : "There appears to be a drone in the air at the moment. Are you sure you want to continue?",
                            useTraditionalChinese ? "確認操作" : "continue", MessageBoxButtons.YesNo);
                        if (result == (int)DialogResult.Yes)
                            break;
                        return;
                    }
                }
            }

            DG.CurrentMode = DroneGroup.Mode.idle;
        }

        private static string GetTraditionalModeName(DroneGroup.Mode mode)
        {
            switch (mode)
            {
                case DroneGroup.Mode.idle: return "待命";
                case DroneGroup.Mode.RTH: return "返航";
                case DroneGroup.Mode.LandAlt: return "返航並降落";
                default: return mode.ToString();
            }
        }

        private void but_rth_Click(object sender, EventArgs e)
        {
            DG.CurrentMode = DroneGroup.Mode.RTH;
        }

        private void num_useroffline_ValueChanged(object sender, EventArgs e)
        {
            DG.OffPathTrigger = (double)num_useroffline.Value;
        }

        private void chk_V_CheckedChanged(object sender, EventArgs e)
        {
            DG.V = chk_V.Checked;
        }

        private void num_rtl_alt_ValueChanged(object sender, EventArgs e)
        {
            DG.Takeoff_Land_alt_sep = (double)num_rtl_alt.Value;
        }

        private void chk_alt_interleave_CheckedChanged(object sender, EventArgs e)
        {
            DG.AltInterleave = chk_alt_interleave.Checked;
        }

        private void WPControl_FormClosing(object sender, FormClosingEventArgs e)
        {
            threadrun = false;
            System.Threading.Thread.Sleep(500);
        }

        private void but_setmoderltland_Click(object sender, EventArgs e)
        {
            DG.CurrentMode = DroneGroup.Mode.LandAlt;
        }

        private void num_wpnav_accel_ValueChanged(object sender, EventArgs e)
        {
            DG.WPNAV_ACCEL = num_wpnav_accel.Value;
        }
    }
}
