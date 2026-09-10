using MissionPlanner.Controls;
using MissionPlanner.Joystick;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    /// <summary>
    /// Operator-facing shell for the multi-GCS relay-control arbiter.  The panel is
    /// deliberately fail-closed: no remote station can be granted control until a
    /// future authenticated relay session reports that station as ready.
    /// </summary>
    internal sealed class FmtRelayControlPanel : UserControl
    {
        private static readonly Color Background = Color.FromArgb(13, 29, 37);
        private static readonly Color CardBackground = Color.FromArgb(18, 36, 45);
        private static readonly Color Cyan = Color.FromArgb(46, 174, 220);
        private static readonly Color Green = Color.FromArgb(43, 190, 99);
        private static readonly Color Amber = Color.FromArgb(255, 174, 72);
        private static readonly Color Red = Color.FromArgb(218, 55, 55);
        private static readonly Color Muted = Color.FromArgb(170, 187, 195);

        private readonly Label serviceStatus;
        private readonly Label currentController;
        private readonly Label pendingRequest;
        private readonly ComboBox localStationSelector;
        private readonly Button takeLocalControl;
        private readonly Button revokeControl;
        private readonly Button stopAll;
        private readonly FlowLayoutPanel stationList;
        private readonly TextBox eventLog;
        private readonly Timer statusTimer;
        private readonly TabControl settingsTabs;
        private readonly TabPage forwardingPage;
        private readonly TabPage joystickPage;
        private readonly Panel forwardingHost;
        private readonly Panel joystickHost;
        private readonly List<StationCard> stations = new List<StationCard>();
        private SerialOutputPass forwardingSettings;
        private JoystickSetup joystickSettings;
        private int activeStation;
        private bool updatingLocalStationSelector;

        internal FmtRelayControlPanel()
        {
            Name = "fmtEmbeddedRelayControl";
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BackColor = Background;
            ForeColor = Color.White;
            Font = new Font("Microsoft JhengHei UI", 9F);

            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = CardBackground };
            var title = new Label
            {
                Text = "接力控制  Relay Control",
                AutoSize = true,
                Location = new Point(12, 7),
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                ForeColor = Color.White
            };
            var localStationCaption = new Label
            {
                Text = "本機站號",
                AutoSize = true,
                Location = new Point(245, 10),
                ForeColor = Muted
            };
            localStationSelector = new ComboBox
            {
                Name = "fmtRelayLocalStationSelector",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(310, 6),
                Size = new Size(142, 25),
                FlatStyle = FlatStyle.Flat
            };
            localStationSelector.Items.AddRange(new object[]
            {
                "1 號（主站）",
                "2 號（接力站）",
                "3 號（接力站）",
                "4 號（接力站）",
                "5 號（接力站）"
            });
            serviceStatus = new Label
            {
                Text = "拓樸：飛機 → 1 號主站 → 2–5 號站（4G VPN）｜所有遠端控制封包均阻擋。",
                AutoEllipsis = true,
                Location = new Point(14, 32),
                Size = new Size(880, 20),
                ForeColor = Amber
            };
            var refresh = MakeButton("重新整理", 96, Cyan);
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Click += (sender, args) => RefreshRuntimeStatus();
            header.Controls.Add(title);
            header.Controls.Add(localStationCaption);
            header.Controls.Add(localStationSelector);
            header.Controls.Add(serviceStatus);
            header.Controls.Add(refresh);
            header.Resize += (sender, args) =>
            {
                refresh.Location = new Point(Math.Max(500, header.ClientSize.Width - refresh.Width - 12), 12);
                serviceStatus.Width = Math.Max(240, refresh.Left - serviceStatus.Left - 10);
            };

            var commandBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = Background,
                Padding = new Padding(8, 5, 8, 5)
            };
            var currentCaption = MakeLabel("目前控制站", 108, Muted, true);
            currentCaption.Location = new Point(10, 6);
            currentController = MakeLabel("未指派｜遠端控制已阻擋", 270, Color.White, true);
            currentController.Location = new Point(10, 29);
            var requestCaption = MakeLabel("待處理接管", 108, Muted, true);
            requestCaption.Location = new Point(300, 6);
            pendingRequest = MakeLabel("無", 210, Color.White, true);
            pendingRequest.Location = new Point(300, 29);
            takeLocalControl = MakeButton("收回至 1 號站", 126, Cyan);
            revokeControl = MakeButton("撤銷控制權", 112, Amber);
            stopAll = MakeButton("全部停用", 104, Red);
            takeLocalControl.Anchor = revokeControl.Anchor = stopAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            takeLocalControl.Click += (sender, args) => SetLocalControl();
            revokeControl.Click += (sender, args) => SetActiveStation(0, "已撤銷目前控制權");
            stopAll.Click += (sender, args) => SetActiveStation(0, "已執行全部停用；所有遠端控制封包保持阻擋");
            commandBar.Controls.Add(currentCaption);
            commandBar.Controls.Add(currentController);
            commandBar.Controls.Add(requestCaption);
            commandBar.Controls.Add(pendingRequest);
            commandBar.Controls.Add(takeLocalControl);
            commandBar.Controls.Add(revokeControl);
            commandBar.Controls.Add(stopAll);
            commandBar.Resize += (sender, args) =>
            {
                stopAll.Location = new Point(commandBar.ClientSize.Width - stopAll.Width - 10, 14);
                revokeControl.Location = new Point(stopAll.Left - revokeControl.Width - 8, 14);
                takeLocalControl.Location = new Point(
                    (revokeControl.Visible ? revokeControl.Left : commandBar.ClientSize.Width - 10) -
                    takeLocalControl.Width - 8, 14);
            };

            stationList = new FlowLayoutPanel
            {
                Name = "fmtRelayStations",
                Dock = DockStyle.Top,
                Height = 140,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(7, 4, 7, 4),
                BackColor = Background
            };
            for (var stationNumber = 1; stationNumber <= 5; stationNumber++)
            {
                var card = new StationCard(stationNumber);
                card.GrantRequested += GrantStation;
                stations.Add(card);
                stationList.Controls.Add(card);
            }

            updatingLocalStationSelector = true;
            localStationSelector.SelectedIndex = FmtRelayStationIdentity.StationNumber - 1;
            updatingLocalStationSelector = false;
            localStationSelector.SelectedIndexChanged += LocalStationSelector_SelectedIndexChanged;
            ApplyLocalStationIdentity(false);

            var lower = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(7, 2, 7, 7),
                BackColor = Background
            };
            lower.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 395F));
            lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var safeguards = BuildSafeguardsPanel();
            var logPanel = new Panel { Dock = DockStyle.Fill, BackColor = CardBackground, Margin = new Padding(4) };
            var logTitle = MakeLabel("接力事件紀錄", 180, Color.White, true);
            logTitle.Location = new Point(9, 6);
            eventLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(10, 25, 32),
                ForeColor = Color.FromArgb(205, 215, 220),
                Font = new Font("Consolas", 8.5F),
                Location = new Point(9, 29),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            logPanel.Controls.Add(logTitle);
            logPanel.Controls.Add(eventLog);
            logPanel.Resize += (sender, args) =>
                eventLog.Size = new Size(Math.Max(20, logPanel.ClientSize.Width - 18),
                    Math.Max(20, logPanel.ClientSize.Height - 38));
            lower.Controls.Add(safeguards, 0, 0);
            lower.Controls.Add(logPanel, 1, 0);

            var controlPage = MakeTabPage("站台與控制權");
            controlPage.Controls.Add(lower);
            controlPage.Controls.Add(stationList);
            controlPage.Controls.Add(commandBar);

            forwardingPage = MakeTabPage("MAVLink 轉發");
            forwardingHost = BuildSettingsHost(forwardingPage,
                "設定主站與各分站的 TCP／UDP／序列埠轉發。正式接力使用時請保持「允許回寫」關閉，直到控制權仲裁通道完成驗證。",
                Amber);
            joystickPage = MakeTabPage("本機搖桿設定");
            joystickHost = BuildSettingsHost(joystickPage,
                "每一部導控站需分別完成本機搖桿軸、反向、Expo 與按鍵設定；啟用前請確認搖桿置中及油門安全。",
                Cyan);

            settingsTabs = new TabControl
            {
                Name = "fmtRelaySettingsTabs",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei UI", 9F),
                Padding = new Point(18, 5)
            };
            settingsTabs.TabPages.Add(controlPage);
            settingsTabs.TabPages.Add(forwardingPage);
            settingsTabs.TabPages.Add(joystickPage);
            settingsTabs.SelectedIndexChanged += SettingsTabs_SelectedIndexChanged;

            Controls.Add(settingsTabs);
            Controls.Add(header);

            statusTimer = new Timer { Interval = 1000 };
            statusTimer.Tick += (sender, args) => RefreshRuntimeStatus();
            statusTimer.Start();
            AppendLog("接力控制介面已載入；遠端回寫預設為阻擋。", false);
            if (FmtRelayStationIdentity.AutomaticallyAssigned)
                AppendLog("偵測到同機站號衝突，本次已自動使用 " +
                          FmtRelayStationIdentity.StationNumber + " 號站。", true);
            RefreshRuntimeStatus();
        }

        private void LocalStationSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingLocalStationSelector || localStationSelector.SelectedIndex < 0)
                return;

            var requestedStationNumber = localStationSelector.SelectedIndex + 1;
            string error;
            if (!FmtRelayStationIdentity.TrySetStationNumber(requestedStationNumber, true, out error))
            {
                updatingLocalStationSelector = true;
                localStationSelector.SelectedIndex = FmtRelayStationIdentity.StationNumber - 1;
                updatingLocalStationSelector = false;
                AppendLog(error, true);
                CustomMessageBox.Show(error, "本機站號");
                return;
            }

            ApplyLocalStationIdentity(true);
            RefreshRuntimeStatus();
        }

        private void ApplyLocalStationIdentity(bool writeLog)
        {
            var localStationNumber = FmtRelayStationIdentity.StationNumber;
            var isMainStation = localStationNumber == 1;
            takeLocalControl.Text = isMainStation ? "收回至 1 號站" : "向 1 號站申請控制";
            takeLocalControl.Width = isMainStation ? 126 : 142;
            revokeControl.Visible = isMainStation;
            stopAll.Visible = isMainStation;
            if (!isMainStation && takeLocalControl.Parent != null)
                takeLocalControl.Location = new Point(
                    Math.Max(10, takeLocalControl.Parent.ClientSize.Width - takeLocalControl.Width - 10), 14);
            foreach (var station in stations)
                station.SetLocalStation(station.StationNumber == localStationNumber, isMainStation);

            if (writeLog)
                AppendLog("本機已設定為 " + localStationNumber +
                          (localStationNumber == 1 ? " 號主站。" : " 號接力站。"), false);
        }

        private static TabPage MakeTabPage(string title)
        {
            return new TabPage
            {
                Text = title,
                BackColor = Background,
                ForeColor = Color.White,
                Padding = Padding.Empty,
                UseVisualStyleBackColor = false
            };
        }

        private static Panel BuildSettingsHost(TabPage page, string hint, Color hintColor)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(7, 5, 7, 7),
                BackColor = Background
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var note = new Label
            {
                Text = hint,
                Dock = DockStyle.Fill,
                ForeColor = hintColor,
                BackColor = CardBackground,
                Padding = new Padding(10, 7, 10, 4),
                AutoEllipsis = true
            };
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Background,
                Margin = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(note, 0, 0);
            layout.Controls.Add(host, 0, 1);
            page.Controls.Add(layout);
            return host;
        }

        private void SettingsTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (settingsTabs.SelectedTab == forwardingPage)
                EnsureForwardingSettings();
            else if (settingsTabs.SelectedTab == joystickPage)
                EnsureJoystickSettings();
        }

        private void EnsureForwardingSettings()
        {
            if (forwardingSettings != null && !forwardingSettings.IsDisposed)
                return;

            forwardingSettings = new SerialOutputPass
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
                ShowInTaskbar = false,
                RelayControlMode = true
            };
            forwardingHost.Controls.Add(forwardingSettings);
            forwardingSettings.Show();
            LocalizeForwardingGrid(forwardingSettings);
            ThemeManager.ApplyThemeTo(forwardingSettings);
            forwardingSettings.BringToFront();
            AppendLog("已開啟 MAVLink 轉發設定。", false);
        }

        private static void LocalizeForwardingGrid(Control root)
        {
            foreach (var grid in FindControls<DataGridView>(root))
            {
                SetColumnHeader(grid, "Type", "協定");
                SetColumnHeader(grid, "Direction", "方向");
                SetColumnHeader(grid, "Port", "連接埠");
                SetColumnHeader(grid, "Extra", "目標位址／鮑率");
                SetColumnHeader(grid, "Write", "允許回寫（測試）");
                SetColumnHeader(grid, "Go", "啟動／停止");
                SetColumnHeader(grid, "RuntimeStatus", "狀態");
                SetColumnHeader(grid, "RelayStation", "接力站號");
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                if (grid.Columns.Contains("RuntimeStatus"))
                {
                    grid.Columns["RuntimeStatus"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    grid.Columns["RuntimeStatus"].Width = 52;
                }
            }
        }

        private static void SetColumnHeader(DataGridView grid, string name, string text)
        {
            if (grid.Columns.Contains(name))
                grid.Columns[name].HeaderText = text;
        }

        private void EnsureJoystickSettings()
        {
            if (joystickSettings != null && !joystickSettings.IsDisposed)
                return;

            joystickSettings = new JoystickSetup
            {
                Location = Point.Empty,
                Width = Math.Max(1050, joystickHost.ClientSize.Width - 4),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            joystickHost.Controls.Add(joystickSettings);
            ThemeManager.ApplyThemeTo(joystickSettings);
            joystickSettings.BringToFront();
            AppendLog("已開啟本機搖桿設定；各導控站須分別設定。", false);
        }

        private static IEnumerable<T> FindControls<T>(Control root) where T : Control
        {
            foreach (Control child in root.Controls)
            {
                var match = child as T;
                if (match != null)
                    yield return match;
                foreach (var nested in FindControls<T>(child))
                    yield return nested;
            }
        }

        private Panel BuildSafeguardsPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = CardBackground, Margin = new Padding(4) };
            var title = MakeLabel("安全交接條件", 180, Color.White, true);
            title.Location = new Point(9, 6);
            var neutral = new CheckBox
            {
                Text = "交接前確認搖桿置中與油門安全",
                Checked = true,
                AutoSize = true,
                Location = new Point(10, 31),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            var failClosed = new CheckBox
            {
                Text = "心跳逾時立即撤銷控制權（Fail-closed）",
                Checked = true,
                AutoSize = true,
                Location = new Point(10, 55),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            var leaseLabel = MakeLabel("控制租約", 70, Muted, false);
            leaseLabel.Location = new Point(10, 82);
            var lease = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 30,
                Value = 3,
                Size = new Size(58, 24),
                Location = new Point(82, 80)
            };
            var leaseUnit = MakeLabel("秒", 30, Muted, false);
            leaseUnit.Location = new Point(145, 82);
            var latencyLabel = MakeLabel("延遲上限", 70, Muted, false);
            latencyLabel.Location = new Point(190, 82);
            var latency = new NumericUpDown
            {
                Minimum = 50,
                Maximum = 5000,
                Increment = 50,
                Value = 500,
                Size = new Size(72, 24),
                Location = new Point(262, 80)
            };
            var latencyUnit = MakeLabel("ms", 32, Muted, false);
            latencyUnit.Location = new Point(339, 82);
            panel.Controls.Add(title);
            panel.Controls.Add(neutral);
            panel.Controls.Add(failClosed);
            panel.Controls.Add(leaseLabel);
            panel.Controls.Add(lease);
            panel.Controls.Add(leaseUnit);
            panel.Controls.Add(latencyLabel);
            panel.Controls.Add(latency);
            panel.Controls.Add(latencyUnit);
            return panel;
        }

        internal void RefreshRuntimeStatus()
        {
            var connected = MainV2.comPort?.BaseStream != null && MainV2.comPort.BaseStream.IsOpen;
            var joystickReady = MainV2.joystick != null && MainV2.joystick.enabled;
            var localStationNumber = FmtRelayStationIdentity.StationNumber;
            var isMainStation = localStationNumber == 1;
            FmtRelayControlService.UpdateLocalRuntime(connected, joystickReady);
            activeStation = FmtRelayControlService.ActiveStation;
            for (var index = 0; index < stations.Count; index++)
            {
                var isLocal = index + 1 == localStationNumber;
                if (isLocal)
                {
                    stations[index].SetRuntimeState(connected, joystickReady,
                        connected ? "本機" : "等待飛控", connected ? "< 1 ms" : "-- ms");
                }
                else
                {
                    var peer = FmtRelayControlService.GetPeer(index + 1);
                    var online = peer != null && peer.IsOnline;
                    stations[index].SetRuntimeState(online && peer.FlightLinkConnected,
                        online && peer.JoystickReady,
                        online ? peer.FlightLinkConnected ? "已連線" : "無飛控資料" : "通道未連線",
                        online ? peer.LatencyMilliseconds.ToString() + " ms" : "-- ms");
                }
            }
            for (var index = 0; index < stations.Count; index++)
            {
                FmtGroundStationPosition stationPosition;
                stations[index].SetPositionAvailable(FmtGroundStationPositionStore.TryGet(index + 1,
                    TimeSpan.FromSeconds(60), out stationPosition));
                stations[index].SetActive(stations[index].StationNumber == activeStation);
            }

            currentController.Text = activeStation == 0
                ? isMainStation ? "未指派｜所有遠端控制已阻擋" :
                    FmtRelayControlService.HasFreshMainAuthority ? "1 號站尚未授權本站" : "等待 1 號主站授權"
                : activeStation + " 號站｜由 1 號主站授權";
            currentController.ForeColor = activeStation == 0 ? Color.White : Green;
            var pending = FmtRelayControlService.PendingRequestStation;
            var localRequestPending = FmtRelayControlService.LocalRequestPending;
            pendingRequest.Text = isMainStation
                ? pending > 1 ? "● " + pending + " 號站申請接管" : "無"
                : localRequestPending ? "● 已送出，等待 1 號站核准" :
                    FmtRelayControlService.HasFreshMainAuthority ? "由 1 號主站仲裁" : "主站通道未連線";
            pendingRequest.ForeColor = pending > 1 || localRequestPending ? Color.Gold : Color.White;

            takeLocalControl.Enabled = connected && joystickReady &&
                                       (isMainStation || FmtRelayControlService.HasFreshMainAuthority);
            revokeControl.Enabled = isMainStation && activeStation != 0;
            stopAll.Enabled = isMainStation;
            var coordinationError = FmtRelayControlService.LastError;
            if (!string.IsNullOrEmpty(coordinationError))
            {
                serviceStatus.Text = coordinationError;
                serviceStatus.ForeColor = Red;
            }
            else if (!isMainStation && !FmtRelayControlService.HasFreshMainAuthority)
            {
                serviceStatus.Text = "本機 " + localStationNumber +
                                     " 號接力站｜等待 1 號主站協調心跳；導控輸出已鎖定。";
                serviceStatus.ForeColor = Amber;
            }
            else
            {
                serviceStatus.Text = "本機 " + localStationNumber + " 號" +
                                     (isMainStation ? "主站" : "接力站") +
                                     (connected ? "已接收飛控資料" : "尚未收到飛控資料") +
                                     "｜控制權：" + (activeStation == 0 ? "未指派" : activeStation + " 號站") + "。";
                serviceStatus.ForeColor = connected ? Green : Amber;
            }
            if (isMainStation && !connected && activeStation != 0)
                SetActiveStation(0, "飛控連線中斷，已撤銷控制權");
        }

        private void GrantStation(StationCard card)
        {
            if (FmtRelayStationIdentity.StationNumber != 1)
            {
                AppendLog("只有 1 號主站可以授權控制權。", true);
                return;
            }
            if (!card.IsOnline || !card.IsJoystickReady)
            {
                AppendLog(card.StationName + " 尚未通過連線與搖桿檢查，拒絕授權。", true);
                return;
            }
            SetActiveStation(card.StationNumber, "已授權 " + card.StationName);
        }

        private void SetLocalControl()
        {
            var localStationNumber = FmtRelayStationIdentity.StationNumber;
            if (MainV2.joystick == null || !MainV2.joystick.enabled)
            {
                AppendLog(localStationNumber + " 號站搖桿尚未啟用，無法收回控制。", true);
                return;
            }
            if (localStationNumber == 1)
            {
                SetActiveStation(1, "控制權已收回至 1 號主站");
                return;
            }

            string error;
            if (!FmtRelayControlService.RequestLocalControl(out error))
            {
                AppendLog(error, true);
                CustomMessageBox.Show(error, "接力控制");
                return;
            }
            AppendLog("已向 1 號主站送出 " + localStationNumber + " 號站接管申請。", false);
            pendingRequest.Text = "等待 1 號主站核准";
        }

        private void SetActiveStation(int stationNumber, string logText)
        {
            string error;
            if (!FmtRelayControlService.TrySetActiveStation(stationNumber, out error))
            {
                AppendLog(error, true);
                CustomMessageBox.Show(error, "接力控制");
                return;
            }
            activeStation = FmtRelayControlService.ActiveStation;
            foreach (var station in stations)
                station.SetActive(station.StationNumber == stationNumber);
            currentController.Text = stationNumber == 0
                ? "未指派｜遠端控制已阻擋"
                : stationNumber + " 號站｜控制租約有效";
            currentController.ForeColor = stationNumber == 0 ? Color.White : Green;
            AppendLog(logText, stationNumber == 0);
        }

        private void AppendLog(string text, bool warning)
        {
            if (eventLog == null)
                return;
            var prefix = DateTime.Now.ToString("HH:mm:ss") + (warning ? "  !  " : "  •  ");
            eventLog.AppendText(prefix + text + Environment.NewLine);
        }

        private static Label MakeLabel(string text, int width, Color color, bool bold)
        {
            return new Label
            {
                Text = text,
                Size = new Size(width, 21),
                AutoEllipsis = true,
                ForeColor = color,
                Font = new Font("Microsoft JhengHei UI", 9F, bold ? FontStyle.Bold : FontStyle.Regular)
            };
        }

        private static Button MakeButton(string text, int width, Color color)
        {
            return new Button
            {
                Text = text,
                Size = new Size(width, 30),
                BackColor = color,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                statusTimer?.Stop();
                statusTimer?.Dispose();
                if (joystickSettings != null && !joystickSettings.IsDisposed)
                {
                    joystickSettings.Deactivate();
                    joystickSettings.Dispose();
                }
                if (forwardingSettings != null && !forwardingSettings.IsDisposed)
                    forwardingSettings.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class StationCard : Panel
        {
            private readonly Label state;
            private readonly Label title;
            private readonly Label telemetry;
            private readonly Label joystick;
            private readonly Label position;
            private readonly TextBox vpnAddress;
            private readonly Button grant;
            private bool isLocalStation;
            private bool canGrant;
            private bool updatingVpnAddress;

            internal StationCard(int stationNumber)
            {
                StationNumber = stationNumber;
                StationName = stationNumber + " 號站";
                Size = new Size(222, 122);
                Margin = new Padding(4);
                Padding = new Padding(6);
                BackColor = CardBackground;
                BorderStyle = BorderStyle.FixedSingle;

                title = MakeLabel(StationName + (stationNumber == 1 ? "（主站）" : ""), 112,
                    Color.White, true);
                title.Location = new Point(8, 5);
                state = MakeLabel("● 離線", 96, Amber, false);
                state.Location = new Point(8, 35);
                telemetry = MakeLabel("延遲 -- ms", 92, Muted, false);
                telemetry.Location = new Point(110, 35);
                joystick = MakeLabel("搖桿：未就緒", 120, Muted, false);
                joystick.Location = new Point(8, 58);
                position = MakeLabel("定位：--", 88, Muted, false);
                position.Location = new Point(127, 58);
                var vpnCaption = MakeLabel("VPN", 34, Muted, false);
                vpnCaption.Location = new Point(8, 88);
                vpnAddress = new TextBox
                {
                    Text = Settings.Instance["FMT_RelayStation" + stationNumber + "Address"] ?? "",
                    ReadOnly = false,
                    Size = new Size(171, 24),
                    Location = new Point(43, 84),
                    BackColor = Color.FromArgb(229, 235, 238),
                    ForeColor = Color.FromArgb(20, 30, 35),
                    BorderStyle = BorderStyle.FixedSingle
                };
                vpnAddress.TextChanged += (sender, args) =>
                {
                    if (!updatingVpnAddress && !isLocalStation)
                        Settings.Instance["FMT_RelayStation" + stationNumber + "Address"] =
                            vpnAddress.Text.Trim();
                };
                grant = MakeButton("授權此站", 88, Cyan);
                grant.Size = new Size(88, 27);
                grant.Location = new Point(127, 4);
                grant.Enabled = false;
                grant.Click += (sender, args) => GrantRequested?.Invoke(this);
                Controls.Add(title);
                Controls.Add(state);
                Controls.Add(telemetry);
                Controls.Add(joystick);
                Controls.Add(position);
                Controls.Add(vpnCaption);
                Controls.Add(vpnAddress);
                Controls.Add(grant);
            }

            internal int StationNumber { get; }
            internal string StationName { get; }
            internal bool IsOnline { get; private set; }
            internal bool IsJoystickReady { get; private set; }
            internal event Action<StationCard> GrantRequested;

            internal void SetLocalStation(bool local, bool localIsMainStation)
            {
                isLocalStation = local;
                canGrant = localIsMainStation;
                title.Text = StationName + (StationNumber == 1 ? "（主站）" : "");
                updatingVpnAddress = true;
                vpnAddress.ReadOnly = local;
                vpnAddress.Text = local
                    ? StationNumber == 1 ? "本機主站" : "本機接力站"
                    : Settings.Instance["FMT_RelayStation" + StationNumber + "Address"] ?? "";
                updatingVpnAddress = false;
                grant.Text = local ? "設為本機" : "授權此站";
                grant.Visible = localIsMainStation;
            }

            internal void SetRuntimeState(bool online, bool joystickReady, string statusText, string latency)
            {
                IsOnline = online;
                IsJoystickReady = joystickReady;
                state.Text = "● " + statusText;
                state.ForeColor = online ? Green : Amber;
                telemetry.Text = "延遲 " + latency;
                joystick.Text = joystickReady ? "搖桿：已就緒" : "搖桿：未就緒";
                joystick.ForeColor = joystickReady ? Green : Muted;
                grant.Enabled = canGrant && online && joystickReady;
            }

            internal void SetPositionAvailable(bool available)
            {
                position.Text = available ? "定位：有效" : "定位：--";
                position.ForeColor = available ? Green : Muted;
            }

            internal void SetActive(bool active)
            {
                BorderStyle = BorderStyle.FixedSingle;
                BackColor = active ? Color.FromArgb(20, 65, 72) : CardBackground;
                grant.Text = active ? "控制中" : isLocalStation ? "設為本機" : "授權此站";
                grant.Enabled = canGrant && !active && IsOnline && IsJoystickReady;
            }
        }
    }
}
