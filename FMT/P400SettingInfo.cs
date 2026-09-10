using System.Collections.Generic;
using System.Linq;

namespace MissionPlanner.FMT
{
    // Display metadata from P400 manual v1.33, register reference pp.104–114.
    // Values remain protocol codes; translated labels are never sent to the modem.
    public static class P400SettingInfo
    {
        public static Dictionary<string, string> Options(int mode, int register)
        {
            var result = new Dictionary<string, string>();
            if (mode < 0 || mode > 2) return result;
            bool fh = mode != 0;
            string[] labels = null;
            switch (register)
            {
                case 128: labels = new[] { "400 MHz 窄頻 NB", "900 MHz 跳頻 FH", "400 MHz 跳頻 FH" }; break;
                case 101: labels = fh ? new[] { "主站 Master", "中繼 Repeater", "從站 Slave" } : new[] { "主站／基地台（依協定）", "中繼", "從站", "第二中繼（Trimtalk）" }; break;
                case 102: labels = new[] { "230400 bps", "115200 bps", "57600 bps", "38400 bps", "28800 bps", "19200 bps", "14400 bps", "9600 bps", "7200 bps", "4800 bps", "3600 bps", "2400 bps", "1200 bps", "600 bps", "300 bps" }; break;
                case 103:
                    if (fh)
                    {
                        int[] codes = { 0,1,2,3,4,5,6,8 };
                        int[] rates = { 19200,115200,172800,230000,247000,340000,24700,57600 };
                        for (int i = 0; i < codes.Length; i++) result.Add(codes[i].ToString(), codes[i] + " — " + rates[i] + " bps");
                    }
                    else labels = new[] { "1200 bps", "2400 bps", "3600 bps", "4800 bps", "7200 bps", "9600 bps", "14400 bps", "19200 bps", "16000 bps" };
                    break;
                case 108:
                    int[] mw = { 100,125,160,200,250,320,400,500,630,800,1000 };
                    for (int i = 0; i < mw.Length; i++) result.Add((20+i).ToString(), (20+i) + " dBm — " + mw[i] + " mW");
                    result.Add("33", "33 dBm — 2 W（原廠選配，本頁唯讀）");
                    break;
                case 110:
                    string[] formats = { "8N1", "8N2", "8E1", "8O1", "7N1", "7N2", "7E1", "7O1", "7E2", "7O2" };
                    for (int i = 0; i < formats.Length; i++) result.Add((i+1).ToString(), (i+1) + " — " + formats[i]);
                    break;
                case 125: if (!fh) labels = new[] { "6.25 kHz", "12.5 kHz", "25 kHz" }; break;
                case 131: case 132:
                    if (!fh) for (int i = 0; i <= 63; i++) result.Add(i.ToString(), "頻道 " + i);
                    break;
                case 133:
                    if (fh) { result.Add("0", "0 — 一對多 PMP"); result.Add("1", "1 — 點對點 PP"); result.Add("2", "2 — P2P／E2E"); result.Add("4", "4 — PMP（含 ACK）"); }
                    break;
                case 141: labels = new[] { "無中繼器", "有一個或多個中繼器" }; break;
                case 142: labels = new[] { "RS-232", "RS-485 半雙工", "RS-485 全雙工（TX 切換）", "RS-485 全雙工（TX 常開）" }; break;
                case 150: if (fh) labels = new[] { "一般同步", "快速同步／等 ACK（PP）", "快速同步／等逾時" }; break;
                case 153: if (fh) labels = new[] { "不附加地址", "附加來源地址（4 bytes）" }; break;
                case 217: if (fh) labels = new[] { "透明傳輸", "MODBUS RTU", "DF1 全雙工／地址過濾" }; break;
                case 158:
                    if (fh) { result.Add("0", "0 — 不使用 FEC"); result.Add("1", "1 — Hamming (7,4)"); result.Add("2", "2 — Hamming (15,11)"); result.Add("3", "3 — Hamming (31,24)"); result.Add("5", "5 — BCH (47,36)"); result.Add("6", "6 — Golay (23,12,7)"); result.Add("7", "7 — Reed-Solomon (15,11)"); }
                    else labels = new[] { "關閉 FEC", "啟用 FEC" };
                    break;
            }
            if (labels != null) for (int i = 0; i < labels.Length; i++) result.Add(i.ToString(), i + " — " + labels[i]);
            return result;
        }

