using System;
using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtParameterAccessForm : Form
    {
        private static readonly Color SkyBlue = Color.FromArgb(41, 171, 226);
        private readonly TextBox password = new TextBox { UseSystemPasswordChar = true };
        private readonly Label error = new Label { AutoSize = true, ForeColor = Color.Firebrick };

        internal FmtParameterAccessForm()
        {
            Text = "Protected Parameter Access";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 210);

            Controls.Add(new Label
            {
                Text = "FeiMaoTecPlanner V1 Parameter Security",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = SkyBlue,
                AutoSize = true,
                Location = new Point(24, 20)
            });
            Controls.Add(new Label
            {
                Text = "Enter the parameter password to continue.",
                AutoSize = true,
                Location = new Point(27, 61)
            });
            password.Location = new Point(29, 91);
            password.Width = 370;
            Controls.Add(password);
            error.Location = new Point(29, 122);
            Controls.Add(error);

            var change = new Button { Text = "Change password", Width = 130, Location = new Point(29, 157) };
            change.Click += (sender, args) => ChangePassword();
            Controls.Add(change);

            var unlock = new Button
            {
                Text = "Unlock",
                Width = 100,
                Location = new Point(299, 157),
                BackColor = SkyBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            unlock.FlatAppearance.BorderSize = 0;
            unlock.Click += (sender, args) => Unlock();
            Controls.Add(unlock);
            AcceptButton = unlock;
        }

        private void Unlock()
        {
            if (FmtAuthentication.ValidateParameterPassword(password.Text))
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                error.Text = "Invalid parameter password.";
                password.Clear();
                password.Focus();
            }
        }

        private void ChangePassword()
        {
            if (!FmtAuthentication.ValidateParameterPassword(password.Text))
            {
                error.Text = "Enter the current password first.";
                return;
            }

            using (var dialog = new FmtChangeParameterPasswordForm())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    error.ForeColor = Color.ForestGreen;
                    error.Text = "Parameter password updated.";
                    password.Clear();
                }
            }
        }
    }

    internal sealed class FmtChangeParameterPasswordForm : Form
    {
        private readonly TextBox first = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox second = new TextBox { UseSystemPasswordChar = true };
        private readonly Label error = new Label { AutoSize = true, ForeColor = Color.Firebrick };

        internal FmtChangeParameterPasswordForm()
        {
            Text = "Change Parameter Password";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(390, 190);
            MaximizeBox = false;
            MinimizeBox = false;

            AddField("New password", first, 25);
            AddField("Confirm", second, 73);
            error.Location = new Point(140, 112);
            Controls.Add(error);
            var save = new Button { Text = "Save", Width = 90, Location = new Point(270, 143) };
            save.Click += SavePassword;
            Controls.Add(save);
            AcceptButton = save;
        }

        private void AddField(string caption, Control field, int y)
        {
            Controls.Add(new Label { Text = caption, AutoSize = true, Location = new Point(22, y + 4) });
            field.Location = new Point(140, y);
            field.Width = 220;
            Controls.Add(field);
        }

        private void SavePassword(object sender, EventArgs e)
        {
            if (!string.Equals(first.Text, second.Text, StringComparison.Ordinal))
            {
                error.Text = "Passwords do not match.";
                return;
            }

            try
            {
                FmtAuthentication.ChangeParameterPassword(first.Text);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ArgumentException ex)
            {
                error.Text = ex.Message;
            }
        }
    }
}
