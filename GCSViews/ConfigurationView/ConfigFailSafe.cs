using MissionPlanner.ArduPilot;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigFailSafe : MyUserControl, IActivate, IDeactivate
    {
        private readonly Timer _timer = new Timer();
        //

        public ConfigFailSafe()
        {
            InitializeComponent();
            ApplyFmtTraditionalChineseLayout();

            // setup rc update
            _timer.Tick += timer_Tick;
        }

        private void ApplyFmtTraditionalChineseLayout()
        {
            groupBox2.Text = "遙控失效保護";
            groupBox4.Text = "電池失效保護";
            groupBox3.Text = "地面站／固定翼失效保護";
            label3.Text = "失效保護 PWM 門檻";
            label6.Text = "低電壓延遲";
            if (lbl_armed.Text == "關閉" || lbl_armed.Text == "Dissarmed")
                lbl_armed.Text = "已上鎖";

            mavlinkCheckBoxFS_GCS_ENABLE.Text = "啟用地面站斷線保護";
            mavlinkCheckBoxgcs_fs.Text = "地面站斷線保護";
            mavlinkCheckBoxshort_fs.Text = "短時失效動作（0／1）";
            mavlinkCheckBoxlong_fs.Text = "長時失效動作（0／1）";

            var gcsOptions = new[]
            {
                mavlinkCheckBoxFS_GCS_ENABLE,
                mavlinkCheckBoxgcs_fs,
                mavlinkCheckBoxshort_fs,
                mavlinkCheckBoxlong_fs
            };

            groupBox3.Height = 108;
            for (var index = 0; index < gcsOptions.Length; index++)
            {
                var option = gcsOptions[index];
                option.AutoSize = false;
                option.SetBounds(8, 18 + index * 21, groupBox3.ClientSize.Width - 16, 20);
                option.TextAlign = ContentAlignment.MiddleLeft;
                option.AutoEllipsis = true;
            }

            toolTip1.SetToolTip(mavlinkCheckBoxFS_GCS_ENABLE,
                "啟用後，飛控在地面站遙測連線中斷時執行設定的失控保護動作。");
            toolTip1.SetToolTip(mavlinkCheckBoxgcs_fs,
                "固定翼地面站連線失效保護；實際動作依飛控韌體與其他失控保護參數決定。");
            toolTip1.SetToolTip(mavlinkCheckBoxshort_fs,
                "固定翼短時失效動作 FS_SHORT_ACTN，可能由遙控訊號遺失或油門失效觸發。此開關僅切換 0／1；0 不代表全面停用，實際動作依飛行模式而異。其他選項請使用完整參數頁。");
            toolTip1.SetToolTip(mavlinkCheckBoxlong_fs,
                "固定翼長時失效動作 FS_LONG_ACTN，逾時由 FS_LONG_TIMEOUT 設定。此開關僅切換 0／1；0 不代表全面停用，實際動作依飛行模式而異。其他選項請使用完整參數頁。");
        }

        public void Activate()
        {
            ApplyFmtTraditionalChineseLayout();

            mavlinkComboBox_fs_thr_enable.setup(
                ParameterMetaDataRepository.GetParameterOptionsInt("FS_THR_ENABLE",
                    MainV2.comPort.MAV.cs.firmware.ToString()), "FS_THR_ENABLE", MainV2.comPort.MAV.param);

            // arducopter
            if (MainV2.comPort.MAV.param.ContainsKey("BATT_FS_LOW_ACT"))
            {
                mavlinkComboBoxfs_batt_enable.setup(
                ParameterMetaDataRepository.GetParameterOptionsInt("BATT_FS_LOW_ACT",
                    MainV2.comPort.MAV.cs.firmware.ToString()), "BATT_FS_LOW_ACT", MainV2.comPort.MAV.param);
            }
            else
            {
                mavlinkComboBoxfs_batt_enable.setup(
                ParameterMetaDataRepository.GetParameterOptionsInt("FS_BATT_ENABLE",
                    MainV2.comPort.MAV.cs.firmware.ToString()), "FS_BATT_ENABLE", MainV2.comPort.MAV.param);
            }
            mavlinkNumericUpDownfs_thr_value.setup(800, 1200, 1, 1, "FS_THR_VALUE", MainV2.comPort.MAV.param);

            // low battery
            if (MainV2.comPort.MAV.param.ContainsKey("LOW_VOLT"))
            {
                mavlinkNumericUpDownlow_voltage.setup(6, 99, 1, 0.1f, "LOW_VOLT", MainV2.comPort.MAV.param, PNL_low_bat);
            }
            else if (MainV2.comPort.MAV.param.ContainsKey("FS_BATT_VOLTAGE"))
            {
                mavlinkNumericUpDownlow_voltage.setup(6, 99, 1, 0.1f, "FS_BATT_VOLTAGE", MainV2.comPort.MAV.param,
                    PNL_low_bat);
            }
            else
            {
                mavlinkNumericUpDownlow_voltage.setup(6, 99, 1, 0.1f, "BATT_LOW_VOLT", MainV2.comPort.MAV.param,
                    PNL_low_bat);
            }

            if (MainV2.comPort.MAV.param.ContainsKey("FS_BATT_MAH"))
            {
                mavlinkNumericUpDownFS_BATT_MAH.setup(0, 99999, 1, 1, "FS_BATT_MAH", MainV2.comPort.MAV.param, pnlmah);
            }
            else
            {
                mavlinkNumericUpDownFS_BATT_MAH.setup(0, 99999, 1, 1, "BATT_LOW_MAH", MainV2.comPort.MAV.param, pnlmah);
            }

            if (MainV2.comPort.MAV.param.ContainsKey("BATT_LOW_TIMER"))
            {
                mavlinkNumericUpDownBATT_LOW_TIMER.setup(0, 120, 1, 1, "BATT_LOW_TIMER", MainV2.comPort.MAV.param, pnltimer);
            }

            // removed at randys request
            //mavlinkCheckBoxfs_gps_enable.setup(1, 0, "FS_GPS_ENABLE", MainV2.comPort.MAV.param);
            mavlinkCheckBoxFS_GCS_ENABLE.setup(1, 0, "FS_GCS_ENABLE", MainV2.comPort.MAV.param);

            // plane
            mavlinkCheckBoxthr_fs.setup(1, 0, "THR_FAILSAFE", MainV2.comPort.MAV.param, mavlinkNumericUpDownthr_fs_value);
            mavlinkNumericUpDownthr_fs_value.setup(800, 1200, 1, 1, "THR_FS_VALUE", MainV2.comPort.MAV.param);
            mavlinkCheckBoxthr_fs_action.setup(1, 0, "THR_FS_ACTION", MainV2.comPort.MAV.param);
            mavlinkCheckBoxgcs_fs.setup(1, 0, "FS_GCS_ENABL", MainV2.comPort.MAV.param);
            mavlinkCheckBoxshort_fs.setup(1, 0, "FS_SHORT_ACTN", MainV2.comPort.MAV.param);
            mavlinkCheckBoxlong_fs.setup(1, 0, "FS_LONG_ACTN", MainV2.comPort.MAV.param);

            _timer.Enabled = true;
            _timer.Interval = 100;
            _timer.Start();

            CustomMessageBox.Show("Ensure your props are not on the Plane/Quad", "FailSafe", MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation);
        }

        public void Deactivate()
        {
            _timer.Stop();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            // update all linked controls - 10hz
            try
            {
                MainV2.comPort.MAV.cs.UpdateCurrentSettings(currentStateBindingSource.UpdateDataSource(MainV2.comPort.MAV.cs));
            }
            catch
            {
            }
        }

        private void LNK_wiki_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduCopter2)
            {
                Process.Start(new ProcessStartInfo("https://ardupilot.org/copter/docs/failsafe-landing-page.html"));
            }
            else
            {
                Process.Start(new ProcessStartInfo("https://ardupilot.org/plane/docs/advanced-failsafe-configuration.html"));
            }
        }

        private void lbl_armed_Paint(object sender, PaintEventArgs e)
        {
            lbl_armed.SuspendLayout();
            if (lbl_armed.Text == "True")
            {
                lbl_armed.Text = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                    ? "已解鎖" : "Armed";
            }
            else if (lbl_armed.Text == "False")
            {
                lbl_armed.Text = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                    ? "已上鎖" : "Disarmed";
            }
            lbl_armed.ResumeLayout();
        }

        private void lbl_gpslock_Paint(object sender, PaintEventArgs e)
        {
            var _gpsfix = 0;
            try
            {
                if (!int.TryParse(lbl_gpslock.Text, out _gpsfix))
                    return;
            }
            catch
            {
                return;
            }
            bool chinese = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            var gps = "GPS: " + MainV2.GetFmtGpsFixLabel(_gpsfix, chinese);
            lbl_gpslock.SuspendLayout();
            lbl_gpslock.Text = gps;
            lbl_gpslock.ResumeLayout();
        }

        private void lbl_currentmode_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (MainV2.comPort.MAV.param.ContainsKey("FS_THR_VALUE"))
                {
                    if (MainV2.comPort.MAV.cs.ch3in < (float)MainV2.comPort.MAV.param["FS_THR_VALUE"])
                    {
                        lbl_currentmode.ForeColor = Color.Red;
                    }
                    else
                    {
                        lbl_currentmode.ForeColor = Color.White;
                    }
                }
            }
            catch
            {
            }
        }
    }
}
