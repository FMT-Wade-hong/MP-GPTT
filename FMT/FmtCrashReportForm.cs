using MissionPlanner.Utilities;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    /// <summary>
    /// Builds a local, editable and de-identified crash report. Nothing is uploaded by this form.
    /// The operator must explicitly copy the report, review it and submit it through GitHub.
    /// </summary>
    internal sealed class FmtCrashReportForm : Form
    {
        private const string GitHubNewIssueUrl =
            "https://github.com/FMT-Wade-hong/MP-GPTT/issues/new";

        private static readonly Color Background = Color.FromArgb(18, 34, 43);
        private static readonly Color ContentBackground = Color.FromArgb(28, 48, 58);
        private static readonly Color SkyBlue = Color.FromArgb(45, 169, 220);

        private readonly Exception exception;
        private readonly RichTextBox reportPreview = new RichTextBox();

        internal FmtCrashReportForm(Exception exception)
        {
            this.exception = exception ?? new Exception("Unknown application error");

            Text = "FMTPlanner GitHub 錯誤回報";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(860, 640);
            MinimumSize = new Size(680, 480);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Background;
            ForeColor = Color.White;
            Font = SystemFonts.MessageBoxFont;
            ShowInTaskbar = false;
            MaximizeBox = true;
            MinimizeBox = false;
            FmtBranding.ApplyApplicationIcon(this);

            BuildInterface();
            reportPreview.Text = BuildReport(this.exception);
        }

        private void BuildInterface()
        {
            var heading = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(16, 10, 16, 4),
                Text = "程式發生錯誤",
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14F, FontStyle.Bold),
                ForeColor = SkyBlue,
                BackColor = Background,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var privacyNotice = new Label
            {
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(16, 6, 16, 8),
                Text = "下方報告只在本機產生，不會自動上傳。已自動隱藏使用者名稱、電腦名稱、IP 與個人資料夾路徑。\r\n" +
                       "請先檢查並修改內容；GitHub Issue 可能公開，確認無敏感資料後再送出。",
                ForeColor = Color.White,
                BackColor = Background,
                TextAlign = ContentAlignment.MiddleLeft
            };

            reportPreview.Dock = DockStyle.Fill;
            reportPreview.BorderStyle = BorderStyle.FixedSingle;
            reportPreview.BackColor = ContentBackground;
            reportPreview.ForeColor = Color.White;
            reportPreview.Font = new Font("Consolas", 10F);
            reportPreview.WordWrap = false;
            reportPreview.ScrollBars = RichTextBoxScrollBars.Both;
            reportPreview.AcceptsTab = true;

            var previewHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 0, 16, 12),
                BackColor = Background
            };
            previewHost.Controls.Add(reportPreview);

            var openGitHub = CreateButton("複製並開啟 GitHub", 168, SkyBlue);
            openGitHub.Click += OpenGitHub_Click;

            var close = CreateButton("關閉", 100, Color.FromArgb(66, 78, 86));
            close.DialogResult = DialogResult.Cancel;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 292,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 8),
                BackColor = Background
            };
            buttons.Controls.Add(openGitHub);
            buttons.Controls.Add(close);

            var footerText = new Label
            {
                AutoSize = true,
                Text = "送出前仍由使用者做最後確認",
                ForeColor = Color.Gainsboro,
                Location = new Point(16, 23)
            };

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = Background
            };
            footer.Controls.Add(buttons);
            footer.Controls.Add(footerText);

            Controls.Add(previewHost);
            Controls.Add(privacyNotice);
            Controls.Add(heading);
            Controls.Add(footer);
            CancelButton = close;
        }

        private static Button CreateButton(string text, int width, Color background)
        {
            var button = new Button
            {
                Text = text,
                Size = new Size(width, 38),
                Margin = new Padding(6, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = background,
                ForeColor = Color.White,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void OpenGitHub_Click(object sender, EventArgs e)
        {
            var report = reportPreview.Text.Trim();
            if (string.IsNullOrWhiteSpace(report))
            {
                MessageBox.Show(this, "錯誤報告內容不可為空白。", "FMTPlanner 錯誤回報",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Clipboard.SetText(report);
                var title = "[V" + FmtAuthentication.ProductVersion + "] " + exception.GetType().Name;
                var url = GitHubNewIssueUrl + "?title=" + Uri.EscapeDataString(title);
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                MessageBox.Show(this,
                    "錯誤報告已複製到剪貼簿。\r\n\r\n" +
                    "請在 GitHub Issue 貼上內容，再次確認後由您按下送出。",
                    "已開啟 GitHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception openException)
            {
                MessageBox.Show(this,
                    "無法開啟 GitHub。請確認瀏覽器可用，或手動前往：\r\n" +
                    GitHubNewIssueUrl + "\r\n\r\n" + openException.Message,
                    "無法開啟 GitHub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string BuildReport(Exception exception)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## 問題說明");
            builder.AppendLine("請在此說明發生錯誤前的操作步驟與預期結果。");
            builder.AppendLine();
            builder.AppendLine("## 執行環境");
            builder.AppendLine("- FMTPlanner：V" + FmtAuthentication.ProductVersion);
            builder.AppendLine("- 作業系統：" + Sanitize(Environment.OSVersion.VersionString));
            builder.AppendLine("- .NET CLR：" + Environment.Version);
            builder.AppendLine("- 發生時間（UTC）：" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine();
            builder.AppendLine("## 錯誤類型");
            builder.AppendLine("`" + exception.GetType().FullName + "`");
            builder.AppendLine();
            builder.AppendLine("## 錯誤訊息");
            builder.AppendLine("```");
            builder.AppendLine(Sanitize(exception.Message));
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("## 堆疊追蹤");
            builder.AppendLine("```");
            builder.AppendLine(Sanitize(exception.StackTrace ?? "沒有堆疊追蹤資料"));
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("<!-- 送出前請刪除任何不希望公開的資料。 -->");
            return builder.ToString();
        }

        private static string Sanitize(string value)
        {
            var sanitized = value ?? string.Empty;
            sanitized = ReplaceIgnoreCase(sanitized,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%");
            sanitized = ReplaceIgnoreCase(sanitized, Environment.UserName, "<使用者>");
            sanitized = ReplaceIgnoreCase(sanitized, Environment.UserDomainName, "<網域>");
            sanitized = ReplaceIgnoreCase(sanitized, Environment.MachineName, "<電腦>");
            sanitized = Regex.Replace(sanitized,
                @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "<電子郵件>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            sanitized = Regex.Replace(sanitized,
                @"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])", "<IP位址>",
                RegexOptions.CultureInvariant);
            return sanitized;
        }

        private static string ReplaceIgnoreCase(string value, string oldValue, string newValue)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(oldValue))
                return value ?? string.Empty;

            return Regex.Replace(value, Regex.Escape(oldValue),
                match => newValue, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
