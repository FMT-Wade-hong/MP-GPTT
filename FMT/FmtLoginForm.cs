using MissionPlanner.Utilities;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtLoginForm : Form
    {
        private const string RememberAccountKey = "fmt_remember_login_account";
        private const string RememberedUserKey = "fmt_remembered_login_user";

        private static readonly Color CardBackground = Color.FromArgb(5, 13, 17);
        private readonly FmtLoginInput userName = new FmtLoginInput(false);
        private readonly FmtLoginInput password = new FmtLoginInput(true);
        private readonly Label error = new Label();
        private readonly CheckBox rememberAccount = new CheckBox();

        internal FmtLoginForm()
        {
            FmtBranding.ApplyApplicationIcon(this);
            Text = FmtAuthentication.ProductTitle + " Login";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(900, 685);
            BackColor = Color.Black;
            DoubleBuffered = true;

            BackgroundImage = FmtVisualAssets.LoadClientBackground(FmtVisualAssets.LoginBackground, 70);
            BackgroundImageLayout = ImageLayout.Stretch;

            // Coordinates are scaled from the approved 1392 x 1060 client-area mock-up.
            AddInput(userName, 414, 428, 540, 59);
            AddInput(password, 414, 553, 540, 58);

            var rememberBounds = ScaleRectangle(410, 631, 260, 39);
            rememberAccount.Text = "Remember account";
            rememberAccount.AutoSize = false;
            rememberAccount.ForeColor = Color.FromArgb(210, 215, 218);
            rememberAccount.BackColor = CardBackground;
            rememberAccount.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            rememberAccount.Location = rememberBounds.Location;
            rememberAccount.Size = rememberBounds.Size;
            rememberAccount.Cursor = Cursors.Hand;
            Controls.Add(rememberAccount);

            var loginBounds = ScaleRectangle(414, 689, 540, 73);
            var login = new FmtGradientButton
            {
                Text = "Sign in",
                Font = new Font("Segoe UI", 14F, FontStyle.Regular),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Location = loginBounds.Location,
                Size = loginBounds.Size
            };
            login.Click += SignIn;
            Controls.Add(login);

            var errorBounds = ScaleRectangle(414, 766, 540, 34);
            error.AutoSize = false;
            error.BackColor = CardBackground;
            error.ForeColor = Color.FromArgb(255, 105, 105);
            error.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            error.TextAlign = ContentAlignment.MiddleCenter;
            error.Location = errorBounds.Location;
            error.Size = errorBounds.Size;
            Controls.Add(error);

            var remember = string.Equals(Settings.Instance[RememberAccountKey], "true",
                StringComparison.OrdinalIgnoreCase);
            rememberAccount.Checked = remember;
            userName.Text = remember && !string.IsNullOrWhiteSpace(Settings.Instance[RememberedUserKey])
                ? Settings.Instance[RememberedUserKey]
                : FmtAuthentication.DefaultUserName;

            AcceptButton = login;
            Shown += (sender, args) => password.FocusInput();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                BackgroundImage?.Dispose();

            base.Dispose(disposing);
        }

        private void AddInput(FmtLoginInput input, int x, int y, int width, int height)
        {
            var bounds = ScaleRectangle(x, y, width, height);
            input.Location = bounds.Location;
            input.Size = bounds.Size;
            Controls.Add(input);
        }

        private Rectangle ScaleRectangle(int x, int y, int width, int height)
        {
            const float sourceWidth = 1392F;
            const float sourceHeight = 1060F;
            return new Rectangle(
                (int)Math.Round(x * ClientSize.Width / sourceWidth),
                (int)Math.Round(y * ClientSize.Height / sourceHeight),
                (int)Math.Round(width * ClientSize.Width / sourceWidth),
                (int)Math.Round(height * ClientSize.Height / sourceHeight));
        }

        private void SignIn(object sender, EventArgs e)
        {
            if (FmtAuthentication.ValidateLogin(userName.Text, password.Text))
            {
                Settings.Instance[RememberAccountKey] = rememberAccount.Checked ? "true" : "false";
                Settings.Instance[RememberedUserKey] = rememberAccount.Checked ? userName.Text.Trim() : string.Empty;
                Settings.Instance.Save();

                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            error.Text = "Invalid account or password.";
            password.Text = string.Empty;
            password.FocusInput();
        }
    }

    internal sealed class FmtLoginInput : Panel
    {
        private readonly TextBox input = new TextBox();
        private readonly bool isPassword;
        private bool passwordVisible;

        internal FmtLoginInput(bool passwordField)
        {
            isPassword = passwordField;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(4, 13, 17);
            Cursor = Cursors.IBeam;

            input.BorderStyle = BorderStyle.None;
            input.BackColor = BackColor;
            input.ForeColor = Color.FromArgb(226, 231, 234);
            input.Font = new Font("Segoe UI", 14F, FontStyle.Regular);
            input.UseSystemPasswordChar = passwordField;
            Controls.Add(input);
        }

        public override string Text
        {
            get => input.Text;
            set => input.Text = value ?? string.Empty;
        }

        internal void FocusInput()
        {
            input.Focus();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            var eyeSpace = isPassword ? 46 : 14;
            input.SetBounds(13, Math.Max(5, (Height - input.PreferredHeight) / 2),
                Math.Max(20, Width - eyeSpace - 13), input.PreferredHeight);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (isPassword && e.X >= Width - 46)
            {
                passwordVisible = !passwordVisible;
                input.UseSystemPasswordChar = !passwordVisible;
                input.Focus();
                Invalidate();
            }
            else
            {
                input.Focus();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(background, ClientRectangle);

            using (var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 7))
            using (var border = new Pen(Color.FromArgb(55, 186, 202)))
                e.Graphics.DrawPath(border, path);

            if (!isPassword)
                return;

            var centerX = Width - 24;
            var centerY = Height / 2;
            using (var pen = new Pen(Color.FromArgb(65, 207, 223), 2F))
            {
                e.Graphics.DrawArc(pen, centerX - 13, centerY - 8, 26, 16, 200, 140);
                e.Graphics.DrawArc(pen, centerX - 13, centerY - 8, 26, 16, 20, 140);
                e.Graphics.DrawEllipse(pen, centerX - 3, centerY - 3, 6, 6);
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class FmtGradientButton : Button
    {
        internal FmtGradientButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 7))
            using (var brush = new LinearGradientBrush(ClientRectangle,
                       Color.FromArgb(24, 198, 226), Color.FromArgb(143, 218, 17), 0F))
                pevent.Graphics.FillPath(brush, path);

            TextRenderer.DrawText(pevent.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            var diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
