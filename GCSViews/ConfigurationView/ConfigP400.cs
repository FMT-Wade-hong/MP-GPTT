using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.Controls;
using MissionPlanner.FMT;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public sealed class ConfigP400 : UserControl, IActivate, IDeactivate
    {
        private readonly ComboBox ports = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox baud = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox commandMode = new CheckBox { AutoSize = true, Text = "已用 CONFIG 強制進入 AT（9600／8N1，不送 +++）" };
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill };
        private readonly TextBox output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill };
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly ConfigPicoConfig fallback = new ConfigPicoConfig();
        private readonly ConfigP400FrequencyTable frequencyEditor = new ConfigP400FrequencyTable();
        private readonly DataGridView rightGrid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private DataGridView selectedGrid;
        private readonly Label description = new Label { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(4) };
        private static readonly int[] LeftOrder = {101,103,105,108,113,116,123,140,142,150,153,158};
        private static readonly int[] RightOrder = {102,104,107,110,213,124,133,141,217,151,154,128,125,131,132};
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        private readonly Button connect, refresh, read, help, frequency, write, resume, release, cancel;
        private P400AtClient client;
        private CancellationTokenSource cancellation;
        private bool busy;
        private List<P400Setting> settings = new List<P400Setting>();

        public ConfigP400()
        {
            Dock = DockStyle.Fill;
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var at = new TabPage("DATA / AT 設定");
            var external = new TabPage("PicoConfig 備用視窗");
            external.Controls.Add(fallback);
            tabs.TabPages.Add(at); tabs.TabPages.Add(external); Controls.Add(tabs);
            var frequencyTab = new TabPage("頻率表／10 組合");
            frequencyTab.Controls.Add(frequencyEditor); tabs.TabPages.Add(frequencyTab);
            frequencyEditor.ReadRadioRequested += async () => await RunAsync(async () => {
                if (client?.Ready != true) throw new InvalidOperationException("請先在第一分頁進入 AT。");
                var text = await client.ReadFrequencyTableAsync(frequencyEditor.RadioTable, cancellation.Token);
                frequencyEditor.LoadRadio(text);
            });
            frequencyEditor.WriteRadioRequested += async () => await RunAsync(WriteRadioFrequencyAsync);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(8) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "P400 本機 DATA 埠設定｜8N1\r\n進入 AT 模式會中斷數傳，只能在地面安全狀態操作。請先中斷導控／PicoConfig 並釋放序列埠。\r\n修改 S102/S110 須用 CONFIG 強制 9600/8N1 進入 AT；返回 DATA 後採用新值。S128/S142 須單獨寫入。" }, 0, 0);
            actions.Controls.Add(ports); actions.Controls.Add(baud); actions.Controls.Add(commandMode);
            refresh = AddButton("重新整理 COM", () => { RefreshPorts(); return Task.CompletedTask; });
            connect = AddButton("進入 AT／讀取", ConnectAsync);
            read = AddButton("重新讀取", ReadAsync);
            help = AddButton("選取參數說明", async () => {
                if (selectedGrid?.CurrentRow?.Tag == null) return;
                var item = (P400Setting)selectedGrid.CurrentRow.Tag;
                output.Text = await client.ReadHelpAsync(item.Register, cancellation.Token);
            });
            frequency = AddButton("讀取頻率表", async () => { output.Text = await client.ReadFrequencyTableAsync(cancellation.Token); frequencyEditor.SetModuleSnapshot(output.Text); });
            write = AddButton("寫入／驗證／儲存", WriteAsync);
            resume = AddButton("返回 DATA 並釋放", async () => {
                var dataSettings = string.Join("；", settings.Where(p => p.Register == 102 || p.Register == 110).Select(p => "S" + p.Register + "=" + P400SettingInfo.Display(client.Mode, p.Register, p.Value)));
                await client.ReturnToDataAsync(cancellation.Token);
                ClosePort(); status.Text = "已送出 ATA 並釋放序列埠；尚未驗證 RF 連線，請回導控確認遙測。";
                status.Text += " DATA 連接端請使用：" + dataSettings;
            });
            release = AddButton("僅釋放序列埠", () => { ClosePort(); return Task.CompletedTask; });
            cancel = new Button { Text = "取消作業", AutoSize = true };
            cancel.Click += (s, e) => cancellation?.Cancel(); actions.Controls.Add(cancel);
            layout.Controls.Add(actions, 0, 1); layout.Controls.Add(status, 0, 2);
            foreach (var table in new[] { grid, rightGrid })
            {
                table.CellContentClick += (sender, e) => {
                    if (busy || e.RowIndex < 0 || e.ColumnIndex != table.Columns["proposed"].Index) return;
                    var row = table.Rows[e.RowIndex]; var item = row.Tag as P400Setting;
                    if (item?.Register == 107 && item.Editable) EditPassword(item, row);
                };
                table.Columns.Add("register", "S 編號／功能"); table.Columns.Add("name", "參數");
                table.Columns.Add("current", "讀回值"); table.Columns.Add("proposed", "新值／選單"); table.Columns.Add("note", "說明");
                table.Columns.Add("default", "手冊預設值");
                foreach (DataGridViewColumn column in table.Columns) column.ReadOnly = column.Name != "proposed";
                table.Columns[1].Visible = table.Columns[4].Visible = false;
                table.Columns[0].FillWeight = 110;
                table.RowTemplate.Height = 28;
                table.EditMode = DataGridViewEditMode.EditOnEnter;
                table.CurrentCellDirtyStateChanged += (s, e) => { if (table.IsCurrentCellDirty && table.CurrentCell is DataGridViewComboBoxCell) table.CommitEdit(DataGridViewDataErrorContexts.Commit); };
                table.CellEnter += (s, e) => {
                    selectedGrid = table;
                    if (e.RowIndex >= 0) description.Text = Convert.ToString(table.Rows[e.RowIndex].Cells["note"].Value);
                };
            }
            selectedGrid = grid;
            var columns = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = Padding.Empty };
            foreach (var table in new[] { grid, rightGrid })
            {
                table.Dock = DockStyle.None;
                table.Margin = new Padding(0, 0, 8, 0);
                table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                table.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                table.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                columns.Controls.Add(table);
                FitColumns(table);
            }
            columns.SizeChanged += (s, e) => {
                foreach (var table in new[] { grid, rightGrid })
                    table.Height = Math.Max(80, columns.ClientSize.Height - SystemInformation.HorizontalScrollBarHeight - 2);
            };
            var details = new Panel { Dock = DockStyle.Fill };
            details.SizeChanged += (s, e) => description.MaximumSize = new Size(Math.Max(100, details.ClientSize.Width - 8), 0);
            details.Controls.Add(output); details.Controls.Add(description);
            layout.Controls.Add(columns, 0, 3); layout.Controls.Add(details, 0, 4); at.Controls.Add(layout);
            baud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200", "230400" });
            baud.SelectedItem = "57600";
            status.Text = "尚未連線；不會自動選 COM 或寫入設定。";
            RefreshPorts(); UpdateButtons();
        }

        private static void FitColumns(DataGridView table)
        {
            // Explicit measurement also works before WinForms creates the native handle.
            foreach (DataGridViewColumn column in table.Columns)
            {
                if (!column.Visible) continue;
                int width = TextRenderer.MeasureText(column.HeaderText, table.Font).Width + 20;
                foreach (DataGridViewRow row in table.Rows)
                    width = Math.Max(width, TextRenderer.MeasureText(Convert.ToString(row.Cells[column.Index].FormattedValue), table.Font).Width + 20);
                column.Width = Math.Max(column.MinimumWidth, width);
            }
            // Include every option, not only the currently selected value, plus the arrow.
            foreach (DataGridViewRow row in table.Rows)
            {
                var combo = row.Cells["proposed"] as DataGridViewComboBoxCell;
                var options = combo?.DataSource as IEnumerable<KeyValuePair<string, string>>;
                if (options == null) continue;
                foreach (var option in options)
                    table.Columns["proposed"].Width = Math.Max(table.Columns["proposed"].Width,
                        TextRenderer.MeasureText(option.Value, table.Font).Width + SystemInformation.VerticalScrollBarWidth + 16);
            }
            table.Width = table.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).Sum(c => c.Width)
                + SystemInformation.VerticalScrollBarWidth + 4;
        }

        private async Task WriteRadioFrequencyAsync()
        {
            if (client?.Ready != true) throw new InvalidOperationException("請先在第一分頁進入 AT。");
            int table = frequencyEditor.RadioTable;
            var plan = frequencyEditor.RadioPlan();
            var original = P400FrequencyPlan.Parse(await client.ReadFrequencyTableAsync(table, cancellation.Token));
            original.ValidateForRadio();
            var differences = Enumerable.Range(0, 50).Where(i => original.Frequencies[i] != plan.Frequencies[i]).ToArray();
            if (differences.Length == 0) { frequencyEditor.Report("頻率表相同，未寫入。"); return; }
            var summary = string.Join("\r\n", differences.Take(10).Select(i => (i + 1) + ": " + original.Frequencies[i] + " → " + plan.Frequencies[i]));
            if (MessageBox.Show(this, "將覆蓋 ATP" + table + " 完整 50 筆（變更 " + differences.Length + " 筆）。\r\n" + summary +
                "\r\n模組會自動保存，取消或失敗也可能已部分修改。只修改此表，不同步配對端。\r\n確認設備在地面安全狀態且頻率適用，是否寫入？", "頻率寫入確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var backup = System.IO.Path.Combine(MissionPlanner.Utilities.Settings.GetUserDataDirectory(), "P400FrequencyPlans", "backups", "ATP" + table + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json");
            original.Save(backup);
            frequencyEditor.Report("原表已備份：" + backup);
            await client.WriteFrequencyTableAsync(table, plan, original, cancellation.Token);
            frequencyEditor.Report("ATP" + table + " 50 筆讀回一致，模組已回覆完成；仍在 AT 模式。原表備份：" + backup);
        }

        private void EditPassword(P400Setting item, DataGridViewRow row)
        {
            using (var dialog = new Form { Text = "S107 新通訊密碼", ClientSize = new Size(440, 155), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            using (var input = new TextBox { Left = 15, Top = 55, Width = 400, MaxLength = 16, UseSystemPasswordChar = true })
            {
                dialog.Controls.Add(new Label { Left = 15, Top = 10, Width = 410, Height = 40, Text = "1–16 個英數字、底線、句點或連字號。\r\n留空可取消待寫入密碼，不會清除模組密碼。" });
                dialog.Controls.Add(input);
                var ok = new Button { Text = "確定", Left = 245, Top = 105 };
                var cancelPassword = new Button { Text = "取消", Left = 335, Top = 105, DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) => {
                    try { if (input.Text.Length > 0) P400AtClient.ValidatePassword(input.Text); dialog.DialogResult = DialogResult.OK; }
                    catch (FormatException ex) { MessageBox.Show(dialog, ex.Message); }
                };
                dialog.Controls.Add(ok); dialog.Controls.Add(cancelPassword); dialog.AcceptButton = ok; dialog.CancelButton = cancelPassword;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    item.Proposed = input.Text;
                    row.Cells["proposed"].Value = input.Text.Length == 0 ? "輸入新密碼…" : "已輸入（隱藏）…";
                }
                input.Clear();
            }
        }

        private Button AddButton(string text, Func<Task> action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += async (s, e) => await RunAsync(action);
            actions.Controls.Add(button); return button;
        }

        private static void CheckAccess()
        {
            if (MainV2.comPort?.BaseStream?.IsOpen == true ||
                (MainV2.Comports != null && MainV2.Comports.ToArray().Any(p => p?.BaseStream?.IsOpen == true)))
                throw new InvalidOperationException("導控有使用中的遙測連線，已停止 AT 操作；請先中斷連線。");
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (busy) return;
            busy = true; cancellation = new CancellationTokenSource(); UpdateButtons();
            try { await action(); }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    status.Text = ex is OperationCanceledException ? "作業已取消；模組狀態需重新確認。" : ex.Message;
                    if (client?.Unsaved == true) status.Text += " 注意：部分參數可能已修改／儲存結果不明，不可視為回復原值。";
                    frequencyEditor.Report(status.Text);
                }
                // Cancellation/timeouts leave framing uncertain. Never send another command.
                if (ex is OperationCanceledException || ex is TimeoutException) ClosePort(false);
            }
            finally
            {
                cancellation.Dispose(); cancellation = null; busy = false;
                if (!IsDisposed) UpdateButtons();
            }
        }

        private async Task ConnectAsync()
        {
            CheckAccess();
            if (ports.SelectedItem == null) throw new InvalidOperationException("請選擇 P400 DATA 序列埠。");
            if (commandMode.Checked && baud.Text != "9600") throw new InvalidOperationException("CONFIG 強制 AT 請選 9600 bps；修改 S102/S110 後 AT 期間仍維持此速度。");
            if (MessageBox.Show(this, "進入 AT 模式將中斷此模組的無線數傳。已確認飛機在地面安全狀態？", "P400", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            CheckAccess();
            client = new P400AtClient(new P400SerialTransport(ports.Text, int.Parse(baud.Text)), CheckAccess);
            try { await client.EnterAsync(commandMode.Checked, cancellation.Token); }
            catch { ClosePort(false); throw; }
            await ReadAsync();
        }

        private async Task ReadAsync()
        {
            settings.Clear(); grid.Rows.Clear(); rightGrid.Rows.Clear();
            output.Text = await client.ReadConfigurationAsync(cancellation.Token);
            settings = await client.ReadSettingsAsync(cancellation.Token);
            foreach (var item in settings.OrderBy(s => LeftOrder.Contains(s.Register) ? Array.IndexOf(LeftOrder, s.Register) : 100 + Array.IndexOf(RightOrder, s.Register)))
            {
                var grid = LeftOrder.Contains(item.Register) ? this.grid : rightGrid;
                int index = grid.Rows.Add("S" + item.Register + " — " + item.Name, item.Name,
                    P400SettingInfo.Display(client.Mode, item.Register, item.Value), item.Proposed,
                    P400SettingInfo.Details(client.Mode, item.Register) + (item.Editable ? "" : "\r\n" + item.Note),
                    P400SettingInfo.ManualDefault(client.Mode, item.Register));
                grid.Rows[index].Tag = item;
                var options = P400SettingInfo.Options(client.Mode, item.Register);
                if (item.Register == 107 && item.Editable)
                    grid.Rows[index].Cells["proposed"] = new DataGridViewButtonCell { Value = "輸入新密碼…", UseColumnTextForButtonValue = false };
                else if (item.Editable && options.ContainsKey(item.Proposed))
                {
                    grid.Rows[index].Cells["proposed"] = new DataGridViewComboBoxCell
                    {
                        DataSource = options.Where(o => P400AtClient.IsValid(client.Mode, item.Register, P400AtClient.NumericValue(o.Key))).ToList(), DisplayMember = "Value", ValueMember = "Key",
                        ValueType = typeof(string), Value = item.Proposed,
                        DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                        FlatStyle = FlatStyle.Flat
                    };
                }
                else if (!item.Editable)
                    grid.Rows[index].Cells["proposed"].Value = P400SettingInfo.Display(client.Mode, item.Register, item.Proposed);
                grid.Rows[index].Cells["proposed"].ReadOnly = !item.Editable;
                grid.Rows[index].Cells["register"].ToolTipText = P400SettingInfo.Details(client.Mode, item.Register);
                grid.Rows[index].Cells["default"].ToolTipText = "手冊參考值，不是目前值，也不會自動套用。";
            }
            FitColumns(grid); FitColumns(rightGrid);
            status.Text = "P400 身分已確認：" + client.Identity + "；模式 S128=" + client.Mode + "。讀取完成，尚在 AT 模式。";
            if (client.Unsaved) status.Text += " 有未確認的修改，讀取不代表已永久儲存。";
        }

        private async Task WriteAsync()
        {
            grid.EndEdit();
            rightGrid.EndEdit();
            var changes = new List<P400Setting>();
            foreach (DataGridViewRow row in grid.Rows.Cast<DataGridViewRow>().Concat(rightGrid.Rows.Cast<DataGridViewRow>()))
            {
                var item = (P400Setting)row.Tag;
                if (item.Register == 107)
                {
                    if (!string.IsNullOrEmpty(item.Proposed)) { P400AtClient.ValidatePassword(item.Proposed); changes.Add(item); }
                    continue;
                }
                item.Proposed = Convert.ToString(row.Cells["proposed"].Value).Trim();
                if (item.Editable && item.Proposed != item.Value)
                {
                    if (!P400AtClient.IsValid(client.Mode, item.Register, P400AtClient.NumericValue(item.Proposed)))
                        throw new InvalidOperationException("S" + item.Register + " 的新值超出此頁支援範圍。");
                    changes.Add(item);
                }
            }
            if (changes.Count == 0) { status.Text = "沒有修改。"; return; }
            var summary = string.Join("\r\n", changes.Select(p => p.Register == 107 ? "S107：更新密碼（隱藏；僅確認指令接受，不能讀回驗證原文）" : "S" + p.Register + ": " + p.Value + " → " + p.Proposed));
            if (changes.Any(p => new[] {102,110,128,142,153,217}.Contains(p.Register)))
                summary += "\r\n注意：將改變連線速度、格式、介面或資料處理；配對／連接設備必須相符，可能失聯。S102/S110 在 CONFIG AT 期间不切換，返回 DATA 才使用新值。";
            if (MessageBox.Show(this, summary + "\r\n將逐項讀回後執行 AT&W。配對端不會同步更改；確認功率符合設備與所在地限制。是否寫入？", "P400 寫入確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            await client.WriteAndSaveAsync(changes, cancellation.Token);
            await ReadAsync();
            status.Text = changes.Any(p => p.Register == 107) ? "S107 寫入及 AT&W 回覆 OK；密碼無法讀回驗證，需確認配對端通訊。仍在 AT 模式。" : "逐項讀回一致，AT&W 回覆 OK；仍在 AT 模式。可按「返回 DATA 並釋放」。";
        }

        private void UpdateButtons()
        {
            bool connected = client != null, ready = client?.Ready == true;
            connect.Enabled = !busy && !connected;
            ports.Enabled = baud.Enabled = commandMode.Enabled = refresh.Enabled = !busy && !connected;
            read.Enabled = help.Enabled = frequency.Enabled = !busy && ready;
            write.Enabled = !busy && ready && settings.Count > 0;
            resume.Enabled = !busy && ready && !client.Unsaved;
            release.Enabled = !busy && connected; cancel.Enabled = busy;
            grid.Enabled = rightGrid.Enabled = !busy;
            fallback.Enabled = !busy && !connected;
            frequencyEditor.Enabled = !busy;
        }

        private void RefreshPorts()
        {
            if (client != null) return;
            string selected = ports.Text; ports.Items.Clear();
            ports.Items.AddRange(SerialPort.GetPortNames().OrderBy(p => p).Cast<object>().ToArray());
            if (ports.Items.Contains(selected)) ports.SelectedItem = selected;
        }

        private void ClosePort(bool report = true)
        {
            bool hadPort = client != null;
            client?.Dispose(); client = null; settings.Clear();
            if (!IsDisposed)
            {
                grid.Rows.Clear(); rightGrid.Rows.Clear();
                if (hadPort && report) status.Text = "已釋放 COM；未自動送 ATA 或儲存，模組可能仍在 AT 模式／有未儲存修改。重新連線請勾選已進入 AT，或依手冊重啟後確認。";
            }
        }
        public void Activate() { RefreshPorts(); UpdateButtons(); }
        public void Deactivate() { cancellation?.Cancel(); ClosePort(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { cancellation?.Cancel(); ClosePort(false); }
            base.Dispose(disposing);
        }
    }
}
