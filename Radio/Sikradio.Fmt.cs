using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Comms;

namespace MissionPlanner.Radio
{
    public partial class Sikradio
    {
        // Optional host-owned connection: the standalone SiK program keeps its
        // original behavior, while the MP settings page never takes MP's port.
        public Func<ICommsSerial> EmbeddedPortProvider { get; set; }
        public Action EmbeddedPortRelease { get; set; }
        public bool EmbeddedOperationActive { get; private set; }
        public event Action EmbeddedOperationChanged;
        private bool disconnectWhenIdle;
        private bool fmtChinese;

        private void ExecuteEmbeddedOperation(Action operation)
        {
            if (EmbeddedOperationActive) return;
            EmbeddedOperationActive = true;
            EmbeddedOperationChanged?.Invoke();
            try { operation(); }
            finally
            {
                EmbeddedOperationActive = false;
                if (disconnectWhenIdle)
                {
                    disconnectWhenIdle = false;
                    try { Disconnect(); } catch { EmbeddedPortRelease?.Invoke(); }
                }
                EmbeddedOperationChanged?.Invoke();
            }
        }

        public void RequestEmbeddedDisconnect()
        {
            // Legacy operations pump UI messages. Leaving the page must not close
            // the serial port halfway through an EEPROM or firmware write.
            if (EmbeddedOperationActive) { disconnectWhenIdle = true; return; }
            Disconnect();
        }

        public void ClearEmbeddedSettings()
        {
            _LocalSettings = null;
            _RemoteSettings = null;
            if (!IsDisposed) EnableConfigControls(true, false);
        }

