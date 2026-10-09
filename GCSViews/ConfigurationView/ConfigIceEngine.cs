using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    // Capability-based: legacy/new parameters are never synthesized on the FC.
    public class ConfigIceEngine : MyUserControl, IActivate, IDeactivate
    {
        internal sealed class Definition
        {
            public string Name, Title, Unit, Range, Meaning, Advice;
            public double? Min, Max;
            public bool Integer;
            public Definition(string name, string title, string unit, string range,
                double? min, double? max, bool integer, string meaning, string advice)
            {
                Name = name; Title = title; Unit = unit; Range = range;
                Min = min; Max = max; Integer = integer; Meaning = meaning; Advice = advice;
            }
            internal bool Accepts(double value) => !double.IsNaN(value) && !double.IsInfinity(value) &&
                (!Min.HasValue || value >= Min.Value) && (!Max.HasValue || value <= Max.Value) &&
                (!Integer || value == Math.Truncate(value));
        }

        internal static readonly Definition[] Definitions =
        {
            new Definition("ICE_ENABLE", "啟用引擎管理", "", "0 / 1", 0, 1, true,
                "0：停用；1：啟用 ICE。", "完成接線與熄火機制確認後才設 1；啟用後重新取得完整參數，必要時安全重啟飛控。"),
            new Definition("ICE_STARTER_TIME", "單次啟動時間", "s", "0.1–5（參數文件）", 0.1, 5, false,
                "每次啟動器通電的持續時間。", "機載啟動器須依額定工作週期設定；官方預設 3 s 不是通用安全值。手持啟動器指南提到至少 10 s，與參數範圍不同，本頁不自動套用該例外。"),
            new Definition("ICE_START_DELAY", "再次啟動等待", "s", "1–10", 1, 10, false,
                "啟動嘗試之間的等待時間。", "官方預設 2 s；依啟動器散熱／工作週期增加等待，不能只為快速重啟而縮短。"),
            new Definition("ICE_START_PCT", "啟動油門", "%", "0–100", 0, 100, true,
                "啟動階段覆寫的油門百分比。", "依引擎啟動要求低量調整；官方預設 5%，須先校準油門連桿與行程。"),
            new Definition("ICE_IDLE_PCT", "運轉最低油門", "%", "0–100", 0, 100, true,
                "引擎運轉時的最低油門；大於 0 可能允許未解鎖時運轉。", "依實測穩定怠速設定，無通用建議區間。檢查 ICE_OPTIONS bit 3，避免意外未解鎖啟動。"),
            new Definition("ICE_RPM_CHAN", "引擎 RPM 來源", "", "0 / 1 / 2", 0, 2, true,
                "0：無；1：RPM1；2：RPM2。不是 RC 通道號碼。", "有機載啟動器時建議安裝並驗證 RPM 感測器；選擇實際接線的 RPM 實例。"),
            new Definition("ICE_RPM_THRESH", "運轉判定門檻", "RPM", "100–100000", 100, 100000, false,
                "低於此 RPM 時，運轉中的 ICE 邏輯可能重新嘗試啟動。", "設定低於穩定怠速、避開感測器雜訊；不能把官方預設 100 當成所有引擎的建議值。"),
            new Definition("ICE_IDLE_RPM", "怠速 governor 目標", "RPM", "-1：停用；其餘官方未列範圍", -1, null, true,
                "閉迴路怠速轉速目標。", "未驗證 RPM／油門前保持 -1；啟用後依原廠怠速規格設定。"),
            new Definition("ICE_IDLE_DB", "怠速容許誤差", "RPM", "官方未列範圍", 0, null, true,
                "怠速 governor 可容許的 RPM 死區。", "官方預設 50 RPM；依感測雜訊與怠速穩定度調整，過小可能反覆修正。"),
            new Definition("ICE_IDLE_SLEW", "怠速油門調整速率", "%/s", "官方未列範圍", 0, null, false,
                "怠速控制每秒調整油門的百分點數。", "官方預設 1；先保守調整，過快可能造成轉速振盪。"),
            new Definition("ICE_REDLINE_RPM", "最高轉速限制", "RPM", "0–2000000；0 停用", 0, 2000000, false,
                "引擎 redline governor 的轉速上限。", "依引擎與螺旋槳原廠容許上限設定，取較低者；需可靠 RPM，並確認 ICE_OPTIONS bit 1 未停用 governor。"),
            new Definition("ICE_STRT_MX_RTRY", "啟動重試上限", "次", "0–127；0 不限次數", 0, 127, true,
                "限制啟動重試次數；僅支援的韌體提供。", "依啟動器與電池能力設定有限次數。0 不代表禁止重試；重試間隔不可接近螺旋槳。"),
            new Definition("ICE_STARTCHN_MIN", "RC 停機輸入下限", "µs", "0–1300", 0, 1300, true,
                "低於此值的 RC 啟動通道輸入會被忽略，避免異常低 PWM 誤熄火。", "預設 0；僅在掌握接收機失聯輸出時調整，必須保留有效的低檔停機區間並實測 failsafe。"),
            new Definition("ICE_OPTIONS", "引擎選項 bitmask", "", "各 bit 數值相加；依韌體版本", 0, 65535, true,
                "bit 0（1）：RC failsafe 關閉點火。\r\nbit 1（2）：停用 redline governor。\r\nbit 2（4）：MANUAL 未解鎖且 safety 關閉時允許油門控制。\r\nbit 3（8）：禁止未解鎖運轉（MAVLink 特定旗標可例外）。\r\nbit 4（16）：反向啟動，僅較新且支援的啟動硬體／韌體。",
                "不是單選值，請逐 bit 確認；4.5 文件只列 bit 0–3。保留不明 bit；不要為通過測試而停用安全保護。"),
            new Definition("ICE_START_CHAN", "舊版 RC 啟動通道", "", "0–16（4.5）", 0, 16, true,
                "0：未指定；1–16：RC 通道。低檔停機、中檔允許指令、高檔啟動。", "4.6 起改為 RCx_OPTION=179；勿同時套用不同版本的設定。"),
            Pwm("ICE_PWM_IGN_ON", "舊版點火開啟 PWM", "點火開關開啟"),
            Pwm("ICE_PWM_IGN_OFF", "舊版點火關閉 PWM", "點火開關關閉"),
            Pwm("ICE_PWM_STRT_ON", "舊版啟動器開啟 PWM", "啟動器通電"),
            Pwm("ICE_PWM_STRT_OFF", "舊版啟動器關閉 PWM", "啟動器斷電")
        };

        private static Definition Pwm(string name, string title, string state) =>
            new Definition(name, title, "µs", "1000–2000（4.5）", 1000, 2000, true,
                state + "時的 PWM 輸出。", "依開關模組的觸發門檻／極性設定，沒有通用安全值。4.6 起改用對應 SERVOx_MIN/MAX/REVERSED。先斷開點火與啟動器電源再驗證。" );

        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox details = new TextBox();
        private readonly TextBox proposed = new TextBox();
        private readonly Label status = new Label();
        private readonly Button write = new Button { Text = "確認寫入所選參數", AutoSize = true, Enabled = false };
        private readonly Button refresh = new Button { Name = "BUT_FmtRefreshPage", Text = "更新當頁參數", AutoSize = true };
        private readonly Button read = new Button { Text = "讀取所選參數", AutoSize = true, Enabled = false };
        private readonly CheckBox safe = new CheckBox { Text = "已停止引擎、斷開點火／啟動器動力並確認機體安全", AutoSize = true };
        private readonly CheckBox starter = new CheckBox { Text = "有機載啟動器（未勾選：鎖定啟動器參數，不更改飛控設定）", AutoSize = true };
        private volatile bool hasStarter;
        private MAVLinkInterface port;
        private MAVState target;
        private bool busy, active;
        private Definition[] pageDefinitions;
        private readonly string pageGuide;
        private readonly bool icePage;
        private ComboBox profile;
        private CheckBox showAll;
        private Label profileHint;
        private System.Func<string, int, bool> profileFilter;
        private System.Func<int, string> profileGuide;

        internal void AddProfileSelector(string[] choices, System.Func<string, int, bool> filter,
            System.Func<int, string> guide, System.Func<int, Definition[]> definitionsForProfile = null)
        {
            profileFilter = filter; profileGuide = guide;
            profile = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 225 };
            profile.Items.AddRange(choices); profile.SelectedIndex = 0;
            showAll = new CheckBox { Text = "顯示全部參數（進階核對）", AutoSize = true };
            profileHint = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
            var host = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var line = new FlowLayoutPanel { Dock = DockStyle.Fill };
            line.Controls.Add(new Label { Text = "起飛方式：", AutoSize = true });
            line.Controls.Add(profile); line.Controls.Add(showAll);
            host.Controls.Add(line, 0, 0); host.Controls.Add(profileHint, 0, 1);
            var layout = (TableLayoutPanel)starter.Parent;
            layout.Controls.Remove(starter);
            layout.RowStyles[2].Height = 100;
            layout.Controls.Add(host, 0, 2);
            profile.SelectedIndexChanged += (s, e) =>
            {
                safe.Checked = false;
                if (definitionsForProfile != null) pageDefinitions = definitionsForProfile(profile.SelectedIndex);
                LoadValues();
            };
            showAll.CheckedChanged += (s, e) => LoadValues();
            LoadValues();
        }

        private bool ProfileReady => profile == null || profile.SelectedIndex > 0;

        public ConfigIceEngine() : this("引擎控制 ICE", Definitions, WiringGuide, true) { }

        internal ConfigIceEngine(string title, Definition[] definitions, string guide, bool isIce, string introduction = null)
        {
            pageDefinitions = definitions; pageGuide = guide; icePage = isIce;
            AutoScaleMode = AutoScaleMode.Font;
            Size = new Size(1100, 730);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(10) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, isIce ? 32 : 0));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.Controls.Add(new Label { Text = title + "｜設定與說明", Dock = DockStyle.Fill,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 15, FontStyle.Bold) });
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text =
                introduction ?? (isIce ? "此頁只設定參數，不會啟動引擎。選取參數可查看完整定義與建議。\r\n官方範圍不等於硬體安全範圍；未回傳的參數僅供查閱。啟用 ICE 後請重新取得完整參數。\r\n4.5 與 4.6+ 接線／RC 設定不同，請參考下方「接線與版本說明」。"
                : "固定翼 TAKEOFF／AUTO 起飛參數；本頁不會切換模式、解鎖或啟動動力。\r\n選取參數查看單位、定義與建議；官方範圍不等於適合每架飛機的安全值。\r\n滑跑與手拋／彈射設定不可直接互用；只允許寫入飛控已回傳的參數，不自動套用預設值。") });
            starter.Visible = isIce;
            layout.Controls.Add(starter, 0, 2);
            starter.CheckedChanged += (s, e) =>
            {
                hasStarter = starter.Checked;
                grid.Invalidate();
                SelectParameter();
            };
            grid.Dock = DockStyle.Left; grid.ReadOnly = true; grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false;
            grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            foreach (var heading in new[] { "參數", "用途", "目前值", "單位", "官方範圍／選項" }) grid.Columns.Add(heading, heading);
            grid.CurrentCellChanged += (s, e) => SelectParameter();
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0 && IsStarterLocked((grid.Rows[e.RowIndex].Tag as Definition)?.Name))
                {
                    e.CellStyle.ForeColor = Color.Gray;
                    e.CellStyle.SelectionForeColor = Color.Silver;
                    e.CellStyle.BackColor = Color.FromArgb(55, 60, 64);
                    e.CellStyle.SelectionBackColor = Color.FromArgb(65, 70, 74);
                }
            };
            var gridHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            gridHost.Controls.Add(grid);
            gridHost.SizeChanged += (s, e) => FitGridWidth();
            grid.ColumnWidthChanged += (s, e) => FitGridWidth();
            layout.Controls.Add(gridHost, 0, 4);
            details.Dock = DockStyle.Fill; details.Multiline = true; details.ReadOnly = true; details.ScrollBars = ScrollBars.Vertical;
            layout.Controls.Add(details, 0, 5);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
            actions.Controls.Add(new Label { Text = "新值：", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            proposed.Width = 90; actions.Controls.Add(proposed); actions.Controls.Add(write); actions.Controls.Add(read); actions.Controls.Add(refresh);
            var help = new Button { Text = isIce ? "接線與版本說明" : "操作與設定說明", AutoSize = true };
            help.Click += (s, e) => details.Text = pageGuide;
            actions.Controls.Add(help); actions.Controls.Add(safe); layout.Controls.Add(actions, 0, 3);
            var writeHint = new Label { AutoSize = true, MaximumSize = new Size(800, 0) };
            actions.SetFlowBreak(safe, true);
            actions.Controls.Add(writeHint);
            var availabilityTimer = new System.Windows.Forms.Timer { Interval = 500 };
            availabilityTimer.Tick += (s, e) =>
            {
                if (!active || !Visible) return;
                UpdateButtons();
                writeHint.Text = !ProfileReady ? "請先選擇起飛方式，才可編輯及寫入；顯示全部參數只供進階核對。"
                    : !SameTarget() ? "尚未連線至目前飛控，或正在回放紀錄。"
                    : port.ReadOnly ? "目前為唯讀連線，無法寫入。"
                    : target.cs.armed ? "飛控已解鎖；請先安全上鎖。"
                    : !FMT.FmtRelayControlService.CanLocalStationTransmitControl ? "目前站台沒有控制權。"
                    : !MainV2.IsFmtIcePacketFresh(target.lastvalidpacket, DateTime.UtcNow) ? "等待飛控即時資料。"
                    : IsStarterLocked(Selected?.Name) ? "此項需先勾選有機載啟動器。"
                    : target.param[Selected?.Name ?? ""] == null ? "請先讀取所選參數。"
                    : !safe.Checked ? "請先確認機體安全並勾選安全確認，再輸入新值寫入。"
                    : "可寫入所選參數；寫入後會重新讀取確認。";
            };
            availabilityTimer.Start();
            Disposed += (s, e) => availabilityTimer.Dispose();
            status.Dock = DockStyle.Fill; layout.Controls.Add(status, 0, 6);
            Controls.Add(layout);
            refresh.Click += async (s, e) =>
            {
                if (busy) return;
                busy = true; UpdateButtons();
                try
                {
                    var names = grid.Rows.Cast<DataGridViewRow>().Where(row => row.Visible && !row.IsNewRow)
                        .Select(row => Convert.ToString(row.Cells[0].Value)).ToArray();
                    await FmtPageParameterRefresh.RefreshAsync(this, refresh, names, LoadValues,
                        Selected != null && target?.param[Selected.Name] != null &&
                        proposed.Text != ((double)target.param[Selected.Name]).ToString("G", CultureInfo.InvariantCulture));
                }
                finally { busy = false; if (!IsDisposed) UpdateButtons(); }
            };
            write.Click += async (s, e) => await Transfer(true);
            read.Click += async (s, e) => await Transfer(false);
            safe.CheckedChanged += (s, e) => UpdateButtons();
            LoadValues();
        }

        internal const string WiringGuide =
            "設定順序：確認原廠規格與停機機制 → 校準油門行程 → 設定點火／啟動輸出 → RPM 驗證 → ICE 參數 → 地面測試。\r\n\r\n" +
            "4.6+：RCx_OPTION=179 為引擎三段開關；低檔停機，中檔允許 MAVLink／任務控制，高檔啟動。使用導控按鈕前置於中檔，並實測低檔熄火。\r\n" +
            "4.5：以 ICE_START_CHAN 指定三段開關通道。\r\n\r\n" +
            "PWM 輸出：SERVOx_FUNCTION=67 點火、69 啟動器、70 油門。請在 Servo Output 頁指定未占用的輸出，勿覆蓋舵面／馬達。4.6+ 點火與啟動器開關端點取決於 SERVOx_MIN/MAX 及 REVERSED；4.5 使用 ICE_PWM_*。GPIO／Relay 請依官方 Relay Switch 文件設定，不能把 PWM 數值直接套用到 Relay。\r\n\r\n" +
            "油門：SERVOx_MIN/MAX 必須符合連桿機械行程；THR_MIN 依怠速實測，THR_MAX 依動力限制。THR_SLEWRATE 官方指南以 20 %/s 作為起始參考，不是通用安全值。\r\n" +
            "RPM：先確認 RPM1／RPM2 感測器與倍率正確，再設定 ICE_RPM_CHAN。轉速門檻低於穩定怠速，redline 不超過引擎／螺旋槳額定上限。QuadPlane 可查 Q_LAND_ICE_CUT 的降落熄火設定。\r\n\r\n" +
            "注意：未解鎖不代表引擎不會啟動；重試間隔也可能突然再啟動。本頁不提供自動套用建議值。更改輸出或 ICE_ENABLE 後按韌體要求安全重啟並重新讀取參數。\r\n\r\n" +
            "官方說明：https://ardupilot.org/plane/docs/common-ice.html\r\n" +
            "參數依據：ArduPilot Plane-4.5.7 / Plane-4.6.3 的 libraries/AP_ICEngine/AP_ICEngine.cpp；較新韌體以實際回傳及對應版本文件為準。";

        public void Activate() { active = true; if (!busy) LoadValues(); }
        public void Deactivate() { active = false; safe.Checked = false; }
        private Definition Selected => grid.CurrentRow?.Tag as Definition;
        internal static bool IsStarterParameter(string name) => name == "ICE_STARTER_TIME" ||
            name == "ICE_START_DELAY" || name == "ICE_STRT_MX_RTRY" ||
            name == "ICE_PWM_STRT_ON" || name == "ICE_PWM_STRT_OFF";
        private bool IsStarterLocked(string name) => icePage && !hasStarter && IsStarterParameter(name);
        private void FitGridWidth()
        {
            if (grid.Parent == null) return;
            var contentWidth = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width)
                + SystemInformation.VerticalScrollBarWidth + 8;
            // Keep long text readable; narrow windows retain horizontal scrolling.
            grid.Width = Math.Max(1, Math.Min(contentWidth, grid.Parent.ClientSize.Width));
        }
        private void LoadValues()
        {
            if (!ReferenceEquals(target, MainV2.comPort?.MAV)) starter.Checked = false;
            port = MainV2.comPort; target = port?.MAV;
            var selected = Selected?.Name;
            grid.Rows.Clear();
            foreach (var def in pageDefinitions)
            {
                var value = target?.param[def.Name];
                // Non-zero off-profile values remain visible for explicit review/correction.
                // Hiding a row never disables the corresponding autopilot feature.
                if (profile != null && profile.SelectedIndex > 0 && !showAll.Checked &&
                    !profileFilter(def.Name, profile.SelectedIndex) &&
                    (value == null || (double)value == 0)) continue;
                var row = grid.Rows[grid.Rows.Add(def.Name, def.Title,
                    value == null ? "未回傳" : ((double)value).ToString("G", CultureInfo.InvariantCulture), def.Unit, def.Range)];
                row.Tag = def;
                if (def.Name == selected) grid.CurrentCell = row.Cells[0];
            }
            status.Text = "顯示最近讀取的參數；「更新當頁參數」僅讀取本頁篩選後已載入的參數，不下載完整參數表。未回傳可能尚未啟用或載入。";
            if (profile != null) profileHint.Text = profileGuide(profile.SelectedIndex) +
                "\r\n僅篩選介面，不改寫飛控。非本方式但目前值不為 0 的項目保留供核對；隱藏不等於停用。";
            if (grid.CurrentCell == null && grid.Rows.Count > 0) grid.CurrentCell = grid.Rows[0].Cells[0];
            SelectParameter();
            UpdateButtons();
            FitGridWidth();
        }
        private void SelectParameter()
        {
            var def = Selected;
            if (def == null) return;
            details.Text = def.Name + "｜" + def.Title + "\r\n\r\n定義：" + def.Meaning +
                "\r\n\r\n官方範圍：" + def.Range + " " + def.Unit + "\r\n\r\n設定建議：" + def.Advice;
            if (IsStarterLocked(def.Name)) details.Text += "\r\n\r\n未勾選「有機載啟動器」：此項僅供查看，禁止修改；原有飛控數值仍然有效。手持啟動器若需調整啟動時間，請先明確解除此介面鎖定。";
            if (profile != null && profile.SelectedIndex > 0)
                details.Text += "\r\n\r\n本方式指引：" + profileGuide(profile.SelectedIndex) +
                    (profileFilter(def.Name, profile.SelectedIndex) ? "" : "\r\n此項非本方式的主要設定；請核對是否為先前設定殘留，勿直接沿用。必要時依官方說明修正，不自動清零。");
            proposed.Text = target?.param[def.Name] == null ? "" : ((double)target.param[def.Name]).ToString("G", CultureInfo.InvariantCulture);
            UpdateButtons();
        }
        private bool SameTarget() => active && ReferenceEquals(port, MainV2.comPort) &&
            ReferenceEquals(target, port?.MAV) && port?.BaseStream != null && port.BaseStream.IsOpen && !port.logreadmode;
        private bool Writable() => SameTarget() && !port.ReadOnly && !target.cs.armed &&
            MainV2.IsFmtIcePacketFresh(target.lastvalidpacket, DateTime.UtcNow) &&
            FMT.FmtRelayControlService.CanLocalStationTransmitControl;
        private void UpdateButtons()
        {
            read.Enabled = !busy && SameTarget() && Selected != null;
            write.Enabled = !busy && ProfileReady && !IsStarterLocked(Selected?.Name) && safe.Checked && Writable() && target.param[Selected?.Name ?? ""] != null;
            refresh.Enabled = !busy; proposed.Enabled = !busy && ProfileReady && !IsStarterLocked(Selected?.Name); grid.Enabled = !busy;
            starter.Enabled = !busy;
            if (profile != null) { profile.Enabled = !busy; showAll.Enabled = !busy; }
        }
        private async Task Transfer(bool writing)
        {
            if (busy || !SameTarget() || Selected == null) return;
            var def = Selected; var originalTarget = target; var originalPort = port;
            if (!writing)
            {
                busy = true; UpdateButtons();
                try
                {
                    await FmtPageParameterRefresh.RefreshAsync(this, read, new[] { def.Name }, () =>
                    {
                        LoadValues();
                        status.Text = def.Name + " 已從飛控讀取。";
                    }, allowUnloaded: true);
                }
                finally { busy = false; if (!IsDisposed) UpdateButtons(); }
                return;
            }
            double value = 0;
            if (writing)
            {
                if (!ProfileReady || IsStarterLocked(def.Name) || !safe.Checked || !Writable() || target.param[def.Name] == null) return;
                if (!double.TryParse(proposed.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !def.Accepts(value))
                { status.Text = "數值格式或範圍不符：" + def.Range; return; }
            }
            busy = true; UpdateButtons();
            try
            {
                if (writing && CustomMessageBox.Show(def.Name + "：" + target.param[def.Name] + " → " + value +
                    "\r\n\r\n" + def.Advice + "\r\n\r\n確定只寫入此參數？", "參數設定確認", MessageBoxButtons.YesNo) != (int)DialogResult.Yes) return;
                var actual = await Task.Run(() =>
                {
                    if (!SameTarget() || (writing && (!Writable() || IsStarterLocked(def.Name)))) throw new InvalidOperationException("連線、控制權、啟動器選項或解鎖狀態已變更，已停止操作。");
                    if (writing && !originalPort.setParam(originalTarget.sysid, originalTarget.compid, def.Name, value))
                        throw new InvalidOperationException("飛控未接受寫入。");
                    return originalPort.GetParam(originalTarget.sysid, originalTarget.compid, def.Name);
                });
                if (IsDisposed || !active) return;
                if (!SameTarget()) throw new InvalidOperationException("目標飛控已變更；請重新讀取原飛控確認結果。");
                LoadValues();
                status.Text = writing
                    ? (Math.Abs(actual - value) <= Math.Max(0.0001, Math.Abs(value) * 0.000001)
                        ? def.Name + " 已寫入並讀回確認。" + (icePage ? "ICE 啟用後請重新取得完整參數。" : "請於安全條件下驗證設定。")
                        : def.Name + " 讀回值不同：" + actual + "；未確認成功，請重新檢查。")
                    : def.Name + " 已從飛控讀取：" + actual;
            }
            catch (Exception ex) { if (!IsDisposed) status.Text = "操作未確認成功：" + ex.Message; }
            finally { busy = false; if (!IsDisposed) UpdateButtons(); }
        }
    }
}
