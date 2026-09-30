using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    // Only configures the FC UART. No receiver commands, reboot, arming or calibration writes.
    internal sealed class FmtElrsSerialForm : Form
    {
        private readonly ComboBox port = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        private readonly Button preview = new Button { Text = "預覽目前值／變更", AutoSize = true };
        private readonly Button apply = new Button { Text = "確認並寫入飛控", AutoSize = true, Enabled = false };
        private readonly TextBox detail = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        private readonly CheckBox safe = new CheckBox { Text = "已移除槳葉／確保安全；透過 USB 或其他獨立鏈路連線（不是要修改的 ELRS 埠）", AutoSize = true };
        private Dictionary<string, double> planned;
        private Dictionary<string, double> original;
        private object previewLink;
        private byte systemId, componentId;
        private bool busy;

        internal static double SetMaskBit(double original, int bit, bool enabled)
        {
            if (bit < 0 || bit > 31 || double.IsNaN(original) || original < 0 || original > uint.MaxValue || Math.Truncate(original) != original)
                throw new InvalidOperationException("位元遮罩值不合法，停止變更。");
            var mask = (uint)original;
            return enabled ? mask | (1u << bit) : mask & ~(1u << bit);
        }

        internal static Dictionary<string, double> BuildPlan(string serial, bool mavlink, IDictionary<string, double> current)
        {
            if (serial == null || !Regex.IsMatch(serial, @"^SERIAL[1-9][0-9]*$"))
                throw new InvalidOperationException("請選接收機實際接線的 SERIAL 埠（不可選 USB SERIAL0）。");
            var result = new Dictionary<string, double>();
            result.Add(serial + "_BAUD", mavlink ? 460 : 115); // Parameter codes, not literal baud.
            result.Add(serial + "_PROTOCOL", mavlink ? 2 : 23); // Protocol last: it can interrupt the link.
            result.Add("RSSI_TYPE", mavlink ? 5 : 3);
            if (mavlink)
            {
                // ExpressLRS SerialMavlink::sendRCFrame sends RC_CHANNELS_OVERRIDE
                // (3.5.0 and current upstream). Do not leave its input blocked.
                // Preserve unrelated global options, including other receivers.
                if (current.ContainsKey("RC_OPTIONS"))
                    result.Add("RC_OPTIONS", SetMaskBit(current["RC_OPTIONS"], 1, false));
            }
            else
            {
                if (!current.ContainsKey("RC_OPTIONS")) throw new InvalidOperationException("飛控未提供 RC_OPTIONS。");
                result.Add("RC_OPTIONS", SetMaskBit(SetMaskBit(current["RC_OPTIONS"], 13, true), 9, true));
                if (current.ContainsKey("RC_PROTOCOLS") && SetMaskBit(current["RC_PROTOCOLS"], 0, false) == current["RC_PROTOCOLS"])
                    result.Add("RC_PROTOCOLS", SetMaskBit(current["RC_PROTOCOLS"], 9, true));
            }
            // MAVLink does not require the separate MAVRadio RC_PROTOCOLS backend.
            AddStreamPlan(serial, mavlink, current, result);
            foreach (var key in result.Keys)
                if (!current.ContainsKey(key)) throw new InvalidOperationException("飛控未提供必要參數：" + key);
            return result;
        }

        // ArduPilot assigns MAVLink instances by ascending SERIAL index (protocols
        // 1/2/43). Legacy SR starts at 0, MAVn (4.7+) starts at 1. Preserve rates
        // of other links when changing protocol shifts their instance numbers.
        internal static void AddStreamPlan(string serial, bool mavlink, IDictionary<string, double> current, Dictionary<string, double> result)
        {
            Func<double, bool> isMav = p => p == 1 || p == 2 || p == 43;
            if (!mavlink && !isMav(current[serial + "_PROTOCOL"])) return;
            if (!current.ContainsKey("SERIAL0_PROTOCOL"))
                throw new InvalidOperationException("缺少 SERIAL0_PROTOCOL，無法確認 MAVLink 通道順序；請完整讀取參數。");
            var ports = current.Keys.Where(k => Regex.IsMatch(k, @"^SERIAL\d+_PROTOCOL$"))
                .OrderBy(k => int.Parse(Regex.Match(k, @"\d+").Value)).ToArray();
            var before = ports.Where(k => isMav(current[k])).ToArray();
            var after = ports.Where(k => k == serial + "_PROTOCOL" ? mavlink : isMav(current[k])).ToArray();
            bool legacy = current.ContainsKey("SR0_RAW_SENS"), modern = current.ContainsKey("MAV1_RAW_SENS");
            if (legacy == modern)
                throw new InvalidOperationException("無法唯一辨識 SRx／MAVx 資料流參數，停止套用；請完整讀取並確認韌體。");
            Func<int, string> prefix = index => legacy ? "SR" + index + "_" : "MAV" + (index + 1) + "_";
            var suffixes = new[] { "RAW_SENS", "EXT_STAT", "RC_CHAN", "POSITION", "EXTRA1", "EXTRA2", "EXTRA3", "ADSB", "PARAMS", "RAW_CTRL" };
            for (int index = 0; index < after.Length; index++)
            {
                bool target = after[index] == serial + "_PROTOCOL";
                int oldIndex = Array.IndexOf(before, after[index]);
                if (!target && oldIndex == index) continue;
                foreach (var suffix in suffixes)
                {
                    string key = prefix(index) + suffix;
                    if (!current.ContainsKey(key))
                        throw new InvalidOperationException("所需資料流參數尚未提供：" + key + "。請先確認韌體／完整參數；不會部分套用。");
                    if (target) result[key] = suffix == "ADSB" || suffix == "PARAMS" || suffix == "RAW_CTRL" ? 0 : 1;
                    else
                    {
                        string oldKey = prefix(oldIndex) + suffix;
                        if (oldIndex < 0 || !current.ContainsKey(oldKey))
                            throw new InvalidOperationException("缺少其他鏈路原始資料流設定，停止套用：" + oldKey);
                        result[key] = current[oldKey];
                    }
                }
                // Do not move newer MAVn_OPTIONS implicitly: they contain forwarding
                // and security policy. Require unchanged instance mapping if present.
                if (!target && oldIndex != index && current.ContainsKey(prefix(index) + "OPTIONS"))
                    throw new InvalidOperationException("切換會改變其他 MAVLink 通道編號及 MAVn_OPTIONS 對應，需先確認鏈路配置；未開始寫入。");
            }
        }

        internal FmtElrsSerialForm()
        {
            Text = "ELRS 飛控串列埠設定";
            ClientSize = new Size(880, 620);
            MinimumSize = new Size(700, 450);
            StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            controls.Controls.AddRange(new Control[] { new Label { Text = "接收機 SERIAL 埠", AutoSize = true }, port, mode, preview, apply });
            layout.Controls.Add(controls, 0, 0);
            layout.Controls.Add(detail, 0, 1);
            layout.Controls.Add(safe, 0, 2);
            Controls.Add(layout);
            mode.Items.AddRange(new object[] { "1. CRSF 模式（RCIN）", "2. MAVLink 模式（MAVLink2）" });
            mode.SelectedIndex = 0;
            foreach (var name in MainV2.comPort.MAV.param.Keys.Where(n => Regex.IsMatch(n, @"^SERIAL[1-9][0-9]*_PROTOCOL$"))
                .OrderBy(n => int.Parse(Regex.Match(n, @"\d+").Value)))
                port.Items.Add(name.Replace("_PROTOCOL", ""));
            // No automatic port choice: UART labels differ between flight controller boards.
            preview.Click += (s, e) => Preview();
            port.SelectedIndexChanged += (s, e) => InvalidatePreview();
            mode.SelectedIndexChanged += (s, e) => InvalidatePreview();
            safe.CheckedChanged += (s, e) => apply.Enabled = !busy && planned != null && safe.Checked;
            apply.Click += Apply;
            FormClosing += (s, e) => { if (busy) e.Cancel = true; };
            InvalidatePreview();
        }

        private void InvalidatePreview()
        {
            planned = null;
            apply.Enabled = false;
            detail.Text = "只設定飛控串列埠；不代表模組已切換或遙控鏈路已驗證。\r\n請先備份參數，再選埠並預覽。\r\n\r\n" + Instructions();
        }

        private string Instructions()
        {
            return mode.SelectedIndex == 0
                ? "CRSF：接收機 TX/RX 與飛控 UART RX/TX 交叉接線並共地。\r\n" +
                  "模組端：在 ELRS Lua／接收機設定確認輸出協定為 CRSF；MAVLink 使用者需先依版本切回 Normal。\r\n" +
                  "自動設定 SERIALx_PROTOCOL=23、RSSI_TYPE=3，啟用 RC_OPTIONS bit 13 (420K) 與 bit 9（抑制 ELRS 模式／速率提示）。\r\n" +
                  "SERIALx_BAUD=115（官方相容設定；實際 CRSF 420K 由 RC_OPTIONS bit 13 控制）。RC_PROTOCOLS 若非 All，補開 CRSF 位元；其他位元保留。\r\n" +
                  "完成後安全斷電重啟，重新連線並檢查所有通道、遙測及失聯保護。\r\n\r\n官方：https://www.expresslrs.org/quick-start/ardupilot-setup/"
                : "MAVLink：本頁寫 SERIALx_BAUD=460（460800 bps）、SERIALx_PROTOCOL=2。\r\n" +
                  "模組端：需要相容硬體與 ELRS 3.5 以上。v3 在 Lua Other Devices 將 RX serial protocol 設為 MAVLink；v4 會配合 TX 模式自動設定。\r\n" +
                  "接收機關機後，TX Link Mode 改 MAVLink，再開接收機確認連線。\r\n" +
                  "自動設定 RSSI_TYPE=5；不變更 RC_PROTOCOLS，不檢查 MAVRadio 或本機參數說明資料。\r\n" +
                  "ELRS 使用 RC_CHANNELS_OVERRIDE 傳送搖桿：取消 RC_OPTIONS bit 1（Ignore MAVLink Overrides），其餘位元保留。\r\n" +
                  "依 SERIAL 協定順序計算對應 SRx／MAVx 通道：一般資料流=1，ADSB／PARAMS／RAW_CTRL=0；預覽會列出所有變更。\r\n" +
                  "不變更失聯保護、RC override 逾時或 SERIAL_OPTIONS。完成後安全重啟並驗證通道與失聯行為。\r\n\r\n官方：https://www.expresslrs.org/software/mavlink/";
        }

        private void Preview()
        {
            InvalidatePreview();
            try
            {
                var link = MainV2.comPort;
                if (link.BaseStream == null || !link.BaseStream.IsOpen) throw new InvalidOperationException("請先連線並讀取飛控參數。");
                var current = link.MAV.param.Keys.ToDictionary(k => k, k => link.MAV.param[k].Value);
                if (link.MAV.param.TotalReported <= 0 || link.MAV.param.TotalReceived != link.MAV.param.TotalReported)
                    throw new InvalidOperationException("飛控參數尚未完整讀取，無法確認所有 SERIAL 與資料流通道，請先完成參數讀取。");
                planned = BuildPlan(port.SelectedItem as string, mode.SelectedIndex == 1, current);
                original = planned.Keys.ToDictionary(k => k, k => current[k]);
                // Also validate dependencies used to map/preserve other stream groups.
                foreach (var p in current.Where(p => Regex.IsMatch(p.Key, @"^SERIAL\d+_PROTOCOL$") ||
                    Regex.IsMatch(p.Key, @"^(SR\d+|MAV\d+)_(RAW_SENS|EXT_STAT|RC_CHAN|POSITION|EXTRA[123]|ADSB|PARAMS|RAW_CTRL)$")))
                    original[p.Key] = p.Value;
                previewLink = link;
                systemId = (byte)link.sysidcurrent;
                componentId = (byte)link.compidcurrent;
                detail.Text = "目標 SYSID=" + systemId + "，COMPID=" + componentId + "（目前值為已讀取的參數快取）\r\n" +
                    string.Join("\r\n", planned.Select(p => p.Key + "：" + original[p.Key].ToString(CultureInfo.InvariantCulture) + " → " + p.Value)) +
                    "\r\n\r\n注意：切換可能中斷遙控／數傳。只可在地面未解鎖時操作；本頁不自動重啟。\r\n\r\n" + Instructions();
                detail.Text += "\r\n\r\n全域目前值：" + string.Join("，", new[] { "RC_OPTIONS", "RC_PROTOCOLS", "RSSI_TYPE" }
                    .Select(k => k + "=" + (current.ContainsKey(k) ? current[k].ToString(CultureInfo.InvariantCulture) : "未提供")));
                if (planned.ContainsKey("RC_OPTIONS")) detail.Text += "\r\nRC_OPTIONS 逐位變更：\r\n" + string.Join("\r\n",
                    Enumerable.Range(0, 32).Where(bit => (((uint)original["RC_OPTIONS"] ^ (uint)planned["RC_OPTIONS"]) & (1u << bit)) != 0)
                    .Select(bit => "bit " + bit + "：" + ((((uint)original["RC_OPTIONS"] & (1u << bit)) != 0) ? "勾選" : "取消") +
                        " → " + ((((uint)planned["RC_OPTIONS"] & (1u << bit)) != 0) ? "勾選" : "取消")));
                apply.Enabled = safe.Checked;
            }
            catch (Exception ex) { planned = null; detail.Text = ex.Message + "\r\n\r\n" + Instructions(); }
        }

        private async void Apply(object sender, EventArgs e)
        {
            var link = MainV2.comPort;
            if (planned == null || busy || !safe.Checked) return;
            Func<bool> valid = () => ReferenceEquals(link, MainV2.comPort) && ReferenceEquals(link, previewLink) &&
                link.sysidcurrent == systemId && link.compidcurrent == componentId &&
                link.BaseStream != null && link.BaseStream.IsOpen && !link.ReadOnly && !link.MAV.cs.armed;
            if (!valid()) { MessageBox.Show(this, "連線／機體已變更、唯讀或已解鎖。請重新預覽並確認安全狀態。"); return; }
            foreach (var entry in original)
                if (!link.MAV.param.ContainsKey(entry.Key) || link.MAV.param[entry.Key].Value != entry.Value)
                { MessageBox.Show(this, "參數已變更，請重新預覽。"); return; }
            if (MessageBox.Show(this, detail.Text + "\r\n\r\n確定寫入上述 SERIAL／RC／RSSI 參數？RC_OPTIONS 是全域設定。", "ELRS 寫入確認",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            var edits = planned.OrderBy(p => p.Key.EndsWith("_PROTOCOL", StringComparison.Ordinal) ? 1 : 0).ToArray();
            busy = true;
            apply.Enabled = preview.Enabled = port.Enabled = mode.Enabled = safe.Enabled = false;
            var completed = new List<string>();
            string failure = null;
            try
            {
                await Task.Run(() =>
                {
                    var readDeadline = DateTime.UtcNow.AddSeconds(60);
                    // Re-read baseline before the first write; the preview cache may be stale.
                    foreach (var entry in original)
                    {
                        if (DateTime.UtcNow > readDeadline) throw new InvalidOperationException("寫入前讀取驗證逾時，未開始寫入。請確認鏈路後重試。");
                        if (!valid()) throw new InvalidOperationException("連線／機體／解鎖狀態改變，停止寫入。");
                        if (link.GetParam(systemId, componentId, entry.Key) != entry.Value)
                            throw new InvalidOperationException(entry.Key + " 實際值與預覽不同，未開始寫入；請重新預覽。");
                    }
                    foreach (var edit in edits)
                    {
                        if (!valid()) throw new InvalidOperationException("連線／機體／解鎖狀態改變，停止後續寫入。");
                        if (!link.setParam(systemId, componentId, edit.Key, edit.Value))
                            throw new InvalidOperationException(edit.Key + " 未確認成功；可能已送達，請重新讀取。");
                        if (!valid()) throw new InvalidOperationException("寫入後連線／機體／解鎖狀態改變，請重新讀取確認。");
                        var actual = link.GetParam(systemId, componentId, edit.Key);
                        if (actual != edit.Value)
                            throw new InvalidOperationException(edit.Key + " 讀回值 " + actual + " 與目標 " + edit.Value + " 不符，停止後續寫入。");
                        completed.Add(edit.Key + "=" + actual.ToString(CultureInfo.InvariantCulture));
                    }
                });
            }
            catch (Exception ex) { failure = ex.Message; }
            finally
            {
                busy = false;
                preview.Enabled = port.Enabled = mode.Enabled = safe.Enabled = true;
                planned = null;
                detail.Text = "已寫入並讀回驗證：" + string.Join("、", completed) + "\r\n" +
                    (failure ?? "串列埠參數寫入完成；模組尚需手動設定，未驗證端到端遙控。") +
                    "\r\n不會自動回復或重啟。若中途中斷，部分參數可能已變更；請重新讀取確認。\r\n\r\n" + Instructions();
            }
        }
    }
}
