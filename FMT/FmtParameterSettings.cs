using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    /// <summary>
    /// Top-level FMT parameter page. Access is validated before MainV2 opens this screen;
    /// the full parameter grid itself intentionally has no password gate.
    /// </summary>
    public sealed class FmtParameterSettings : MyUserControl, IActivate, IDeactivate
    {
        private static readonly Color SkyBlue = Color.FromArgb(41, 171, 226);
        private readonly FmtProtectedParameters fullParameters = new FmtProtectedParameters();
        private readonly Panel header = new Panel { Dock = DockStyle.Fill };
        private readonly TableLayoutPanel pageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        private readonly Label connectionStatus = new Label { AutoSize = true };
        private readonly Label passwordStatus = new Label { AutoSize = true };

        public FmtParameterSettings()
        {
            Dock = DockStyle.Fill;
            BuildHeader();

            pageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            fullParameters.Dock = DockStyle.Fill;
            pageLayout.Controls.Add(header, 0, 0);
            pageLayout.Controls.Add(fullParameters, 0, 1);
            Controls.Add(pageLayout);
            ApplyFmtTheme();
        }

        public void Activate()
        {
            ApplyFmtTheme();
            var connected = MainV2.comPort?.BaseStream != null && MainV2.comPort.BaseStream.IsOpen;
            connectionStatus.Text = connected
                ? "● 已連線，可直接修改完整參數；修改後請按「寫入參數」。"
                : "● 尚未連線；連線飛控後才可修改與寫入參數。";
            connectionStatus.ForeColor = connected ? Color.LimeGreen : Color.OrangeRed;

            fullParameters.Activate();
            ApplyFmtTheme();
        }

        public void Deactivate()
        {
            fullParameters.Deactivate();
            passwordStatus.Text = string.Empty;
        }

        private void BuildHeader()
        {
            var title = new Label
            {
                Text = "完整參數列表",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Location = new Point(14, 8)
            };
            var description = new Label
            {
                Text = "此頁密碼只保護「參數設定」入口，不會鎖住下方參數表。",
                AutoSize = true,
                Location = new Point(16, 39)
            };
            connectionStatus.Location = new Point(430, 13);
            passwordStatus.Location = new Point(430, 41);

            var changePassword = new Button
            {
                Name = "BUT_FmtParameterPassword",
                Text = "設定參數密碼",
                Width = 140,
                Height = 34,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Width - 154, 20),
                BackColor = SkyBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            changePassword.FlatAppearance.BorderSize = 0;
            changePassword.Click += ChangePassword_Click;
            header.Resize += (sender, args) =>
                changePassword.Left = System.Math.Max(720, header.ClientSize.Width - changePassword.Width - 14);

            header.Controls.Add(title);
            header.Controls.Add(description);
            header.Controls.Add(connectionStatus);
            header.Controls.Add(passwordStatus);
            header.Controls.Add(changePassword);
        }

        private void ChangePassword_Click(object sender, System.EventArgs e)
        {
            using (var dialog = new FmtChangeParameterPasswordForm())
            {
                ThemeManager.ApplyThemeTo(dialog);
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    passwordStatus.ForeColor = Color.LimeGreen;
                    passwordStatus.Text = "參數設定密碼已更新。";
                }
            }
        }

        private void ApplyFmtTheme()
        {
            ThemeManager.ApplyThemeTo(this);
            BackColor = ThemeManager.BGColor;
            ForeColor = ThemeManager.TextColor;
            header.BackColor = Color.FromArgb(18, 36, 45);
            foreach (Control control in header.Controls)
            {
                if (!(control is Button) && control != connectionStatus && control != passwordStatus)
                    control.ForeColor = ThemeManager.TextColor;
            }
        }
    }
}
