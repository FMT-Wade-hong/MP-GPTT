using MissionPlanner.Controls;
using MissionPlanner.GCSViews.ConfigurationView;
using System.Drawing;
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
            using (var access = new FmtParameterAccessForm())
            {
                if (access.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    ShowLockedMessage();
                    return;
                }
            }

            Controls.Clear();
            if (parameterControl == null || parameterControl.IsDisposed)
                parameterControl = new ConfigRawParams { Dock = DockStyle.Fill };
            Controls.Add(parameterControl);
            parameterControl.Activate();
        }

        public void Deactivate()
        {
            parameterControl?.Deactivate();
        }

        private void ShowLockedMessage()
        {
            Controls.Clear();
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Parameter access is locked. Select this page again to unlock it.",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(41, 171, 226)
            });
        }
    }
}