        private void ConfigureFmtChinese()
        {
            fmtChinese = true;
            SetFmtCaption("groupBoxLocal", "本機數傳（電腦端）");
            SetFmtCaption("groupBoxRemote", "遠端數傳（飛機端）");
            SetFmtCaption("BUT_getcurrent", "讀取設定");
            SetFmtCaption("BUT_savesettings", "寫入設定");
            SetFmtCaption("BUT_resettodefault", "恢復預設");
            SetFmtCaption("BUT_upload", "標準韌體更新");
            SetFmtCaption("BUT_loadcustom", "自訂韌體更新");
            SetFmtCaption("BUT_Syncoptions", "複製必要參數\r\n至遠端");
            SetFmtCaption("BUT_SetPPMFailSafe,BUT_SetPPMFailSafeRemote", "PPM 失控保護");
            SetFmtCaption("btnSaveToFile,btnRemoteSaveToFile", "匯出設定檔…");
            SetFmtCaption("btnLoadFromFile,btnRemoteLoadFromFile", "匯入設定檔…");
            SetFmtCaption("linkLabel1", "狀態燈說明");
            SetFmtCaption("linkLabel_mavlink", "一般 MAVLink 設定");
            SetFmtCaption("linkLabel_lowlatency", "低延遲設定");
            SetFmtCaption("label11,label9", "韌體版本");
            SetFmtCaption("label12", "訊號 RSSI");
            SetFmtCaption("label1,label32", "序列鮑率");
            SetFmtCaption("label2,label31", "格式版本");
            SetFmtCaption("label3,label30", "空中速率");
            SetFmtCaption("lblNETID,lblRNETID", "網路編號");
            SetFmtCaption("lblTXPOWER,lblRTXPOWER", "發射功率");
            SetFmtCaption("lblECC,lblRECC", "錯誤更正");
            SetFmtCaption("lblOPPRESEND,lblROPPRESEND", "機會重送");
            SetFmtCaption("lblMAVLINK,lblRMAVLINK", "MAVLink 模式");
            SetFmtCaption("lblANT_MODE,lblRANT_MODE", "天線模式");
            SetFmtCaption("lblSER_BRK_DETMS,lblRSER_BRK_DETMS", "中斷偵測");
            SetFmtCaption("lblGLOBAL_RETRIES,lblRGLOBAL_RETRIES", "全域重試");
            SetFmtCaption("lblMAX_RETRIES,lblRMAX_RETRIES", "重試上限");
            SetFmtCaption("lblMAX_DATA,lblRMAX_DATA", "資料上限");
            SetFmtCaption("lblENCRYPTION_LEVEL,lblRENCRYPTION_LEVEL", "AES 加密");
            SetFmtCaption("label35,label38", "AES 金鑰");
            SetFmtCaption("btnRandom", "隨機產生");
            SetFmtCaption("lblRTSCTS,lblRRTSCTS", "硬體流控");
            SetFmtCaption("lblMAX_WINDOW,lblRMAX_WINDOW", "視窗上限 ms");
            SetFmtCaption("lblMIN_FREQ,lblRMIN_FREQ", "最低頻率");
            SetFmtCaption("lblMAX_FREQ,lblRMAX_FREQ", "最高頻率");
            SetFmtCaption("lblNUM_CHANNELS,lblRNUM_CHANNELS", "跳頻通道數");
            SetFmtCaption("lblDUTY_CYCLE,lblRDUTY_CYCLE", "發射占空比");
            SetFmtCaption("lblLBT_RSSI,lblRLBT_RSSI", "先聽後送門檻");
            SetFmtCaption("lblNODEID,lblRNODEID", "節點編號");
            SetFmtCaption("lblDESTID,lblRDESTID", "目標編號");
            SetFmtCaption("lblTX_ENCAP_METHOD,lblRTX_ENCAP_METHOD", "傳送封裝");
            SetFmtCaption("lblRX_ENCAP_METHOD,lblRRX_ENCAP_METHOD", "接收封裝");
            SetFmtCaption("label45,label46", "速率／頻段");
            SetFmtCaption("label49,label50", "國家／地區");
            SetFmtCaption("label54,lblRFSFRAMELOSS", "失控遺失幀數");
            SetFmtCaption("lblGPI1_1R_CIN,lblRGPI1_1R_CIN", "PPM 輸入");
            SetFmtCaption("lblGPO1_1R_COUT,lblRGPO1_1R_COUT", "PPM 輸出");
            SetFmtCaption("lblSBUSIN,lblRSBUSIN", "SBUS 輸入");
            SetFmtCaption("lblSBUSOUT,lblRSBUSOUT", "SBUS 輸出");
            SetFmtCaption("lblGPO1_3AUXOUT,lblRGPO1_3AUXOUT", "輔助輸出");
            SetFmtCaption("lblGPI1_2AUXIN,lblRGPI1_2AUXIN", "輔助輸入");
            SetFmtCaption("lblGPIO1_1FUNC,lblRGPIO1_1FUNC", "GPIO 功能");
            SetFmtCaption("lblGPO1_0TXEN485,lblRGPO1_0TXEN485", "RS485 發送");
            SetFmtCaption("lblGPO1_3STATLED,lblRGPO1_3STATLED", "狀態燈輸出");
            SetFmtToolTip("SERIAL_SPEED,RSERIAL_SPEED", "數傳序列埠鮑率，57 代表 57600 bps；須與所接設備一致。");
            SetFmtToolTip("AIR_SPEED,RAIR_SPEED", "兩端數傳之間的空中資料速率（kbps），不是序列埠鮑率。兩端須相容。");
            SetFmtToolTip("NETID,RNETID", "數傳配對的網路編號；本機與遠端須一致，避免與附近其他設備混用。");
            SetFmtToolTip("TXPOWER,RTXPOWER", "發射功率（dBm）。請依設備規格與所在地允許範圍設定。");
            SetFmtToolTip("ECC,RECC", "啟用 Golay 錯誤更正；增加傳輸負擔，可改善雜訊下的可靠度。");
            SetFmtToolTip("OPPRESEND,ROPPRESEND", "通道有餘裕時重送資料，提高封包送達機率。");
            SetFmtToolTip("MIN_FREQ,RMIN_FREQ,MAX_FREQ,RMAX_FREQ", "跳頻頻率範圍，單位 kHz。兩端須相容，並符合所在地規定。");
            SetFmtToolTip("NUM_CHANNELS,RNUM_CHANNELS", "跳頻使用的通道數。");
            SetFmtToolTip("DUTY_CYCLE,RDUTY_CYCLE", "允許發送的時間百分比。");
            SetFmtToolTip("LBT_RSSI,RLBT_RSSI", "先聽後送的訊號門檻，用於判斷通道是否忙碌。");
            SetFmtToolTip("RTSCTS,RRTSCTS", "啟用 RTS/CTS 硬體流量控制；接線及對端必須支援。");
            SetFmtToolTip("GPI1_1R_CIN,RGPI1_1R_CIN", "將對應 GPIO 設為 PPM 輸入。");
            SetFmtToolTip("GPO1_1R_COUT,RGPO1_1R_COUT", "將對應 GPIO 設為 PPM 輸出。");
            SetFmtToolTip("ENCRYPTION_LEVEL,RENCRYPTION_LEVEL,AESKEY,RAESKEY", "AES 加密等級與金鑰；配對兩端必須相符。匯出設定檔請視為敏感資料。");
            SetFmtToolTip("FSFRAMELOSS,RFSFRAMELOSS", "SBUS／PPM 遺失幀數達此門檻時進入失控保護。");
            SetFmtToolTip("BUT_SetPPMFailSafe,BUT_SetPPMFailSafeRemote", "記錄目前 PPM 訊號作為失控保護；請先確認各通道為安全值。");
            SetFmtToolTip("RSSI", "訊號品質與錯誤統計。txe/rxe：收發錯誤；stx/rrx：序列溢位；ecc：已更正資料。");
            SetFmtToolTip("linkLabel1", "綠燈閃爍：搜尋；綠燈恆亮：已配對；紅燈閃爍：傳送；紅燈恆亮：韌體更新模式。");
            _KnownNameDescriptions["RSSI_IN_DBM"] = "RSSI（dBm）";
            _KnownNameDescriptions["AUXSER_SPEED"] = "輔助序列鮑率";
            _KnownNameDescriptions["AIR_FRAMELEN"] = "空中封包長度";
            // Translate captions only. Never translate firmware enum values or
            // ComboBox.Text: the legacy parser uses their raw protocol values.
            lbl_status.TextChanged += (sender, args) =>
            {
                var translated = TranslateFmtText(lbl_status.Text);
                if (translated != lbl_status.Text) lbl_status.Text = translated;
            };
            lbl_status.Text = "先讀取設定；修改配對參數後，請核對本機與遠端設定，再按「寫入設定」。";
            dlgSave.Title = "匯出數傳設定";
            dlgOpen.Title = "匯入數傳設定（不會立即寫入設備）";
            ConfigureFmtLayout();
        }

