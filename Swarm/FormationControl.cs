using MissionPlanner.Utilities;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GeoAPI.CoordinateSystems;
using GeoAPI.CoordinateSystems.Transformations;

namespace MissionPlanner.Swarm
{
    public partial class FormationControl : Form
    {
        Formation SwarmInterface = null;
        volatile bool threadrun = false;
        private System.Threading.Thread worker;
        internal bool IsRunning => threadrun || (worker != null && worker.IsAlive);

        public FormationControl()
        {
            InitializeComponent();
            ApplyChineseLayout();
            Disposed += (s, e) => { threadrun = false; };

            SwarmInterface = new Formation();

            TopMost = false;

            Dictionary<String, MAVState> mavStates = new Dictionary<string, MAVState>();

            foreach (var port in MainV2.Comports)
            {
                foreach (var mav in port.MAVlist)
                {
                    mavStates.Add(port.BaseStream.PortName + " " + mav.sysid + " " + mav.compid, mav);
                }
            }

            if (mavStates.Count == 0)
                return;

            bindingSource1.DataSource = mavStates;

            CMB_mavs.DataSource = bindingSource1;
            CMB_mavs.ValueMember = "Value";
            CMB_mavs.DisplayMember = "Key";

            updateicons();

            this.MouseWheel += new MouseEventHandler(FollowLeaderControl_MouseWheel);

            MessageBox.Show("群飛編隊為實驗性功能，請先在 SITL 驗證。控制按鈕可能影響多台機體。", "群飛控制提醒");

            MissionPlanner.Utilities.Tracking.AddPage(this.GetType().ToString(), this.Text);
        }