        public static string Display(int mode, int register, string value)
        {
            string label;
            return value != null && Options(mode, register).TryGetValue(value.Trim(), out label) ? label : value;
        }

        public static string ManualDefault(int mode, int register)
        {
            // Do not infer defaults from current values or factory-example screenshots.
            switch (register)
            {
                case 102: return "7 — 9600 bps";
                case 104: return mode == 0 ? "NB 不適用" : "1234567890";
                case 107: return mode == 0 ? "空字串（PCC）" : mode > 0 && mode < 3 ? "default（字面值）" : "依模式";
                case 108: return "30 dBm — 1 W";
                case 110: return "1 — 8N1";
                case 113: return "5";
                case 116: return "10 — 2.5 字元";
                case 123: case 124: return "量測值／無預設";
                case 151: return mode == 0 ? "NB 不適用" : "200 ms";
                case 153: return mode == 0 ? "NB 不適用" : "0 — 關閉";
                case 158: return mode == 0 ? "Satel: 0；PCC: 1" : mode > 0 && mode < 3 ? "7 — RS (15,11)" : "依模式／協定";
                case 213: return mode == 0 ? "NB 不適用" : "5";
                case 217: return mode == 0 ? "NB 不適用" : "0 — 透明傳輸";
                case 101: case 103: case 105: case 128: case 133: case 140: return "依設定組合";
                case 131: case 132: return mode == 0 ? "依頻率表／設定" : "FH 不適用";
                case 125: return mode == 0 ? "手冊未註明" : "FH 不適用";
                default: return "手冊未註明";
            }
        }

        public static string Source(int register)
        {
            switch (register)
            {
                case 101: case 102: case 103: return "P400 v1.33 p.105 / p.126";
                case 104: case 105: case 107: case 108: return "P400 v1.33 p.106 / p.126";
                case 110: return "P400 v1.33 p.107 / p.126";
                case 113: return "P400 v1.33 p.108 / p.126（範圍記載不一致，本頁採 0–254）";
                case 116: return "P400 v1.33 p.109";
                case 123: case 124: case 125: case 128: return "P400 v1.33 p.110";
                case 131: case 132: case 133: return "P400 v1.33 p.111";
                case 140: case 141: case 142: return "P400 v1.33 p.112";
                case 150: case 151: case 153: return "P400 v1.33 p.113";
                case 158: return "P400 v1.33 p.114 / p.126";
                case 213: case 217: return "P400 v1.33 p.116";
                case 154: return "P400 v1.33 未收錄，需目前韌體說明";
                default: return "未核實";
            }
        }

        public static string Details(int mode, int register)
        {
            var options = Options(mode, register);
            return Description(mode, register) + "\r\n手冊預設：" + ManualDefault(mode, register)
                + (options.Count == 0 ? "" : "\r\n手冊選項：" + string.Join("；", options.Values))
                + "\r\n依據：" + Source(register) + "。預設僅供參考，不會套用；以目前韌體支援及實際讀回為準。";
        }