        private void ConfigureFmtLayout()
        {
            SuspendLayout();
            foreach (var group in new[] { groupBoxLocal, groupBoxRemote })
            {
                group.SuspendLayout();
                bool local = group == groupBoxLocal;
                int secondColumn = local ? 180 : 168;
                int firstEditor = local ? 96 : 88;
                // Leave room for Chinese MAVLink captions without narrowing value fields.
                // Keep all controls directly in their original group: firmware lookup and
                // per-parameter enable/disable logic depend on this ownership.
                foreach (Control item in group.Controls)
                {
                    if (item.Top >= 82)
                    {
                        if (item.Left >= secondColumn) item.Left += 20;
                        else if (!(item is Label) && item.Left >= firstEditor) item.Left += 12;
                    }
                    // Firmware fields must start below, not through, the group title.
                    item.Top += 16;
                    // Footer buttons otherwise touch the taller Chinese GPIO caption above.
                    if (item is Button && item.Top >= 444) item.Top += 10;
                    if (item is Label) item.Paint += PaintFmtDisabledCaption;
                }
                group.Width += 20;
                group.Height += 26;
                group.Paint += PaintFmtDisabledCaption;
                group.ResumeLayout(false);
            }
            groupBoxRemote.Left = groupBoxLocal.Right + 8;
            Width = groupBoxRemote.Right + 12;

            // Firmware and RSSI labels were sized for short English captions.
            SetFmtFieldStart(ATI, Math.Max(80, label11.Right + 8));
            SetFmtFieldStart(RTI, Math.Max(80, label9.Right + 8));
            SetFmtFieldStart(RSSI, Math.Max(92, label12.Right + 8));

            // The original 13px separation stacked two 19px-high translated links.
            linkLabel_mavlink.Top = AESKEY.Bottom + 7;
            linkLabel_lowlatency.Location = new Point(linkLabel_mavlink.Right + 12, linkLabel_mavlink.Top);
            label54.Top = FSFRAMELOSS.Top - Math.Max(19, label54.PreferredSize.Height) - 5;
            lblRFSFRAMELOSS.Top = RFSFRAMELOSS.Top - Math.Max(19, lblRFSFRAMELOSS.PreferredSize.Height) - 5;

            // Reserve independent space for the full instruction and two-line copy button.
            BUT_Syncoptions.Size = new Size(128, 46);
            BUT_Syncoptions.Location = new Point(Width - BUT_Syncoptions.Width - 12, groupBoxLocal.Bottom + 10);
            lbl_status.AutoSize = false;
            lbl_status.Location = new Point(12, BUT_Syncoptions.Top);
            lbl_status.Size = new Size(BUT_Syncoptions.Left - 24, BUT_Syncoptions.Height);
            Progressbar.Location = new Point(12, BUT_Syncoptions.Bottom + 10);
            Progressbar.Width = Width - 24;
            Height = Progressbar.Bottom + 12;
            ResumeLayout(false);
        }

