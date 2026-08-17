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
        private readonly bool fmtCommonSettingsOnly;
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
        private Label fmtVehicleNotice;
        private GroupBox fmtCommonSettings;
        private NumericUpDown fmtNavigationSpeed;
        private NumericUpDown fmtGpsSpeed;
        private NumericUpDown fmtWpRadius;
        private ComboBox fmtWpYaw;
        private ComboBox fmtRtlYaw;
        private NumericUpDown fmtRtlSpeed;
        private NumericUpDown fmtClimbSpeed;
        private NumericUpDown fmtDescentSpeed;
        private NumericUpDown fmtRtlAltitude;
        private Label fmtCommonParameterHint;
        private Button fmtSaveCommonSettings;
        private string fmtNavigationSpeedParameter;
        private string fmtGpsSpeedParameter;
        private string fmtWpRadiusParameter;
        private string fmtYawBehaviorParameter;
        private string fmtRtlSpeedParameter;
        private string fmtClimbSpeedParameter;
        private string fmtDescentSpeedParameter;
        private string fmtRtlAltitudeParameter;
        private Label fmtMultirotorStatus;

        private GroupBox fmtFixedWingSettings;
        private NumericUpDown fmtPlaneCruiseSpeed;
        private NumericUpDown fmtPlaneGpsSpeed;
        private NumericUpDown fmtPlaneWpRadius;
        private NumericUpDown fmtPlaneLoiterRadius;
        private NumericUpDown fmtPlaneRtlRadius;
        private NumericUpDown fmtPlaneRtlAltitude;
        private Label fmtPlaneStatus;
        private Label fmtPlaneParameterHint;
        private Button fmtSavePlaneSettings;
        private string fmtPlaneCruiseSpeedParameter;
        private string fmtPlaneGpsSpeedParameter;
        private string fmtPlaneWpRadiusParameter;
        private string fmtPlaneLoiterRadiusParameter;
        private string fmtPlaneRtlRadiusParameter;
        private string fmtPlaneRtlAltitudeParameter;

        private GroupBox fmtVtolSettings;
        private NumericUpDown fmtVtolCruiseSpeed;
        private NumericUpDown fmtVtolNavigationSpeed;
        private NumericUpDown fmtVtolGpsSpeed;
        private NumericUpDown fmtVtolWpRadius;
        private NumericUpDown fmtVtolClimbSpeed;
        private NumericUpDown fmtVtolDescentSpeed;
        private NumericUpDown fmtVtolRtlAltitude;
        private ComboBox fmtVtolRtlMode;
        private Label fmtVtolStatus;
        private Label fmtVtolParameterHint;
        private Button fmtSaveVtolSettings;
        private string fmtVtolCruiseSpeedParameter;
        private string fmtVtolNavigationSpeedParameter;
        private string fmtVtolGpsSpeedParameter;
        private string fmtVtolWpRadiusParameter;
        private string fmtVtolRtlModeParameter;
        private string fmtVtolClimbSpeedParameter;
        private string fmtVtolDescentSpeedParameter;
        private string fmtVtolRtlAltitudeParameter;

        private GroupBox fmtHelicopterSettings;
        private NumericUpDown fmtHeliRpmLower;
        private NumericUpDown fmtHeliRpmUpper;
        private Label fmtHeliStatus;
        private Button fmtSaveHeliSettings;

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

        public ConfigFlightModes() : this(false)
        {
        }

        protected ConfigFlightModes(bool commonSettingsOnly)
        {
            try
            {
                fmtCommonSettingsOnly = commonSettingsOnly;
                InitializeComponent();
                if (fmtCommonSettingsOnly)
                {
                    Controls.Clear();
                    CreateFmtCommonSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Activate()
        {
            if (fmtCommonSettingsOnly)
            {
                LoadFmtCommonSettings();
                return;
            }

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
            if (fmtCommonSettingsOnly)
                return base.ProcessCmdKey(ref msg, keyData);

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
            fmtVehicleNotice = new Label
            {
                Name = "FmtVehicleNotice",
                Text = "連線後會依飛控構型啟用對應設定方框。",
                Location = new Point(4, 4),
                Size = new Size(587, 22),
                ForeColor = Color.FromArgb(41, 171, 226),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(fmtVehicleNotice);

            fmtCommonSettings = new GroupBox
            {
                Name = "FmtCommonSettings",
                Text = "多旋翼常用設定",
                Location = new Point(0, fmtVehicleNotice.Bottom + 4),
                Size = new Size(593, 356),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Visible = true
            };

            fmtNavigationSpeed = CreateFmtSpeedControl("FmtNavigationSpeed", new Point(143, 27), 0.1m);
            fmtGpsSpeed = CreateFmtSpeedControl("FmtGpsSpeed", new Point(430, 27), 0.1m);
            fmtWpRadius = CreateFmtDistanceControl("FmtWpRadius", new Point(143, 66), 0.1m);
            fmtRtlSpeed = CreateFmtSpeedControl("FmtRtlSpeed", new Point(430, 66), 0m);
            fmtClimbSpeed = CreateFmtSpeedControl("FmtClimbSpeed", new Point(143, 144), 0.1m);
            fmtDescentSpeed = CreateFmtSpeedControl("FmtDescentSpeed", new Point(430, 144), 0.1m);
            fmtRtlAltitude = CreateFmtDistanceControl("FmtRtlAltitude", new Point(143, 183), 0m);

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
                Location = new Point(430, 221),
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
                Location = new Point(16, 323),
                Size = new Size(575, 22),
                AutoEllipsis = true,
                ForeColor = Color.Gray,
                Text = "連線後顯示飛控實際使用的參數名稱。"
            };

            fmtMultirotorStatus = CreateFmtStatusLabel("FmtMultirotorStatus", new Point(16, 223),
                "等待辨識構型");

            fmtCommonSettings.Controls.AddRange(new Control[]
            {
                CreateFmtLabel("導航速度參數", new Point(16, 30), new Size(122, 22)),
                fmtNavigationSpeed,
                CreateFmtUnitLabel(new Point(231, 30)),
                CreateFmtLabel("GPS 速度參數", new Point(295, 30), new Size(130, 22)),
                fmtGpsSpeed,
                CreateFmtUnitLabel(new Point(518, 30)),
                CreateFmtLabel("WP 接受半徑", new Point(16, 69), new Size(122, 22)),
                fmtWpRadius,
                CreateFmtDistanceUnitLabel(new Point(231, 69)),
                CreateFmtLabel("RTL 速度", new Point(295, 69), new Size(130, 22)),
                fmtRtlSpeed,
                CreateFmtUnitLabel(new Point(518, 69)),
                CreateFmtLabel("WP 航向", new Point(16, 108), new Size(122, 22)),
                fmtWpYaw,
                CreateFmtLabel("RTL 航向", new Point(295, 108), new Size(130, 22)),
                fmtRtlYaw,
                CreateFmtLabel("最大上升速度", new Point(16, 147), new Size(122, 22)),
                fmtClimbSpeed,
                CreateFmtUnitLabel(new Point(231, 147)),
                CreateFmtLabel("最大下降速度", new Point(295, 147), new Size(130, 22)),
                fmtDescentSpeed,
                CreateFmtUnitLabel(new Point(518, 147)),
                CreateFmtLabel("RTL 返航高度", new Point(16, 186), new Size(122, 22)),
                fmtRtlAltitude,
                CreateFmtDistanceUnitLabel(new Point(231, 186)),
                fmtMultirotorStatus,
                fmtSaveCommonSettings,
                CreateFmtDescriptionLabel(new Point(16, 259),
                    "說明：導航、定點、上升／下降、WP 半徑與 RTL 高度／速度均使用 Copter 對應參數；WP／RTL 航向由共用 WP_YAW_BEHAVIOR 控制。"),
                fmtCommonParameterHint
            });
            Controls.Add(fmtCommonSettings);

            CreateFmtFixedWingSettings();
            CreateFmtVtolSettings();
            ApplyFmtCommonSettingsTheme();
        }

        private void CreateFmtFixedWingSettings()
        {
            fmtFixedWingSettings = new GroupBox
            {
                Name = "FmtFixedWingSettings",
                Text = "定翼機常用設定",
                Location = new Point(0, fmtCommonSettings.Bottom + 10),
                Size = new Size(593, 326),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Visible = true
            };

            fmtPlaneCruiseSpeed = CreateFmtSpeedControl("FmtPlaneCruiseSpeed", new Point(143, 28), 0.1m);
            fmtPlaneGpsSpeed = CreateFmtSpeedControl("FmtPlaneGpsSpeed", new Point(430, 28), 0m);
            fmtPlaneWpRadius = CreateFmtDistanceControl("FmtPlaneWpRadius", new Point(143, 67), 0m);
            fmtPlaneLoiterRadius = CreateFmtDistanceControl("FmtPlaneLoiterRadius", new Point(430, 67), 0m);
            fmtPlaneRtlRadius = CreateFmtDistanceControl("FmtPlaneRtlRadius", new Point(143, 106), 0m);
            fmtPlaneRtlAltitude = CreateFmtDistanceControl("FmtPlaneRtlAltitude", new Point(430, 106), 0m);
            fmtPlaneStatus = CreateFmtStatusLabel("FmtPlaneStatus", new Point(16, 183),
                "等待辨識構型");
            fmtSavePlaneSettings = CreateFmtSaveButton("FmtSavePlaneSettings", new Point(430, 177),
                "儲存定翼設定", SaveFmtPlaneSettings);
            fmtPlaneParameterHint = CreateFmtParameterHint("FmtPlaneParameterHint", new Point(16, 293));

            fmtFixedWingSettings.Controls.AddRange(new Control[]
            {
                CreateFmtLabel("巡航／RTL 空速", new Point(16, 31), new Size(122, 22)),
                fmtPlaneCruiseSpeed,
                CreateFmtUnitLabel(new Point(231, 31)),
                CreateFmtLabel("最低 GPS 地速", new Point(295, 31), new Size(130, 22)),
                fmtPlaneGpsSpeed,
                CreateFmtUnitLabel(new Point(518, 31)),
                CreateFmtLabel("WP 接受半徑", new Point(16, 70), new Size(122, 22)),
                fmtPlaneWpRadius,
                CreateFmtDistanceUnitLabel(new Point(231, 70)),
                CreateFmtLabel("盤旋半徑", new Point(295, 70), new Size(130, 22)),
                fmtPlaneLoiterRadius,
                CreateFmtDistanceUnitLabel(new Point(518, 70)),
                CreateFmtLabel("RTL 盤旋半徑", new Point(16, 109), new Size(122, 22)),
                fmtPlaneRtlRadius,
                CreateFmtDistanceUnitLabel(new Point(231, 109)),
                CreateFmtLabel("RTL 返航高度", new Point(295, 109), new Size(130, 22)),
                fmtPlaneRtlAltitude,
                CreateFmtDistanceUnitLabel(new Point(518, 109)),
                CreateFmtLabel("WP／RTL 航向", new Point(16, 148), new Size(122, 22)),
                CreateFmtValueLabel("由航線自動控制", new Point(143, 148), new Size(180, 22)),
                fmtPlaneStatus,
                fmtSavePlaneSettings,
                CreateFmtDescriptionLabel(new Point(16, 219),
                    "說明：巡航空速供 AUTO／GUIDED／RTL 使用；最低地速協助逆風飛行；WP、盤旋與 RTL 半徑控制航線轉彎，RTL 高度控制返航盤旋高度。"),
                fmtPlaneParameterHint
            });
            Controls.Add(fmtFixedWingSettings);
        }

        private void CreateFmtVtolSettings()
        {
            fmtVtolSettings = new GroupBox
            {
                Name = "FmtVtolSettings",
                Text = "VTOL／QuadPlane 常用設定",
                Location = new Point(0, fmtFixedWingSettings.Bottom + 10),
                Size = new Size(593, 362),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Visible = true
            };

            fmtVtolCruiseSpeed = CreateFmtSpeedControl("FmtVtolCruiseSpeed", new Point(143, 28), 0.1m);
            fmtVtolNavigationSpeed = CreateFmtSpeedControl("FmtVtolNavigationSpeed", new Point(430, 28), 0.1m);
            fmtVtolGpsSpeed = CreateFmtSpeedControl("FmtVtolGpsSpeed", new Point(143, 67), 0.1m);
            fmtVtolRtlMode = CreateFmtYawCombo("FmtVtolRtlMode", new Point(430, 67));
            fmtVtolWpRadius = CreateFmtDistanceControl("FmtVtolWpRadius", new Point(143, 106), 0.1m);
            fmtVtolClimbSpeed = CreateFmtSpeedControl("FmtVtolClimbSpeed", new Point(143, 145), 0.1m);
            fmtVtolDescentSpeed = CreateFmtSpeedControl("FmtVtolDescentSpeed", new Point(430, 145), 0.1m);
            fmtVtolRtlAltitude = CreateFmtDistanceControl("FmtVtolRtlAltitude", new Point(143, 184), 0m);
            fmtVtolRtlMode.Items.AddRange(new object[]
            {
                new FmtSelectionOption(0, "定翼返航盤旋"),
                new FmtSelectionOption(1, "接近後垂直降落"),
                new FmtSelectionOption(2, "VTOL 進場"),
                new FmtSelectionOption(3, "總是使用 QRTL")
            });
            fmtVtolStatus = CreateFmtStatusLabel("FmtVtolStatus", new Point(16, 224),
                "等待辨識構型");
            fmtSaveVtolSettings = CreateFmtSaveButton("FmtSaveVtolSettings", new Point(430, 218),
                "儲存 VTOL 設定", SaveFmtVtolSettings);
            fmtVtolParameterHint = CreateFmtParameterHint("FmtVtolParameterHint", new Point(16, 329));

            fmtVtolSettings.Controls.AddRange(new Control[]
            {
                CreateFmtLabel("定翼巡航空速", new Point(16, 31), new Size(122, 22)),
                fmtVtolCruiseSpeed,
                CreateFmtUnitLabel(new Point(231, 31)),
                CreateFmtLabel("VTOL 導航／返航速度", new Point(295, 31), new Size(130, 22)),
                fmtVtolNavigationSpeed,
                CreateFmtUnitLabel(new Point(518, 31)),
                CreateFmtLabel("VTOL GPS 速度", new Point(16, 70), new Size(122, 22)),
                fmtVtolGpsSpeed,
                CreateFmtUnitLabel(new Point(231, 70)),
                CreateFmtLabel("VTOL 返航模式", new Point(295, 70), new Size(130, 22)),
                fmtVtolRtlMode,
                CreateFmtLabel("VTOL WP 半徑", new Point(16, 109), new Size(122, 22)),
                fmtVtolWpRadius,
                CreateFmtDistanceUnitLabel(new Point(231, 109)),
                CreateFmtLabel("WP 航向", new Point(295, 109), new Size(130, 22)),
                CreateFmtValueLabel("航線／QAUTO 控制", new Point(430, 109), new Size(145, 22)),
                CreateFmtLabel("VTOL 上升速度", new Point(16, 148), new Size(122, 22)),
                fmtVtolClimbSpeed,
                CreateFmtUnitLabel(new Point(231, 148)),
                CreateFmtLabel("VTOL 下降速度", new Point(295, 148), new Size(130, 22)),
                fmtVtolDescentSpeed,
                CreateFmtUnitLabel(new Point(518, 148)),
                CreateFmtLabel("QRTL 返航高度", new Point(16, 187), new Size(122, 22)),
                fmtVtolRtlAltitude,
                CreateFmtDistanceUnitLabel(new Point(231, 187)),
                CreateFmtLabel("RTL 航向", new Point(295, 187), new Size(130, 22)),
                CreateFmtValueLabel("由 QRTL 模式控制", new Point(430, 187), new Size(145, 22)),
                fmtVtolStatus,
                fmtSaveVtolSettings,
                CreateFmtDescriptionLabel(new Point(16, 260),
                    "說明：定翼巡航空速用於轉換前後；Q 導航、定點、上升／下降、WP 半徑、QRTL 高度與模式共同控制垂直飛行及返航降落。"),
                fmtVtolParameterHint
            });
            Controls.Add(fmtVtolSettings);
        }

        private void CreateFmtHelicopterSettings()
        {
            fmtHelicopterSettings = new GroupBox
            {
                Name = "FmtHelicopterSettings",
                Text = "直升機常用設定",
                Location = new Point(0, fmtVtolSettings.Bottom + 10),
                Size = new Size(593, 190),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Visible = true
            };

            fmtHeliRpmLower = new NumericUpDown
            {
                Name = "FmtHeliRpmLower",
                Location = new Point(143, 29),
                Size = new Size(82, 24),
                Minimum = 0,
                Maximum = 9999,
                DecimalPlaces = 0,
                TextAlign = HorizontalAlignment.Right
            };
            fmtHeliRpmUpper = new NumericUpDown
            {
                Name = "FmtHeliRpmUpper",
                Location = new Point(430, 29),
                Size = new Size(82, 24),
                Minimum = 1,
                Maximum = 9999,
                DecimalPlaces = 0,
                TextAlign = HorizontalAlignment.Right
            };
            fmtHeliStatus = CreateFmtStatusLabel("FmtHeliStatus", new Point(16, 72),
                "等待辨識構型");
            fmtSaveHeliSettings = CreateFmtSaveButton("FmtSaveHeliSettings", new Point(430, 68),
                "儲存轉速警告", SaveFmtHelicopterSettings);

            fmtHelicopterSettings.Controls.AddRange(new Control[]
            {
                CreateFmtLabel("RPM1 轉速下限", new Point(16, 32), new Size(122, 22)),
                fmtHeliRpmLower,
                CreateFmtLabel("RPM", new Point(231, 32), new Size(45, 22)),
                CreateFmtLabel("RPM1 轉速上限", new Point(295, 32), new Size(130, 22)),
                fmtHeliRpmUpper,
                CreateFmtLabel("RPM", new Point(518, 32), new Size(45, 22)),
                fmtHeliStatus,
                fmtSaveHeliSettings,
                CreateFmtDescriptionLabel(new Point(16, 108),
                    "說明：主畫面讀取 RPM1；轉速低於下限或高於上限時以警告色顯示。數值限制為 0～9999 RPM，不顯示小數。")
            });
            Controls.Add(fmtHelicopterSettings);
        }

        private static Label CreateFmtStatusLabel(string name, Point location, string text)
        {
            return new Label
            {
                Name = name,
                Location = location,
                Size = new Size(260, 28),
                Text = text,
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateFmtParameterHint(string name, Point location)
        {
            return new Label
            {
                Name = name,
                Location = location,
                Size = new Size(575, 22),
                AutoEllipsis = true,
                ForeColor = Color.Gray,
                Text = "連線後顯示飛控實際使用的參數名稱。"
            };
        }

        private static Button CreateFmtSaveButton(string name, Point location, string text,
            EventHandler clickHandler)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                Location = location,
                Size = new Size(145, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 171, 226),
                ForeColor = Color.White
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += clickHandler;
            return button;
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

        private static NumericUpDown CreateFmtDistanceControl(string name, Point location, decimal minimum)
        {
            var control = CreateFmtSpeedControl(name, location, minimum);
            control.Maximum = 1000m;
            return control;
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

        private static Label CreateFmtValueLabel(string text, Point location, Size size)
        {
            var label = CreateFmtLabel(text, location, size);
            label.ForeColor = Color.Silver;
            return label;
        }

        private static Label CreateFmtUnitLabel(Point location)
        {
            return CreateFmtLabel("m/s", location, new Size(45, 22));
        }

        private static Label CreateFmtDistanceUnitLabel(Point location)
        {
            return CreateFmtLabel("m", location, new Size(45, 22));
        }

        private static Label CreateFmtDescriptionLabel(Point location, string text)
        {
            return new Label
            {
                Location = location,
                Size = new Size(559, 31),
                Text = text,
                ForeColor = Color.Silver
            };
        }

        private void LoadFmtCommonSettings()
        {
            if (fmtCommonSettings == null)
                return;

            var hasParameterData = MainV2.comPort.MAV.param.Count > 0;
            var isCopter = hasParameterData &&
                           MainV2.comPort.MAV.cs.firmware == Firmwares.ArduCopter2;
            var isHelicopter = isCopter &&
                               (MainV2.comPort.MAV.param.ContainsKey("H_RSC_MODE") ||
                                MainV2.comPort.MAV.param.ContainsKey("H_SWASH_TYPE") ||
                                MainV2.comPort.MAV.param.ContainsKey("H_COL_MIN"));
            var isMultirotor = isCopter && !isHelicopter;
            var isPlane = hasParameterData &&
                          (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduPlane ||
                           MainV2.comPort.MAV.cs.firmware == Firmwares.Ateryx);
            var isVtol = isPlane && IsFmtVtolEnabled();
            var isFixedWing = isPlane && !isVtol;
            var canWrite = MainV2.comPort.BaseStream != null && MainV2.comPort.BaseStream.IsOpen &&
                           !MainV2.comPort.ReadOnly && !MainV2.comPort.MAV.cs.armed;

            ApplyFmtCommonSettingsTheme();
            LoadFmtMultirotorSettings(isMultirotor, canWrite);
            LoadFmtFixedWingSettings(isFixedWing, canWrite);
            LoadFmtVtolSettings(isVtol, canWrite);
            fmtVehicleNotice.Text = isHelicopter
                ? "目前辨識構型：直升機（導航與轉速警告請至「直升機設定」）"
                : isMultirotor
                ? "目前辨識構型：多旋翼"
                : isVtol
                    ? "目前辨識構型：VTOL／QuadPlane"
                    : isFixedWing
                        ? "目前辨識構型：定翼機"
                        : "連線後會依飛控構型啟用對應設定方框。";
        }

        private void LoadFmtHelicopterSettings(bool active)
        {
            var lower = Math.Max(0, Math.Min(9999,
                Settings.Instance.GetInt32("FMT_HeliRpmLower", 1000)));
            var upper = Math.Max(1, Math.Min(9999,
                Settings.Instance.GetInt32("FMT_HeliRpmUpper", 2500)));
            fmtHeliRpmLower.Value = lower;
            fmtHeliRpmUpper.Value = upper;
            SetFmtStatus(fmtHeliStatus, active, "直升機構型已啟用；RPM1 警告生效");
        }

        private void LoadFmtMultirotorSettings(bool active, bool canWrite)
        {
            fmtNavigationSpeedParameter = active ? FindFmtParameter("WP_SPD", "WPNAV_SPEED") : null;
            fmtGpsSpeedParameter = active
                ? FindFmtParameter("LOIT_SPEED_MS", "WPNAV_LOIT_SPEED", "LOIT_SPEED")
                : null;
            fmtWpRadiusParameter = active ? FindFmtParameter("WP_RADIUS_M", "WPNAV_RADIUS") : null;
            fmtYawBehaviorParameter = active ? FindFmtParameter("WP_YAW_BEHAVIOR") : null;
            fmtRtlSpeedParameter = active ? FindFmtParameter("RTL_SPEED_MS", "RTL_SPEED") : null;
            fmtClimbSpeedParameter = active ? FindFmtParameter("WPNAV_SPEED_UP") : null;
            fmtDescentSpeedParameter = active ? FindFmtParameter("WPNAV_SPEED_DN") : null;
            fmtRtlAltitudeParameter = active ? FindFmtParameter("RTL_ALT") : null;

            LoadFmtSpeed(fmtNavigationSpeed, fmtNavigationSpeedParameter);
            LoadFmtSpeed(fmtGpsSpeed, fmtGpsSpeedParameter);
            LoadFmtDistance(fmtWpRadius, fmtWpRadiusParameter);
            LoadFmtSpeed(fmtRtlSpeed, fmtRtlSpeedParameter);
            LoadFmtSpeed(fmtClimbSpeed, fmtClimbSpeedParameter);
            LoadFmtSpeed(fmtDescentSpeed, fmtDescentSpeedParameter);
            LoadFmtDistance(fmtRtlAltitude, fmtRtlAltitudeParameter);

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
            AddFmtParameterName(parameters, fmtWpRadiusParameter);
            AddFmtParameterName(parameters, fmtYawBehaviorParameter);
            AddFmtParameterName(parameters, fmtRtlSpeedParameter);
            AddFmtParameterName(parameters, fmtClimbSpeedParameter);
            AddFmtParameterName(parameters, fmtDescentSpeedParameter);
            AddFmtParameterName(parameters, fmtRtlAltitudeParameter);
            fmtCommonParameterHint.Text = !active
                ? "此方框只寫入多旋翼參數。"
                : parameters.Count == 0
                ? "目前飛控沒有支援的常用設定參數。"
                : "使用參數：" + string.Join("、", parameters);
            fmtSaveCommonSettings.Enabled = active && canWrite && parameters.Count > 0;
            SetFmtStatus(fmtMultirotorStatus, active, "多旋翼構型已啟用");
        }

        private void LoadFmtFixedWingSettings(bool active, bool canWrite)
        {
            fmtPlaneCruiseSpeedParameter = active
                ? FindFmtParameter("AIRSPEED_CRUISE", "TRIM_ARSPD_CM")
                : null;
            fmtPlaneGpsSpeedParameter = active
                ? FindFmtParameter("MIN_GROUNDSPEED", "MIN_GNDSPD_CM")
                : null;
            fmtPlaneWpRadiusParameter = active ? FindFmtParameter("WP_RADIUS") : null;
            fmtPlaneLoiterRadiusParameter = active ? FindFmtParameter("WP_LOITER_RAD") : null;
            fmtPlaneRtlRadiusParameter = active ? FindFmtParameter("RTL_RADIUS") : null;
            fmtPlaneRtlAltitudeParameter = active ? FindFmtParameter("ALT_HOLD_RTL") : null;

            LoadFmtSpeed(fmtPlaneCruiseSpeed, fmtPlaneCruiseSpeedParameter);
            LoadFmtSpeed(fmtPlaneGpsSpeed, fmtPlaneGpsSpeedParameter);
            LoadFmtDistance(fmtPlaneWpRadius, fmtPlaneWpRadiusParameter);
            LoadFmtDistance(fmtPlaneLoiterRadius, fmtPlaneLoiterRadiusParameter);
            LoadFmtDistance(fmtPlaneRtlRadius, fmtPlaneRtlRadiusParameter);
            LoadFmtDistance(fmtPlaneRtlAltitude, fmtPlaneRtlAltitudeParameter);

            var parameters = new List<string>();
            AddFmtParameterName(parameters, fmtPlaneCruiseSpeedParameter);
            AddFmtParameterName(parameters, fmtPlaneGpsSpeedParameter);
            AddFmtParameterName(parameters, fmtPlaneWpRadiusParameter);
            AddFmtParameterName(parameters, fmtPlaneLoiterRadiusParameter);
            AddFmtParameterName(parameters, fmtPlaneRtlRadiusParameter);
            AddFmtParameterName(parameters, fmtPlaneRtlAltitudeParameter);
            fmtPlaneParameterHint.Text = !active
                ? "此方框只寫入純定翼機參數；WP／RTL 航向由航線自動控制。"
                : parameters.Count == 0
                    ? "目前飛控沒有支援的定翼常用參數。"
                    : "使用參數：" + string.Join("、", parameters) + "；WP／RTL 航向由航線自動控制。";
            fmtSavePlaneSettings.Enabled = active && canWrite && parameters.Count > 0;
            SetFmtStatus(fmtPlaneStatus, active, "定翼機構型已啟用");
        }

        private void LoadFmtVtolSettings(bool active, bool canWrite)
        {
            fmtVtolCruiseSpeedParameter = active
                ? FindFmtParameter("AIRSPEED_CRUISE", "TRIM_ARSPD_CM")
                : null;
            fmtVtolNavigationSpeedParameter = active
                ? FindFmtParameter("Q_WP_SPD", "Q_WP_SPEED")
                : null;
            fmtVtolGpsSpeedParameter = active
                ? FindFmtParameter("Q_LOIT_SPEED_MS", "Q_LOIT_SPEED")
                : null;
            fmtVtolWpRadiusParameter = active
                ? FindFmtParameter("Q_WP_RADIUS_M", "Q_WP_RADIUS")
                : null;
            fmtVtolRtlModeParameter = active ? FindFmtParameter("Q_RTL_MODE") : null;
            fmtVtolClimbSpeedParameter = active
                ? FindFmtParameter("Q_WP_SPD_UP", "Q_WP_SPEED_UP")
                : null;
            fmtVtolDescentSpeedParameter = active
                ? FindFmtParameter("Q_WP_SPD_DN", "Q_WP_SPEED_DN")
                : null;
            fmtVtolRtlAltitudeParameter = active ? FindFmtParameter("Q_RTL_ALT") : null;

            LoadFmtSpeed(fmtVtolCruiseSpeed, fmtVtolCruiseSpeedParameter);
            LoadFmtSpeed(fmtVtolNavigationSpeed, fmtVtolNavigationSpeedParameter);
            LoadFmtSpeed(fmtVtolGpsSpeed, fmtVtolGpsSpeedParameter);
            LoadFmtDistance(fmtVtolWpRadius, fmtVtolWpRadiusParameter);
            LoadFmtSpeed(fmtVtolClimbSpeed, fmtVtolClimbSpeedParameter);
            LoadFmtSpeed(fmtVtolDescentSpeed, fmtVtolDescentSpeedParameter);
            LoadFmtDistance(fmtVtolRtlAltitude, fmtVtolRtlAltitudeParameter);
            fmtVtolRtlMode.Enabled = fmtVtolRtlModeParameter != null;
            fmtVtolRtlMode.SelectedIndex = -1;
            if (fmtVtolRtlModeParameter != null)
            {
                var rtlMode = (int)Math.Round(MainV2.comPort.MAV.param[fmtVtolRtlModeParameter].Value);
                SelectFmtOption(fmtVtolRtlMode, rtlMode);
            }

            var parameters = new List<string>();
            AddFmtParameterName(parameters, fmtVtolCruiseSpeedParameter);
            AddFmtParameterName(parameters, fmtVtolNavigationSpeedParameter);
            AddFmtParameterName(parameters, fmtVtolGpsSpeedParameter);
            AddFmtParameterName(parameters, fmtVtolWpRadiusParameter);
            AddFmtParameterName(parameters, fmtVtolRtlModeParameter);
            AddFmtParameterName(parameters, fmtVtolClimbSpeedParameter);
            AddFmtParameterName(parameters, fmtVtolDescentSpeedParameter);
            AddFmtParameterName(parameters, fmtVtolRtlAltitudeParameter);
            fmtVtolParameterHint.Text = !active
                ? "此方框只在 Q_ENABLE／VTOL 構型啟用後寫入 Q_ 參數。"
                : parameters.Count == 0
                    ? "目前飛控沒有支援的 VTOL 常用參數。"
                    : "使用參數：" + string.Join("、", parameters);
            fmtSaveVtolSettings.Enabled = active && canWrite && parameters.Count > 0;
            SetFmtStatus(fmtVtolStatus, active, "VTOL／QuadPlane 構型已啟用");
        }

        private static bool IsFmtVtolEnabled()
        {
            if (MainV2.comPort.MAV.param.ContainsKey("Q_ENABLE"))
                return IsFmtParameterEnabled("Q_ENABLE");

            return IsFmtParameterEnabled("Q_TILT_ENABLE") ||
                   IsFmtParameterEnabled("Q_TAILSIT_ENABLE") ||
                   IsFmtParameterEnabled("Q_FRAME_CLASS") ||
                   MainV2.comPort.MAV.param.ContainsKey("Q_WP_SPD") ||
                   MainV2.comPort.MAV.param.ContainsKey("Q_WP_SPEED");
        }

        private static bool IsFmtParameterEnabled(string parameterName)
        {
            return MainV2.comPort.MAV.param.ContainsKey(parameterName) &&
                   Math.Abs(MainV2.comPort.MAV.param[parameterName].Value) > double.Epsilon;
        }

        private static void SetFmtStatus(Label status, bool active, string activeText)
        {
            status.Text = active ? "● " + activeText : "○ 非目前連線構型";
            status.ForeColor = active ? Color.LimeGreen : Color.Gray;
        }

        private void ApplyFmtCommonSettingsTheme()
        {
            ThemeManager.ApplyThemeTo(fmtCommonSettings);
            ThemeManager.ApplyThemeTo(fmtFixedWingSettings);
            ThemeManager.ApplyThemeTo(fmtVtolSettings);
            var inputBackground = Color.FromArgb(67, 68, 69);
            foreach (var control in new Control[]
                     {
                         fmtNavigationSpeed, fmtGpsSpeed, fmtRtlSpeed, fmtWpYaw, fmtRtlYaw,
                         fmtWpRadius, fmtClimbSpeed, fmtDescentSpeed, fmtRtlAltitude,
                         fmtPlaneCruiseSpeed, fmtPlaneGpsSpeed, fmtPlaneWpRadius,
                         fmtPlaneLoiterRadius, fmtPlaneRtlRadius, fmtPlaneRtlAltitude,
                         fmtVtolCruiseSpeed, fmtVtolNavigationSpeed, fmtVtolGpsSpeed, fmtVtolWpRadius,
                         fmtVtolClimbSpeed, fmtVtolDescentSpeed, fmtVtolRtlAltitude, fmtVtolRtlMode
                     })
            {
                control.BackColor = inputBackground;
                control.ForeColor = Color.White;
            }

            fmtSaveCommonSettings.BackColor = Color.FromArgb(41, 171, 226);
            fmtSaveCommonSettings.ForeColor = Color.White;
            fmtSavePlaneSettings.BackColor = Color.FromArgb(41, 171, 226);
            fmtSavePlaneSettings.ForeColor = Color.White;
            fmtSaveVtolSettings.BackColor = Color.FromArgb(41, 171, 226);
            fmtSaveVtolSettings.ForeColor = Color.White;
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

        private static void LoadFmtDistance(NumericUpDown control, string parameterName)
        {
            control.Enabled = parameterName != null;
            if (parameterName == null)
                return;

            var meters = FmtDistanceRawToMeters(parameterName,
                MainV2.comPort.MAV.param[parameterName].Value);
            control.Value = (decimal)Math.Max((double)control.Minimum,
                Math.Min((double)control.Maximum, meters));
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

        internal static double FmtDistanceRawToMeters(string parameterName, double rawValue)
        {
            return IsFmtLegacyCentimeterDistance(parameterName) ? rawValue / 100.0 : rawValue;
        }

        internal static double FmtDistanceMetersToRaw(string parameterName, double meters)
        {
            return IsFmtLegacyCentimeterDistance(parameterName)
                ? Math.Round(meters * 100.0, MidpointRounding.AwayFromZero)
                : meters;
        }

        private static bool IsFmtLegacyCentimeterDistance(string parameterName)
        {
            return string.Equals(parameterName, "WPNAV_RADIUS", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "Q_WP_RADIUS", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "RTL_ALT", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "Q_RTL_ALT", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "ALT_HOLD_RTL", StringComparison.Ordinal);
        }

        private static bool IsFmtLegacyCentimeterSpeed(string parameterName)
        {
            return string.Equals(parameterName, "WPNAV_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "WPNAV_LOIT_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "WPNAV_SPEED_UP", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "WPNAV_SPEED_DN", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "LOIT_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "RTL_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "TRIM_ARSPD_CM", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "MIN_GNDSPD_CM", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "Q_WP_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "Q_LOIT_SPEED", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "Q_WP_SPEED_UP", StringComparison.Ordinal) ||
                   string.Equals(parameterName, "Q_WP_SPEED_DN", StringComparison.Ordinal);
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

        private void SaveFmtHelicopterSettings(object sender, EventArgs e)
        {
            var lower = (int)fmtHeliRpmLower.Value;
            var upper = (int)fmtHeliRpmUpper.Value;
            if (lower >= upper)
            {
                CustomMessageBox.Show("轉速下限必須小於轉速上限。", "直升機轉速警告",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Settings.Instance["FMT_HeliRpmLower"] = lower.ToString();
            Settings.Instance["FMT_HeliRpmUpper"] = upper.ToString();
            CustomMessageBox.Show("RPM1 轉速上下限警告已儲存。", "直升機轉速警告",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveFmtCommonSettings(object sender, EventArgs e)
        {
            if (!ValidateFmtCommonWrite("多旋翼常用設定"))
                return;

            var confirmation = "確定要將導航、GPS 定位、WP 半徑、WP／RTL 航向及 RTL 速度寫入飛控嗎？";
            if (CustomMessageBox.Show(confirmation, "常用設定", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != (int)DialogResult.Yes)
                return;

            try
            {
                var failures = new List<string>();
                SaveFmtSpeed(fmtNavigationSpeed, fmtNavigationSpeedParameter, failures);
                SaveFmtSpeed(fmtGpsSpeed, fmtGpsSpeedParameter, failures);
                SaveFmtDistance(fmtWpRadius, fmtWpRadiusParameter, failures);
                SaveFmtSpeed(fmtRtlSpeed, fmtRtlSpeedParameter, failures);
                SaveFmtSpeed(fmtClimbSpeed, fmtClimbSpeedParameter, failures);
                SaveFmtSpeed(fmtDescentSpeed, fmtDescentSpeedParameter, failures);
                SaveFmtDistance(fmtRtlAltitude, fmtRtlAltitudeParameter, failures);

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

        private void SaveFmtPlaneSettings(object sender, EventArgs e)
        {
            if (!ValidateFmtCommonWrite("定翼機常用設定"))
                return;

            if (CustomMessageBox.Show("確定要將定翼巡航／RTL 空速、最低 GPS 地速、航點／盤旋半徑及 RTL 高度寫入飛控嗎？",
                    "定翼機常用設定", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) !=
                (int)DialogResult.Yes)
                return;

            try
            {
                var failures = new List<string>();
                SaveFmtSpeed(fmtPlaneCruiseSpeed, fmtPlaneCruiseSpeedParameter, failures);
                SaveFmtSpeed(fmtPlaneGpsSpeed, fmtPlaneGpsSpeedParameter, failures);
                SaveFmtDistance(fmtPlaneWpRadius, fmtPlaneWpRadiusParameter, failures);
                SaveFmtDistance(fmtPlaneLoiterRadius, fmtPlaneLoiterRadiusParameter, failures);
                SaveFmtDistance(fmtPlaneRtlRadius, fmtPlaneRtlRadiusParameter, failures);
                SaveFmtDistance(fmtPlaneRtlAltitude, fmtPlaneRtlAltitudeParameter, failures);
                FinishFmtCommonWrite("定翼機常用設定", failures);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("定翼機常用設定寫入失敗：" + ex.Message, "定翼機常用設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveFmtVtolSettings(object sender, EventArgs e)
        {
            if (!ValidateFmtCommonWrite("VTOL／QuadPlane 常用設定"))
                return;

            if (CustomMessageBox.Show("確定要將定翼巡航、VTOL 導航／返航、GPS 速度、WP 半徑及返航模式寫入飛控嗎？",
                    "VTOL／QuadPlane 常用設定", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) !=
                (int)DialogResult.Yes)
                return;

            try
            {
                var failures = new List<string>();
                SaveFmtSpeed(fmtVtolCruiseSpeed, fmtVtolCruiseSpeedParameter, failures);
                SaveFmtSpeed(fmtVtolNavigationSpeed, fmtVtolNavigationSpeedParameter, failures);
                SaveFmtSpeed(fmtVtolGpsSpeed, fmtVtolGpsSpeedParameter, failures);
                SaveFmtDistance(fmtVtolWpRadius, fmtVtolWpRadiusParameter, failures);
                SaveFmtSpeed(fmtVtolClimbSpeed, fmtVtolClimbSpeedParameter, failures);
                SaveFmtSpeed(fmtVtolDescentSpeed, fmtVtolDescentSpeedParameter, failures);
                SaveFmtDistance(fmtVtolRtlAltitude, fmtVtolRtlAltitudeParameter, failures);
                var rtlMode = fmtVtolRtlMode.SelectedItem as FmtSelectionOption;
                if (fmtVtolRtlModeParameter != null && rtlMode != null &&
                    !SetFmtParameter(fmtVtolRtlModeParameter, rtlMode.Value))
                    failures.Add(fmtVtolRtlModeParameter);
                FinishFmtCommonWrite("VTOL／QuadPlane 常用設定", failures);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("VTOL／QuadPlane 常用設定寫入失敗：" + ex.Message,
                    "VTOL／QuadPlane 常用設定", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool ValidateFmtCommonWrite(string title)
        {
            if (MainV2.comPort.BaseStream == null || !MainV2.comPort.BaseStream.IsOpen)
            {
                CustomMessageBox.Show("請先連線飛控。", title, MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (MainV2.comPort.MAV.cs.armed)
            {
                CustomMessageBox.Show("常用設定只能在飛機上鎖（未解鎖）時修改。", title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (MainV2.comPort.ReadOnly)
            {
                CustomMessageBox.Show("目前為唯讀連線，無法寫入參數。", title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private void FinishFmtCommonWrite(string title, ICollection<string> failures)
        {
            if (failures.Count > 0)
            {
                CustomMessageBox.Show("下列參數寫入失敗：" + string.Join("、", failures), title,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            CustomMessageBox.Show("常用設定已寫入飛控。", title, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            LoadFmtCommonSettings();
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

        private static void SaveFmtDistance(NumericUpDown control, string parameterName,
            ICollection<string> failures)
        {
            if (parameterName == null || !control.Enabled)
                return;

            var rawValue = FmtDistanceMetersToRaw(parameterName, (double)control.Value);
            if (!SetFmtParameter(parameterName, rawValue))
                failures.Add(parameterName);
        }

        private static bool SetFmtParameter(string parameterName, double value)
        {
            return MainV2.comPort.setParam((byte)MainV2.comPort.sysidcurrent,
                (byte)MainV2.comPort.compidcurrent, parameterName, value);
        }
    }

    /// <summary>
    /// Dedicated FMT common-settings page. Keeping this separate prevents the
    /// vehicle-specific settings cards from stretching the flight-mode page.
    /// </summary>
    public sealed class ConfigFmtCommonSettings : ConfigFlightModes
    {
        public ConfigFmtCommonSettings() : base(true)
        {
        }
    }
}
