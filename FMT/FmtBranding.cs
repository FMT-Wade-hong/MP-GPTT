using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal static class FmtBranding
    {
        private const string FmtApplicationId = "FMT.FeiMaoTecPlanner";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        internal static void ApplyTaskbarIdentity()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;

            try
            {
                SetCurrentProcessExplicitAppUserModelID(FmtApplicationId);
            }
            catch
            {
                // Older Windows/compatibility environments do not expose this API.
                // The embedded FMT application icon remains the fallback.
            }
        }

        internal static void ApplyApplicationIcon(Form form)
        {
            if (form == null || MissionPlanner.Properties.Resources.mpdesktop == null)
                return;

            form.Icon = (Icon)MissionPlanner.Properties.Resources.mpdesktop.Clone();
        }
    }
}