        private static void SetFmtFieldStart(Control field, int left)
        {
            int right = field.Right;
            field.Left = left;
            field.Width = Math.Max(80, right - left);
        }

        // WinForms' embossed disabled text is almost illegible on the dark theme.
        // Repaint captions only; leave Enabled and every input's safety gate intact.
        private static void PaintFmtDisabledCaption(object sender, PaintEventArgs e)
        {
            var control = (Control)sender;
            if (control.Enabled || string.IsNullOrEmpty(control.Text)) return;
            Color background = control.BackColor;
            if (background.A < 255) background = control.Parent.BackColor;
            Color foreground = background.GetBrightness() < 0.5f ? Color.Gainsboro : Color.FromArgb(65, 65, 65);
            Rectangle bounds = control.ClientRectangle;
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
            if (control is GroupBox)
            {
                var size = TextRenderer.MeasureText(control.Text, control.Font);
                bounds = new Rectangle(8, 0, size.Width, size.Height);
                flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            }
            using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, bounds);
            TextRenderer.DrawText(e.Graphics, control.Text, control.Font, bounds, foreground, flags);
        }

        private void SetFmtCaption(string names, string caption)
        {
            foreach (string name in names.Split(','))
                foreach (Control control in Controls.Find(name, true)) control.Text = caption;
        }

        private void SetFmtToolTip(string names, string caption)
        {
            foreach (string name in names.Split(','))
                foreach (Control control in Controls.Find(name, true)) toolTip1.SetToolTip(control, caption);
        }

