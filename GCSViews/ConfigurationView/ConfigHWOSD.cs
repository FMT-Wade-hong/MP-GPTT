using MissionPlanner.Controls;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigHWOSD : MyUserControl, IActivate
    {
        public ConfigHWOSD()
        {
            InitializeComponent();
        }

        public void Activate()
        {
            Enabled = MainV2.comPort?.BaseStream?.IsOpen == true && !MainV2.comPort.ReadOnly && !MainV2.comPort.logreadmode;
        }

        internal static string[] GetRateParameters(IEnumerable<string> available)
        {
            var names = new HashSet<string>(available, StringComparer.Ordinal);
            var result = new List<string>();
            // Retain the existing OSD stream instances (0, 1, 3). In 4.7
            // SR0/SR1/SR3 became MAV1/MAV2/MAV4; serial port IDs are unrelated.
            foreach (var instance in new[] { 0, 1, 3 })
            foreach (var suffix in new[] { "EXT_STAT", "EXTRA1", "EXTRA2", "EXTRA3", "POSITION", "RAW_CTRL", "RAW_SENS", "RC_CHAN" })
            {
                var modern = "MAV" + (instance + 1) + "_" + suffix;
                var legacy = "SR" + instance + "_" + suffix;
                if (names.Contains(modern)) result.Add(modern);
                else if (names.Contains(legacy)) result.Add(legacy);
            }
            return result.ToArray();
        }

        private void BUT_osdrates_Click(object sender, EventArgs e)
        {
            var port = MainV2.comPort;
            if (port?.BaseStream?.IsOpen != true || port.ReadOnly || port.logreadmode) return;
            try
            {
                var names = GetRateParameters(port.MAV.param.Keys);
                if (names.Length == 0) throw new InvalidOperationException("No OSD stream parameters reported.");
                foreach (var name in names)
                    if (!port.setParam((byte)port.sysidcurrent, (byte)port.compidcurrent, name, 2))
                        throw new InvalidOperationException("Failed to set " + name);
            }
            catch
            {
                CustomMessageBox.Show("Failed to set OSD rates.");
            }
        }
    }
}
