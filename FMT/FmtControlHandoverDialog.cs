using MissionPlanner.Joystick;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtCriticalRcSwitch
    {
        internal int Channel { get; set; }
        internal int Option { get; set; }
        internal string FunctionName { get; set; }
        internal int InitialPwm { get; set; }
        internal string InitialPosition { get; set; }
    }

    /// <summary>
    /// Performs the GCS side of a bumpless receiver-to-joystick handover.
    /// No control packets are emitted by this form; it only previews the calibrated
    /// joystick PWM and compares it with the active RC input reported by the vehicle.
    /// </summary>
    internal sealed class FmtControlHandoverDialog : Form
    {
        private const int RequiredStableMilliseconds = 800;
        private const double RcTelemetryTimeoutSeconds = 1.5;

        private readonly MAVLinkInterface port;
        private readonly JoystickBase joystick;
        private readonly List<ChannelState> channels;
        private readonly DataGridView valuesGrid;
        private readonly Label statusLabel;
        private readonly ProgressBar stableProgress;
        private readonly Button acceptButton;
        private readonly Timer updateTimer;
        private DateTime? stableSinceUtc;

        internal List<FmtCriticalRcSwitch> CriticalSwitches { get; }

        internal FmtControlHandoverDialog(MAVLinkInterface port, JoystickBase joystick)
        {
            this.port = port ?? throw new ArgumentNullException(nameof(port));
            this.joystick = joystick ?? throw new ArgumentNullException(nameof(joystick));
            CriticalSwitches = GetConfiguredCriticalSwitches(port);
            channels = BuildChannelList();

            Text = "導控搖桿對位檢查";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(760, Math.Min(650, Math.Max(380, 250 + channels.Count * 28)));

            var explanation = new Label
            {
                Dock = DockStyle.Top,
                Height = 54,
                Padding = new Padding(12, 9, 12, 3),
                Text = "請移動導控搖桿，使待輸出的 PWM 與目前實體遙控器一致。\r\n" +
                       "操縱軸必須對位；安全開關改由導控按鈕管理，不要求重複映射到電腦搖桿。全部穩定 0.8 秒後才可切換。",
                TextAlign = ContentAlignment.MiddleLeft
            };

            valuesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(18, 34, 43),
                BorderStyle = BorderStyle.FixedSingle,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 30,
                RowTemplate = { Height = 27 }
            };
            valuesGrid.Columns.Add("Control", "控制通道");
            valuesGrid.Columns.Add("Receiver", "目前遙控器 PWM");
            valuesGrid.Columns.Add("Joystick", "導控預覽／接管方式");
            valuesGrid.Columns.Add("Difference", "差值");
            valuesGrid.Columns.Add("State", "對位提示");
            valuesGrid.Columns[0].FillWeight = 105;
            valuesGrid.Columns[1].FillWeight = 105;
            valuesGrid.Columns[2].FillWeight = 105;
            valuesGrid.Columns[3].FillWeight = 65;
            valuesGrid.Columns[4].FillWeight = 150;

            foreach (var channel in channels)
            {
                channel.RowIndex = valuesGrid.Rows.Add(channel.DisplayName, "--", "--", "--", "等待資料");
            }

            statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(8, 0, 8, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "等待 RC_CHANNELS 與搖桿資料…"
            };
            stableProgress = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 10,
                Minimum = 0,
                Maximum = RequiredStableMilliseconds
            };
            acceptButton = new Button
            {
                Text = "對位完成，切換控制",
                DialogResult = DialogResult.OK,
                Enabled = false,
                AutoSize = true,
                Padding = new Padding(10, 3, 10, 3),
                Margin = new Padding(6)
            };
            var cancelButton = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                Padding = new Padding(10, 3, 10, 3),
                Margin = new Padding(6)
            };
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(4)
            };
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(acceptButton);

            var footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 84, Padding = new Padding(8, 4, 8, 2) };
            footerPanel.Controls.Add(stableProgress);
            footerPanel.Controls.Add(statusLabel);
            Controls.Add(valuesGrid);
            Controls.Add(footerPanel);
            Controls.Add(buttonPanel);
            Controls.Add(explanation);
            AcceptButton = acceptButton;
            CancelButton = cancelButton;

            updateTimer = new Timer { Interval = 100 };
            updateTimer.Tick += UpdateTimer_Tick;
            Shown += (sender, args) => updateTimer.Start();
            FormClosed += (sender, args) => updateTimer.Dispose();
            ThemeManager.ApplyThemeTo(this);
        }

        private List<ChannelState> BuildChannelList()
        {
            var roleByChannel = new Dictionary<int, string>();
            AddMappedRole(roleByChannel, "RCMAP_ROLL", 1, "橫滾");
            AddMappedRole(roleByChannel, "RCMAP_PITCH", 2, "俯仰");
            AddMappedRole(roleByChannel, "RCMAP_THROTTLE", 3, "油門／總距");
            AddMappedRole(roleByChannel, "RCMAP_YAW", 4, "偏航");

            var result = new List<ChannelState>();
            for (var channel = 1; channel <= 18; channel++)
            {
                var usesJoystick = joystick.getJoystickAxis(channel) != joystickaxis.None;
                var safetySwitch = CriticalSwitches.FirstOrDefault(item => item.Channel == channel);
                if (!usesJoystick && safetySwitch == null)
                    continue;

                string role;
                if (!roleByChannel.TryGetValue(channel, out role))
                    role = "輔助";

                var isCritical = safetySwitch != null;
                if (isCritical)
                    role = "安全：" + safetySwitch.FunctionName;

                result.Add(new ChannelState
                {
                    Channel = channel,
                    DisplayName = role + "  RC" + channel,
                    Tolerance = GetTolerance(channel, role == "油門／總距"),
                    UsesJoystick = usesJoystick,
                    SafetySwitch = safetySwitch
                });
            }

            return result;
        }

        private void AddMappedRole(IDictionary<int, string> roles, string parameterName, int fallback, string role)
        {
            var channel = fallback;
            try
            {
                if (port.MAV.param.ContainsKey(parameterName))
                    channel = (int)Math.Round(port.MAV.param[parameterName].Value);
            }
            catch
            {
                channel = fallback;
            }

            if (channel >= 1 && channel <= 18)
                roles[channel] = role;
        }

        private int GetTolerance(int channel, bool throttle)
        {
            var tolerance = throttle ? 25 : 30;
            try
            {
                var name = "RC" + channel + "_DZ";
                if (port.MAV.param.ContainsKey(name))
                    tolerance = Math.Max(tolerance, (int)Math.Round(port.MAV.param[name].Value));
            }
            catch
            {
                // Use the conservative default if the parameter is unavailable.
            }

            return Math.Min(60, tolerance);
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (!channels.Any(channel => channel.UsesJoystick))
            {
                SetUnavailable("搖桿尚未配置任何 RC 通道，請先完成本機搖桿設定。");
                return;
            }

            if (port.BaseStream == null || !port.BaseStream.IsOpen)
            {
                SetUnavailable("MAVLink 已中斷，已取消對位。");
                return;
            }

            if (!IsRcTelemetryFresh())
            {
                SetUnavailable("RC_CHANNELS 資料逾時，請確認遙控器與遙測連線。");
                return;
            }

            var allMatched = true;
            foreach (var channel in channels)
            {
                var receiver = GetReceiverPwm(channel.Channel);
                short preview = 0;
                if (channel.UsesJoystick)
                {
                    try
                    {
                        preview = joystick.getValueForChannel(channel.Channel);
                    }
                    catch
                    {
                        preview = 0;
                    }
                }

                var receiverValid = receiver >= 800 && receiver <= 2200;
                var previewValid = !channel.UsesJoystick || (preview >= 800 && preview <= 2200);
                var valid = receiverValid && previewValid;
                var difference = valid ? preview - receiver : 0;
                var matched = valid && (!channel.UsesJoystick ||
                    (channel.SafetySwitch != null
                        ? GetSwitchPosition(receiver) == GetSwitchPosition(preview)
                        : Math.Abs(difference) <= channel.Tolerance));
                allMatched &= matched;

                if (channel.SafetySwitch != null && receiverValid)
                {
                    channel.SafetySwitch.InitialPwm = receiver;
                    channel.SafetySwitch.InitialPosition = GetSwitchPosition(receiver);
                }

                var row = valuesGrid.Rows[channel.RowIndex];
                row.Cells[1].Value = receiverValid ? receiver + "（" + GetSwitchPosition(receiver) + "）" : "--";
                row.Cells[2].Value = channel.UsesJoystick
                    ? (previewValid ? preview.ToString() : "--")
                    : "導控安全按鈕";
                row.Cells[3].Value = channel.UsesJoystick && valid
                    ? (difference > 0 ? "+" : "") + difference
                    : "--";
                row.Cells[4].Value = GetChannelStatus(channel, valid, matched, difference, receiver);
                row.DefaultCellStyle.ForeColor = matched ? Color.FromArgb(80, 220, 125) : Color.Gold;
            }

            if (!allMatched)
            {
                stableSinceUtc = null;
                stableProgress.Value = 0;
                acceptButton.Enabled = false;
                statusLabel.ForeColor = Color.Gold;
                statusLabel.Text = "尚未對位：請依各通道提示調整導控搖桿。";
                return;
            }

            if (!stableSinceUtc.HasValue)
                stableSinceUtc = DateTime.UtcNow;

            var elapsed = (int)Math.Max(0, (DateTime.UtcNow - stableSinceUtc.Value).TotalMilliseconds);
            stableProgress.Value = Math.Min(RequiredStableMilliseconds, elapsed);
            acceptButton.Enabled = elapsed >= RequiredStableMilliseconds;
            statusLabel.ForeColor = acceptButton.Enabled ? Color.FromArgb(80, 220, 125) : Color.LightSkyBlue;
            statusLabel.Text = acceptButton.Enabled
                ? "對位完成，可以切換為導控控制。"
                : "位置一致，正在確認穩定性…";
        }

        private static string GetChannelStatus(ChannelState channel, bool valid, bool matched, int difference,
            int receiver)
        {
            if (!valid)
                return "等待有效資料";
            if (channel.SafetySwitch != null && !channel.UsesJoystick)
                return "已記錄安全狀態：" + GetSwitchPosition(receiver);
            if (channel.SafetySwitch != null)
                return matched ? "安全開關位置一致" : "安全開關位置不同";
            if (matched)
                return "一致（±" + channel.Tolerance + "）";
            return difference < 0 ? "提高導控搖桿" : "降低導控搖桿";
        }

        private static string GetSwitchPosition(int pwm)
        {
            // ArduPilot AuxSwitchPos thresholds: LOW < 1200, HIGH > 1800.
            if (pwm < 1200)
                return "低";
            if (pwm > 1800)
                return "高";
            return "中";
        }

        internal static List<FmtCriticalRcSwitch> GetConfiguredCriticalSwitches(MAVLinkInterface port)
        {
            var result = new List<FmtCriticalRcSwitch>();
            if (port?.MAV?.param == null)
                return result;

            for (var channel = 1; channel <= 18; channel++)
            {
                var parameterName = "RC" + channel + "_OPTION";
                if (!port.MAV.param.ContainsKey(parameterName))
                    continue;

                var option = (int)Math.Round(port.MAV.param[parameterName].Value);
                string functionName;
                if (!TryGetCriticalFunctionName(option, out functionName))
                    continue;

                result.Add(new FmtCriticalRcSwitch
                {
                    Channel = channel,
                    Option = option,
                    FunctionName = functionName,
                    InitialPosition = "未知"
                });
            }

            return result;
        }

        private static bool TryGetCriticalFunctionName(int option, out string name)
        {
            switch (option)
            {
                case 21: name = "降落傘啟用"; return true;
                case 22: name = "降落傘釋放"; return true;
                case 23: name = "降落傘三段開關"; return true;
                case 31: name = "馬達緊急停止"; return true;
                case 32: name = "馬達連鎖／熄火"; return true;
                case 41: name = "解鎖／上鎖（舊版）"; return true;
                case 46: name = "RC Override 啟用"; return true;
                case 81: name = "強制上鎖"; return true;
                case 85: name = "發電機控制"; return true;
                case 100: name = "停用 IMU1"; return true;
                case 101: name = "停用 IMU2"; return true;
                case 110: name = "停用 IMU3"; return true;
                case 153: name = "解鎖／上鎖"; return true;
                case 154: name = "解鎖／上鎖＋AirMode"; return true;
                case 161: name = "直升機渦輪啟動"; return true;
                case 165: name = "解鎖／馬達緊急停止"; return true;
                default: name = null; return false;
            }
        }

        private void SetUnavailable(string message)
        {
            stableSinceUtc = null;
            stableProgress.Value = 0;
            acceptButton.Enabled = false;
            statusLabel.ForeColor = Color.OrangeRed;
            statusLabel.Text = message;
        }

        private bool IsRcTelemetryFresh()
        {
            var rc = port.MAV.getPacketLast((uint)MAVLink.MAVLINK_MSG_ID.RC_CHANNELS);
            var raw = port.MAV.getPacketLast((uint)MAVLink.MAVLINK_MSG_ID.RC_CHANNELS_RAW);
            var last = DateTime.MinValue;
            if (rc != null && rc.rxtime > last)
                last = rc.rxtime;
            if (raw != null && raw.rxtime > last)
                last = raw.rxtime;
            return last != DateTime.MinValue && (DateTime.UtcNow - last).TotalSeconds <= RcTelemetryTimeoutSeconds;
        }

        private int GetReceiverPwm(int channel)
        {
            var state = port.MAV.cs;
            switch (channel)
            {
                case 1: return (int)Math.Round(state.ch1in);
                case 2: return (int)Math.Round(state.ch2in);
                case 3: return (int)Math.Round(state.ch3in);
                case 4: return (int)Math.Round(state.ch4in);
                case 5: return (int)Math.Round(state.ch5in);
                case 6: return (int)Math.Round(state.ch6in);
                case 7: return (int)Math.Round(state.ch7in);
                case 8: return (int)Math.Round(state.ch8in);
                case 9: return (int)Math.Round(state.ch9in);
                case 10: return (int)Math.Round(state.ch10in);
                case 11: return (int)Math.Round(state.ch11in);
                case 12: return (int)Math.Round(state.ch12in);
                case 13: return (int)Math.Round(state.ch13in);
                case 14: return (int)Math.Round(state.ch14in);
                case 15: return (int)Math.Round(state.ch15in);
                case 16: return (int)Math.Round(state.ch16in);
                case 17: return (int)Math.Round(state.ch17in);
                case 18: return (int)Math.Round(state.ch18in);
                default: return 0;
            }
        }

        private sealed class ChannelState
        {
            internal int Channel;
            internal string DisplayName;
            internal int Tolerance;
            internal int RowIndex;
            internal bool UsesJoystick;
            internal FmtCriticalRcSwitch SafetySwitch;
        }
    }

    /// <summary>
    /// Mandatory operator cross-check for GCS-to-receiver handover. Standard ArduPilot
    /// telemetry reports the effective RC value, not the isolated receiver's standby raw
    /// value, so this dialog deliberately does not claim an automatic comparison.
    /// </summary>
    internal sealed class FmtCriticalSwitchReturnDialog : Form
    {
        private readonly Button acceptButton;
        private readonly List<CheckBox> checks = new List<CheckBox>();

        internal FmtCriticalSwitchReturnDialog(IEnumerable<FmtCriticalRcSwitch> switches, bool armed)
        {
            var items = (switches ?? Enumerable.Empty<FmtCriticalRcSwitch>()).ToList();
            Text = "實體遙控器安全開關確認";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(690, Math.Min(650, Math.Max(300, 205 + items.Count * 42)));

            var title = new Label
            {
                Dock = DockStyle.Top,
                Height = 72,
                Padding = new Padding(12, 8, 12, 4),
                ForeColor = Color.Gold,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "偵測到會直接影響解鎖、馬達或安全設備的 RC 開關。\r\n" +
                       "切回實體遙控器前，必須逐項確認其位置與導控目前狀態一致。飛控目前：" +
                       (armed ? "已解鎖" : "未解鎖") + "。"
            };
            var list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 8, 12, 8)
            };
            foreach (var item in items)
            {
                var reference = item.InitialPosition == null || item.InitialPosition == "未知"
                    ? string.Empty
                    : "；接管前位置：" + item.InitialPosition;
                var check = new CheckBox
                {
                    AutoSize = false,
                    Width = 635,
                    Height = 34,
                    Text = "RC" + item.Channel + "  " + item.FunctionName +
                           "：已與導控目前安全狀態一致" + reference,
                    Tag = item
                };
                check.CheckedChanged += (sender, args) => UpdateAcceptState();
                checks.Add(check);
                list.Controls.Add(check);
            }

            acceptButton = new Button
            {
                Text = "全部確認，切回遙控器",
                DialogResult = DialogResult.OK,
                Enabled = false,
                AutoSize = true,
                Padding = new Padding(10, 3, 10, 3),
                Margin = new Padding(6)
            };
            var cancelButton = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                AutoSize = true,
                Padding = new Padding(10, 3, 10, 3),
                Margin = new Padding(6)
            };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(4)
            };
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(acceptButton);

            Controls.Add(list);
            Controls.Add(buttons);
            Controls.Add(title);
            AcceptButton = acceptButton;
            CancelButton = cancelButton;
            ThemeManager.ApplyThemeTo(this);
        }

        private void UpdateAcceptState()
        {
            acceptButton.Enabled = checks.Count > 0 && checks.All(check => check.Checked);
        }
    }
}
