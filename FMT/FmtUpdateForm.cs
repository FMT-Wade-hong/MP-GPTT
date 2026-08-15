using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtUpdateForm : Form
    {
        private static readonly Color Background = Color.FromArgb(18, 34, 43);
        private static readonly Color ContentBackground = Color.FromArgb(28, 48, 58);
        private static readonly Color SkyBlue = Color.FromArgb(45, 169, 220);

        internal FmtUpdateForm(string latestVersion, string releaseNotes)
        {
            Text = "FMTPlanner 版本更新";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 560);
            MinimumSize = new Size(640, 420);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Background;
            ForeColor = Color.White;
            Font = SystemFonts.MessageBoxFont;
            ShowIcon = true;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            FmtBranding.ApplyApplicationIcon(this);

            var heading = new Label
            {
                Dock = DockStyle.Top,
                Height = 58,
                Padding = new Padding(18, 12, 18, 6),
                Text = "FeiMaoTecPlanner 有新版本 V" + latestVersion,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13F, FontStyle.Bold),
                ForeColor = SkyBlue,
                BackColor = Background,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var notes = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = ContentBackground,
                ForeColor = Color.White,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F),
                Text = NormalizeReleaseNotes(releaseNotes),
                Margin = new Padding(18),
                TabStop = true
            };
            notes.LinkClicked += (sender, args) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(args.LinkText);
                }
                catch
                {
                }
            };

            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 0, 18, 12),
                BackColor = Background
            };
            contentHost.Controls.Add(notes);

            var prompt = new Label
            {
                AutoSize = true,
                ForeColor = Color.Gainsboro,
                Text = "是否開啟飛貓科技版本下載頁？",
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 16, 0)
            };

            var openButton = CreateButton("開啟下載頁", DialogResult.Yes, SkyBlue);
            var laterButton = CreateButton("稍後", DialogResult.No, Color.FromArgb(66, 78, 86));

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 276,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 8),
                BackColor = Background
            };
            buttons.Controls.Add(openButton);
            buttons.Controls.Add(laterButton);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                Padding = new Padding(18, 0, 18, 0),
                BackColor = Background
            };
            footer.Controls.Add(buttons);
            footer.Controls.Add(prompt);
            prompt.Location = new Point(18, 22);

            Controls.Add(contentHost);
            Controls.Add(heading);
            Controls.Add(footer);

            AcceptButton = openButton;
            CancelButton = laterButton;
        }

        private static Button CreateButton(string text, DialogResult result, Color background)
        {
            return new Button
            {
                Text = text,
                DialogResult = result,
                Size = new Size(126, 38),
                Margin = new Padding(6, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = background,
                ForeColor = Color.White,
                UseVisualStyleBackColor = false
            };
        }

        private static string NormalizeReleaseNotes(string releaseNotes)
        {
            var notes = (releaseNotes ?? string.Empty).Trim();
            var replacementCount = notes.Count(character => character == '\uFFFD');
            if (string.IsNullOrWhiteSpace(notes) || replacementCount > 2)
            {
                return "此版本的線上更新說明無法正確解碼。\r\n\r\n" +
                       "請開啟下載頁查看完整的繁體中文版本說明。";
            }

            return notes;
        }
    }
}
