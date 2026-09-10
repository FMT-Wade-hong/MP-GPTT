using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MissionPlanner.FMT;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public sealed class ConfigP400FrequencyTable : UserControl
    {
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false };
        private readonly ComboBox slots = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly TextBox name = new TextBox { Width = 180, MaxLength = 80 };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoSize = true };
        private string moduleSnapshot;
        private bool dirty, loading;
        public event Action ReadRadioRequested;
        public event Action WriteRadioRequested;
        private readonly ComboBox radioTable = new ComboBox { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        public int RadioTable { get { return radioTable.SelectedIndex; } }
        public void Report(string message) { status.Text = message; }
        public void LoadRadio(string text) { var plan = P400FrequencyPlan.Parse(text); if (ConfirmReplace()) Apply(plan); }
        public P400FrequencyPlan RadioPlan() { var plan = ReadPlan(); plan.ValidateForRadio(); return plan; }
        private string SlotPath { get { return Path.Combine(Settings.GetUserDataDirectory(), "P400FrequencyPlans", "slot-" + (slots.SelectedIndex + 1).ToString("00") + ".json"); } }

        public ConfigP400FrequencyTable()
        {
            Dock = DockStyle.Fill;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, AutoSize = true, Text = "50 筆頻率表（MHz）｜先在第一分頁進入 AT，再讀取／寫入模組\r\n10 個本機組合；保存組合不會寫入模組。寫入需完整 50 筆，僅 S128=2、S238=1。\r\n寫入自動保存並讀回驗證；主／副表分別操作，配對端不會同步修改。請在地面安全狀態操作。" }, 0, 0);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            radioTable.Items.AddRange(new object[] { "主頻率表 ATP0", "副頻率表 ATP1" }); radioTable.SelectedIndex = 0;
            actions.Controls.Add(radioTable);
            Button(actions, "讀取模組頻率", () => ReadRadioRequested?.Invoke());
            Button(actions, "寫入模組頻率", () => WriteRadioRequested?.Invoke());
            actions.Controls.Add(slots); actions.Controls.Add(new Label { AutoSize = true, Text = "組合名稱", Margin = new Padding(4, 7, 4, 0) }); actions.Controls.Add(name);
            Button(actions, "載入組合", () => { var plan = P400FrequencyPlan.Load(SlotPath); if (ConfirmReplace()) Apply(plan); });
            Button(actions, "保存至此組合", () => {
                var plan = ReadPlan();
                if (File.Exists(SlotPath) && MessageBox.Show(this, "覆蓋此組合？上一版保留為 .bak。", "保存組合", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                plan.Save(SlotPath); dirty = false; status.Text = "已保存組合 " + (slots.SelectedIndex + 1) + "；僅本機檔案，未寫入模組。";
            });
            Button(actions, "匯入", Import);
            Button(actions, "匯出 CSV", Export);
            Button(actions, "貼上頻率清單", () => { var plan = P400FrequencyPlan.Parse(Clipboard.GetText()); if (ConfirmReplace()) Apply(plan); });
            Button(actions, "帶入最近模組讀回", () => {
                if (moduleSnapshot == null) throw new InvalidOperationException("請先在第一分頁按「讀取頻率表」。");
                var plan = P400FrequencyPlan.Parse(moduleSnapshot); if (ConfirmReplace()) Apply(plan);
            });
            Button(actions, "清空草稿", () => { if (ConfirmReplace()) Apply(new P400FrequencyPlan()); });
            layout.Controls.Add(actions, 0, 1);
            grid.Columns.Add("channel", "編號"); grid.Columns.Add("frequency", "頻率 MHz");
            grid.Columns[0].ReadOnly = true; grid.Columns[0].Width = 65; grid.Columns[1].Width = 170;
            grid.EditMode = DataGridViewEditMode.EditOnEnter;
            for (int i = 1; i <= 50; i++) grid.Rows.Add(i, "");
            grid.CellValueChanged += (s, e) => { if (!loading) { dirty = true; status.Text = "草稿已修改；請保存組合或匯出。未寫入模組。"; } };
            name.TextChanged += (s, e) => { if (!loading) dirty = true; };
            grid.CellValidating += (s, e) => {
                if (e.ColumnIndex != 1) return;
                try { P400FrequencyPlan.Normalize(Convert.ToString(e.FormattedValue)); grid.Rows[e.RowIndex].ErrorText = ""; }
                catch (FormatException ex) { e.Cancel = true; grid.Rows[e.RowIndex].ErrorText = ex.Message; status.Text = ex.Message; }
            };
            for (int i = 1; i <= 10; i++) slots.Items.Add("固定組合 " + i.ToString("00"));
            slots.SelectedIndex = 0;
            layout.Controls.Add(grid, 0, 2); layout.Controls.Add(status, 0, 3); Controls.Add(layout);
            status.Text = "可填入 1–50 筆；空白列保留，匯出使用 1–50 編號。選取組合不會自動覆蓋草稿。";
        }

        public void SetModuleSnapshot(string text) { moduleSnapshot = text; }
        private void Button(FlowLayoutPanel parent, string caption, Action action)
        {
            var button = new System.Windows.Forms.Button { Text = caption, AutoSize = true };
            button.Click += (s, e) => { try { action(); } catch (Exception ex) { status.Text = ex.Message; } };
            parent.Controls.Add(button);
        }
        private bool ConfirmReplace()
        { return !dirty || MessageBox.Show(this, "取代目前尚未保存的草稿？", "頻率表", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes; }
        private P400FrequencyPlan ReadPlan()
        {
            if (!grid.EndEdit()) throw new FormatException("請先修正目前輸入。");
            var plan = new P400FrequencyPlan { Name = name.Text, Frequencies = grid.Rows.Cast<DataGridViewRow>().Select(r => Convert.ToString(r.Cells[1].Value)).ToArray() };
            plan.Validate(); return plan;
        }
        private void Apply(P400FrequencyPlan plan)
        {
            plan.Validate(); grid.CancelEdit(); loading = true;
            try { name.Text = plan.Name; for (int i = 0; i < 50; i++) { grid.Rows[i].Cells[1].Value = plan.Frequencies[i]; grid.Rows[i].ErrorText = ""; } }
            finally { loading = false; }
            dirty = true;
            int count = plan.Frequencies.Count(f => !string.IsNullOrEmpty(f));
            status.Text = "已載入草稿 " + count + " / 50 筆；請保存或匯出。未寫入模組。";
        }
        private void Import()
        {
            using (var dialog = new OpenFileDialog { Filter = "頻率檔案|*.csv;*.txt;*.json", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (new FileInfo(dialog.FileName).Length > 65536) throw new FormatException("檔案過大，最多 64 KB。");
                var plan = Path.GetExtension(dialog.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)
                    ? P400FrequencyPlan.Load(dialog.FileName) : P400FrequencyPlan.Parse(File.ReadAllText(dialog.FileName));
                if (ConfirmReplace()) Apply(plan);
            }
        }
        private void Export()
        {
            var plan = ReadPlan();
            using (var dialog = new SaveFileDialog { Filter = "CSV 頻率表|*.csv", FileName = "P400-frequencies.csv", OverwritePrompt = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, plan.ToCsv(), new UTF8Encoding(true));
                status.Text = "已匯出 CSV（含空白列）；組合名稱需使用「保存至此組合」保存。未寫入模組。";
            }
        }
    }
}
