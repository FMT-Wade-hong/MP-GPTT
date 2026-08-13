using MissionPlanner.ArduPilot;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigFlightModes : MyUserControl, IActivate, IDeactivate
    {
        [Flags]
        public enum SimpleMode
        {
            None = 0,
            Simple1 = 1,
            Simple2 = 2,
            Simple3 = 4,
            Simple4 = 8,
            Simple5 = 16,
            Simple6 = 32
        }

        private readonly Timer _timer = new Timer();
        private GroupBox fmtCommonSettings;
        private NumericUpDown fmtNavigationSpeed;
        private NumericUpDown fmtGpsSpeed;
        private ComboBox fmtWpYaw;
        private ComboBox fmtRtlYaw;
        private NumericUpDown fmtRtlSpeed;
        private Label fmtCommonParameterHint;
        private Button fmtSaveCommonSettings;
        private string fmtNavigationSpeedParameter;
        private string fmtGpsSpeedParameter;
        private string fmtYawBehaviorParameter;
        private string fmtRtlSpeedParameter;

        private sealed class FmtSelectionOption
        {
            internal readonly int Value;
            private readonly string text;

            internal FmtSelectionOption(int value, string text)
            {
                Value = value;
                this.text = text;
            }

            public override string ToString()
            {
                return text;
            }
        }

        public ConfigFlightModes()
        {
            try
            {
                InitializeComponent();
                CreateFmtCommonSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Activate()
        {
            if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduPlane ||
                MainV2.comPort.MAV.cs.firmware == Firmwares.Ateryx) // APM
            {
                CB_simple1.Visible = false;
                CB_simple2.Visible = false;
                CB_simple3.Visible = false;
                CB_simple4.Visible = false;
                CB_simple5.Visible = false;
                CB_simple6.Visible = false;

                chk_ss1.Visible = false;
                chk_ss2.Visible = false;
                chk_ss3.Visible = false;
                chk_ss4.Visible = false;
                chk_ss5.Visible = false;
                chk_ss6.Visible = false;

                linkLabel1_ss.Visible = false;

                try
                {
                    updateDropDown(CMB_fmode1, "FLTMODE1");
                    updateDropDown(CMB_fmode2, "FLTMODE2");
                    updateDropDown(CMB_fmode3, "FLTMODE3");
                    updateDropDown(CMB_fmode4, "FLTMODE4");
                    updateDropDown(CMB_fmode5, "FLTMODE5");
                    updateDropDown(CMB_fmode6, "FLTMODE6");

                    CMB_fmode1.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE1"].ToString());
                    CMB_fmode2.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE2"].ToString());
                    CMB_fmode3.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE3"].ToString());
                    CMB_fmode4.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE4"].ToString());
                    CMB_fmode5.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE5"].ToString());
                    CMB_fmode6.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE6"].ToString());
                }
                catch
                {
                }
            }
            else if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduRover) // APM
            {
                CB_simple1.Visible = false;
                CB_simple2.Visible = false;
                CB_simple3.Visible = false;
                CB_simple4.Visible = false;
                CB_simple5.Visible = false;
                CB_simple6.Visible = false;

                chk_ss1.Visible = false;
                chk_ss2.Visible = false;
                chk_ss3.Visible = false;
                chk_ss4.Visible = false;
                chk_ss5.Visible = false;
                chk_ss6.Visible = false;

                linkLabel1_ss.Visible = false;

                try
                {
                    updateDropDown(CMB_fmode1, "MODE1");
                    updateDropDown(CMB_fmode2, "MODE2");
                    updateDropDown(CMB_fmode3, "MODE3");
                    updateDropDown(CMB_fmode4, "MODE4");
                    updateDropDown(CMB_fmode5, "MODE5");
                    updateDropDown(CMB_fmode6, "MODE6");

                    CMB_fmode1.SelectedValue = int.Parse(MainV2.comPort.MAV.param["MODE1"].ToString());
                    CMB_fmode2.SelectedValue = int.Parse(MainV2.comPort.MAV.param["MODE2"].ToString());
                    CMB_fmode3.SelectedValue = int.Parse(MainV2.comPort.MAV.param["MODE3"].ToString());
                    CMB_fmode4.SelectedValue = int.Parse(MainV2.comPort.MAV.param["MODE4"].ToString());
                    CMB_fmode5.SelectedValue = int.Parse(MainV2.comPort.MAV.param["MODE5"].ToString());
                    CMB_fmode6.SelectedValue = int.Parse(MainV2.comPort.MAV.param["MODE6"].ToString());
                }
                catch
                {
                }
            }
            else if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduCopter2) // ac2
            {
                if (MainV2.DisplayConfiguration.standardFlightModesOnly)
                {
                    CB_simple1.Visible = false;
                    CB_simple2.Visible = false;
                    CB_simple3.Visible = false;
                    CB_simple4.Visible = false;
                    CB_simple5.Visible = false;
                    CB_simple6.Visible = false;

                    chk_ss1.Visible = false;
                    chk_ss2.Visible = false;
                    chk_ss3.Visible = false;
                    chk_ss4.Visible = false;
                    chk_ss5.Visible = false;
                    chk_ss6.Visible = false;

                    linkLabel1_ss.Visible = false;
                }
                try
                {
                    updateDropDown(CMB_fmode1, "FLTMODE1");
                    updateDropDown(CMB_fmode2, "FLTMODE2");
                    updateDropDown(CMB_fmode3, "FLTMODE3");
                    updateDropDown(CMB_fmode4, "FLTMODE4");
                    updateDropDown(CMB_fmode5, "FLTMODE5");
                    updateDropDown(CMB_fmode6, "FLTMODE6");

                    CMB_fmode1.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE1"].ToString());
                    CMB_fmode2.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE2"].ToString());
                    CMB_fmode3.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE3"].ToString());
                    CMB_fmode4.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE4"].ToString());
                    CMB_fmode5.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE5"].ToString());
                    CMB_fmode6.SelectedValue = int.Parse(MainV2.comPort.MAV.param["FLTMODE6"].ToString());
                    CMB_fmode6.Enabled = true;

                    if (MainV2.comPort.MAV.param.ContainsKey("SIMPLE"))
                    {
                        var simple = int.Parse(MainV2.comPort.MAV.param["SIMPLE"].ToString());

                        CB_simple1.Checked = ((simple >> 0 & 1) == 1);
                        CB_simple2.Checked = ((simple >> 1 & 1) == 1);
                        CB_simple3.Checked = ((simple >> 2 & 1) == 1);
                        CB_simple4.Checked = ((simple >> 3 & 1) == 1);
                        CB_simple5.Checked = ((simple >> 4 & 1) == 1);
                        CB_simple6.Checked = ((simple >> 5 & 1) == 1);
                    }

                    if (MainV2.comPort.MAV.param.ContainsKey("SUPER_SIMPLE"))
                    {
                        var simple = int.Parse(MainV2.comPort.MAV.param["SUPER_SIMPLE"].ToString());

                        chk_ss1.Checked = ((simple >> 0 & 1) == 1);
                        chk_ss2.Checked = ((simple >> 1 & 1) == 1);
                        chk_ss3.Checked = ((simple >> 2 & 1) == 1);
                        chk_ss4.Checked = ((simple >> 3 & 1) == 1);
                        chk_ss5.Checked = ((simple >> 4 & 1) == 1);
                        chk_ss6.Checked = ((simple >> 5 & 1) == 1);
                    }
                }
                catch
                {
                }
            }
            else if (MainV2.comPort.MAV.cs.firmware == Firmwares.PX4) // APM
            {
                CB_simple1.Visible = false;
                CB_simple2.Visible = false;
                CB_simple3.Visible = false;
                CB_simple4.Visible = false;
                CB_simple5.Visible = false;
                CB_simple6.Visible = false;

                chk_ss1.Visible = false;
                chk_ss2.Visible = false;
                chk_ss3.Visible = false;
                chk_ss4.Visible = false;
                chk_ss5.Visible = false;
                chk_ss6.Visible = false;

                linkLabel1_ss.Visible = false;

                try
                {
                    updateDropDown(CMB_fmode1, "COM_FLTMODE1");
                    CMB_fmode1.DataSource = ParameterMetaDataRepository.GetParameterOptionsInt("COM_FLTMODE1", "PX4");
                    updateDropDown(CMB_fmode2, "COM_FLTMODE2");
                    CMB_fmode2.DataSource = ParameterMetaDataRepository.GetParameterOptionsInt("COM_FLTMODE2", "PX4");
                    updateDropDown(CMB_fmode3, "COM_FLTMODE3");
                    CMB_fmode3.DataSource = ParameterMetaDataRepository.GetParameterOptionsInt("COM_FLTMODE3", "PX4");
                    updateDropDown(CMB_fmode4, "COM_FLTMODE4");
                    CMB_fmode4.DataSource = ParameterMetaDataRepository.GetParameterOptionsInt("COM_FLTMODE4", "PX4");
                    updateDropDown(CMB_fmode5, "COM_FLTMODE5");
                    CMB_fmode5.DataSource = ParameterMetaDataRepository.GetParameterOptionsInt("COM_FLTMODE5", "PX4");
                    updateDropDown(CMB_fmode6, "COM_FLTMODE6");
                    CMB_fmode6.DataSource = ParameterMetaDataRepository.GetParameterOptionsInt("COM_FLTMODE6", "PX4");

                    CMB_fmode1.SelectedValue = int.Parse(MainV2.comPort.MAV.param["COM_FLTMODE1"].ToString());
                    CMB_fmode2.SelectedValue = int.Parse(MainV2.comPort.MAV.param["COM_FLTMODE2"].ToString());
                    CMB_fmode3.SelectedValue = int.Parse(MainV2.comPort.MAV.param["COM_FLTMODE3"].ToString());
                    CMB_fmode4.SelectedValue = int.Parse(MainV2.comPort.MAV.param["COM_FLTMODE4"].ToString());
                    CMB_fmode5.SelectedValue = int.Parse(MainV2.comPort.MAV.param["COM_FLTMODE5"].ToString());
                    CMB_fmode6.SelectedValue = int.Parse(MainV2.comPort.MAV.param["COM_FLTMODE6"].ToString());
                }
                catch
                {
                }
            }

            LoadFmtCommonSettings();

            _timer.Tick += timer_Tick;

            _timer.Enabled = true;
            _timer.Interval = 100;
            _timer.Start();
        }

        public void Deactivate()
        {
            _timer.Stop();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                BUT_SaveModes_Click(null, null);
                return true;
            }

            return false;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.MAV.cs.UpdateCurrentSettings(currentStateBindingSource.UpdateDataSource(MainV2.comPort.MAV.cs));
            }
            catch
            {
            }

            float pwm = 0;


            if (MainV2.comPort.MAV.param.ContainsKey("FLTMODE_CH") ||
                MainV2.comPort.MAV.param.ContainsKey("MODE_CH"))
            {
                var sw = 0;
                if (MainV2.comPort.MAV.param.ContainsKey("FLTMODE_CH"))
                {
                    sw = (int)MainV2.comPort.MAV.param["FLTMODE_CH"].Value;
                }
                else
                {
                    sw = (int)MainV2.comPort.MAV.param["MODE_CH"].Value;
                }

                switch (sw)
                {
                    case 5:
                        pwm = MainV2.comPort.MAV.cs.ch5in;
                        break;
                    case 6:
                        pwm = MainV2.comPort.MAV.cs.ch6in;
                        break;
                    case 7:
                        pwm = MainV2.comPort.MAV.cs.ch7in;
                        break;
                    case 8:
                        pwm = MainV2.comPort.MAV.cs.ch8in;
                        break;
                    case 9:
                        pwm = MainV2.comPort.MAV.cs.ch9in;
                        break;
                    case 10:
                        pwm = MainV2.comPort.MAV.cs.ch10in;
                        break;
                    case 11:
                        pwm = MainV2.comPort.MAV.cs.ch11in;
                        break;
                    case 12:
                        pwm = MainV2.comPort.MAV.cs.ch12in;
                        break;
                    case 13:
                        pwm = MainV2.comPort.MAV.cs.ch13in;
                        break;
                    case 14:
                        pwm = MainV2.comPort.MAV.cs.ch14in;
                        break;
                    case 15:
                        pwm = MainV2.comPort.MAV.cs.ch15in;
                        break;
                    case 16:
                        pwm = MainV2.comPort.MAV.cs.ch16in;
                        break;
                    default:

                        break;
                }

                if (MainV2.comPort.MAV.param.ContainsKey("FLTMODE_CH"))
                {
                    LBL_flightmodepwm.Text = MainV2.comPort.MAV.param["FLTMODE_CH"] + ": " + pwm;
                }
                else
                {
                    LBL_flightmodepwm.Text = MainV2.comPort.MAV.param["MODE_CH"] + ": " + pwm;
                }
            }

            Control[] fmodelist = { CMB_fmode1, CMB_fmode2, CMB_fmode3, CMB_fmode4, CMB_fmode5, CMB_fmode6 };

            foreach (var ctl in fmodelist)
            {
                ThemeManager.ApplyThemeTo(ctl);
            }

            var no = readSwitch(pwm);

            fmodelist[no].BackColor = ThemeManager.CurrentPPMBackground;
        }

        // from arducopter code
        private byte readSwitch(float inpwm)
        {
            var pulsewidth = (int)inpwm; // default for Arducopter

            if (pulsewidth > 1230 && pulsewidth <= 1360) return 1;
            if (pulsewidth > 1360 && pulsewidth <= 1490) return 2;
            if (pulsewidth > 1490 && pulsewidth <= 1620) return 3;
            if (pulsewidth > 1620 && pulsewidth <= 1749) return 4; // Software Manual
            if (pulsewidth >= 1750) return 5; // Hardware Manual
            return 0;
        }

        private void BUT_SaveModes_Click(object sender, EventArgs e)
        {
            try
            {
                if (MainV2.comPort.MAV.param.ContainsKey("FLTMODE1"))
                {
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "FLTMODE1", int.Parse(CMB_fmode1.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "FLTMODE2", int.Parse(CMB_fmode2.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "FLTMODE3", int.Parse(CMB_fmode3.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "FLTMODE4", int.Parse(CMB_fmode4.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "FLTMODE5", int.Parse(CMB_fmode5.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "FLTMODE6", int.Parse(CMB_fmode6.SelectedValue.ToString()));
                }
                else if (MainV2.comPort.MAV.param.ContainsKey("MODE1"))
                {
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "MODE1", int.Parse(CMB_fmode1.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "MODE2", int.Parse(CMB_fmode2.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "MODE3", int.Parse(CMB_fmode3.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "MODE4", int.Parse(CMB_fmode4.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "MODE5", int.Parse(CMB_fmode5.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "MODE6", int.Parse(CMB_fmode6.SelectedValue.ToString()));
                }
                else if (MainV2.comPort.MAV.param.ContainsKey("COM_FLTMODE1"))
                {
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "COM_FLTMODE1", int.Parse(CMB_fmode1.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "COM_FLTMODE2", int.Parse(CMB_fmode2.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "COM_FLTMODE3", int.Parse(CMB_fmode3.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "COM_FLTMODE4", int.Parse(CMB_fmode4.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "COM_FLTMODE5", int.Parse(CMB_fmode5.SelectedValue.ToString()));
                    MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "COM_FLTMODE6", int.Parse(CMB_fmode6.SelectedValue.ToString()));
                }

                if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduCopter2) // ac2
                {
                    // simple
                    var value = (float)(CB_simple1.Checked ? (int)SimpleMode.Simple1 : 0) +
                                (CB_simple2.Checked ? (int)SimpleMode.Simple2 : 0) +
                                (CB_simple3.Checked ? (int)SimpleMode.Simple3 : 0)
                                + (CB_simple4.Checked ? (int)SimpleMode.Simple4 : 0) +
                                (CB_simple5.Checked ? (int)SimpleMode.Simple5 : 0) +
                                (CB_simple6.Checked ? (int)SimpleMode.Simple6 : 0);
                    if (MainV2.comPort.MAV.param.ContainsKey("SIMPLE"))
                        MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "SIMPLE", value);

                    // supersimple
                    value = (float)(chk_ss1.Checked ? (int)SimpleMode.Simple1 : 0) +
                            (chk_ss2.Checked ? (int)SimpleMode.Simple2 : 0) +
                            (chk_ss3.Checked ? (int)SimpleMode.Simple3 : 0)
                            + (chk_ss4.Checked ? (int)SimpleMode.Simple4 : 0) +
                            (chk_ss5.Checked ? (int)SimpleMode.Simple5 : 0) +
                            (chk_ss6.Checked ? (int)SimpleMode.Simple6 : 0);
                    if (MainV2.comPort.MAV.param.ContainsKey("SUPER_SIMPLE"))
                        MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, "SUPER_SIMPLE", value);
                }
            }
            catch
            {
                CustomMessageBox.Show(Strings.ErrorSettingParameter, Strings.ERROR);
            }
            BUT_SaveModes.Text = "Complete";
        }

        private void updateDropDown(ComboBox ctl, string param)
        {
            ctl.DataSource = ArduPilot.Common.getModesList(MainV2.comPort.MAV.cs.firmware);
            ctl.DisplayMember = "Value";
            ctl.ValueMember = "Key";
        }

        private void linkLabel1_ss_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                Process.Start("https://ardupilot.org/copter/docs/simpleandsuper-simple-modes.html");
            }
            catch
            {
                CustomMessageBox.Show(Strings.ERROR +
                                      " https://ardupilot.org/copter/docs/simpleandsuper-simple-modes.html");
            }
        }

        private void flightmode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduCopter2)
            {
                var sender2 = (Control)sender;
                var currentmode = sender2.Text.ToLower();

                if (currentmode.Contains("althold") || currentmode.Contains("auto") ||
                    currentmode.Contains("autotune") || currentmode.Contains("land") ||
                    currentmode.Contains("loiter") || currentmode.Contains("ofloiter") ||
                    currentmode.Contains("poshold") || currentmode.Contains("rtl") ||
                    currentmode.Contains("sport") || currentmode.Contains("stabilize") ||
                    currentmode.Contains("flowhold") || currentmode.Contains("zigzag"))
                {
                    //CMB_fmode1
                    //CB_simple1
                    //chk_ss1

                    var number = sender2.Name.Substring(sender2.Name.Length - 1);

                    findandenableordisable("CB_simple" + number, true);
                    findandenableordisable("chk_ss" + number, true);
                }
                else
                {
                    var number = sender2.Name.Substring(sender2.Name.Length - 1);

                    findandenableordisable("CB_simple" + number, false);
                    findandenableordisable("chk_ss" + number, false);
                }
            }
        }

        private void findandenableordisable(string ctl, bool enable)
        {
            var items = Controls.Find(ctl, true);

            if (items.Length > 0)
            {
                items[0].Enabled = enable;
            }
        }

        private void CreateFmtCommonSettings()
        {
            AutoScroll = true;
            fmtCommonSettings = new GroupBox
            {
                Name = "FmtCommonSettings",
                Text = "常用設定（ArduCopter）",
                Location = new Point(0, tableLayoutPanel1.Bottom + 10),
                Size = new Size(593, 213),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Visible = false
            };

            fmtNavigationSpeed = CreateFmtSpeedControl("FmtNavigationSpeed", new Point(143, 27), 0.1m);
            fmtGpsSpeed = CreateFmtSpeedControl("FmtGpsSpeed", new Point(430, 27), 0.1m);
            fmtRtlSpeed = CreateFmtSpeedControl("FmtRtlSpeed", new Point(430, 66), 0m);

            fmtWpYaw = CreateFmtYawCombo("FmtWpYaw", new Point(143, 105));
            fmtWpYaw.Items.AddRange(new object[]
            {
                new FmtSelectionOption(0, "保持航向"),
                new FmtSelectionOption(1, "朝向下一航點"),
                new FmtSelectionOption(3, "沿 GPS 航跡方向")
            });

            fmtRtlYaw = CreateFmtYawCombo("FmtRtlYaw", new Point(430, 105));
            fmtRtlYaw.Items.AddRange(new object[]
            {
                new FmtSelectionOption(0, "保持航向"),
                new FmtSelectionOption(1, "朝向返航點"),
                new FmtSelectionOption(3, "沿 GPS 航跡方向")
            });
            fmtWpYaw.SelectedIndexChanged += FmtYawSelectionChanged;
            fmtRtlYaw.SelectedIndexChanged += FmtYawSelectionChanged;

            fmtSaveCommonSettings = new Button
            {
                Name = "FmtSaveCommonSettings",
                Text = "儲存常用設定",
                Location = new Point(430, 143),
                Size = new Size(125, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 171, 226),
                ForeColor = Color.White
            };
            fmtSaveCommonSettings.FlatAppearance.BorderSize = 0;
            fmtSaveCommonSettings.Click += SaveFmtCommonSettings;

            fmtCommonParameterHint = new Label
            {
                Name = "FmtCommonParameterHint",
                Location = new Point(16, 180),
                Size = new Size(575, 22),
                AutoEllipsis = true,
                ForeColor = Color.Gray,
                Text = "連線後顯示飛控實際使用的參數名稱。"
            };

            fmtCommonSettings.Controls.AddRange(new Control[]
            {
                CreateFmtLabel("導航速度參數", new Point(16, 30), new Size(122, 22)),
                fmtNavigationSpeed,
                CreateFmtUnitLabel(new Point(231, 30)),
                CreateFmtLabel("GPS 速度參數", new Point(295, 30), new Size(130, 22)),
                fmtGpsSpeed,
                CreateFmtUnitLabel(new Point(518, 30)),
                CreateFmtLabel("RTL 速度", new Point(295, 69), new Size(130, 22)),
                fmtRtlSpeed,
                CreateFmtUnitLabel(new Point(518, 69)),
                CreateFmtLabel("WP 航向", new Point(16, 108), new Size(122, 22)),
                fmtWpYaw,
                CreateFmtLabel("RTL 航向", new Point(295, 108), new Size(130, 22)),
                fmtRtlYaw,
                fmtSaveCommonSettings,
                fmtCommonParameterHint
            });
            Controls.Add(fmtCommonSettings);
            fmtCommonSettings.BringToFront();
            ThemeManager.ApplyThemeTo(fmtCommonSettings);
        }

        private static NumericUpDown CreateFmtSpeedControl(string name, Point location, decimal minimum)
        {
            return new NumericUpDown
            {
                Name = name,
                Location = location,
                Size = new Size(82, 24),
                DecimalPlaces = 1,
                Increment = 0.1m,
                Minimum = minimum,
                Maximum = 100m,
                TextAlign = HorizontalAlignment.Right
            };
        }

        private static ComboBox CreateFmtYawCombo(string name, Point location)
        {
            return new ComboBox
            {
                Name = name,
                Location = location,
                Size = new Size(140, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
        }

        private static Label CreateFmtLabel(string text, Point location, Size size)
        {
            return new Label
            {
                Text = text,
                Location = location,
                Size = size,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateFmtUnitLabel(Point location)
        {
            return CreateFmtLabel("m/s", location, new Size(45, 22));
        }

        private void LoadFmtCommonSettings()
        {
            if (fmtCommonSettings == null)
                return;

            var isCopter = MainV2.comPort.MAV.cs.firmware == Firmwares.ArduCopter2;
            fmtCommonSettings.Visible = isCopter;
            if (!isCopter)
                return;

            fmtNavigationSpeedParameter = FindFmtParameter("WP_SPD", "WPNAV_SPEED");
            fmtGpsSpeedParameter = FindFmtParameter("LOIT_SPEED_MS", "WPNAV_LOIT_SPEED", "LOIT_SPEED");
            fmtYawBehaviorParameter = FindFmtParameter("WP_YAW_BEHAVIOR");
            fmtRtlSpeedParameter = FindFmtParameter("RTL_SPEED_MS", "RTL_SPEED");

            LoadFmtSpeed(fmtNavigationSpeed, fmtNavigationSpeedParameter);
            LoadFmtSpeed(fmtGpsSpeed, fmtGpsSpeedParameter);
            LoadFmtSpeed(fmtRtlSpeed, fmtRtlSpeedParameter);

            fmtWpYaw.Enabled = fmtYawBehaviorParameter != null;
            fmtRtlYaw.Enabled = fmtYawBehaviorParameter != null;
            fmtWpYaw.SelectedIndex = -1;
            fmtRtlYaw.SelectedIndex = -1;
            if (fmtYawBehaviorParameter != null)
            {
                var yawValue = (int)Math.Round(MainV2.comPort.MAV.param[fmtYawBehaviorParameter].Value);
                SelectFmtYawBehavior(yawValue);
            }

            var parameters = new List<string>();
            AddFmtParameterName(parameters, fmtNavigationSpeedParameter);
            AddFmtParameterName(parameters, fmtGpsSpeedParameter);
            AddFmtParameterName(parameters, fmtYawBehaviorParameter);
            AddFmtParameterName(parameters, fmtRtlSpeedParameter);
            fmtCommonParameterHint.Text = parameters.Count == 0
                ? "目前飛控沒有支援的常用設定參數。"
                : "使用參數：" + string.Join("、", parameters);
            fmtSaveCommonSettings.Enabled = parameters.Count > 0 &&
                                            MainV2.comPort.BaseStream != null &&
                                            MainV2.comPort.BaseStream.IsOpen &&
                                            !MainV2.comPort.ReadOnly &&
                                            !MainV2.comPort.MAV.cs.armed;
            ThemeManager.ApplyThemeTo(fmtCommonSettings);
            fmtSaveCommonSettings.BackColor = Color.FromArgb(41, 171, 226);
            fmtSaveCommonSettings.ForeColor = Color.White;
        }

        private static void AddFmtParameterName(ICollection<string> parameters, string parameterName)
        {
            if (!string.IsNullOrEmpty(parameterName))
                parameters.Add(parameterName);
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

        private static void LoadFmtSpeed(NumericUpDown control, string parameterName)
        {
            control.Enabled = parameterName != null;
            if (parameterName == null)
                return;

            var metersPerSecond = FmtSpeedRawToMetersPerSecond(parameterName,
                MainV2.comPort.MAV.param[parameterName].Value);
            var value = (decimal)Math.Max((double)control.Minimum,
                Math.Min((double)control.Maximum, metersPerSecond));
            control.Value = value;
        }

        internal static double FmtSpeedRawToMetersPerSecond(string parameterName, double rawValue)
        {
            return IsFmtLegacyCentimeterSpeed(parameterName) ? rawValue / 100.0 : rawValue;
        }

        internal static double FmtSpeedMetersPerSecondToRaw(string parameterName, double metersPerSecond)
        {
            return IsFmtLegacyCentimeterSpeed(parameterName)
                ? Math.Round(metersPerSecond * 100.0, MidpointRounding.AwayFromZero)
                : metersPerSecond;
        }

        private static bool IsFmtLegacyCentimeterSpeed(string parameterName)
        {
            return string.Equals(parameterName, "WPNAV_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "WPNAV_LOIT_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "LOIT_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "RTL_SPEED", StringComparison.Ordinal);
        }

        private bool fmtUpdatingYawSelection;

        private void SelectFmtYawBehavior(int yawValue)
        {
            fmtUpdatingYawSelection = true;
            try
            {
                switch (yawValue)
                {
                    case 0:
                        SelectFmtOption(fmtWpYaw, 0);
                        SelectFmtOption(fmtRtlYaw, 0);
                        break;
                    case 1:
                        SelectFmtOption(fmtWpYaw, 1);
                        SelectFmtOption(fmtRtlYaw, 1);
                        break;
                    case 2:
                        SelectFmtOption(fmtWpYaw, 1);
                        SelectFmtOption(fmtRtlYaw, 0);
                        break;
                    case 3:
                        SelectFmtOption(fmtWpYaw, 3);
                        SelectFmtOption(fmtRtlYaw, 3);
                        break;
                }
            }
            finally
            {
                fmtUpdatingYawSelection = false;
            }
        }

        private static void SelectFmtOption(ComboBox control, int value)
        {
            for (var index = 0; index < control.Items.Count; index++)
            {
                if (((FmtSelectionOption)control.Items[index]).Value == value)
                {
                    control.SelectedIndex = index;
                    return;
                }
            }
        }

        private void FmtYawSelectionChanged(object sender, EventArgs e)
        {
            if (fmtUpdatingYawSelection || fmtWpYaw.SelectedItem == null || fmtRtlYaw.SelectedItem == null)
                return;

            var wpYaw = ((FmtSelectionOption)fmtWpYaw.SelectedItem).Value;
            var rtlYaw = ((FmtSelectionOption)fmtRtlYaw.SelectedItem).Value;
            var behavior = FmtYawSelectionsToBehavior(wpYaw, rtlYaw);
            if (behavior >= 0)
                return;

            // WP_YAW_BEHAVIOR is one shared ArduPilot parameter. If the requested pair
            // is unsupported, keep the user's last selection and adjust the other list
            // to the closest valid firmware behavior.
            if (ReferenceEquals(sender, fmtWpYaw))
                SelectFmtYawBehavior(wpYaw == 0 ? 0 : wpYaw == 3 ? 3 : 2);
            else
                SelectFmtYawBehavior(rtlYaw == 1 ? 1 : rtlYaw == 3 ? 3 : 2);
        }

        internal static int FmtYawSelectionsToBehavior(int wpYaw, int rtlYaw)
        {
            if (wpYaw == 0 && rtlYaw == 0) return 0;
            if (wpYaw == 1 && rtlYaw == 1) return 1;
            if (wpYaw == 1 && rtlYaw == 0) return 2;
            if (wpYaw == 3 && rtlYaw == 3) return 3;
            return -1;
        }

        private void SaveFmtCommonSettings(object sender, EventArgs e)
        {
            if (MainV2.comPort.BaseStream == null || !MainV2.comPort.BaseStream.IsOpen)
            {
                CustomMessageBox.Show("請先連線飛控。", "常用設定", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (MainV2.comPort.MAV.cs.armed)
            {
                CustomMessageBox.Show("常用設定只能在飛機上鎖（未解鎖）時修改。", "常用設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MainV2.comPort.ReadOnly)
            {
                CustomMessageBox.Show("目前為唯讀連線，無法寫入參數。", "常用設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmation = "確定要將導航、GPS 定位、WP／RTL 航向及 RTL 速度寫入飛控嗎？";
            if (CustomMessageBox.Show(confirmation, "常用設定", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != (int)DialogResult.Yes)
                return;

            try
            {
                var failures = new List<string>();
                SaveFmtSpeed(fmtNavigationSpeed, fmtNavigationSpeedParameter, failures);
                SaveFmtSpeed(fmtGpsSpeed, fmtGpsSpeedParameter, failures);
                SaveFmtSpeed(fmtRtlSpeed, fmtRtlSpeedParameter, failures);

                var wpYaw = fmtWpYaw.SelectedItem as FmtSelectionOption;
                var rtlYaw = fmtRtlYaw.SelectedItem as FmtSelectionOption;
                if (fmtYawBehaviorParameter != null && wpYaw != null && rtlYaw != null)
                {
                    var yawBehavior = FmtYawSelectionsToBehavior(wpYaw.Value, rtlYaw.Value);
                    if (yawBehavior < 0 || !SetFmtParameter(fmtYawBehaviorParameter, yawBehavior))
                        failures.Add(fmtYawBehaviorParameter);
                }

                if (failures.Count > 0)
                {
                    CustomMessageBox.Show("下列參數寫入失敗：" + string.Join("、", failures), "常用設定",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                CustomMessageBox.Show("常用設定已寫入飛控。", "常用設定", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                LoadFmtCommonSettings();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("常用設定寫入失敗：" + ex.Message, "常用設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SaveFmtSpeed(NumericUpDown control, string parameterName,
            ICollection<string> failures)
        {
            if (parameterName == null || !control.Enabled)
                return;

            var rawValue = FmtSpeedMetersPerSecondToRaw(parameterName, (double)control.Value);
            if (!SetFmtParameter(parameterName, rawValue))
                failures.Add(parameterName);
        }

        private static bool SetFmtParameter(string parameterName, double value)
        {
            return MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent,
                (byte)MainV2.comPort.compidcurrent, parameterName, value);
        }
    }
}