        private bool ConfirmEmbeddedWrite(string action, string detail)
        {
            if (EmbeddedPortProvider == null) return true;
            return MessageBox.Show(this, detail + "\r\n\r\n僅於地面安全狀態操作。確定要「" + action + "」嗎？",
                "數傳設定確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private bool ValidateEmbeddedKey(ComboBox level, TextBox key, string side)
        {
            if (!GetIsEncryptionEnabled(level)) return true;
            int maximum = GetEncryptionMaxKeyLength(level);
            var text = key.Text.Trim();
            if (text.Length > 0 && text.Length <= maximum &&
                System.Text.RegularExpressions.Regex.IsMatch(text, "\\A[0-9a-fA-F]+\\z")) return true;
            ShowRadioMessage(side + " AES 金鑰無效；須為 1～" + maximum + " 個十六進位字元。尚未寫入設備。");
            return false;
        }

        private void ShowRadioMessage(string message, string title = null)
        {
            MsgBox.CustomMessageBox.Show(TranslateFmtText(message), fmtChinese ? "數傳設定" : title ?? "SiK Radio");
        }

        private static readonly Dictionary<string, string> FmtMessages = new Dictionary<string, string>
        {
            { "Failed to enter command mode.  Try power-cycling modem.", "無法進入指令模式，請確認序列埠／鮑率，必要時將數傳重新上電。" },
            { "Failed to enter command mode", "無法進入指令模式，請確認序列埠、鮑率及數傳接線。" },
            { "Couldn't communicate with modem.  Try power-cycling modem.", "無法與數傳通訊，請檢查接線、鮑率或重新上電。" },
            { "The ranges and options shown for the remote modem may not be accurate.  To ensure accurate, use the same firmware version in both the local and remote modems", "遠端數傳的參數範圍／選項可能不準確；請確認本機與遠端使用相同韌體版本。" },
            { "Please read settings first.", "請先讀取數傳設定，再進行寫入或設定檔操作。" },
            { "Failed to reset parameters to factory defaults", "恢復原廠預設失敗" },
            { "Failed to write parameters to EEPROM", "寫入 EEPROM 失敗" },
            { "Failed to save parameters", "寫入參數失敗" },
            { "Set Command error", "設定指令失敗" },
            { "Invalid ComPort or in use", "序列埠無效或已被其他程式占用" },
            { "Encryption key not valid hex number <= ", "加密金鑰須為十六進位，長度不可超過 " },
            { " hex numerals", " 個十六進位字元" },
            { "Error during read ", "讀取設定時發生錯誤：" },
            { "Error copying file", "處理韌體檔案時發生錯誤" },
            { "Saved settings to ", "已匯出設定至：" },
            { "Failed to save settings to ", "無法匯出設定至：" },
            { "Failed to load settings from ", "無法載入設定檔：" },
            { "Loaded\n", "已載入下列設定（尚未寫入設備）：\n" },
            { "Beta set to ", "測試版韌體模式：" },
            { "Done.  Some settings in modem were invalid.", "讀取完成，但數傳內部分設定無效，請檢查。" },
            { "Doing Command", "正在執行指令" },
            { "Connecting", "正在連線" },
            { "Done", "完成" }, { "Fail", "失敗" }, { "Error", "錯誤" }, { "Reset", "已重設" },
            { "Determining mode...", "正在判斷數傳模式…" }, { "Mode is ", "目前模式：" },
            { "Unknown modem", "無法識別數傳型號" },
            { "Asking user for firmware file", "請選擇韌體檔案" },
            { "Getting firmware from internet", "正在下載韌體" },
            { "Programming firmware into device", "正在寫入韌體，請勿斷電" },
            { "Programmed firmware into device", "韌體寫入完成" },
            { "Programming failed.  (Try again?)", "韌體寫入失敗，請檢查後再試" },
            { "Firmware file selection cancelled", "已取消選擇韌體檔案" }
        };

        private string TranslateFmtText(string text)
        {
            if (!fmtChinese || string.IsNullOrEmpty(text)) return text;
            if (text.StartsWith("The Sik Radios have 2 status LEDs", StringComparison.Ordinal))
                return "SiK 數傳有紅、綠兩個狀態燈：\r\n綠燈閃爍：搜尋另一端數傳\r\n綠燈恆亮：兩端已連線\r\n紅燈閃爍：傳送資料\r\n紅燈恆亮：韌體更新模式";
            foreach (var pair in FmtMessages.OrderByDescending(p => p.Key.Length))
                if (text.StartsWith(pair.Key, StringComparison.Ordinal))
                    return pair.Value + text.Substring(pair.Key.Length).Replace(" hex numerals", " 個十六進位字元");
            return text; // Preserve device-supplied diagnostics and protocol tokens.
        }
    }
}
