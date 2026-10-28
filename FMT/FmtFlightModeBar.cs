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

        /// <summary>
        /// WinForms normally replaces a disabled Button's foreground colour with a
        /// low-contrast system colour. Flight-mode buttons are intentionally disabled
        /// while disconnected, so paint their caption ourselves and keep it readable.
        /// The display colours are separate from BackColor/ForeColor because the global
        /// Mission Planner theme pass may rewrite those standard properties later.
        /// </summary>
        private sealed class HighContrastModeButton : Button
        {
            internal Color DisplayBackColor { get; set; } = Color.FromArgb(39, 54, 64);
            internal Color DisplayBorderColor { get; set; } = Color.FromArgb(45, 169, 220);

            internal HighContrastModeButton()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (var background = new SolidBrush(DisplayBackColor))
                    e.Graphics.FillRectangle(background, ClientRectangle);

                ControlPaint.DrawBorder(e.Graphics, ClientRectangle, DisplayBorderColor,
                    ButtonBorderStyle.Solid);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                if (Focused && ShowFocusCues)
                {
                    var focusBounds = Rectangle.Inflate(ClientRectangle, -4, -4);
                    ControlPaint.DrawFocusRectangle(e.Graphics, focusBounds, Color.White,
                        DisplayBackColor);
                }
            }
        }

        private sealed class DoubleBufferedFlowLayoutPanel : FlowLayoutPanel
        {
            internal DoubleBufferedFlowLayoutPanel()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                UpdateStyles();
            }
        }

        private static readonly Color Background = Color.FromArgb(12, 27, 36);
        private static readonly Color PanelBackground = Color.FromArgb(20, 37, 47);
        private static readonly Color SkyBlue = Color.FromArgb(45, 169, 220);
        private static readonly Color ActiveGreen = Color.FromArgb(34, 156, 72);
        private static readonly Color Disconnected = Color.FromArgb(196, 64, 64);
        private readonly Label statusLabel;
        private readonly Panel modesPanel;
        private bool arrangingModes;
        private Font scaledModeFont;
        private Font scaledStatusFont;
        private float appliedScale;
        private readonly Dictionary<string, HighContrastModeButton> modeButtons =
            new Dictionary<string, HighContrastModeButton>(StringComparer.OrdinalIgnoreCase);
        private List<ModeGroup> visibleGroups = new List<ModeGroup>();
        private string layoutSignature = string.Empty;
        private string vehicleStateSignature = string.Empty;
        private bool connected;
        private string currentMode = string.Empty;
        private int availableHeight = 572;

        internal void SetAvailableHeight(int height)
        {
            height = Math.Max(0, height);
            if (availableHeight == height) return;
            availableHeight = height;
            RecalculateHeight();
        }

        internal FmtFlightModeBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            UpdateStyles();
            Name = "fmtFlightModeBar";
            BackColor = Background;
            Padding = new Padding(5, 4, 5, 4);
            Dock = DockStyle.None;
            MinimumSize = Size.Empty;

            statusLabel = new Label
            {
                Name = "fmtFlightModeStatus",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.White,
                BackColor = Background,
                Font = SystemFonts.MessageBoxFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };

            modesPanel = new Panel
            {
                Name = "fmtFlightModeGroups",
                Dock = DockStyle.Fill,
                BackColor = PanelBackground,
                AutoScroll = false,
                Padding = new Padding(4, 3, 4, 3),
                Margin = Padding.Empty
            };

            Controls.Add(modesPanel);
            Controls.Add(statusLabel);
            SizeChanged += (sender, args) => RecalculateHeight();
        }

        internal event EventHandler<string> ModeRequested;

        internal void UpdateVehicle(Firmwares firmware, bool isQuadPlane, bool isHelicopter, bool isConnected,
            string activeMode, IEnumerable<string> supportedModes)
        {
            var supported = (supportedModes ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(NormalizeModeName)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var groups = GetModeGroups(firmware, isQuadPlane, isHelicopter);
            var signature = firmware + "|" + isQuadPlane + "|" + isHelicopter + "|" +
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

            var vehicleName = GetVehicleName(firmware, isQuadPlane, isHelicopter);
            statusLabel.Text = (connected ? "● 已連線" : "● 未連線") + "  |  " + vehicleName +
                               "  |  目前模式：" + (string.IsNullOrWhiteSpace(currentMode) ? "--" : currentMode);
            statusLabel.ForeColor = connected ? Color.LimeGreen : Disconnected;

            foreach (var pair in modeButtons)
            {
                var isActive = string.Equals(NormalizeModeName(pair.Key), NormalizeModeName(currentMode),
                    StringComparison.OrdinalIgnoreCase);
                var backColor = isActive ? ActiveGreen : Color.FromArgb(39, 54, 64);
                var borderColor = isActive ? Color.LimeGreen : SkyBlue;
                var enabled = connected && pair.Value.Tag != null;
                if (pair.Value.DisplayBackColor != backColor ||
                    pair.Value.DisplayBorderColor != borderColor || pair.Value.Enabled != enabled)
                {
                    pair.Value.DisplayBackColor = backColor;
                    pair.Value.DisplayBorderColor = borderColor;
                    pair.Value.Enabled = enabled;
                    pair.Value.Invalidate();
                }
            }

            foreach (var groupLabel in modesPanel.Controls.OfType<Label>())
            {
                var group = groupLabel.Tag as ModeGroup;
                var groupActive = connected && group != null && group.Modes.Any(definition =>
                    string.Equals(NormalizeModeName(definition.Mode), NormalizeModeName(currentMode),
                        StringComparison.OrdinalIgnoreCase));
                var text = (groupActive ? "● " : "○ ") + (group == null ? string.Empty : group.Title);
                var color = groupActive ? Color.LimeGreen : Color.Gainsboro;
                if (!string.Equals(groupLabel.Text, text, StringComparison.Ordinal))
                    groupLabel.Text = text;
                if (groupLabel.ForeColor != color)
                    groupLabel.ForeColor = color;
            }
        }

        private void RebuildButtons(Dictionary<string, string> supported)
        {
            modesPanel.SuspendLayout();
            foreach (Control old in modesPanel.Controls.Cast<Control>().ToArray()) old.Dispose();
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

                foreach (var definition in group.Modes)
                {
                    string actualMode;
                    supported.TryGetValue(NormalizeModeName(definition.Mode), out actualMode);
                    var button = new HighContrastModeButton
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
                        Font = scaledModeFont ?? Font,
                        UseVisualStyleBackColor = false,
                        Enabled = connected && actualMode != null
                    };
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = actualMode == null ? Color.DimGray : SkyBlue;
                    button.DisplayBackColor = Color.FromArgb(39, 54, 64);
                    button.DisplayBorderColor = actualMode == null ? Color.DimGray : SkyBlue;
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
            if (arrangingModes || IsDisposed || Disposing) return;
            arrangingModes = true;
            try
            {
                var heightLimit = Math.Max(0, Math.Min(286, availableHeight / 2));
                if (visibleGroups.Count == 0)
                {
                    Height = Math.Min(92, heightLimit);
                    return;
                }

                // Fit complete fixed rows into the viewport. There is no minimum
                // scale that could turn a narrow or short pane into a scroll page.
                var innerWidth = Math.Max(0, ClientSize.Width - Padding.Horizontal);
                var baseWidth = visibleGroups.Max(group => group.Modes.Length) * 72 + 8;
                var baseHeight = 24 + visibleGroups.Count * 76 + 8;
                var scale = (float)Math.Max(0, Math.Min(1.8, Math.Min(
                    innerWidth / (double)baseWidth,
                    Math.Max(0, heightLimit - Padding.Vertical) / (double)baseHeight)));
                var oldFont = scaledModeFont;
                var oldStatusFont = scaledStatusFont;
                if (scaledStatusFont == null || Math.Abs(appliedScale - scale) > 0.001f)
                {
                    var nextStatusFont = new Font(SystemFonts.MessageBoxFont.FontFamily,
                        Math.Max(1F, 9F * scale), FontStyle.Bold);
                    if (oldStatusFont != null && oldStatusFont.Equals(nextStatusFont))
                    {
                        nextStatusFont.Dispose();
                        scaledStatusFont = oldStatusFont;
                    }
                    else scaledStatusFont = nextStatusFont;
                    appliedScale = scale;
                }
                statusLabel.Font = scaledStatusFont;
                statusLabel.Height = (int)Math.Round(24 * scale);
                Height = Math.Min(heightLimit, Padding.Vertical + (int)Math.Ceiling(baseHeight * scale));

                int groupIndex = -1, buttonIndex = 0, columns = 1;
                var inset = (int)Math.Round(4 * scale);
                var gap = (int)Math.Round(6 * scale);
                modesPanel.SuspendLayout();
                try
                {
                    modesPanel.AutoScrollPosition = Point.Empty;
                    modesPanel.AutoScrollMinSize = Size.Empty;
                    foreach (Control control in modesPanel.Controls)
                    {
                        if (control is Label)
                        {
                            groupIndex++; buttonIndex = 0;
                            columns = visibleGroups[groupIndex].Modes.Length;
                            control.SetBounds(inset, (int)Math.Round((4 + groupIndex * 76) * scale),
                                Math.Max(0, innerWidth - 2 * inset), (int)Math.Round(20 * scale));
                        }
                        else
                        {
                            // Round cell edges, not widths, so every last button fits.
                            var rowWidth = Math.Max(0, innerWidth - 2 * inset + gap);
                            var left = inset + (int)Math.Round(buttonIndex * rowWidth / (double)columns);
                            var right = inset + (int)Math.Round(++buttonIndex * rowWidth / (double)columns) - gap;
                            control.SetBounds(left, (int)Math.Round((26 + groupIndex * 76) * scale),
                                Math.Max(0, right - left), (int)Math.Round(48 * scale));
                        }
                    }
                    var nextModeFont = CreateFittedModeFont(Math.Max(1F, 8.25F * scale));
                    // Control.Font retains its existing instance when values compare
                    // equal. Reuse it too, rather than disposing a font still in use.
                    if (oldFont != null && oldFont.Equals(nextModeFont))
                    {
                        nextModeFont.Dispose();
                        scaledModeFont = oldFont;
                    }
                    else scaledModeFont = nextModeFont;
                    foreach (Control control in modesPanel.Controls) control.Font = scaledModeFont;
                }
                finally
                {
                    modesPanel.ResumeLayout();
                    if (oldFont != null && oldFont != scaledModeFont) oldFont.Dispose();
                    if (oldStatusFont != null && oldStatusFont != scaledStatusFont) oldStatusFont.Dispose();
                }
            }
            finally { arrangingModes = false; }
        }

        private Font CreateFittedModeFont(float size)
        {
            // GDI rounds small fonts to whole pixels. Measuring the actual captions
            // keeps long names such as STABILIZE inside even the narrowest buttons.
            for (int attempt = 0; ; attempt++)
            {
                var font = new Font(SystemFonts.MessageBoxFont.FontFamily, size, FontStyle.Bold);
                var fit = 1F;
                foreach (var button in modeButtons.Values)
                {
                    var textSize = TextRenderer.MeasureText(button.Text, font, Size.Empty,
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    fit = Math.Min(fit, Math.Min(Math.Max(1, button.Width - 4) / (float)Math.Max(1, textSize.Width),
                        Math.Max(1, button.Height - 4) / (float)Math.Max(1, textSize.Height)));
                }
                if (fit >= 1 || size <= 1 || attempt >= 15) return font;
                font.Dispose();
                size = Math.Max(1F, size * Math.Min(0.95F, fit));
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                scaledModeFont?.Dispose(); scaledModeFont = null;
                scaledStatusFont?.Dispose(); scaledStatusFont = null;
            }
        }

        private static List<ModeGroup> GetModeGroups(Firmwares firmware, bool isQuadPlane, bool isHelicopter)
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

            if (isHelicopter)
            {
                return OneGroup("直升機常用模式",
                    Mode("STABILIZE", "自穩"), Mode("ACRO", "特技"), Mode("ALT_HOLD", "定高"),
                    Mode("LOITER", "定點"), Mode("BRAKE", "煞車"), Mode("AUTO", "任務"),
                    Mode("RTL", "返航"), Mode("LAND", "降落"));
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
                        Mode("BRAKE", "煞車"), Mode("AUTO", "任務"), Mode("RTL", "返航"),
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

        private static string GetVehicleName(Firmwares firmware, bool isQuadPlane, bool isHelicopter)
        {
            if (isQuadPlane)
                return "QuadPlane／VTOL";

            if (isHelicopter)
                return "直升機";

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
