using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MissionPlanner.FMT
{
    public interface IP400Transport : IDisposable
    {
        void Write(string text);
        string ReadExisting();
    }

    public sealed class P400SerialTransport : IP400Transport
    {
        private readonly SerialPort port;
        public P400SerialTransport(string name, int baud)
        {
            port = new SerialPort(name, baud, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None, WriteTimeout = 1500,
                DtrEnable = true, RtsEnable = true
            };
            try { port.Open(); } catch { port.Dispose(); throw; }
        }
        public void Write(string text) { port.Write(text); }
        public string ReadExisting() { return port.ReadExisting(); }
        public void Dispose() { port.Dispose(); }
    }

    public sealed class P400Setting
    {
        public int Register { get; private set; }
        public string Name { get; private set; }
        public string Value { get; set; }
        public string Proposed { get; set; }
        public bool Editable { get; set; }
        public string Note { get; set; }
        public P400Setting(int register, string name)
        { Register = register; Name = name; }
    }

    // P400 manual v1.33 pp.37-38, 104-115, 125. No SiK command assumptions.
    public sealed class P400AtClient : IDisposable
    {
        private readonly IP400Transport transport;
        private readonly Action checkAccess;
        public bool Ready { get; private set; }
        public bool Unsaved { get; private set; }
        public int Mode { get; private set; } = -1;
        public string Identity { get; private set; }
        public bool ForcedCommandMode { get; private set; }

        public P400AtClient(IP400Transport transport, Action checkAccess)
        { this.transport = transport; this.checkAccess = checkAccess ?? (() => { }); }

        public static readonly int[] Registers = { 128, 101, 102, 103, 104, 105, 108, 110, 113, 125, 131, 132, 133, 140, 141, 158, 116, 123, 142, 150, 153, 107, 213, 124, 217, 151, 154 };
        private static readonly string[] Names = {
            "機型模式（唯讀）", "網路角色", "DATA 鮑率代碼（唯讀）", "無線速率代碼",
            "網路 ID", "本機地址", "發射功率 dBm", "DATA 格式（唯讀）", "封包重傳次數",
            "NB 頻寬（唯讀）", "NB TX 頻道（唯讀）", "NB RX 頻道（唯讀）", "網路類型",
            "目的地址", "中繼器有無", "FEC 代碼", "字元逾時", "上行 RSSI", "串列介面模式", "同步模式", "地址標記", "通訊密碼（Static Mask）", "封包重試限制", "下行 RSSI", "協定類型", "快速同步逾時", "多主站模式" };

        public async Task EnterAsync(bool alreadyInCommandMode, CancellationToken token)
        {
            ForcedCommandMode = alreadyInCommandMode;
            checkAccess();
            transport.ReadExisting();
            if (!alreadyInCommandMode)
            {
                await Task.Delay(1100, token).ConfigureAwait(false);
                checkAccess();
                transport.Write("+++"); // No CR; one second guard time on each side.
                await Task.Delay(1100, token).ConfigureAwait(false);
                transport.ReadExisting();
            }
            await ExchangeAsync("AT", token).ConfigureAwait(false);
            Identity = await ExchangeAsync("ATI1", token).ConfigureAwait(false);
            if (!Regex.IsMatch(Identity, @"\bP400\b", RegexOptions.IgnoreCase))
                throw new InvalidOperationException("產品識別不是 P400，禁止寫入。回覆：" + Identity);
            Ready = true;
        }

        public static string Payload(string response, string command)
        {
            return string.Join("\r\n", response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s != "OK" && !s.Equals(command, StringComparison.OrdinalIgnoreCase)));
        }

        private async Task<string> ExchangeAsync(string command, CancellationToken token, bool raw = false)
        {
            var commandLabel = command.StartsWith("ATS107=", StringComparison.OrdinalIgnoreCase) ? "ATS107=[隱藏]" : command;
            token.ThrowIfCancellationRequested();
            checkAccess();
            transport.ReadExisting();
            transport.Write(command + (raw ? "" : "\r"));
            var text = new StringBuilder();
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 5000)
            {
                token.ThrowIfCancellationRequested();
                checkAccess();
                text.Append(transport.ReadExisting());
                if (text.Length > 131072) { Ready = false; throw new IOException("回覆過長，已停止。"); }
                var reply = text.ToString();
                if (Regex.IsMatch(reply, @"(^|[\r\n])\s*ERROR[^\r\n]*[\r\n]", RegexOptions.IgnoreCase))
                    throw new InvalidOperationException(commandLabel + "：模組拒絕指令。");
                if (Regex.IsMatch(reply, @"(^|[\r\n])\s*OK\s*[\r\n]"))
                    return Payload(reply, command);
                await Task.Delay(30, token).ConfigureAwait(false);
            }
            Ready = false;
            throw new TimeoutException(commandLabel + " 回覆逾時；停止後續寫入，請釋放序列埠再重新讀取。");
        }

        private void EnsureReady()
        { if (!Ready) throw new InvalidOperationException("請先確認 P400 身分並進入 AT 模式。"); }

        public async Task<string> ReadConfigurationAsync(CancellationToken token)
        { EnsureReady(); return await ExchangeAsync("AT&V", token).ConfigureAwait(false); }

        public async Task<string> ReadFrequencyTableAsync(CancellationToken token)
        { return await ReadFrequencyTableAsync(0, token).ConfigureAwait(false); }

        public async Task<string> ReadFrequencyTableAsync(int table, CancellationToken token)
        {
            EnsureReady();
            if (table != 0 && table != 1) throw new ArgumentOutOfRangeException(nameof(table));
            return await ExchangeAsync("ATP" + table + "?", token).ConfigureAwait(false);
        }

        public async Task WriteFrequencyTableAsync(int table, P400FrequencyPlan plan, P400FrequencyPlan original, CancellationToken token)
        {
            EnsureReady();
            if (table != 0 && table != 1) throw new ArgumentOutOfRangeException(nameof(table));
            plan.ValidateForRadio(); original.ValidateForRadio();
            if (Unsaved) throw new InvalidOperationException("已有未確認修改，請先處理後再寫頻率表。");
            if (NumericValue(await ExchangeAsync("ATS128?", token).ConfigureAwait(false)) != 2 ||
                NumericValue(await ExchangeAsync("ATS238?", token).ConfigureAwait(false)) != 1)
                throw new InvalidOperationException("僅支援 S128=2、S238=1 的 400 MHz 跳頻表；不修改模式。");
            var current = P400FrequencyPlan.Parse(await ReadFrequencyTableAsync(table, token).ConfigureAwait(false));
            if (!current.Frequencies.SequenceEqual(original.Frequencies)) throw new InvalidOperationException("頻率表已改變，請重新讀取及確認。");
            token.ThrowIfCancellationRequested(); checkAccess();
            Unsaved = true;
            try
            {
                // Manual pp.76-78: text-file transfer, all 50 CR/LF lines, automatic save.
                // No per-line OK is expected; completion OK follows the complete table.
                await ExchangeAsync("ATP" + table + "=\r" + string.Join("\r\n", plan.Frequencies) + "\r\n", token, true).ConfigureAwait(false);
                var readback = P400FrequencyPlan.Parse(await ReadFrequencyTableAsync(table, token).ConfigureAwait(false));
                if (!readback.Frequencies.SequenceEqual(plan.Frequencies)) throw new IOException("頻率讀回不符；可能已部分或全部保存，未自動回復。");
                Unsaved = false;
            }
            catch { Ready = false; throw; } // Unknown editor state: require release/reconnect, never more AT commands.
        }

        public async Task<string> ReadHelpAsync(int register, CancellationToken token)
        {
            EnsureReady();
            if (!Registers.Contains(register)) throw new ArgumentOutOfRangeException(nameof(register));
            return await ExchangeAsync("ATS" + register + " /?", token).ConfigureAwait(false);
        }

        public static long NumericValue(string payload)
        {
            long value;
            if (!long.TryParse(payload.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new FormatException("模組回覆不是單一整數，保留原文並禁止編輯：" + payload);
            return value;
        }

        public async Task<List<P400Setting>> ReadSettingsAsync(CancellationToken token)
        {
            EnsureReady();
            Mode = (int)NumericValue(await ExchangeAsync("ATS128?", token).ConfigureAwait(false));
            var settings = new List<P400Setting>();
            for (int i = 0; i < Registers.Length; i++)
            {
                var item = new P400Setting(Registers[i], Names[i].Replace("（唯讀）", ""));
                try
                {
                    item.Value = await ExchangeAsync("ATS" + item.Register + "?", token).ConfigureAwait(false);
                    item.Proposed = item.Value;
                    if (item.Register == 107)
                    {
                        item.Value = "****"; item.Proposed = ""; item.Editable = Mode >= 0 && Mode <= 2;
                        item.Note = "輸入新密碼才更新；讀回遮蔽，無法驗證原文。";
                        settings.Add(item); continue;
                    }
                    if (new[] { 123, 124 }.Contains(item.Register))
                    {
                        item.Note = "唯讀；字串、N/A 或複合回覆均保留原值，不作整數轉換。";
                        settings.Add(item);
                        continue;
                    }
                    var value = NumericValue(item.Value);
                    item.Editable = IsValid(Mode, item.Register, value);
                    item.Proposed = item.Value;
                    item.Note = item.Editable ? "請選取後查閱模組參數說明；輸入代碼／整數" : "唯讀／目前模式或值尚未支援";
                }
                catch (InvalidOperationException ex) { item.Note = ex.Message; }
                catch (FormatException ex) { item.Note = ex.Message; }
                settings.Add(item);
            }
            return settings;
        }

        public static bool IsValid(int mode, int register, long value)
        {
            if (mode < 0 || mode > 2) return false;
            bool fh = mode != 0;
            switch (register)
            {
                case 102: return value >= 0 && value <= 14;
                case 110: return value >= 1 && value <= 10;
                case 116: return value >= 0 && value <= 254;
                case 125: return !fh && value >= 0 && value <= 2;
                case 128: return value >= 0 && value <= 2;
                case 131: case 132: return !fh && value >= 0 && value <= 63;
                case 142: return value >= 0 && value <= 3;
                case 150: return fh && value >= 0 && value <= 2;
                case 151: return fh && value >= 100 && value <= 65000;
                case 153: return fh && (value == 0 || value == 1);
                case 213: return fh && value >= 0 && value <= 254;
                case 217: return fh && value >= 0 && value <= 2;
                case 101: return value >= 0 && value <= (fh ? 2 : 3);
                case 103: return fh ? new long[] { 0,1,2,3,4,5,6,8 }.Contains(value) : value >= 0 && value <= 8;
                case 104: return fh && value >= 0 && value <= 4000000000L;
                case 105: return value >= 1 && value <= (fh ? 65534 : 255);
                // Conservative base-model range; factory-enabled higher power stays read-only.
                case 108: return value >= 20 && value <= 30;
                case 113: return value >= 0 && value <= 254;
                case 133: return fh && new long[] { 0,1,2,4 }.Contains(value);
                case 140: return fh && value >= 1 && value <= 65535;
                case 141: return value == 0 || value == 1;
                case 158: return fh ? new long[] { 0,1,2,3,5,6,7 }.Contains(value) : value == 0 || value == 1;
                default: return false;
            }
        }

        public async Task WriteAndSaveAsync(List<P400Setting> changes, CancellationToken token)
        {
            EnsureReady();
            if (changes.Count == 0) throw new InvalidOperationException("沒有修改項目。");
            if (changes.Any(s => s.Register == 102 || s.Register == 110) && !ForcedCommandMode)
                throw new InvalidOperationException("修改 S102/S110 請先釋放連線，用 CONFIG 強制進入 9600/8N1 AT 模式後重新讀取。");
            if (changes.Any(s => s.Register == 128 || s.Register == 142) && changes.Count != 1)
                throw new InvalidOperationException("S128 工作模式及 S142 實體介面須單獨寫入，再重新讀取設定；不可與其他項目同批修改。");
            var mode = (int)NumericValue(await ExchangeAsync("ATS128?", token).ConfigureAwait(false));
            if (mode != Mode) throw new InvalidOperationException("模組模式已改變，請重新讀取。");
            // Validate every change and its original value BEFORE any write.
            foreach (var item in changes)
            {
                if (item.Register == 107)
                {
                    if (!item.Editable || mode < 0 || mode > 2) throw new InvalidOperationException("S107 目前不可修改。");
                    ValidatePassword(item.Proposed); continue;
                }
                if (!item.Editable || !IsValid(mode, item.Register, NumericValue(item.Proposed)))
                    throw new InvalidOperationException("S" + item.Register + " 不允許此值。");
                var current = NumericValue(await ExchangeAsync("ATS" + item.Register + "?", token).ConfigureAwait(false));
                if (current != NumericValue(item.Value)) throw new InvalidOperationException("參數已變動，請重新讀取。");
            }
            var roleChange = changes.FirstOrDefault(s => s.Register == 101);
            var addressChange = changes.FirstOrDefault(s => s.Register == 105);
            var role = roleChange != null ? NumericValue(roleChange.Proposed) : NumericValue(await ExchangeAsync("ATS101?", token).ConfigureAwait(false));
            var address = addressChange != null ? NumericValue(addressChange.Proposed) : NumericValue(await ExchangeAsync("ATS105?", token).ConfigureAwait(false));
            if (mode != 0 && role == 0 && address != 1) throw new InvalidOperationException("FH 主站地址必須為 1。");
            var sync = changes.FirstOrDefault(s => s.Register == 150);
            var topologyChange = changes.FirstOrDefault(s => s.Register == 133);
            if (sync != null && NumericValue(sync.Proposed) == 1 &&
                (topologyChange != null ? NumericValue(topologyChange.Proposed) : NumericValue(await ExchangeAsync("ATS133?", token).ConfigureAwait(false))) != 1)
                throw new InvalidOperationException("S150=1 僅適用點對點 S133=1。");
            foreach (var item in changes)
            {
                Unsaved = true; // Even an uncertain write must be treated as potentially applied.
                if (item.Register == 107)
                {
                    await ExchangeAsync("ATS107=" + item.Proposed, token).ConfigureAwait(false);
                    continue; // Masked replies cannot prove password equality. Only command acknowledgement is available.
                }
                var value = NumericValue(item.Proposed);
                await ExchangeAsync("ATS" + item.Register + "=" + value.ToString(CultureInfo.InvariantCulture), token).ConfigureAwait(false);
                var readback = NumericValue(await ExchangeAsync("ATS" + item.Register + "?", token).ConfigureAwait(false));
                if (readback != value) throw new IOException("S" + item.Register + " 讀回不符，未執行永久儲存。");
            }
            await ExchangeAsync("AT&W", token).ConfigureAwait(false);
            Unsaved = false;
        }

        public static void ValidatePassword(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 16 || !Regex.IsMatch(value, @"\A[A-Za-z0-9_.-]+\z"))
                throw new FormatException("S107 請輸入 1–16 個英數字、底線、句點或連字號；不接受遮蔽值、空白或控制字元。");
        }

        public async Task ReturnToDataAsync(CancellationToken token)
        {
            EnsureReady();
            if (Unsaved) throw new InvalidOperationException("有未確認的修改；請重新讀取處理，不可直接返回 DATA。");
            checkAccess(); token.ThrowIfCancellationRequested();
            transport.Write("ATA\r");
            Ready = false; // ATA may return CONNECT or enter streaming mode, not necessarily OK.
            await Task.Delay(200, token).ConfigureAwait(false);
        }
        public void Dispose() { Ready = false; transport.Dispose(); }
    }
}
