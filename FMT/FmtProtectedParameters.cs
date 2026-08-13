using MissionPlanner.Controls;
using MissionPlanner.GCSViews.ConfigurationView;
using MissionPlanner.Utilities;
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
            // This page creates its parameter control after the containing backstage view
            // has already been themed. Clear any cached white control before prompting and
            // explicitly theme dynamically-created content when it is added again.
            Controls.Clear();
            BackColor = ThemeManager.BGColor;
            ForeColor = ThemeManager.TextColor;

            using (var access = new FmtParameterAccessForm())
            {
                ThemeManager.ApplyThemeTo(access);
                if (access.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    ShowLockedMessage();
                    return;
                }
            }

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

        private void ShowLockedMessage()
        {
            Controls.Clear();
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Parameter access is locked. Select this page again to unlock it.",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                BackColor = ThemeManager.BGColor,
                ForeColor = Color.FromArgb(41, 171, 226)
            });
            ThemeManager.ApplyThemeTo(this);
        }
    }
}
