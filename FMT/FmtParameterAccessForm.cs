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
            FmtBranding.ApplyApplicationIcon(this);
            Text = "參數設定驗證";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 190);

            Controls.Add(new Label
            {
                Text = FmtAuthentication.ProductTitle + " 參數設定",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = SkyBlue,
                AutoSize = true,
                Location = new Point(24, 20)
            });
            Controls.Add(new Label
            {
                Text = "請輸入參數設定密碼後進入。",
                AutoSize = true,
                Location = new Point(27, 61)
            });
            password.Location = new Point(29, 91);
            password.Width = 370;
            password.KeyDown += Password_KeyDown;
            Controls.Add(password);
            error.Location = new Point(29, 122);
            Controls.Add(error);

            var unlock = new Button
            {
                Text = "進入",
                Width = 100,
                Location = new Point(299, 145),
                BackColor = SkyBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            unlock.FlatAppearance.BorderSize = 0;
            unlock.Click += (sender, args) => Unlock();
            Controls.Add(unlock);
            AcceptButton = unlock;
        }

        private void Password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            Unlock();
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
                error.Text = "密碼錯誤，請重新輸入。";
                password.Clear();
                password.Focus();
            }
        }
    }

    internal sealed class FmtChangeParameterPasswordForm : Form
    {
        private static readonly Color SkyBlue = Color.FromArgb(41, 171, 226);
        private readonly TextBox current = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox first = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox second = new TextBox { UseSystemPasswordChar = true };
        private readonly Label error = new Label { AutoSize = true, ForeColor = Color.Firebrick };

        internal FmtChangeParameterPasswordForm()
        {
            FmtBranding.ApplyApplicationIcon(this);
            Text = "設定參數密碼";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(420, 240);
            MaximizeBox = false;
            MinimizeBox = false;

            AddField("目前密碼", current, 25);
            AddField("新密碼", first, 73);
            AddField("確認新密碼", second, 121);
            error.Location = new Point(140, 163);
            Controls.Add(error);
            var save = new Button
            {
                Text = "儲存",
                Width = 90,
                Location = new Point(300, 196),
                BackColor = SkyBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            save.FlatAppearance.BorderSize = 0;
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
            if (!FmtAuthentication.ValidateParameterPassword(current.Text))
            {
                error.Text = "目前密碼不正確。";
                current.Clear();
                current.Focus();
                return;
            }

            if (!string.Equals(first.Text, second.Text, StringComparison.Ordinal))
            {
                error.Text = "兩次輸入的新密碼不一致。";
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
                error.Text = ex.Message.Contains("four")
                    ? "參數密碼至少需要四個字元。"
                    : ex.Message;
            }
        }
    }
}
