using MissionPlanner.ArduPilot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtFlightModeBar : UserControl
    {
        private sealed class ModeDefinition
        {
            internal ModeDefinition(string mode, string caption)
            {
                Mode = mode;
                Caption = caption;
            }

            internal string Mode { get; private set; }
            internal string Caption { get; private set; }
        }

        private sealed class ModeGroup
        {
            internal ModeGroup(string title, params ModeDefinition[] modes)
            {
                Title = title;
                Modes = modes;
            }

            internal string Title { get; private set; }
            internal ModeDefinition[] Modes { get; private set; }
        }

        private static readonly Color Background = Color.FromArgb(12, 27, 36);
        private static readonly Color PanelBackground = Color.FromArgb(20, 37, 47);
        private static readonly Color SkyBlue = Color.FromArgb(45, 169, 220);
        private static readonly Color ActiveGreen = Color.FromArgb(34, 156, 72);
        private static readonly Color Disconnected = Color.FromArgb(196, 64, 64);
        private readonly Label statusLabel;
        private readonly FlowLayoutPanel modesPanel;
        private readonly Dictionary<string, Button> modeButtons =
            new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private List<ModeGroup> visibleGroups = new List<ModeGroup>();
        private string layoutSignature = string.Empty;
        private string vehicleStateSignature = string.Empty;
        private bool connected;
        private string currentMode = string.Empty;

        internal FmtFlightModeBar()
        {
            Name = "fmtFlightModeBar";
            BackColor = Background;
            Padding = new Padding(5, 4, 5, 4);
            Dock = DockStyle.None;
            MinimumSize = new Size(0, 92);

            statusLabel = new Label
            {
                Name = "fmtFlightModeStatus",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.White,
                BackColor = Background,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };

            modesPanel = new FlowLayoutPanel
            {
                Name = "fmtFlightModeGroups",
                Dock = DockStyle.Fill,
                BackColor = PanelBackground,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                Padding = new Padding(4, 3, 4, 3),
                Margin = Padding.Empty
            };

            Controls.Add(modesPanel);
            Controls.Add(statusLabel);
            SizeChanged += (sender, args) => RecalculateHeight();
        }

        internal event EventHandler<string> ModeRequested;

        internal void UpdateVehicle(Firmwares firmware, bool isQuadPlane, bool isConnected,
            string activeMode, IEnumerable<string> supportedModes)
        {
            var supported = (supportedModes ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(NormalizeModeName)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var groups = GetModeGroups(firmware, isQuadPlane);
            var signature = firmware + "|" + isQuadPlane + "|" +
                            string.Join(",", supported.Values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            var normalizedActiveMode = NormalizeModeName(activeMode);
            var nextVehicleStateSignature = signature + "|" + isConnected + "|" + normalizedActiveMode;

            if (string.Equals(vehicleStateSignature, nextVehicleStateSignature, StringComparison.Ordinal))
                return;

            connected = isConnected;
            currentMode = activeMode ?? string.Empty;
            vehicleStateSignature = nextVehicleStateSignature;

            if (!string.Equals(layoutSignature, signature, StringComparison.Ordinal))
            {
                layoutSignature = signature;
                visibleGroups = groups;
                RebuildButtons(supported);
            }

            var vehicleName = GetVehicleName(firmware, isQuadPlane);
            statusLabel.Text = (connected ? "● 已連線" : "● 未連線") + "  |  " + vehicleName +
                               "  |  目前模式：" + (string.IsNullOrWhiteSpace(currentMode) ? "--" : currentMode);
            statusLabel.ForeColor = connected ? Color.LimeGreen : Disconnected;

            foreach (var pair in modeButtons)
            {
                var isActive = string.Equals(NormalizeModeName(pair.Key), NormalizeModeName(currentMode),
                    StringComparison.OrdinalIgnoreCase);
                pair.Value.BackColor = isActive ? ActiveGreen : Color.FromArgb(39, 54, 64);
                pair.Value.FlatAppearance.BorderColor = isActive ? Color.LimeGreen : SkyBlue;
                pair.Value.Enabled = connected && pair.Value.Tag != null;
            }

            foreach (var groupLabel in modesPanel.Controls.OfType<Label>())
            {
                var group = groupLabel.Tag as ModeGroup;
                var groupActive = connected && group != null && group.Modes.Any(definition =>
                    string.Equals(NormalizeModeName(definition.Mode), NormalizeModeName(currentMode),
                        StringComparison.OrdinalIgnoreCase));
                groupLabel.Text = (groupActive ? "● " : "○ ") + (group == null ? string.Empty : group.Title);
                groupLabel.ForeColor = groupActive ? Color.LimeGreen : Color.Gainsboro;
            }
        }

        private void RebuildButtons(Dictionary<string, string> supported)
        {
            modesPanel.SuspendLayout();
            modesPanel.Controls.Clear();
            modeButtons.Clear();

            foreach (var group in visibleGroups)
            {
                var title = new Label
                {
                    AutoSize = false,
                    Height = 20,
                    Width = Math.Max(120, modesPanel.ClientSize.Width - 14),
                    Text = "○ " + group.Title,
                    Tag = group,
                    ForeColor = Color.Gainsboro,
                    BackColor = PanelBackground,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(2, 2, 2, 0)
                };
                modesPanel.Controls.Add(title);
                modesPanel.SetFlowBreak(title, true);

                foreach (var definition in group.Modes)
                {
                    string actualMode;
                    supported.TryGetValue(NormalizeModeName(definition.Mode), out actualMode);
                    var button = new Button
                    {
                        Name = "fmtMode_" + definition.Mode,
                        Text = definition.Caption + Environment.NewLine + definition.Mode,
                        Tag = actualMode,
                        Width = 66,
                        Height = 48,
                        Margin = new Padding(3),
                        ForeColor = Color.White,
                        BackColor = Color.FromArgb(39, 54, 64),
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand,
                        Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 8.25F, FontStyle.Bold),
                        UseVisualStyleBackColor = false,
                        Enabled = connected && actualMode != null
                    };
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = actualMode == null ? Color.DimGray : SkyBlue;
                    button.Click += ModeButtonClick;
                    modeButtons[definition.Mode] = button;
                    modesPanel.Controls.Add(button);
                }
            }

            modesPanel.ResumeLayout(true);
            RecalculateHeight();
        }

        private void ModeButtonClick(object sender, EventArgs e)
        {
            var button = sender as Button;
            var mode = button == null ? null : button.Tag as string;
            if (string.IsNullOrWhiteSpace(mode))
                return;

            var handler = ModeRequested;
            if (handler != null)
                handler(this, mode);
        }

        private void RecalculateHeight()
        {
            if (visibleGroups.Count == 0)
            {
                Height = 92;
                return;
            }

            var availableWidth = Math.Max(72, ClientSize.Width - Padding.Horizontal - 16);
            var buttonsPerRow = Math.Max(1, availableWidth / 72);
            var contentHeight = 8;
            foreach (var group in visibleGroups)
                contentHeight += 22 + (int)Math.Ceiling(group.Modes.Length / (double)buttonsPerRow) * 54;

            Height = Math.Max(92, Math.Min(286, statusLabel.Height + Padding.Vertical + contentHeight));
            foreach (Control control in modesPanel.Controls)
            {
                var label = control as Label;
                if (label != null)
                    label.Width = Math.Max(120, modesPanel.ClientSize.Width - 14);
            }
        }

        private static List<ModeGroup> GetModeGroups(Firmwares firmware, bool isQuadPlane)
        {
            if (isQuadPlane)
            {
                return new List<ModeGroup>
                {
                    new ModeGroup("VTOL／Q 模式",
                        Mode("QSTABILIZE", "Q 自穩"), Mode("QHOVER", "Q 懸停"),
                        Mode("QLOITER", "Q 定點"), Mode("QRTL", "Q 返航"), Mode("QLAND", "Q 降落")),
                    new ModeGroup("固定翼模式",
                        Mode("MANUAL", "手動"), Mode("FBWA", "FBWA"), Mode("FBWB", "FBWB"),
                        Mode("LOITER", "盤旋"), Mode("AUTO", "任務"), Mode("RTL", "返航"))
                };
            }

            switch (firmware)
            {
                case Firmwares.ArduPlane:
                case Firmwares.Ateryx:
                    return OneGroup("固定翼常用模式",
                        Mode("MANUAL", "手動"), Mode("STABILIZE", "自穩"), Mode("FBWA", "FBWA"),
                        Mode("FBWB", "FBWB"), Mode("CRUISE", "巡航"), Mode("CIRCLE", "盤旋"),
                        Mode("AUTO", "任務"), Mode("RTL", "返航"));
                case Firmwares.ArduRover:
                    return OneGroup("車／船常用模式",
                        Mode("MANUAL", "手動"), Mode("HOLD", "保持"), Mode("STEERING", "轉向"),
                        Mode("ACRO", "特技"), Mode("GUIDED", "導引"), Mode("AUTO", "任務"),
                        Mode("RTL", "返航"), Mode("SMART_RTL", "智慧返航"));
                case Firmwares.ArduSub:
                    return OneGroup("潛航器常用模式",
                        Mode("MANUAL", "手動"), Mode("STABILIZE", "自穩"), Mode("ALT_HOLD", "深度保持"),
                        Mode("POSHOLD", "定點"), Mode("GUIDED", "導引"), Mode("AUTO", "任務"),
                        Mode("SURFACE", "上浮"));
                default:
                    return OneGroup("多旋翼常用模式",
                        Mode("STABILIZE", "自穩"), Mode("ALT_HOLD", "定高"), Mode("LOITER", "定點"),
                        Mode("POSHOLD", "位置保持"), Mode("AUTO", "任務"), Mode("RTL", "返航"),
                        Mode("LAND", "降落"));
            }
        }

        private static List<ModeGroup> OneGroup(string title, params ModeDefinition[] modes)
        {
            return new List<ModeGroup> { new ModeGroup(title, modes) };
        }

        private static ModeDefinition Mode(string mode, string caption)
        {
            return new ModeDefinition(mode, caption);
        }

        internal static string NormalizeModeName(string mode)
        {
            return new string((mode ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        private static string GetVehicleName(Firmwares firmware, bool isQuadPlane)
        {
            if (isQuadPlane)
                return "QuadPlane／VTOL";

            switch (firmware)
            {
                case Firmwares.ArduPlane:
                case Firmwares.Ateryx:
                    return "固定翼";
                case Firmwares.ArduRover:
                    return "車／船";
                case Firmwares.ArduSub:
                    return "潛航器";
                case Firmwares.ArduCopter2:
                    return "多旋翼";
                default:
                    return "飛行器";
            }
        }
    }
}
