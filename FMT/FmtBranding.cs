using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal static class FmtBranding
    {
        internal static void ApplyApplicationIcon(Form form)
        {
            if (form == null || MissionPlanner.Properties.Resources.mpdesktop == null)
                return;

            form.Icon = (Icon)MissionPlanner.Properties.Resources.mpdesktop.Clone();
        }
    }
}
