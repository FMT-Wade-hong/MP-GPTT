using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Color = System.Drawing.Color;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigHWCompass2 : MyUserControl, IActivate, IDeactivate
    {
        private List<CompassDeviceInfo> list;

        private bool rebootrequired = false;

        private List<MAVLink.MAVLinkMessage> mprog = new List<MAVLink.MAVLinkMessage>();
        private List<MAVLink.MAVLinkMessage> mrep = new List<MAVLink.MAVLinkMessage>();

        private int packetsub1;
        private int packetsub2;

        public class CompassDeviceInfo : DeviceInfo
        {
            private string _orient;
            private bool _external;

            public CompassDeviceInfo(int index, string ParamName, uint id) : base(index, ParamName, id)
            {
                //set initial state
                var id1 = MainV2.comPort.MAV.param[new[] { "COMPASS_DEV_ID", "COMPASS1_DEV_ID"}];
                var id2 = MainV2.comPort.MAV.param[new[] { "COMPASS_DEV_ID2", "COMPASS2_DEV_ID"}];
                var id3 = MainV2.comPort.MAV.param[new[] { "COMPASS_DEV_ID3", "COMPASS3_DEV_ID"}];

                var idO1 = MainV2.comPort.MAV.param[new[] { "COMPASS_ORIENT", "COMPASS1_ORIENT"}];
                var idO2 = MainV2.comPort.MAV.param[new[] { "COMPASS_ORIENT2", "COMPASS2_ORIENT"}];
                var idO3 = MainV2.comPort.MAV.param[new[] { "COMPASS_ORIENT3", "COMPASS3_ORIENT"}];

                var idE1 = MainV2.comPort.MAV.param[new[] { "COMPASS_EXTERNAL", "COMPASS1_EXTERN"}];
                var idE2 = MainV2.comPort.MAV.param[new[] { "COMPASS_EXTERN2", "COMPASS2_EXTERN"}];
                var idE3 = MainV2.comPort.MAV.param[new[] { "COMPASS_EXTERN3", "COMPASS3_EXTERN"}];

                if (id1 != null && id1?.Value == id)
                {
                    _orient = idO1?.ToString();
                    _external = idE1?.Value > 0 ? true : false;
                }
                if (id2 != null && id2?.Value == id)
                {
                    _orient = idO2?.ToString();
                    _external = idE2?.Value > 0 ? true : false;
                }
                if (id3 != null && id3?.Value == id)
                {
                    _orient = idO3?.ToString();
                    _external = idE3?.Value > 0 ? true : false;
                }
            }

            public string Orient
            {
                get => _orient;
                set => _orient = value;
            }

            public bool External
            {
                get => _external;
                set => _external = value;
            }

            public bool Missing
            {
                get;
                set;
            }
        }

        public ConfigHWCompass2()
        {
            InitializeComponent();
            ApplyFmtCompassLayout();
        }

        private bool UseTraditionalChinese => CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        private void ApplyFmtCompassLayout()
        {
            SuspendLayout();
            AutoScroll = true;
            BackColor = Color.FromArgb(18, 28, 35);

            var zh = UseTraditionalChinese;
            label6.Text = zh ? "羅盤優先順序與校正" : "Compass priority and calibration";
            label1.Text = zh
                ? "依優先順序排列已偵測到的羅盤；第一列為最高優先。變更後請重新啟動飛控並重新校正。"
                : "Order detected compasses by priority. The first row has the highest priority. Reboot and recalibrate after changes.";
            label3.Text = zh ? "啟用要使用的羅盤：" : "Enable the compasses to use:";
            label4.Text = zh ? "變更羅盤配置後必須重新校正。" : "Calibration is required after changing compass configuration.";
            label5.Text = zh ? "變更優先順序後必須重新啟動飛控。" : "A reboot is required after changing priority.";
            but_missing.Text = zh ? "移除遺失羅盤" : "Remove missing";
            but_reboot.Text = zh ? "重新啟動飛控" : "Reboot autopilot";
            but_largemagcal.Text = zh ? "大型載具羅盤校正" : "Large vehicle calibration";
            mavlinkCheckBoxUseCompass1.Text = zh ? "使用羅盤 1" : "Use compass 1";
            mavlinkCheckBoxUseCompass2.Text = zh ? "使用羅盤 2" : "Use compass 2";
            mavlinkCheckBoxUseCompass3.Text = zh ? "使用羅盤 3" : "Use compass 3";
            CHK_compass_learn.Text = zh ? "自動學習偏移量" : "Automatically learn offsets";

            groupBoxonboardcalib.Text = zh ? "飛控內建羅盤校正" : "Onboard compass calibration";
            BUT_OBmagcalstart.Text = zh ? "開始校正" : "Start";
            BUT_OBmagcalaccept.Text = zh ? "接受結果" : "Accept";
            BUT_OBmagcalcancel.Text = zh ? "取消" : "Cancel";
            label7.Text = zh ? "羅盤 1" : "Compass 1";
            label8.Text = zh ? "羅盤 2" : "Compass 2";
            label9.Text = zh ? "羅盤 3" : "Compass 3";
            label10.Text = zh ? "容許誤差" : "Fitness";
            label2.Text = zh ? "校正失敗時可放寬容許誤差" : "Relax fitness if calibration fails";

            Priority.HeaderText = zh ? "優先" : "Priority";
            devIDDataGridViewTextBoxColumn.HeaderText = zh ? "裝置 ID" : "Device ID";
            busTypeDataGridViewTextBoxColumn.HeaderText = zh ? "匯流排類型" : "Bus type";
            busDataGridViewTextBoxColumn.HeaderText = zh ? "匯流排" : "Bus";
            addressDataGridViewTextBoxColumn.HeaderText = zh ? "位址" : "Address";
            devTypeDataGridViewTextBoxColumn.HeaderText = zh ? "感測器型號" : "Sensor type";
            Missing.HeaderText = zh ? "遺失" : "Missing";
            External.HeaderText = zh ? "外接" : "External";
            Orientation.HeaderText = zh ? "安裝方向" : "Orientation";
            Up.HeaderText = zh ? "上移" : "Up";
            Down.HeaderText = zh ? "下移" : "Down";

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = BackColor,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 225F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 39, 47), Margin = new Padding(0, 0, 0, 8) };
            var icon = new Panel { Location = new Point(12, 9), Size = new Size(42, 42), BackColor = Color.Transparent };
            icon.Paint += (sender, args) =>
            {
                args.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(45, 169, 220), 2F))
                using (var brush = new SolidBrush(Color.FromArgb(45, 169, 220)))
                {
                    args.Graphics.DrawEllipse(pen, 4, 4, 33, 33);
                    args.Graphics.DrawLine(pen, 20, 8, 20, 34);
                    args.Graphics.DrawLine(pen, 8, 21, 33, 21);
                    args.Graphics.FillPolygon(brush, new[] { new Point(20, 5), new Point(16, 18), new Point(24, 18) });
                }
            };
            header.Controls.Add(icon);
            label6.AutoSize = true;
            label6.Location = new Point(66, 8);
            label6.ForeColor = Color.White;
            label6.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12F, FontStyle.Bold);
            label1.AutoSize = false;
            label1.Location = new Point(67, 33);
            label1.Size = new Size(Math.Max(520, ClientSize.Width - 110), 28);
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label1.ForeColor = Color.Gainsboro;
            header.Controls.Add(label6);
            header.Controls.Add(label1);
            root.Controls.Add(header, 0, 0);

            myDataGridView1.Dock = DockStyle.Fill;
            myDataGridView1.Margin = new Padding(0, 0, 0, 8);
            myDataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            myDataGridView1.ColumnHeadersHeight = 30;
            myDataGridView1.RowTemplate.Height = 28;
            myDataGridView1.BackgroundColor = Color.FromArgb(22, 34, 42);
            root.Controls.Add(myDataGridView1, 0, 1);

            var options = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                BackColor = Color.FromArgb(27, 39, 47),
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 8)
            };
            label3.AutoSize = true;
            label3.Margin = new Padding(0, 7, 12, 0);
            options.Controls.Add(label3);
            foreach (var check in new Control[] { mavlinkCheckBoxUseCompass1, mavlinkCheckBoxUseCompass2, mavlinkCheckBoxUseCompass3, CHK_compass_learn })
            {
                check.AutoSize = true;
                check.Margin = new Padding(4, 6, 14, 4);
                options.Controls.Add(check);
            }
            foreach (var button in new Control[] { but_missing, but_reboot })
            {
                button.Size = new Size(130, 30);
                button.Margin = new Padding(4);
                options.Controls.Add(button);
            }
            label5.AutoSize = true;
            label5.Margin = new Padding(8, 9, 8, 0);
            options.Controls.Add(label5);
            root.Controls.Add(options, 0, 2);

            groupBoxonboardcalib.Dock = DockStyle.Fill;
            groupBoxonboardcalib.Margin = new Padding(0, 0, 0, 8);
            groupBoxonboardcalib.ForeColor = Color.White;
            groupBoxonboardcalib.BackColor = Color.FromArgb(22, 34, 42);
            lbl_obmagresult.Width = Math.Max(250, groupBoxonboardcalib.Width - lbl_obmagresult.Left - 20);
            lbl_obmagresult.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            root.Controls.Add(groupBoxonboardcalib, 0, 3);

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            label4.AutoSize = true;
            label4.Margin = new Padding(0, 9, 12, 0);
            but_largemagcal.Size = new Size(160, 30);
            footer.Controls.Add(label4);
            footer.Controls.Add(but_largemagcal);
            root.Controls.Add(footer, 0, 4);

            Controls.Clear();
            Controls.Add(root);
            ResumeLayout(true);
        }

        public void Activate()
        {
            // COMPASS_DEV_ID get a list of all connected devices
            list = MainV2.comPort.MAV.param.Where(a => a.Name.StartsWith("COMPASS") && a.Name.Contains("DEV_ID") && a.Value != 0)
                .Select((a, b) => new CompassDeviceInfo(b, a.Name, (uint) a.Value))
                .OrderBy((a) => a.ParamName).ToList();

            // COMPASS_PRIO get a list of all prios
            var prio = MainV2.comPort.MAV.param.Where(a => a.Name.StartsWith("COMPASS_PRIO") && a.Value != 0)
                .Select((a, b) => new CompassDeviceInfo(b, a.Name, (uint)a.Value))
                .OrderBy((a) => a.ParamName).ToList();

            var anymissing = false;
            // mark missing
            prio.ForEach(a =>
            {
                if (a.DevID == 0 || list.Any(b => b.DevID == a.DevID))
                    a.Missing = false;
                else
                {
                    a.Missing = true;
                    anymissing = true;
                }
            });

            //filter list removing prio dups from the list
            list = list.Where(a => !prio.Any(b => b.DevID == a.DevID)).ToList();

            // insert prios at the top
            list.InsertRange(0, prio);

            var bs = new BindingSource();
            bs.DataSource = list;
            myDataGridView1.DataSource = bs;

            mavlinkComboBoxfitness.setup(ParameterMetaDataRepository.GetParameterOptionsInt("COMPASS_CAL_FIT",
                MainV2.comPort.MAV.cs.firmware.ToString()), "COMPASS_CAL_FIT", MainV2.comPort.MAV.param);

            mavlinkCheckBoxUseCompass1.setup(1, 0, new[] { "COMPASS_USE", "COMPASS1_USE"}, MainV2.comPort.MAV.param);
            mavlinkCheckBoxUseCompass2.setup(1, 0, new[] { "COMPASS_USE2", "COMPASS2_USE"}, MainV2.comPort.MAV.param);
            mavlinkCheckBoxUseCompass3.setup(1, 0, new[] { "COMPASS_USE3", "COMPASS3_USE"}, MainV2.comPort.MAV.param);

            CHK_compass_learn.setup(1, 0, "COMPASS_LEARN", MainV2.comPort.MAV.param);

            {
                // set the default items
                var orient_param = MainV2.comPort.MAV.param[new[] { "COMPASS_ORIENT", "COMPASS1_ORIENT"}];
                var source = ParameterMetaDataRepository.GetParameterOptionsInt(orient_param.Name,
                        MainV2.comPort.MAV.cs.firmware.ToString())
                    .Select(a => new KeyValuePair<string, string>(a.Key.ToString(), a.Value)).ToList();
                Orientation.DataSource = source;
                Orientation.DisplayMember = "Value";
                Orientation.ValueMember = "Key";
            }

            if (anymissing)
            {
                CustomMessageBox.Show("Your compass configuration has changed, please review the missing compass", Strings.ERROR);
            }
        }

        public void Deactivate()
        {
            timer1.Stop();

            CheckReboot();
        }

        private bool CheckReboot()
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
                return true;

            if (rebootrequired)
            {
                if (CustomMessageBox.Show("Reboot required, reboot now?", "Reboot",
                        CustomMessageBox.MessageBoxButtons.YesNo) == CustomMessageBox.DialogResult.Yes)
                {
                    try
                    {
                        if (MainV2.comPort.doReboot())
                        {
                            CustomMessageBox.Show("Reboot failed. please manually reboot the hardware.", Strings.ERROR);
                        }
                        rebootrequired = false;
                    }
                    catch
                    {
                        CustomMessageBox.Show(Strings.ErrorCommunicating, Strings.ERROR);
                    }

                    return true;
                }
            }

            return false;
        }

        private async void myDataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == Up.Index && e.RowIndex != 0)
            {
                var item = list[e.RowIndex];
                list.Remove(item);
                list.Insert(e.RowIndex - 1, item);

                await UpdateFirst3();
            }

            if (e.ColumnIndex == Down.Index && e.RowIndex < (myDataGridView1.RowCount-1))
            {
                var item = list[e.RowIndex];
                list.Remove(item);
                list.Insert(e.RowIndex + 1, item);

                await UpdateFirst3();
            }
        }

        private async Task UpdateFirst3()
        {
            if (myDataGridView1.Rows.Count >= 1)
            {
                list[0]._index = 0;
                bool p1 = await MainV2.comPort.setParamAsync((byte)MainV2.comPort.sysidcurrent,
                    (byte)MainV2.comPort.compidcurrent,
                    "COMPASS_PRIO1_ID",
                    int.Parse(myDataGridView1.Rows[0].Cells[devIDDataGridViewTextBoxColumn.Index].Value.ToString()));

                if (!p1)
                    CustomMessageBox.Show(Strings.ErrorSettingParameter, Strings.ERROR);
            }

            if (myDataGridView1.Rows.Count >= 2)
            {
                list[1]._index = 1;
                bool p2 = await MainV2.comPort.setParamAsync((byte)MainV2.comPort.sysidcurrent,
                    (byte)MainV2.comPort.compidcurrent,
                    "COMPASS_PRIO2_ID",
                    int.Parse(myDataGridView1.Rows[1].Cells[devIDDataGridViewTextBoxColumn.Index].Value.ToString()));

                if (!p2)
                    CustomMessageBox.Show(Strings.ErrorSettingParameter, Strings.ERROR);
            }
            else
            {
                // clear it
                await MainV2.comPort.setParamAsync((byte)MainV2.comPort.sysidcurrent,
                    (byte)MainV2.comPort.compidcurrent,
                    "COMPASS_PRIO2_ID",
                    0);
            }

            if (myDataGridView1.Rows.Count >= 3)
            {
                list[2]._index = 2;
                bool p3 = await MainV2.comPort.setParamAsync((byte)MainV2.comPort.sysidcurrent,
                    (byte)MainV2.comPort.compidcurrent,
                    "COMPASS_PRIO3_ID",
                    int.Parse(myDataGridView1.Rows[2].Cells[devIDDataGridViewTextBoxColumn.Index].Value.ToString()));

                if (!p3)
                    CustomMessageBox.Show(Strings.ErrorSettingParameter, Strings.ERROR);
            }
            else
            {
                //clear it
                await MainV2.comPort.setParamAsync((byte)MainV2.comPort.sysidcurrent,
                    (byte)MainV2.comPort.compidcurrent,
                    "COMPASS_PRIO3_ID",
                    0);
            }

            rebootrequired = true;

            myDataGridView1.Invalidate();
        }

        private void BUT_OBmagcalstart_Click(object sender, EventArgs e)
        {
            if (rebootrequired && !CheckReboot())
            {
                return;
            }

            try
            {
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.DO_START_MAG_CAL, 0, 1, 1, 0, 0, 0, 0);
            }
            catch (Exception ex)
            {
                this.LogError(ex);
                CustomMessageBox.Show("Failed to start MAG CAL, check the autopilot is still responding.\n" + ex.ToString(), Strings.ERROR);
                return;
            }

            mprog.Clear();
            mrep.Clear();
            horizontalProgressBar1.Value = 0;
            horizontalProgressBar2.Value = 0;
            horizontalProgressBar3.Value = 0;

            packetsub1 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS, ReceviedPacket, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);
            packetsub2 = MainV2.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT, ReceviedPacket, (byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent);

            BUT_OBmagcalaccept.Enabled = true;
            BUT_OBmagcalcancel.Enabled = true;
            timer1.Start();
        }

        private bool ReceviedPacket(MAVLink.MAVLinkMessage packet)
        {
            if (System.Diagnostics.Debugger.IsAttached)
                MainV2.comPort.DebugPacket(packet, true);

            if (packet.msgid == (byte)MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS)
            {
                lock (this.mprog)
                {
                    this.mprog.Add(packet);
                }

                return true;
            }
            else if (packet.msgid == (byte)MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT)
            {
                lock (this.mrep)
                {
                    this.mrep.Add(packet);
                }

                return true;
            }

            return true;
        }

        private void BUT_OBmagcalaccept_Click(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.DO_ACCEPT_MAG_CAL, 0, 0, 1, 0, 0, 0, 0);

            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.ToString(), Strings.ERROR, MessageBoxButtons.OK);
            }

            MainV2.comPort.UnSubscribeToPacketType(packetsub1);
            MainV2.comPort.UnSubscribeToPacketType(packetsub2);

            timer1.Stop();
        }

        private void BUT_OBmagcalcancel_Click(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.DO_CANCEL_MAG_CAL, 0, 0, 1, 0, 0, 0, 0);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.ToString(), Strings.ERROR, MessageBoxButtons.OK);
            }

            MainV2.comPort.UnSubscribeToPacketType(packetsub1);
            MainV2.comPort.UnSubscribeToPacketType(packetsub2);

            timer1.Stop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            lbl_obmagresult.Clear();
            int compasscount = 0;
            int completecount = 0;
            lock (mprog)
            {
                // somewhere to save our %
                Dictionary<byte, MAVLink.MAVLinkMessage> status = new Dictionary<byte, MAVLink.MAVLinkMessage>();
                foreach (var item in mprog)
                {
                    status[((MAVLink.mavlink_mag_cal_progress_t)item.data).compass_id] = item;
                }

                // message for user
                string message = "";
                foreach (var item in status)
                {
                    var obj = (MAVLink.mavlink_mag_cal_progress_t)item.Value.data;

                    try
                    {
                        if (item.Key == 0)
                            horizontalProgressBar1.Value = obj.completion_pct;
                        if (item.Key == 1)
                            horizontalProgressBar2.Value = obj.completion_pct;
                        if (item.Key == 2)
                            horizontalProgressBar3.Value = obj.completion_pct;
                    }
                    catch { }

                    message += "id:" + item.Key + " " + obj.completion_pct.ToString() + "% ";
                    compasscount++;
                }
                lbl_obmagresult.AppendText(message + "\r\n");
            }

            lock (mrep)
            {
                // somewhere to save our answer
                Dictionary<byte, MAVLink.MAVLinkMessage> status = new Dictionary<byte, MAVLink.MAVLinkMessage>();
                foreach (var item in mrep)
                {
                    var obj = (MAVLink.mavlink_mag_cal_report_t)item.data;

                    if (obj.compass_id == 0 && obj.ofs_x == 0)
                        continue;

                    status[obj.compass_id] = item;
                }

                // message for user
                foreach (var item in status.Values)
                {
                    var obj = (MAVLink.mavlink_mag_cal_report_t)item.data;

                    lbl_obmagresult.AppendText("id:" + obj.compass_id + " x:" + obj.ofs_x.ToString("0.0") + " y:" +
                                               obj.ofs_y.ToString("0.0") + " z:" +
                                               obj.ofs_z.ToString("0.0") + " fit:" + obj.fitness.ToString("0.0") + " " +
                                               (MAVLink.MAG_CAL_STATUS)obj.cal_status + "\n");

                    try
                    {
                        if (obj.compass_id == 0)
                        {
                            horizontalProgressBar1.Value = 100;
                            pictureBox1.BackColor = Color.Green;
                        }

                        if (obj.compass_id == 1)
                        {
                            horizontalProgressBar2.Value = 100;
                            pictureBox2.BackColor = Color.Green;
                        }

                        if (obj.compass_id == 2)
                        {
                            horizontalProgressBar3.Value = 100;
                            pictureBox3.BackColor = Color.Green;
                        }
                    }
                    catch
                    {
                    }

                    if ((MAVLink.MAG_CAL_STATUS)obj.cal_status != MAVLink.MAG_CAL_STATUS.MAG_CAL_SUCCESS)
                    {
                        //CustomMessageBox.Show(Strings.CommandFailed);
                    }

                    if (obj.autosaved == 1)
                    {
                        completecount++;
                        timer1.Interval = 1000;
                    }
                }
            }

            if (compasscount == completecount && compasscount != 0)
            {
                BUT_OBmagcalcancel.Enabled = false;
                BUT_OBmagcalaccept.Enabled = false;
                timer1.Stop();
                CustomMessageBox.Show("Please reboot the autopilot");
            }
        }

        private void myDataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (myDataGridView1.Rows[e.RowIndex].Cells[Priority.Index].Value?.ToString() != (e.RowIndex + 1).ToString())
            {
                myDataGridView1.Rows[e.RowIndex].Cells[Priority.Index].Value = (e.RowIndex + 1).ToString();


            }
        }

        private void but_largemagcal_Click(object sender, EventArgs e)
        {
            double value = 0;
            if (InputBox.Show("MagCal Yaw", "Enter current heading in degrees\nNOTE: gps lock is required. Heading is true, not magnetic", ref value) == DialogResult.OK)
            {
                try
                {
                    if (MainV2.comPort.doCommand(MainV2.comPort.MAV.sysid, MainV2.comPort.MAV.compid,
                        MAVLink.MAV_CMD.FIXED_MAG_CAL_YAW, (float) value, 0, 0, 0, 0, 0, 0))
                    {
                        CustomMessageBox.Show(Strings.Completed, Strings.Completed);
                    }
                    else
                    {
                        CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
                    }
                }
                catch (Exception)
                {
                    CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
                }
            }
        }

        private void but_reboot_Click(object sender, EventArgs e)
        {
            if (CustomMessageBox.Show("Reboot?") == CustomMessageBox.DialogResult.OK)
            {
                MainV2.comPort.doReboot(false, true);
                rebootrequired = false;
            }
        }

        private void myDataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.Cancel = true;
        }

        private void compassDeviceInfoBindingSource_CurrentChanged(object sender, EventArgs e)
        {

        }

        private async void but_missing_ClickAsync(object sender, EventArgs e)
        {
            foreach (DataGridViewRow dataGridViewRow in myDataGridView1.Rows)
            {
                if (dataGridViewRow.Cells[Missing.Index].Value.Equals(true))
                {
                    myDataGridView1.Rows.Remove(dataGridViewRow);
                    but_missing_ClickAsync(null, null);
                    return;
                }
            }

            await UpdateFirst3();
        }
    }
}
