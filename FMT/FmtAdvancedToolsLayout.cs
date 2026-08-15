using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace MissionPlanner
{
    public partial class temp
    {
        private static readonly HashSet<string> FmtToolsRequiringVehicleConnection =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "BUT_follow_me", "BUT_outputnmea", "BUT_outputMD", "BUT_outputMavlink",
                "BUT_movingbase", "BUT_swarm", "BUT_followleader", "but_mavserialport",
                "but_remotedflogger", "but_armandtakeoff", "but_gimbaltest",
                "but_optflowcalib", "BUT_forcecal_accel", "BUT_forcecal_mag",
                "but_disablearmswitch", "but_messageinterval", "but_mavinspector",
                "but_blupdate", "but_acbarohight", "but_lockup", "but_logdlscp",
                "but_paramrestore", "but_td", "but_reboot", "BUT_QNH", "but_trimble",
                "but_signkey", "but_gpsinj", "but_proximity", "but_followswarm",
                "but_dfumode"
            };

        private static readonly HashSet<string> FmtToolsRequiringDisarmedVehicle =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "but_armandtakeoff", "but_optflowcalib", "BUT_forcecal_accel",
                "BUT_forcecal_mag", "but_blupdate", "but_acbarohight",
                "but_paramrestore", "but_reboot", "BUT_QNH", "but_dfumode"
            };

        private Timer fmtAdvancedConnectionTimer;
        private ToolTip fmtAdvancedToolTip;

        private static readonly IReadOnlyDictionary<string, string> FmtToolText =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {"BUT_georefimage", "影像地理定位"}, {"button3", "警告管理員"},
                {"BUT_follow_me", "跟隨我"}, {"BUT_outputnmea", "輸出 NMEA"},
                {"BUT_outputMD", "輸出 MicroDrone"}, {"BUT_outputMavlink", "輸出 MAVLink"},
                {"BUT_paramgen", "產生參數資料"}, {"BUT_lang_edit", "語言翻譯編輯器"},
                {"but_osdvideo", "影片疊加 HUD"}, {"BUT_movingbase", "移動基準站"},
                {"BUT_shptopoly", "SHP 轉多邊形"}, {"but_anonlog", "記錄匿名化"},
                {"BUT_swarm", "多機編隊"}, {"BUT_followleader", "跟隨領隊編隊"},
                {"but_mavserialport", "MAVLink 序列埠直通"},
                {"but_remotedflogger", "啟動遠端飛行記錄"},
                {"BUT_sorttlogs", "整理遙測記錄"}, {"but_getfw", "下載全部韌體"},
                {"BUT_geinjection", "匯入自訂地圖"}, {"BUT_clearcustommaps", "清除自訂地圖"},
                {"but_structtest", "結構轉換測試"}, {"but_dashware", "建立 DashWare 資料"},
                {"but_armandtakeoff", "解鎖並起飛"}, {"but_gimbaltest", "雲台指向測試"},
                {"but_maplogs", "產生記錄航跡地圖"}, {"butlogindex", "飛行記錄瀏覽器"},
                {"but_optflowcalib", "光流校正"}, {"but_apjtool", "APJ 韌體工具"},
                {"BUT_magfit2", "磁羅盤記錄校正"}, {"BUT_CoT", "游標目標輸出（CoT）"},
                {"BUT_forcecal_accel", "標記加速度計已校正"},
                {"BUT_forcecal_mag", "標記磁羅盤已校正"},
                {"but_hexmavlink", "MAVLink 十六進位解析"},
                {"but_driverclean", "移除已安裝驅動程式"},
                {"but_disablearmswitch", "切換安全開關"},
                {"but_messageinterval", "設定訊息週期"},
                {"but_mavinspector", "MAVLink 封包檢視器"},
                {"but_blupdate", "更新開機載入程式"}, {"but_3dmap", "3D 地圖測試"},
                {"but_hwids", "解析硬體識別碼"}, {"but_packetbytes", "解析封包位元組"},
                {"but_acbarohight", "調整飛行器氣壓高度"}, {"but_lockup", "飛控鎖死測試"},
                {"but_dem", "地形資料（DEM）"}, {"but_logdlscp", "以 SCP 下載記錄"},
                {"but_sortlogs", "重新整理所有記錄"}, {"but_GDAL", "自訂 GDAL 地圖來源"},
                {"but_sitl_comb", "SITL 串流合併器"}, {"but_paramrestore", "參數復原"},
                {"BUT_fft", "FFT 頻譜分析"}, {"but_td", "擷取執行緒資訊"},
                {"but_reboot", "重新啟動飛控"}, {"BUT_QNH", "QNH 氣壓校正"},
                {"but_trimble", "序列式編隊"}, {"myButton_vlc", "VLC 影像串流"},
                {"but_agemapdata", "清理過期地圖圖磚"},
                {"myButton1", "分割 DataFlash 記錄"},
                {"but_signkey", "MAVLink 2 簽章設定"},
                {"but_gpsinj", "擷取 GPS 注入資料"},
                {"but_proximity", "近接感測器介面"}, {"but_followswarm", "跟隨式編隊"},
                {"but_ManageCMDList", "管理任務命令清單"}, {"but_dfumode", "進入 DFU 模式"}
            };

        private static readonly IReadOnlyDictionary<string, string> FmtToolDescriptions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {"label1", "此功能已移至飛行記錄頁籤。"},
                {"label2", "建立自訂語音警告。"},
                {"label3", "使用 NMEA GPS 位置執行跟隨功能。"},
                {"label4", "以 NMEA 格式輸出飛行器位置。"},
                {"label5", "以 MicroDrone 格式輸出飛行器位置。"},
                {"label6", "轉送 FMTPlanner 收到的 MAVLink 資料流。"},
                {"label7", "重新產生 FMTPlanner 使用的參數說明資料。"},
                {"label8", "編輯各語言的介面翻譯。"},
                {"label9", "將 HUD 畫面疊加至錄製影片。"},
                {"label10", "在地圖顯示目前所在位置的額外圖示。"},
                {"label11", "將 SHP 圖形檔轉換為多邊形檔案。"},
                {"label12", "輸出 Cursor-on-Target（CoT）資料。"},
                {"label13", "多飛行器編隊控制介面。"},
                {"label14", "讓編隊中的飛行器跟隨領隊。"},
                {"label15", "建立連接 GPS 的專用直通通道（連接埠 500）。"},
                {"label16", "設定 MAVLink 2 通訊簽章。"},
                {"label17", "依飛行器類型與系統識別碼整理遙測記錄。"},
                {"label18", "下載目前提供的全部韌體。"},
                {"label19", "將自訂影像加入 FMTPlanner 地圖。"},
                {"label20", "清除自訂地圖影像。"},
                {"label21", "測試資料結構轉換速度。"},
                {"label22", "建立 DashWare 使用的資料檔。"},
                {"label23", "測試多旋翼解鎖並起飛命令。"},
                {"label24", "執行雲台指向演算法測試。"},
                {"label25", "為資料夾內所有遙測記錄建立航跡地圖。"},
                {"label26", "開啟飛行記錄瀏覽器。"},
                {"label27", "從飛行記錄計算磁羅盤偏移值。"},
                {"label28", "管理 FMTPlanner 支援的任務命令清單。"},
                {"label29", "顯示 PX4 光流感測器的影像資料。"},
                {"label30", "移除電腦中已安裝的飛控驅動程式。"},
                {"label31", "模擬按下飛控安全開關。"},
                {"label32", "設定各 MAVLink 訊息的傳送週期。"},
                {"label33", "檢視正在傳輸的所有 MAVLink 封包。"},
                {"label34", "更新飛控的開機載入程式。"},
                {"label35", "測試 3D 地圖顯示功能。"},
                {"label36", "解析並顯示輸入的硬體識別碼。"},
                {"label37", "解析十六進位 MAVLink 封包字串。"},
                {"label38", "修改飛行器氣壓高度的參考值。"},
                {"label39", "故意使飛控停止回應，僅供開發測試。"},
                {"label40", "顯示目前載入的地形高程資料。"},
                {"label41", "透過 SCP／SSH 下載飛行記錄。"},
                {"label42", "重新整理 FMTPlanner 記錄資料夾。"},
                {"label43", "透過 GDAL 載入自訂地圖圖磚來源。"},
                {"label44", "合併 SITL 模擬資料流。"},
                {"label45", "復原參數資料。"},
                {"label46", "分析飛行記錄的振動頻譜。"},
                {"label47", "重新啟動連線中的飛控。"},
                {"label48", "調整當地海平面氣壓（QNH）。"},
                {"label49", "依序控制多機編隊。"},
                {"label50", "透過 VLC 顯示影像串流；建議優先使用 GStreamer。"},
                {"label51", "移除超過 30 天的地圖圖磚。"},
                {"label52", "將 DataFlash 記錄分割成多個檔案。"},
                {"label53", "從遙測記錄擷取 GPS 注入資料。"},
                {"label54", "開啟近接感測器資訊介面。"},
                {"label55", "使用跟隨式多機編隊控制。"},
                {"label56", "讓相容裝置進入韌體更新（DFU）模式。"},
                {"label57", "參數復原後，標記加速度計已完成校正。"},
                {"label58", "參數復原後，標記磁羅盤已完成校正。"}
            };

        private void ApplyFmtAdvancedToolsLayout()
        {
            var traditionalChinese = CultureInfo.CurrentUICulture.Name.StartsWith(
                "zh", StringComparison.OrdinalIgnoreCase);

            if (traditionalChinese)
            {
                Text = "FMTPlanner 進階工具";
                ApplyFmtText(FmtToolText);
                ApplyFmtText(FmtToolDescriptions);
            }

            SuspendLayout();
            tableLayoutPanel1.Parent = null;
            controlSensorsStatus1.Parent = null;

            var toolsScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(8)
            };

            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.Dock = DockStyle.Top;
            tableLayoutPanel1.Margin = Padding.Empty;
            tableLayoutPanel1.Padding = new Padding(0, 0, 8, 8);
            tableLayoutPanel1.ColumnStyles.Clear();
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));

            foreach (Control control in tableLayoutPanel1.Controls)
            {
                control.Margin = new Padding(3, 2, 3, 2);
                if (control is Button)
                {
                    control.Dock = DockStyle.Fill;
                    control.MinimumSize = new Size(0, 23);
                    control.Font = new Font(Font.FontFamily, 8.25F, FontStyle.Regular);
                }
                else if (control is Label)
                {
                    control.AutoSize = true;
                    control.Anchor = AnchorStyles.Left;
                    control.Font = new Font(Font.FontFamily, 8.25F, FontStyle.Regular);
                }
            }

            toolsScrollPanel.Controls.Add(tableLayoutPanel1);

            var split = new SplitContainer
            {
                Name = "fmtAdvancedToolsSplit",
                // Give the splitter a valid working size before applying minimum
                // panel widths. Its WinForms default is only 150 px wide, which
                // makes Panel1MinSize/Panel2MinSize throw during temp construction.
                Size = new Size(Math.Max(900, ClientSize.Width), Math.Max(560, ClientSize.Height)),
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
                Panel1MinSize = 500,
                Panel2MinSize = 370,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(42, 56, 63)
            };

            var availableWidth = Math.Max(900, ClientSize.Width);
            split.SplitterDistance = Math.Max(500, availableWidth - 410);
            split.Panel1.Controls.Add(toolsScrollPanel);
            split.Panel2.Padding = new Padding(10);
            split.Panel2.AutoScroll = false;

            var sensorTitlePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(22, 38, 47),
                Margin = Padding.Empty,
                Padding = new Padding(8, 5, 8, 4)
            };
            sensorTitlePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            sensorTitlePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            sensorTitlePanel.Controls.Add(new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Text = traditionalChinese ? "飛控感測器狀態" : "Autopilot Sensor Status",
                Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            sensorTitlePanel.Controls.Add(new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Text = traditionalChinese
                    ? "綠色：正常　紅色：異常　灰色：停用或未回報"
                    : "Green: OK   Red: Fault   Gray: Disabled or unavailable",
                Font = new Font(Font.FontFamily, 8.25F, FontStyle.Regular),
                ForeColor = Color.Gainsboro,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            var sensorRegion = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(22, 38, 47)
            };
            sensorRegion.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            sensorRegion.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            sensorRegion.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            controlSensorsStatus1.Dock = DockStyle.Fill;
            sensorRegion.Controls.Add(sensorTitlePanel, 0, 0);
            sensorRegion.Controls.Add(controlSensorsStatus1, 0, 1);
            split.Panel2.Controls.Add(sensorRegion);

            AutoScroll = false;
            MinimumSize = new Size(900, 600);
            Controls.Add(split);
            split.BringToFront();
            ConfigureFmtAdvancedToolAvailability();
            ResumeLayout(true);
        }

        private void ConfigureFmtAdvancedToolAvailability()
        {
            fmtAdvancedToolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 300,
                ReshowDelay = 100
            };

            fmtAdvancedConnectionTimer = new Timer { Interval = 500 };
            fmtAdvancedConnectionTimer.Tick += (sender, args) => UpdateFmtAdvancedToolAvailability();
            fmtAdvancedConnectionTimer.Start();
            FormClosed += (sender, args) =>
            {
                fmtAdvancedConnectionTimer.Stop();
                fmtAdvancedConnectionTimer.Dispose();
                fmtAdvancedToolTip.Dispose();
            };
            UpdateFmtAdvancedToolAvailability();
        }

        private void UpdateFmtAdvancedToolAvailability()
        {
            var connected = MainV2.comPort != null && MainV2.comPort.BaseStream != null &&
                            MainV2.comPort.BaseStream.IsOpen;
            var armed = connected && MainV2.comPort.MAV.cs.armed;

            foreach (Control control in tableLayoutPanel1.Controls)
            {
                var button = control as Button;
                if (button == null || !FmtToolsRequiringVehicleConnection.Contains(button.Name))
                    continue;

                var requiresDisarmed = FmtToolsRequiringDisarmedVehicle.Contains(button.Name);
                button.Enabled = connected && (!requiresDisarmed || !armed);

                var reason = !connected
                    ? "此功能需要先連線飛控。"
                    : requiresDisarmed && armed
                        ? "此功能只能在飛行器上鎖（未解鎖）時使用。"
                        : "已符合使用條件。";
                fmtAdvancedToolTip.SetToolTip(button, reason);
            }
        }

        private bool EnsureFmtAdvancedVehicleReady(string action, bool requireParameters,
            bool requireDisarmed)
        {
            if (MainV2.comPort == null || MainV2.comPort.BaseStream == null ||
                !MainV2.comPort.BaseStream.IsOpen)
            {
                CustomMessageBox.Show("請先連線飛控，再執行「" + action + "」。",
                    "FMTPlanner 進階工具", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (requireDisarmed && MainV2.comPort.MAV.cs.armed)
            {
                CustomMessageBox.Show("「" + action + "」只能在飛行器上鎖（未解鎖）時執行。",
                    "FMTPlanner 進階工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (requireParameters && (MainV2.comPort.MAV.param == null ||
                                      MainV2.comPort.MAV.param.Count == 0))
            {
                CustomMessageBox.Show("尚未取得飛控參數，請等待參數下載完成後再試。",
                    "FMTPlanner 進階工具", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private void ApplyFmtText(IReadOnlyDictionary<string, string> values)
        {
            foreach (Control control in tableLayoutPanel1.Controls)
            {
                string text;
                if (!string.IsNullOrEmpty(control.Name) && values.TryGetValue(control.Name, out text))
                    control.Text = text;
            }
        }
    }
}