        void FollowLeaderControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta < 0)
            {
                grid1.setScale(grid1.getScale() + 4);
            }
            else
            {
                grid1.setScale(grid1.getScale() - 4);
            }
        }

        void updateicons()
        {
            bindingSource1.ResetBindings(false);

            foreach (var port in MainV2.Comports)
            {
                foreach (var mav in port.MAVlist)
                {
                    if (mav == SwarmInterface.getLeader())
                    {
                        ((Formation)SwarmInterface).setOffsets(mav, 0, 0, 0);
                        var vector = SwarmInterface.getOffsets(mav);
                        grid1.UpdateIcon(mav, (float)vector.x, (float)vector.y, (float)vector.z, false);
                    }
                    else
                    {
                        var vector = SwarmInterface.getOffsets(mav);
                        grid1.UpdateIcon(mav, (float)vector.x, (float)vector.y, (float)vector.z, true);
                    }
                }
            }
            grid1.Invalidate();
        }

        private void CMB_mavs_SelectedIndexChanged(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                foreach (var mav in port.MAVlist)
                {
                    if (mav == CMB_mavs.SelectedValue)
                    {
                        MainV2.comPort = port;
                        port.sysidcurrent = mav.sysid;
                        port.compidcurrent = mav.compid;
                    }
                }
            }
        }

        private void BUT_Start_Click(object sender, EventArgs e)
        {
            if (threadrun == true)
            {
                threadrun = false;
                BUT_Start.Text = "開始編隊跟隨";
                return;
            }

            if (SwarmInterface != null)
            {
                if (IsRunning) return;
                if (SwarmInterface.Leader == null) { MessageBox.Show("請先設定領機。"); return; }
                threadrun = true;
                worker = new System.Threading.Thread(mainloop) { IsBackground = true };
                worker.Start();
                BUT_Start.Text = "停止發送跟隨";
            }
        }

        void mainloop()
        {
            // make sure leader is high freq updates
            SwarmInterface.Leader.parent.requestDatastream(MAVLink.MAV_DATA_STREAM.POSITION, 10, SwarmInterface.Leader.sysid, SwarmInterface.Leader.compid);
            SwarmInterface.Leader.cs.rateposition = 10;
            SwarmInterface.Leader.cs.rateattitude = 10;

            while (threadrun && !this.IsDisposed)
            {
                // update leader pos
                SwarmInterface.Update();

                // update other mavs
                SwarmInterface.SendCommand();

                // 10 hz
                System.Threading.Thread.Sleep(100);
            }
        }

        private void BUT_Arm_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.Arm();
            }
        }

        private void BUT_Disarm_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.Disarm();
            }
        }

        private void BUT_Takeoff_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.Takeoff();
            }
        }

        private void BUT_Land_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.Land();
            }
        }

        private void BUT_leader_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                var vectorlead = SwarmInterface.getOffsets(MainV2.comPort.MAV);

                foreach (var port in MainV2.Comports)
                {
                    foreach (var mav in port.MAVlist)
                    {
                        var vector = SwarmInterface.getOffsets(mav);

                        SwarmInterface.setOffsets(mav, (float)(vector.x - vectorlead.x),
                            (float)(vector.y - vectorlead.y),
                            (float)(vector.z - vectorlead.z));
                    }
                }

                SwarmInterface.setLeader(MainV2.comPort.MAV);
                updateicons();
                BUT_Start.Enabled = true;
                BUT_Updatepos.Enabled = true;
            }
        }

        private void BUT_connect_Click(object sender, EventArgs e)
        {
            Comms.CommsSerialScan.Scan(true);

            DateTime deadline = DateTime.Now.AddSeconds(50);

            while (Comms.CommsSerialScan.foundport == false)
            {
                System.Threading.Thread.Sleep(100);

                if (DateTime.Now > deadline)
                {
                    CustomMessageBox.Show("搜尋逾時，未找到 MAVLink 裝置。");
                    return;
                }
            }

            bindingSource1.ResetBindings(false);
        }

        public Vector3 getOffsetFromLeader(MAVState leader, MAVState mav)
        {
            //convert Wgs84ConversionInfo to utm
            CoordinateTransformationFactory ctfac = new CoordinateTransformationFactory();

            IGeographicCoordinateSystem wgs84 = GeographicCoordinateSystem.WGS84;

            int utmzone = (int)((leader.cs.lng - -186.0) / 6.0);

            IProjectedCoordinateSystem utm = ProjectedCoordinateSystem.WGS84_UTM(utmzone,
                leader.cs.lat < 0 ? false : true);

            ICoordinateTransformation trans = ctfac.CreateFromCoordinateSystems(wgs84, utm);

            double[] masterpll = { leader.cs.lng, leader.cs.lat };

            // get leader utm coords
            double[] masterutm = trans.MathTransform.Transform(masterpll);

            double[] mavpll = { mav.cs.lng, mav.cs.lat };

            //getLeader follower utm coords
            double[] mavutm = trans.MathTransform.Transform(mavpll);

            var heading = -leader.cs.yaw;

            var norotation = new Vector3(masterutm[1] - mavutm[1], masterutm[0] - mavutm[0], 0);

            norotation.x *= -1;
            norotation.y *= -1;

            return new Vector3(norotation.x * Math.Cos(heading * MathHelper.deg2rad) - norotation.y * Math.Sin(heading * MathHelper.deg2rad), norotation.x * Math.Sin(heading * MathHelper.deg2rad) + norotation.y * Math.Cos(heading * MathHelper.deg2rad), 0);
        }

        private void grid1_UpdateOffsets(MAVState mav, float x, float y, float z, Grid.icon ico)
        {
            if (mav == SwarmInterface.Leader)
            {
                CustomMessageBox.Show("領機是編隊基準，無法拖曳領機偏移。");
                ico.z = 0;
            }
            else
            {
                ((Formation)SwarmInterface).setOffsets(mav, x, y, z);
            }
        }

        private void Control_FormClosing(object sender, FormClosingEventArgs e)
        {
            threadrun = false;
        }

        private void BUT_Updatepos_Click(object sender, EventArgs e)
        {
            foreach (var port in MainV2.Comports)
            {
                foreach (var mav in port.MAVlist)
                {
                    mav.cs.UpdateCurrentSettings(null, true, port, mav);

                    if (mav == SwarmInterface.Leader)
                        continue;

                    Vector3 offset = getOffsetFromLeader(((Formation)SwarmInterface).getLeader(), mav);

                    if (Math.Abs(offset.x) < 200 && Math.Abs(offset.y) < 200)
                    {
                        grid1.UpdateIcon(mav, (float)offset.y, (float)offset.x, (float)offset.z, true);
                        ((Formation)SwarmInterface).setOffsets(mav, offset.y, offset.x, offset.z);
                    }
                }
            }
        }

        private void timer_status_Tick(object sender, EventArgs e)
        {
            // clean up old
            foreach (Control ctl in PNL_status.Controls)
            {
                bool match = false;
                foreach (var port in MainV2.Comports)
                {
                    foreach (var mav in port.MAVlist)
                    {
                        if (mav == (MAVState)ctl.Tag)
                        {
                            match = true;

                        }
                    }
                }

                if (match == false)
                    ctl.Dispose();
            }

            // setup new
            foreach (var port in MainV2.Comports)
            {
                foreach (var mav in port.MAVlist)
                {
                    bool exists = false;
                    foreach (Control ctl in PNL_status.Controls)
                    {
                        if (ctl is Status && ctl.Tag == mav)
                        {
                            exists = true;
                            ((Status)ctl).GPS.Text = mav.cs.gpsstatus >= 3 ? "已定位" : "定位不足";
                            ((Status)ctl).Armed.Text = mav.cs.armed ? "已解鎖" : "未解鎖";
                            ((Status)ctl).Mode.Text = ChineseMode(mav.cs.mode);
                            ((Status)ctl).MAV.Text = (mav == SwarmInterface.Leader ? "領機 " : "機體 ") + mav.sysid + " / " + mav.compid;
                            ((Status)ctl).Speed.Text = mav.cs.groundspeed.ToString("0.0") + " m/s";
                            ((Status)ctl).Guided.Text = mav.GuidedMode.x / 1e7 + "," + mav.GuidedMode.y / 1e7 + "," +
                                                         mav.GuidedMode.z;
                            ((Status)ctl).Location1.Text = mav.cs.lat + "," + mav.cs.lng + "," +
                                                            mav.cs.alt;

                            if (mav == SwarmInterface.Leader)
                            {
                                ((Status)ctl).ForeColor = Color.FromArgb(255, 193, 80);
                            }
                            else
                            {
                                ((Status)ctl).ForeColor = Color.WhiteSmoke;
                            }
                        }
                    }

                    if (!exists)
                    {
                        Status newstatus = new Status();
                        newstatus.ApplyFormationLayout();
                        newstatus.Tag = mav;
                        PNL_status.Controls.Add(newstatus);
                    }
                }
            }
        }

        private void but_guided_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.GuidedMode();
            }
        }

        internal static string ChineseMode(string mode)
        {
            switch ((mode ?? "").ToUpperInvariant())
            {
                case "STABILIZE": return "自穩（Stabilize）";
                case "GUIDED": return "導引（Guided）";
                case "AUTO": return "自動任務（Auto）";
                case "LOITER": return "定點（Loiter）";
                case "ALTHOLD": return "定高（AltHold）";
                case "RTL": return "返航（RTL）";
                case "LAND": return "降落（Land）";
                case "MANUAL": return "手動（Manual）";
                case "HOLD": return "保持（Hold）";
                default: return mode;
            }
        }

        private void ApplyChineseLayout()
        {
            SuspendLayout();
            Text = "群飛編隊控制（實驗性）";
            Font = new Font("Microsoft JhengHei UI", 9F);
            ClientSize = new Size(1160, 740);
            MinimumSize = new Size(960, 650);
            BackColor = Color.FromArgb(18, 34, 43);
            ForeColor = Color.WhiteSmoke;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
            for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { AutoSize = true, Text = "群飛編隊控制  ｜  實驗性功能，請先使用 SITL 驗證", Font = new Font(Font, FontStyle.Bold) }, 0, 0);
            var selection = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            CMB_mavs.Width = 230;
            CMB_mavs.DropDownStyle = ComboBoxStyle.DropDownList;
            BUT_leader.Text = "設為領機";
            BUT_Updatepos.Text = "依目前位置更新偏移";
            BUT_Start.Text = "開始編隊跟隨";
            selection.Controls.Add(new Label { Text = "目前機體", AutoSize = true, Margin = new Padding(3, 12, 6, 3) });
            selection.Controls.AddRange(new Control[] { CMB_mavs, BUT_leader, BUT_Updatepos, BUT_Start });
            layout.Controls.Add(selection, 0, 1);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            BUT_Arm.Text = "解鎖（不含領機）";
            BUT_Disarm.Text = "鎖定（不含領機）";
            BUT_Takeoff.Text = "起飛 5 m（不含領機）";
            BUT_Land.Text = "全部降落（含領機）";
            but_guided.Text = "導引（不含領機）";
            but_auto.Text = "自動任務（不含領機）";
            actions.Controls.AddRange(new Control[] { BUT_Arm, BUT_Disarm, BUT_Takeoff, BUT_Land, but_guided, but_auto });
            layout.Controls.Add(actions, 0, 2);
            foreach (var row in new[] { selection, actions })
                foreach (Control control in row.Controls)
                    if (control is Button button)
                    {
                        button.AutoSize = true;
                        button.MinimumSize = new Size(120, 36);
                        button.Padding = new Padding(8, 3, 8, 3);
                        button.Margin = new Padding(3, 4, 6, 4);
                        button.BackColor = Color.FromArgb(41, 171, 226);
                        button.ForeColor = Color.Black;
                        button.UseVisualStyleBackColor = false;
                    }
            BUT_Disarm.BackColor = BUT_Land.BackColor = Color.FromArgb(255, 174, 72);
            layout.Controls.Add(new Label
            {
                AutoSize = true, MaximumSize = new Size(1100, 0), Margin = new Padding(3, 8, 3, 8),
                ForeColor = Color.FromArgb(255, 193, 80),
                Text = "編隊執行中拖曳機體會改變跟隨目標；滾輪縮放。停止發送跟隨 ≠ 降落或鎖定。\r\n起飛／降落沿用原版指令，不代表已支援所有機型；座標是領機相對偏移。"
            }, 0, 3);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
            tabControl1.Dock = DockStyle.Fill;
            tabPage1.Text = "編隊偏移（公尺）";
            grid1.Dock = DockStyle.Fill;
            grid1.ApplyTraditionalChinese();
            body.Controls.Add(tabControl1, 0, 0);
            PNL_status.Dock = DockStyle.Fill;
            PNL_status.FlowDirection = FlowDirection.TopDown;
            PNL_status.WrapContents = false;
            var statusGroup = new GroupBox { Text = "機體狀態", Dock = DockStyle.Fill, ForeColor = Color.WhiteSmoke };
            statusGroup.Controls.Add(PNL_status);
            body.Controls.Add(statusGroup, 1, 0);
            layout.Controls.Add(body, 0, 4);
            Controls.Add(layout);
            ResumeLayout(true);
        }

        private void but_auto_Click(object sender, EventArgs e)
        {
            if (SwarmInterface != null)
            {
                SwarmInterface.AutoMode();
            }
        }
    }
}
