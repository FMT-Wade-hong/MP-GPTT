using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner.FMT
{
    // Presentation only: keep the original controls, event handlers and command values.
    internal sealed class FmtFlightActionsLayout
    {
        private readonly TabPage page;
        private readonly TableLayoutPanel table;
        private readonly bool chinese;
        private readonly ToolTip tips = new ToolTip();
        private bool arranging;

        private static readonly Dictionary<string, string> Captions = new Dictionary<string, string>
        {
            { "BUTactiondo", "執行" }, { "BUT_setwp", "設定航點" },
            { "BUT_setmode", "設定模式" }, { "BUT_mountmode", "設定雲台" },
            { "BUT_quickauto", "自動" }, { "BUT_quickmanual", "定點盤旋" },
            { "BUT_quickrtl", "返航" }, { "BUT_joystick", "飛行搖桿" },
            { "BUT_SendMSG", "傳送訊息" }, { "BUT_Homealt", "設定起始高度" },
            { "BUTrestartmission", "重啟任務" }, { "BUT_RAWSensor", "感測器原始資料" },
            { "BUT_ARM", "解鎖／上鎖" }, { "BUT_resumemis", "繼續任務" },
            { "BUT_clear_track", "清除航跡" }, { "BUT_abortland", "中止降落" }
        };

        private static readonly Dictionary<string, string> Choices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Loiter_Unlim", "持續盤旋" }, { "Return_To_Launch", "返回起飛點" },
            { "Preflight_Calibration", "飛行前校正" }, { "Mission_Start", "開始任務" },
            { "Preflight_Reboot_Shutdown", "重啟飛控" }, { "Trigger_Camera", "觸發相機" },
            { "System_Time", "同步系統時間" }, { "Battery_Reset", "重設電池" },
            { "ADSB_Out_Ident", "ADSB 識別" }, { "Scripting_cmd_stop_and_restart", "重啟腳本" },
            { "Scripting_cmd_stop", "停止腳本" }, { "HighLatency_Enable", "啟用高延遲" },
            { "HighLatency_Disable", "停用高延遲" }, { "Toggle_Safety_Switch", "切換安全開關" },
            { "Do_Parachute", "釋放降落傘" }, { "Engine_Start", "啟動引擎" },
            { "Engine_Stop", "停止引擎" }, { "Terminate_Flight", "終止飛行" },
            { "Format_SD_Card", "格式化 SD 卡" },
            { "Auto", "自動" }, { "Manual", "手動" }, { "Stabilize", "自穩" },
            { "Acro", "特技" }, { "AltHold", "定高" }, { "Alt_Hold", "定高" },
            { "Loiter", "定點盤旋" }, { "RTL", "返航" }, { "Guided", "引導" },
            { "Guided_NoGPS", "引導（無 GPS）" }, { "GuidedNoGPS", "引導（無 GPS）" },
            { "Circle", "繞圈" }, { "Land", "降落" }, { "Brake", "煞車" },
            { "PosHold", "位置保持" }, { "Position", "位置" }, { "Sport", "運動" },
            { "Drift", "飄移" }, { "Flip", "翻滾" }, { "AutoTune", "自動調校" },
            { "Throw", "拋投起飛" }, { "Avoid_ADSB", "ADSB 避障" },
            { "Smart_RTL", "智慧返航" }, { "SmartRTL", "智慧返航" },
            { "FlowHold", "光流定點" }, { "Follow", "跟隨" }, { "ZigZag", "折線飛行" },
            { "SystemID", "系統辨識" }, { "Autorotate", "自轉降落" },
            { "Auto_RTL", "自動返航" }, { "Turtle", "翻正" },
            { "FlyByWireA", "線傳 A" }, { "FlyByWireB", "線傳 B" },
            { "FBWA", "線傳 A" }, { "FBWB", "線傳 B" }, { "Cruise", "巡航" },
            { "Takeoff", "起飛" }, { "Training", "訓練" }, { "Initializing", "初始化" },
            { "QStabilize", "垂直自穩" }, { "QHover", "垂直懸停" }, { "QLoiter", "垂直定點" },
            { "QLand", "垂直降落" }, { "QRTL", "垂直返航" }, { "QAutoTune", "垂直自動調校" },
            { "QAcro", "垂直特技" }, { "Hold", "保持位置" }, { "Steering", "轉向" },
            { "Retracted", "收回" }, { "Retract", "收回" }, { "Neutral", "中立" },
            { "MAVLink Targeting", "MAVLink 指向" }, { "MavlinkTargeting", "MAVLink 指向" },
            { "RC Targeting", "遙控指向" }, { "RCTargeting", "遙控指向" },
            { "GPS Point", "GPS 定點" }, { "GPSPoint", "GPS 定點" },
            { "SysId Target", "追蹤系統 ID" }, { "Home Location", "指向起飛點" },
            { "0 (Home)", "0（起飛點）" }
        };

        internal static void Configure(TabPage page, TableLayoutPanel table, bool chinese)
        {
            new FmtFlightActionsLayout(page, table, chinese);
        }

        private FmtFlightActionsLayout(TabPage page, TableLayoutPanel table, bool chinese)
        {
            this.page = page;
            this.table = table;
            this.chinese = chinese;
            page.SuspendLayout();
            table.SuspendLayout();
            page.AutoScroll = true;
            page.Padding = new Padding(3);
            table.AutoSize = false;
            table.Dock = DockStyle.None;
            table.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            table.Margin = Padding.Empty;
            table.Padding = Padding.Empty;
            table.MinimumSize = Size.Empty;
            table.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            foreach (Control control in table.Controls)
            {
                control.AutoSize = false;
                control.MinimumSize = Size.Empty;
                control.Margin = new Padding(3);
                control.Dock = DockStyle.Fill;
                string caption;
                if (chinese && Captions.TryGetValue(control.Name, out caption)) control.Text = caption;
                if (control is ComboBox combo)
                {
                    // Owner drawing translates pixels only. Text, SelectedValue and
                    // enum/custom-action keys must stay raw for FlightData handlers.
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += DrawChoice;
                    combo.DropDown += (sender, args) => MeasureDropDown(combo);
                    combo.SelectedIndexChanged += (sender, args) => UpdateChoiceTip(combo);
                    combo.DataSourceChanged += (sender, args) => Arrange();
                    combo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                    combo.Dock = DockStyle.None;
                }
                if (control is ModifyandSet edit)
                {
                    if (chinese)
                        edit.ButtonText = edit.Name == "modifyandSetSpeed" ? "設定速度" :
                            edit.Name == "modifyandSetAlt" ? "設定高度" : "盤旋半徑";
                    var flow = edit.Controls.OfType<FlowLayoutPanel>().Single();
                    flow.AutoSize = false;
                    flow.WrapContents = false;
                    flow.Padding = Padding.Empty;
                    flow.Margin = Padding.Empty;
                    edit.NumericUpDown.Margin = Padding.Empty;
                    edit.Button.Margin = Padding.Empty;
                    edit.Button.AutoSize = false;
                    edit.SizeChanged += (sender, args) => ArrangeEditor(edit);
                    // Keep units/ranges/decimals and ValueChanged/Click events untouched.
                }
            }
            if (chinese)
            {
                SetTip("BUT_quickmanual", "切換至 Loiter（定點盤旋），不是 Manual 手動模式。");
                SetTip("BUT_resumemis", "從指定航點繼續任務；保留原有安全確認流程。");
            }
            table.ResumeLayout(false);
            page.ResumeLayout(false);
            page.SizeChanged += (sender, args) => Arrange();
            page.FontChanged += (sender, args) => Arrange();
            table.FontChanged += (sender, args) => Arrange();
            table.Disposed += (sender, args) => tips.Dispose();
            Arrange();
        }

        internal static string TranslateChoice(string raw)
        {
            string translated;
            return Choices.TryGetValue(raw ?? "", out translated) ? translated : raw ?? "";
        }

        private string DisplayChoice(ComboBox combo, object item)
        {
            var raw = combo.GetItemText(item);
            if (!chinese) return raw;
            // Initial resources contain translated home-altitude text; leave it intact.
            return TranslateChoice(raw);
        }

        private void UpdateChoiceTip(ComboBox combo)
        {
            var label = DisplayChoice(combo, combo.SelectedItem);
            tips.SetToolTip(combo, label == combo.Text ? label : label + "（" + combo.Text + "）");
        }

        private void DrawChoice(object sender, DrawItemEventArgs e)
        {
            var combo = (ComboBox)sender;
            e.DrawBackground();
            object item = e.Index >= 0 && e.Index < combo.Items.Count ? combo.Items[e.Index] : combo.SelectedItem;
            var color = combo.Enabled ? e.ForeColor : SystemColors.GrayText;
            var bounds = Rectangle.Inflate(e.Bounds, -2, 0);
            TextRenderer.DrawText(e.Graphics, DisplayChoice(combo, item), combo.Font, bounds, color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }

        private void MeasureDropDown(ComboBox combo)
        {
            int width = combo.Width;
            foreach (var item in combo.Items)
                width = Math.Max(width, TextRenderer.MeasureText(DisplayChoice(combo, item), combo.Font).Width + 28);
            combo.DropDownWidth = width;
        }

        private void SetTip(string name, string text)
        {
            var control = table.Controls.Cast<Control>().FirstOrDefault(c => c.Name == name);
            if (control != null) tips.SetToolTip(control, text);
        }

        private int Px(int pixels) => (int)Math.Ceiling(pixels * Math.Max(1.0, table.Font.SizeInPoints / 9.0));

        private int EditorNumberWidth(ModifyandSet edit)
        {
            string value = edit.Minimum < 0 ? edit.Minimum.ToString("F" + edit.DecimalPlaces) : edit.Maximum.ToString("F" + edit.DecimalPlaces);
            return Math.Max(Px(62), TextRenderer.MeasureText(value, edit.NumericUpDown.Font).Width + SystemInformation.VerticalScrollBarWidth + Px(8));
        }

        private void ArrangeEditor(ModifyandSet edit)
        {
            int gap = Px(4);
            int numberWidth = EditorNumberWidth(edit);
            var number = edit.NumericUpDown;
            number.Width = numberWidth;
            number.Margin = new Padding(0, Math.Max(0, (edit.Height - number.PreferredHeight) / 2), gap, 0);
            edit.Button.Size = new Size(Math.Max(1, edit.Width - numberWidth - gap), edit.Height);
        }

        private void Arrange()
        {
            if (arranging || table.IsDisposed) return;
            arranging = true;
            table.SuspendLayout();
            try
            {
                var widths = new int[5];
                int rowHeight = Px(36);
                foreach (Control control in table.Controls)
                {
                    control.Margin = new Padding(Px(3));
                    int width = TextRenderer.MeasureText(control.Text, control.Font).Width + Px(20);
                    rowHeight = Math.Max(rowHeight, control.Font.Height + Px(18));
                    if (control is ComboBox combo)
                    {
                        combo.ItemHeight = combo.Font.Height + Px(4);
                        width = Px(132);
                        // Built-in translated choices fit the selected-item area.
                        if (chinese)
                            width = Math.Max(width, Choices.Values.Max(t => TextRenderer.MeasureText(t, combo.Font).Width) + Px(28));
                        rowHeight = Math.Max(rowHeight, combo.PreferredHeight + control.Margin.Vertical);
                        MeasureDropDown(combo);
                        UpdateChoiceTip(combo);
                    }
                    if (control is ModifyandSet edit)
                    {
                        width = EditorNumberWidth(edit) + Px(4) + TextRenderer.MeasureText(edit.ButtonText, edit.Button.Font).Width + Px(20);
                        rowHeight = Math.Max(rowHeight, edit.NumericUpDown.PreferredHeight + Px(12));
                    }
                    int col = table.GetColumn(control);
                    widths[col] = Math.Max(widths[col], width + control.Margin.Horizontal);
                }
                int minWidth = widths.Sum();
                int widthAvailable = Math.Max(minWidth, page.ClientSize.Width - page.Padding.Horizontal);
                int extra = (widthAvailable - minWidth) / 5;
                table.ColumnStyles.Clear();
                for (int col = 0; col < 5; col++)
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, widths[col] + extra + (col == 4 ? (widthAvailable - minWidth) % 5 : 0)));
                table.RowStyles.Clear();
                for (int row = 0; row < 5; row++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
                table.Size = new Size(widthAvailable, rowHeight * 5);
                table.Location = new Point(page.Padding.Left + page.AutoScrollPosition.X, page.Padding.Top + page.AutoScrollPosition.Y);
            }
            finally
            {
                table.ResumeLayout(true);
                foreach (var edit in table.Controls.OfType<ModifyandSet>()) ArrangeEditor(edit);
                arranging = false;
            }
        }
    }
}
