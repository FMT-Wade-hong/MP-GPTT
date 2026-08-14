using MissionPlanner.Controls;
using MissionPlanner.GCSViews.ConfigurationView;
using MissionPlanner.Utilities;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    public sealed class FmtProtectedParameters : MyUserControl, IActivate, IDeactivate
    {
        private ConfigRawParams parameterControl;

        public FmtProtectedParameters()
        {
            Dock = DockStyle.Fill;
        }

        public void Activate()
        {
            // Password validation is handled by the top-level FMT Parameter Settings page.
            // The full parameter control itself must stay editable and must never display a
            // second password prompt, otherwise its first activation can remain read-only.
            Controls.Clear();
            BackColor = ThemeManager.BGColor;
            ForeColor = ThemeManager.TextColor;

            if (parameterControl == null || parameterControl.IsDisposed)
                parameterControl = new ConfigRawParams { Dock = DockStyle.Fill };

            SuspendLayout();
            parameterControl.Visible = false;
            try
            {
                Controls.Add(parameterControl);
                parameterControl.ApplyFmtReadableTheme();
                parameterControl.Activate();
                parameterControl.ApplyFmtReadableTheme();
                parameterControl.EnableFmtEditing();
            }
            finally
            {
                parameterControl.Visible = true;
                ResumeLayout(true);
            }
        }

        public void Deactivate()
        {
            parameterControl?.Deactivate();
        }
    }
}
