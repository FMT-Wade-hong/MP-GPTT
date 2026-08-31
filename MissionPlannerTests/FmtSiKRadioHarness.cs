// UI/protocol-boundary checks only. Never opens a serial port or starts MainV2.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Globalization;
using System.Windows.Forms;

internal static class FmtSiKRadioHarness
{
    private static int passed;
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var preview = Path.GetFullPath(args[0]);
            var artifacts = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(artifacts);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, request) =>
            {
                var path = Path.Combine(preview, new AssemblyName(request.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-TW");
            Application.EnableVisualStyles();
            var assembly = Assembly.LoadFrom(Path.Combine(preview, "FMTPlanner.exe"));
            var radioType = assembly.GetType("MissionPlanner.Radio.Sikradio", true);
            var pageType = assembly.GetType("MissionPlanner.GCSViews.ConfigurationView.ConfigSiKRadio", true);
            var theme = assembly.GetType("MissionPlanner.Utilities.ThemeManager", true);
            // Load only in-memory theme colors. Do not call SetTheme (writes user settings).
            var themeXml = new System.Xml.XmlDocument();
            var projectRoot = args.Length > 2 ? Path.GetFullPath(args[2]) : Directory.GetParent(preview).Parent.FullName;
            themeXml.Load(Path.Combine(projectRoot, "FMT-SkyBlue.mpsystheme"));
            foreach (System.Xml.XmlNode entry in themeXml.SelectNodes("//ThemeColor"))
            {
                var field = theme.GetField(entry["strVariableName"].InnerText);
                var colorNode = entry["clrColor"];
                Color color = ColorTranslator.FromHtml(colorNode.GetAttribute("Web"));
                if (colorNode.HasAttribute("Alpha")) color = Color.FromArgb(int.Parse(colorNode.GetAttribute("Alpha")), color);
                if (field != null && field.FieldType == typeof(Color)) field.SetValue(null, color);
            }
            // Compare against the same current neutral firmware UI, not the
            // obsolete geometry/option resources shipped with older locales.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            using (var original = (Control)Activator.CreateInstance(radioType))
            using (var chinese = (Control)Activator.CreateInstance(radioType, new object[] { true }))
            {
                Check(Find(chinese, "BUT_getcurrent").Text == "讀取設定", "read action translated");
                Check(Find(chinese, "BUT_savesettings").Text == "寫入設定", "write action translated");
                Check(Find(chinese, "groupBoxLocal").Text.Contains("本機") && Find(chinese, "groupBoxRemote").Text.Contains("遠端"), "local and remote sides clearly distinguished");
                Check(Find(chinese, "label1").Text == "序列鮑率" && Find(chinese, "label3").Text == "空中速率", "serial and air rates translated separately");
                Check(Find(chinese, "BUT_upload").Text.Contains("韌體") && Find(chinese, "BUT_resettodefault").Text == "恢復預設", "firmware and reset captions");
                Check(Find(chinese, "btnLoadFromFile").Text.Contains("匯入") && Find(chinese, "btnRemoteSaveToFile").Text.Contains("匯出"), "settings-file captions");
                Check(Find(chinese, "groupBoxRemote").Right <= chinese.Width && Find(chinese, "groupBoxLocal").Bottom <= chinese.Height,
                    "both modem columns fit within current SiK geometry");
                foreach (string name in new[] { "SERIAL_SPEED", "AIR_SPEED", "NETID", "MAVLINK", "MIN_FREQ", "MAX_WINDOW", "ENCRYPTION_LEVEL" })
                {
                    var before = (ComboBox)Find(original, name);
                    var after = (ComboBox)Find(chinese, name);
                    Check(before.Items.Cast<object>().Select(x => x.ToString()).SequenceEqual(after.Items.Cast<object>().Select(x => x.ToString())) && before.ValueMember == after.ValueMember,
                        "protocol option values unchanged: " + name);
                }
                var status = Find(chinese, "lbl_status");
                status.Text = "Connecting";
                Check(status.Text == "正在連線", "live status translated");
                status.Text = "Doing Command ATI5";
                Check(status.Text == "正在執行指令 ATI5", "AT command token preserved in translated status");
                status.Text = "DEVICE_SPECIFIC_CODE_123";
                Check(status.Text == "DEVICE_SPECIFIC_CODE_123", "device diagnostics preserved");
                var provider = radioType.GetProperty("EmbeddedPortProvider");
                var returnType = provider.PropertyType.GetGenericArguments()[0];
                int acquisitions = 0;
                Func<object> acquire = () => { acquisitions++; return null; };
                provider.SetValue(chinese, Expression.Lambda(provider.PropertyType,
                    Expression.Convert(Expression.Invoke(Expression.Constant(acquire)), returnType)).Compile());
                int releases = 0;
                radioType.GetProperty("EmbeddedPortRelease").SetValue(chinese, (Action)(() => releases++));
                var session = radioType.GetMethod("GetSession", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(chinese, null);
                Check(session == null, "host provider returning null never falls back to MP port");
                acquisitions = 0;
                var encryption = (ComboBox)Find(chinese, "ENCRYPTION_LEVEL");
                radioType.GetMethod("EncryptionCheckChangedEvtHdlr", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(chinese, new object[] { encryption, "ATI5", "AT&E?", (TextBox)Find(chinese, "AESKEY"), false, "ATI5" });
                Check(acquisitions == 0, "editing or importing encryption level never writes to radio");
                var execute = radioType.GetMethod("ExecuteEmbeddedOperation", BindingFlags.Instance | BindingFlags.NonPublic);
                bool nestedRan = false;
                execute.Invoke(chinese, new object[] { (Action)(() =>
                {
                    execute.Invoke(chinese, new object[] { (Action)(() => nestedRan = true) });
                    radioType.GetMethod("RequestEmbeddedDisconnect").Invoke(chinese, null);
                    Check(releases == 0, "page change defers port release while operation is busy");
                }) });
                Check(!nestedRan && releases == 1, "operation cannot reenter; deferred release occurs once");
            }
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-TW");
            using (var form = new Form { ClientSize = new Size(1200, 790), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
            using (var page = (Control)Activator.CreateInstance(pageType))
            {
                theme.GetMethod("ApplyThemeTo", new[] { typeof(Control) }).Invoke(null, new object[] { page });
                form.Controls.Add(page);
                form.Show(); Application.DoEvents();
                var port = (ComboBox)Find(page, "sikPort");
                Check(port.SelectedIndex == -1, "page does not auto-select or connect a COM port");
                Check(Find(page, "sikBaud").Text == "57600", "explicit standalone-style baud selector");
                Check(pageType.GetField("ownedPort", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(page) == null, "opening page never acquires hardware");
                Check(!Find(page, "BUT_savesettings").Enabled, "write disabled until settings have been read");
                Check(!Find(page, "groupBoxLocal").Enabled && !Find(page, "groupBoxRemote").Enabled,
                    "readable disabled captions do not enable uninitialized radio inputs");
                CheckCaptionBounds(Find(page, "groupBoxLocal"));
                CheckCaptionBounds(Find(page, "groupBoxRemote"));
                Check(true, "both modem columns have no caption/input or caption/caption overlap");
                using (var bitmap = new Bitmap(page.Width, page.Height))
                {
                    page.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(artifacts, "sik-settings.png"));
                }
                using (var report = new StreamWriter(Path.Combine(artifacts, "sik-layout.txt")))
                    foreach (Control group in new[] { Find(page, "groupBoxLocal"), Find(page, "groupBoxRemote") })
                        foreach (Control item in group.Controls)
                            if (item.Visible) report.WriteLine(item.Name + " | " + item.Text + " | " + item.Bounds + " | preferred=" + item.PreferredSize + " | " + item.Font);
                form.ClientSize = new Size(760, 470);
                Application.DoEvents();
                var viewport = (Panel)Find(page, "sikSettingsViewport");
                Check(viewport.HorizontalScroll.Visible && viewport.VerticalScroll.Visible, "small view scrolls to all local and remote fields");
                pageType.GetMethod("Deactivate").Invoke(page, null);
                Check(pageType.GetField("ownedPort", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(page) == null, "deactivate releases only page-owned port");
            }
            TestLayoutScales(radioType, theme, artifacts);
            Console.WriteLine("PASS: " + passed + " SiK checks. No radio connected, no firmware or parameters written.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static Control Find(Control parent, string name) => parent.Controls.Find(name, true).Single();
    private static void CheckCaptionBounds(Control group)
    {
        foreach (var label in group.Controls.OfType<Label>().Where(c => c.Visible && c.Text.Length > 0))
        {
            if (!group.ClientRectangle.Contains(label.Bounds)) throw new Exception("Caption outside group: " + label.Name);
            foreach (Control other in group.Controls)
                if (other.Visible && other != label && label.Bounds.IntersectsWith(other.Bounds))
                    throw new Exception("Caption overlap: " + label.Name + " " + label.Bounds + " / " + other.Name + " " + other.Bounds);
        }
    }

    private static void TestLayoutScales(Type radioType, Type theme, string artifacts)
    {
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        using (var radio = (Control)Activator.CreateInstance(radioType, new object[] { true }))
        using (var form = new Form { AutoScaleMode = AutoScaleMode.None, ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) })
        {
            theme.GetMethod("ApplyThemeTo", new[] { typeof(Control) }).Invoke(null, new object[] { radio });
            radioType.GetMethod("EnableConfigControls", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(radio, new object[] { true, false });
            radio.Scale(new SizeF(scale, scale));
            using (var font = new Font(radio.Font.FontFamily, radio.Font.Size * scale))
            {
                radio.Font = font;
                form.ClientSize = new Size(radio.Width + 20, radio.Height + 20);
                form.Controls.Add(radio);
                form.Show(); Application.DoEvents();
                CheckCaptionBounds(Find(radio, "groupBoxLocal"));
                CheckCaptionBounds(Find(radio, "groupBoxRemote"));
                Check(true, "no label overlap with scaled geometry/fonts at " + scale * 100 + "%");
                Check(Find(radio, "label11").Top > radio.Font.Height && Find(radio, "label9").Top > radio.Font.Height,
                    "firmware fields below group titles at " + scale * 100 + "%");
                var status = Find(radio, "lbl_status");
                var copy = Find(radio, "BUT_Syncoptions");
                var progress = Find(radio, "Progressbar");
                Check(!status.Bounds.IntersectsWith(copy.Bounds) && progress.Top >= copy.Bottom,
                    "status, two-line copy button, progress have separate bounds at " + scale * 100 + "%");
                Check(Find(radio, "SERIAL_SPEED").Parent == Find(radio, "groupBoxLocal") &&
                    Find(radio, "RSERIAL_SPEED").Parent == Find(radio, "groupBoxRemote"), "original parameter control ownership preserved");
                using (var bitmap = new Bitmap(radio.Width, radio.Height))
                {
                    radio.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(artifacts, "sik-layout-" + (int)(scale * 100) + ".png"));
                }
                // Changing enabled state must not move labels back over fields.
                Find(radio, "groupBoxLocal").Enabled = true;
                Find(radio, "groupBoxRemote").Enabled = true;
                Application.DoEvents();
                CheckCaptionBounds(Find(radio, "groupBoxLocal"));
                CheckCaptionBounds(Find(radio, "groupBoxRemote"));
                Check(true, "enabled and disabled states retain identical safe layout at " + scale * 100 + "%");
                form.Hide();
            }
        }
    }
    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL " + label);
        passed++; Console.WriteLine("PASS " + label);
    }
}
