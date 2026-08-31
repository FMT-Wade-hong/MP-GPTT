// Offscreen UI checks; no FlightData/MainV2 construction or flight commands.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class FmtFlightActionsHarness
{
    private static int passed;
    private static readonly string[,] Names = {
        { "CMB_action", "BUTactiondo", "BUT_quickauto", "BUT_Homealt", "modifyandSetSpeed" },
        { "CMB_setwp", "BUT_setwp", "BUT_quickmanual", "BUTrestartmission", "modifyandSetAlt" },
        { "CMB_modes", "BUT_setmode", "BUT_quickrtl", "BUT_RAWSensor", "modifyandSetLoiterRad" },
        { "CMB_mountmode", "BUT_mountmode", "BUT_joystick", "BUT_ARM", "BUT_clear_track" },
        { null, null, "BUT_SendMSG", "BUT_resumemis", "BUT_abortland" }
    };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var preview = Path.GetFullPath(args[0]);
            var artifacts = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(artifacts);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, request) => {
                string path = Path.Combine(preview, new AssemblyName(request.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-TW");
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.Combine(preview, "FMTPlanner.exe"));
            var controls = Assembly.LoadFrom(Path.Combine(preview, "MissionPlanner.Controls.dll"));
            var layoutType = app.GetType("MissionPlanner.FMT.FmtFlightActionsLayout", true);
            var resources = new ComponentResourceManager(app.GetType("MissionPlanner.GCSViews.FlightData", true));
            var actionNames = Enum.GetNames(app.GetType("MissionPlanner.GCSViews.FlightData+actions", true));
            var translate = layoutType.GetMethod("TranslateChoice", BindingFlags.Static | BindingFlags.NonPublic);
            Check(actionNames.All(n => (string)translate.Invoke(null, new object[] { n }) != n), "all built-in actions have Chinese labels");
            Check((string)translate.Invoke(null, new object[] { "My_Custom_Command" }) == "My_Custom_Command", "unknown extension commands preserved");
            foreach (float scale in new[] { 1f, 1.5f, 2f })
            using (var font = new Font("Microsoft JhengHei", 9 * scale))
            using (var form = new Form { Font = font, AutoScaleMode = AutoScaleMode.None, StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000), ShowInTaskbar = false, ClientSize = new Size((int)(900 * scale), (int)(300 * scale)) })
            using (var tabs = new TabControl { Dock = DockStyle.Fill })
            using (var page = new TabPage("動作"))
            using (var table = new TableLayoutPanel { ColumnCount = 5, RowCount = 5 })
            {
                form.Controls.Add(tabs);
                tabs.TabPages.Add(page);
                page.Controls.Add(table);
                for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                {
                    var name = Names[row, col];
                    if (name == null) continue;
                    Control control = name.StartsWith("CMB") ? (Control)new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList } :
                        (Control)Activator.CreateInstance(name.StartsWith("modify") ? app.GetType("MissionPlanner.Controls.ModifyandSet", true) : controls.GetType("MissionPlanner.Controls.MyButton", true));
                    resources.ApplyResources(control, name);
                    control.Name = name;
                    control.Font = font;
                    if (name.StartsWith("modify"))
                    {
                        var number = (NumericUpDown)control.GetType().GetProperty("NumericUpDown").GetValue(control);
                        number.Font = font;
                        number.Maximum = name == "modifyandSetSpeed" ? 1000 : 10000;
                        number.Minimum = name == "modifyandSetLoiterRad" ? -10000 : 0;
                        number.DecimalPlaces = name == "modifyandSetLoiterRad" ? 0 : 1;
                        number.Value = 100;
                        ((Control)control.GetType().GetProperty("Button").GetValue(control)).Font = font;
                    }
                    table.Controls.Add(control, col, row);
                }
                var action = (ComboBox)table.Controls["CMB_action"];
                action.DataSource = new BindingList<string>(actionNames.ToList());
                var mode = (ComboBox)table.Controls["CMB_modes"];
                mode.DataSource = new[] { new KeyValuePair<int, string>(3, "Auto"), new KeyValuePair<int, string>(5, "Loiter") };
                mode.DisplayMember = "Value";
                mode.ValueMember = "Key";
                var mount = (ComboBox)table.Controls["CMB_mountmode"];
                mount.DataSource = new[] { new KeyValuePair<int, string>(0, "Retracted"), new KeyValuePair<int, string>(1, "Neutral") };
                mount.DisplayMember = "Value";
                mount.ValueMember = "Key";
                var wp = (ComboBox)table.Controls["CMB_setwp"];
                wp.Items.Clear();
                wp.Items.AddRange(new object[] { "0 (Home)", "1", "2" });
                wp.SelectedIndex = 0;
                layoutType.GetMethod("Configure", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { page, table, true });
                ApplyColors(page);
                form.Show();
                Application.DoEvents();
                Check(action.Text == "Loiter_Unlim" && mode.Text == "Auto" && (int)mode.SelectedValue == 3 && mount.Text == "Retracted" && (int)mount.SelectedValue == 0, "localized drawing leaves raw command text/IDs unchanged " + scale);
                foreach (var name in actionNames) { action.Text = name; Check(action.Text == name, "raw action selection " + name + " scale=" + scale); }
                action.SelectedIndex = 0;
                Check(table.Controls["BUT_quickmanual"].Text == "定點盤旋", "Loiter caption matches existing handler");
                Check(table.Controls["BUT_mountmode"].Text == "設定雲台" && table.Controls["BUT_SendMSG"].Text == "傳送訊息" && table.Controls["BUT_resumemis"].Text == "繼續任務" && table.Controls["BUT_abortland"].Text == "中止降落", "English screenshot buttons translated");
                int stableHeight = table.Height;
                foreach (int width in new[] { 900, 700, 480, 900, 480, 700, 900 })
                {
                    form.ClientSize = new Size((int)(width * scale), (int)(300 * scale));
                    Application.DoEvents();
                    Check(table.Height == stableHeight, "resize does not grow action rows " + width + "/" + scale);
                    CheckLayout(table);
                    if (width == 480) Check(page.HorizontalScroll.Visible, "narrow viewport scrolls instead of clipping controls");
                }
                foreach (Control edit in table.Controls.Cast<Control>().Where(c => c.Name.StartsWith("modify")))
                {
                    var number = (NumericUpDown)edit.GetType().GetProperty("NumericUpDown").GetValue(edit);
                    Check(number.Value == 100 && number.DecimalPlaces == (edit.Name == "modifyandSetLoiterRad" ? 0 : 1), "numeric values and precision unchanged " + edit.Name);
                    var button = (Button)edit.GetType().GetProperty("Button").GetValue(edit);
                    int forwarded = 0;
                    EventHandler click = (sender, e) => forwarded++;
                    edit.GetType().GetEvent("Click", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).AddEventHandler(edit, click);
                    button.PerformClick();
                    Check(forwarded == 1, "compound action forwards exactly one click " + edit.Name);
                }
                mode.DataSource = new[] { new KeyValuePair<int, string>(6, "RTL"), new KeyValuePair<int, string>(5, "Loiter") };
                mode.DisplayMember = "Value";
                mode.ValueMember = "Key";
                mode.Text = "Loiter";
                Check(mode.Text == "Loiter" && (int)mode.SelectedValue == 5, "mode refresh preserves raw selection semantics");
                wp.Items.Clear(); wp.Items.AddRange(new object[] { "0 (Home)", "1", "2" }); wp.SelectedIndex = 2;
                Check(wp.SelectedIndex == 2 && wp.Text == "2", "waypoint refresh leaves index unchanged");
                using (var bitmap = new Bitmap(page.Width, page.Height))
                {
                    page.DrawToBitmap(bitmap, page.ClientRectangle);
                    bitmap.Save(Path.Combine(artifacts, "actions-" + (int)(scale * 100) + ".png"));
                }
                form.Close();
            }
            Console.WriteLine("PASS " + passed + " flight actions checks (offline; no vehicle commands)");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void CheckLayout(TableLayoutPanel table)
    {
        var children = table.Controls.Cast<Control>().ToArray();
        Check(children.All(c => table.ClientRectangle.Contains(c.Bounds)) && children.All(a => children.All(b => a == b || !a.Bounds.IntersectsWith(b.Bounds))), "all action cells within table with no overlap");
        foreach (Control edit in children.Where(c => c.Name.StartsWith("modify")))
        {
            var number = (Control)edit.GetType().GetProperty("NumericUpDown").GetValue(edit);
            var button = (Control)edit.GetType().GetProperty("Button").GetValue(edit);
            Check(number.Parent == button.Parent && number.Parent.ClientRectangle.Contains(number.Bounds) && button.Parent.ClientRectangle.Contains(button.Bounds) && number.Right < button.Left,
                "numeric and button fit same row " + edit.Name);
            Check(TextRenderer.MeasureText(button.Text, button.Font).Width + 8 <= button.Width && button.Font.Height + 4 <= button.Height, "compound caption fits " + edit.Name);
        }
        Check(children.OfType<Button>().All(b => TextRenderer.MeasureText(b.Text, b.Font).Width + 8 <= b.Width && b.Font.Height + 4 <= b.Height), "all button captions fit");
    }

    private static void ApplyColors(Control control)
    {
        control.BackColor = control is ComboBox || control is NumericUpDown ? Color.FromArgb(38, 55, 64) : Color.FromArgb(22, 36, 45);
        control.ForeColor = Color.Gainsboro;
        foreach (var key in new[] { "BGGradTop", "BGGradBot", "TextColor", "Outline" })
        {
            var prop = control.GetType().GetProperty(key);
            if (prop != null) prop.SetValue(control, key == "TextColor" ? Color.FromArgb(10, 55, 74) : key == "BGGradBot" ? Color.FromArgb(113, 201, 226) : Color.FromArgb(36, 174, 221));
        }
        foreach (Control child in control.Controls) ApplyColors(child);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed++;
    }
}
