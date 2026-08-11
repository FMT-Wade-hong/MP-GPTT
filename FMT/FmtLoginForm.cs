using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtLoginForm : Form
    {
        private static readonly Color SkyBlue = Color.FromArgb(41, 171, 226);
        private readonly TextBox userName = new TextBox { Text = FmtAuthentication.DefaultUserName };
        private readonly TextBox password = new TextBox { UseSystemPasswordChar = true };
        private readonly Label error = new Label { AutoSize = true, ForeColor = Color.Firebrick };

        internal FmtLoginForm()
        {
            Text = FmtAuthentication.ProductName + " Login";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 322);
            BackColor = Color.White;

            var banner = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Color.White };
            var logo = LoadLogo();
            if (logo != null)
            {
                banner.Controls.Add(new PictureBox
                {
                    Image = logo,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Location = new Point((ClientSize.Width - 270) / 2, 12),
                    Size = new Size(270, 78),
                    TabStop = false
                });
            }
            banner.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 6, BackColor = SkyBlue });
            Controls.Add(banner);

            AddField("Account", userName, 130);
            AddField("Password", password, 190);
            error.Location = new Point(145, 234);
            Controls.Add(error);

            var login = new Button
            {
                Text = "Sign in",
                BackColor = SkyBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 110,
                Height = 34,
                Location = new Point(278, 270)
            };
            login.FlatAppearance.BorderSize = 0;
            login.Click += SignIn;
            Controls.Add(login);

            AcceptButton = login;
            password.Select();
        }

        private static Image LoadLogo()
        {
            const string resourceName = "MissionPlanner.FMT.Assets.fmt-logo.png";
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;

                using (var source = Image.FromStream(stream))
                    return new Bitmap(source);
            }
        }

        private void AddField(string caption, Control field, int y)
        {
            Controls.Add(new Label
            {
                Text = caption,
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Location = new Point(35, y + 5)
            });
            field.Font = new Font("Segoe UI", 10);
            field.Location = new Point(145, y);
            field.Size = new Size(243, 29);
            Controls.Add(field);
        }

        private void SignIn(object sender, EventArgs e)
        {
            if (FmtAuthentication.ValidateLogin(userName.Text, password.Text))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            error.Text = "Invalid account or password.";
            password.Clear();
            password.Focus();
        }
    }
}