        public static string Description(int mode, int register)
        {
            bool fh = mode != 0;
            switch (register)
            {
                case 128: return "選擇模組 NB／FH 工作模式；須單獨寫入後重新讀取。400 MHz FH 須原廠選配支援，切換不會自動建立有效頻率表。";
                case 101: return "決定主站／中繼／從站角色；FH 網路主站地址 S105 必須為 1。NB 角色依協定而異。";
                case 102: return "DATA 埠鮑率，代碼 0–14。修改須用 CONFIG 強制 9600/8N1 進入 AT；返回 DATA 後使用新速度，電腦及飛控端須同步設定。";
                case 103: return "無線傳輸速率；同網路必須一致，較高速率通常降低接收靈敏度。NB 另受頻寬／調變限制。";
                case 104: return fh ? "網路識別 ID，範圍 0–4000000000；同網路各模組須一致。" : "網路 ID：FH 模式專用；NB 本頁唯讀。";
                case 105: return fh ? "本機唯一地址 1–65534；主站固定為 1，避免各站重複。" : "NB 本機地址 1–255；適用性依 Transparent／Pacific Crest 協定。";
                case 108: return "天線端發射功率；本頁僅開放 20–30 dBm。請依設備、天線及所在地規範設定，不自動開放原廠高功率選項。";
                case 110: return "DATA 埠資料位元／同位檢查／停止位元，代碼 1–10。修改須用 CONFIG 強制 9600/8N1 進入 AT；返回 DATA 後採用新格式，連接端須相符。";
                case 113: return "封包額外重傳次數 0–254（不含初次傳送）；增加可改善可靠性，但占用頻寬並提高延遲。";
                case 116: return "字元逾時：以 1/4 字元時間為單位；10 代表 2.5 字元時間。與 S111/S112 決定送出時機。採手冊數值框 0–254（內文 0–255 記載不同）。";
                case 123: return "來自主站／中繼的上行平均 RSSI（前 8 次跳頻），手冊範圍 −110 至 −55 dBm。NB 無新訊號約 10 秒後回覆 N/A；狀態值不是設定值。";
                case 124: return "FH 來自從站／中繼的下行平均 RSSI（前 8 次跳頻），手冊範圍 −110 至 −55 dBm。NB 不適用；無訊號可能回覆 N/A。";
                case 142: return "RS-232／RS-485 實體介面；須單獨寫入且接線與硬體支援相符。可能立即失聯；逾時停止，不自動重送或宣稱已保存。";
                case 150: return "FH 主站同步方式：0 一般；1 快速同步等 ACK（僅 PP，配合 S152）；2 快速同步直到 S151 逾時。";
                case 151: return "FH 主站快速同步逾時，100–65000 ms；只在 S150=2 時有效。";
                case 153: return "地址標記：0 關閉；1 在資料前附加 4 bytes 來源地址，可能影響 MAVLink 解析。";
                case 107: return "通訊密碼／Static Mask：最多 16 字元，通訊雙方需相符。NB 用於 Pacific Crest，非 AES Key。點選新值欄輸入，畫面不顯示密碼；留空不修改。此頁接受英數字、底線、句點、連字號。**** 是遮蔽回覆，不能驗證密碼原文；寫入只確認指令與保存回覆，仍須確認配對通訊。";
                case 213: return "FH 中繼器由子節點往父節點的上行封包重試限制，0–254；不同於一般重傳 S113。";
                case 217: return "FH 串列協定：0 透明傳輸；1 MODBUS RTU；2 DF1 全雙工／地址過濾。一般 MAVLink 使用透明傳輸，改變協定可能無法解析。";
                case 154: return "模組畫面稱 Multimaster Mode。P400 v1.33 手冊未列 S154 的定義、選項或預設值；不套用其他機型說明。請用選取參數說明查詢目前韌體，維持唯讀。";
                case 125: return fh ? "NB 占用頻寬：FH 模式不適用。" : "NB 占用頻寬：6.25／12.5／25 kHz。不得超過經銷商表限制，須符合速率與調變組合。";
                case 131: return "NB 主發射頻道，0–63，須選已配置且允許 TX 的頻道，不是 MHz。未知複合回覆保留原文並唯讀。";
                case 132: return "NB 主接收頻道，0–63，須選已配置且允許 RX 的頻道，不是 MHz。未知複合回覆保留原文並唯讀。";
                case 133: return fh ? "FH 網路拓樸；同網路須一致，3 為保留值不可選。" : "FH 網路拓樸：NB 不適用，本頁唯讀。";
                case 140: return fh ? "資料最終目的地址 1–65535；65535 為廣播。PP／PMP 從站通常指向主站 1。" : "目的地址：FH 專用，NB 本頁唯讀。";
                case 141: return fh ? "告知 FH 主站網路是否有中繼器；使用中繼會降低有效吞吐量。" : "NB 有中繼時啟用相關 CSMA 機制；各模組須一致。";
                case 158: return "前向錯誤更正 FEC；增加抗干擾能力但占用頻寬。NB 僅適用部分協定，選項與 FH 不同。";
                default: return "請查閱模組參數說明。";
            }
        }
    }
}
