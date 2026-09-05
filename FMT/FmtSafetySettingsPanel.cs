using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtSafetySettingsPanel : UserControl
    {
        private static readonly Color Cyan = Color.FromArgb(46, 174, 220);
        private static readonly Color PanelColor = Color.FromArgb(18, 36, 45);
        private readonly FlowLayoutPanel cards;
        private readonly Label status;
        private readonly Button refresh;

        internal FmtSafetySettingsPanel()
        {
            Name = "fmtEmbeddedSafetySettings";
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BackColor = Color.FromArgb(13, 29, 37);
            ForeColor = Color.White;
            Font = new Font("Microsoft JhengHei UI", 9F);

            var header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = PanelColor };
            var title = new Label
            {
                Text = "安全設定  Failsafe",
                AutoSize = true,
                Location = new Point(12, 8),
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
                ForeColor = Color.White
            };
            status = new Label
            {
                Text = "連線飛控後顯示此構型支援的安全參數。",
                AutoEllipsis = true,
                Location = new Point(14, 37),
                Size = new Size(720, 23),
                ForeColor = Color.FromArgb(255, 174, 72)
            };
            refresh = new Button
            {
                Name = "fmtSafetyRefresh",
                Text = "重新整理參數",
                Size = new Size(130, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Cyan,
                ForeColor = Color.Black,
                UseVisualStyleBackColor = false
            };
            header.Controls.Add(title);
            header.Controls.Add(status);
            header.Controls.Add(refresh);
            header.Resize += (sender, args) =>
            {
                refresh.Location = new Point(Math.Max(610, header.ClientSize.Width - refresh.Width - 12), 17);
                status.Width = Math.Max(260, refresh.Left - status.Left - 12);
            };

            cards = new FlowLayoutPanel
            {
                Name = "fmtSafetyCards",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(13, 29, 37)
            };
            Controls.Add(cards);
            Controls.Add(header);
            refresh.Click += (sender, args) => RefreshParameters();
        }

        internal void RefreshParameters()
        {
            DisposeCards();
            var port = MainV2.comPort;
            var mav = port?.MAV;
            var parameters = mav?.param;
            var connected = port?.BaseStream != null && port.BaseStream.IsOpen;
            if (parameters == null || parameters.Count == 0)
            {
                status.Text = "尚未取得飛控參數；請先連線並完成參數下載。";
                status.ForeColor = Color.FromArgb(255, 174, 72);
                return;
            }

            var availableNames = parameters.Select(parameter => parameter.Name).ToArray();
            var definitions = FmtSafetyParameterCatalog.Definitions
                .Select(definition => new { Definition = definition, Name = definition.Resolve(availableNames) })
                .Where(item => item.Name != null)
                .ToArray();

            foreach (var item in definitions)
            {
                var writeBlockedReason = GetWriteBlockedReason(item.Name, parameters);
                var card = new SafetyParameterCard(item.Definition, item.Name,
                    connected && writeBlockedReason == null, writeBlockedReason);
                card.ApplyRequested += ApplyParameter;
                cards.Controls.Add(card);
            }

            status.Text = connected
                ? "各安全項目獨立寫入；飛行器解鎖時禁止修改。共載入 " + definitions.Length + " 項。"
                : "目前未連線；可檢視快取值，但無法寫入。共載入 " + definitions.Length + " 項。";
            status.ForeColor = connected ? Color.FromArgb(74, 210, 116) : Color.FromArgb(255, 174, 72);
        }

        private static string GetWriteBlockedReason(string parameterName, MAVLink.MAVLinkParamList parameters)
        {
            if (!string.Equals(parameterName, "FENCE_ACTION", StringComparison.OrdinalIgnoreCase) ||
                parameters == null || !parameters.ContainsKey("FENCE_ENABLE"))
                return null;

            return parameters["FENCE_ENABLE"].Value > 0
                ? null
                : "電子圍籬未啟用；請先啟用 FENCE_ENABLE。";
        }

        private void ApplyParameter(SafetyParameterCard card, string parameterName, float value)
        {
            var port = MainV2.comPort;
            if (port?.BaseStream == null || !port.BaseStream.IsOpen)
            {
                MessageBox.Show(this, "飛控尚未連線，無法寫入安全設定。", "安全設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (port.MAV.cs.armed)
            {
                MessageBox.Show(this, "飛行器已解鎖，請先鎖定後再修改安全設定。", "安全設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var writeBlockedReason = GetWriteBlockedReason(parameterName, port.MAV.param);
            if (writeBlockedReason != null)
            {
                MessageBox.Show(this, writeBlockedReason, "安全設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshParameters();
                return;
            }

            var oldValue = port.MAV.param[parameterName]?.Value ?? 0;
            var prompt = card.Title + "\r\n" + parameterName + "：" +
                         oldValue.ToString(CultureInfo.InvariantCulture) + " → " +
                         value.ToString(CultureInfo.InvariantCulture) +
                         "\r\n\r\n確定要寫入飛控嗎？";
            if (MessageBox.Show(this, prompt, "確認安全設定", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            try
            {
                if (!port.setParam((byte)port.sysidcurrent, (byte)port.compidcurrent, parameterName, value, true))
                    throw new InvalidOperationException("飛控未確認參數寫入。");

                card.SetCurrentValue(port.MAV.param[parameterName]?.Value ?? value);
                status.Text = "✓ 已寫入 " + parameterName + "；其他安全項目未變更。";
                status.ForeColor = Color.FromArgb(74, 210, 116);
            }
            catch (Exception ex)
            {
                status.Text = "⚠ " + parameterName + " 寫入失敗：" + ex.Message;
                status.ForeColor = Color.FromArgb(255, 96, 96);
            }
        }

        private void DisposeCards()
        {
            foreach (Control control in cards.Controls.Cast<Control>().ToArray())
                control.Dispose();
            cards.Controls.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCards();
            base.Dispose(disposing);
        }

        private sealed class SafetyParameterCard : Panel
        {
            private readonly ComboBox choices;
            private readonly NumericUpDown number;
            private readonly Button bitmaskButton;
            private readonly ContextMenuStrip bitmaskMenu;
            private readonly Label current;

            internal SafetyParameterCard(FmtSafetyParameterDefinition definition, string parameterName, bool canWrite,
                string writeBlockedReason)
            {
                Title = definition.Title;
                ParameterName = parameterName;
                Size = new Size(300, 102);
                Margin = new Padding(4);
                Padding = new Padding(6);
                BackColor = PanelColor;
                BorderStyle = BorderStyle.FixedSingle;

                var title = new Label
                {
                    Text = definition.Title,
                    Location = new Point(9, 7),
                    Size = new Size(166, 22),
                    ForeColor = Color.White,
                    Font = new Font("Microsoft JhengHei UI", 9F, FontStyle.Bold)
                };
                current = new Label
                {
                    Location = new Point(178, 7),
                    Size = new Size(110, 22),
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = Cyan,
                    AutoEllipsis = true
                };

                var firmware = MainV2.comPort.MAV.cs.firmware.ToString();
                var options = ParameterMetaDataRepository.GetParameterOptionsInt(parameterName, firmware);
                if (parameterName == "FS_OPTIONS")
                {
                    bitmaskMenu = new ContextMenuStrip
                    {
                        ShowImageMargin = false,
                        ShowCheckMargin = true,
                        AutoSize = true,
                        MaximumSize = new Size(320, 0),
                        BackColor = PanelColor,
                        ForeColor = Color.White,
                        Font = new Font("Microsoft JhengHei UI", 9F)
                    };
                    var bits = ParameterMetaDataRepository.GetParameterBitMaskInt(parameterName, firmware);
                    foreach (var bit in bits)
                    {
                        var item = new ToolStripMenuItem
                        {
                            Text = "位元 " + bit.Key + "｜" + FmtSafetyParameterCatalog.TranslateOption(bit.Value),
                            Tag = bit.Key,
                            CheckOnClick = true,
                            AutoSize = false,
                            Size = new Size(300, 28),
                            BackColor = PanelColor,
                            ForeColor = Color.White
                        };
                        item.CheckedChanged += (sender, args) => RefreshBitmaskButton();
                        bitmaskMenu.Items.Add(item);
                    }
                    if (bitmaskMenu.Items.Count == 0)
                    {
                        bitmaskMenu.Items.Add(new ToolStripMenuItem("目前韌體未提供選項說明")
                        {
                            Enabled = false
                        });
                    }
                    bitmaskButton = new Button
                    {
                        Text = "選擇例外項目 ▼",
                        Location = new Point(9, 33),
                        Size = new Size(194, 25),
                        TextAlign = ContentAlignment.MiddleLeft,
                        BackColor = Color.FromArgb(35, 57, 68),
                        ForeColor = Color.White,
                        UseVisualStyleBackColor = false
                    };
                    bitmaskButton.Click += (sender, args) =>
                        bitmaskMenu.Show(bitmaskButton, new Point(0, bitmaskButton.Height));
                    Controls.Add(bitmaskButton);
                }
                else if (options.Count > 0)
                {
                    var currentOptionValue = Convert.ToInt32(MainV2.comPort.MAV.param[parameterName].Value);
                    var translatedOptions = options.Select(option => new SafetyOption
                    {
                        Value = option.Key,
                        Text = option.Key + "－" + FmtSafetyParameterCatalog.TranslateOption(option.Value)
                    }).ToList();
                    if (translatedOptions.All(option => option.Value != currentOptionValue))
                    {
                        translatedOptions.Add(new SafetyOption
                        {
                            Value = currentOptionValue,
                            Text = currentOptionValue + "－目前值（中繼資料未列出）"
                        });
                    }
                    choices = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Location = new Point(9, 33),
                        Size = new Size(194, 25),
                        DropDownWidth = 340,
                        DisplayMember = "Text",
                        ValueMember = "Value"
                    };
                    choices.DataSource = translatedOptions.ToArray();
                    Controls.Add(choices);
                }
                else
                {
                    double minimum = -1000000;
                    double maximum = 1000000;
                    double increment = 1;
                    ParameterMetaDataRepository.GetParameterRange(parameterName, ref minimum, ref maximum,
                        MainV2.comPort.MAV.cs.firmware.ToString());
                    ParameterMetaDataRepository.GetParameterIncrement(parameterName, ref increment,
                        MainV2.comPort.MAV.cs.firmware.ToString());
                    number = new NumericUpDown
                    {
                        Location = new Point(9, 33),
                        Size = new Size(194, 25),
                        Minimum = SafeDecimal(minimum, -1000000M),
                        Maximum = SafeDecimal(maximum, 1000000M),
                        Increment = Math.Max(0.001M, SafeDecimal(increment, 1M)),
                        DecimalPlaces = increment > 0 && increment < 0.01 ? 3 : increment < 1 ? 2 : 0
                    };
                    Controls.Add(number);
                }

                var apply = new Button
                {
                    Text = "套用",
                    Location = new Point(210, 32),
                    Size = new Size(78, 28),
                    Enabled = canWrite && (parameterName != "FS_OPTIONS" ||
                              bitmaskMenu.Items.OfType<ToolStripMenuItem>().Any(item => item.Tag is int)),
                    BackColor = Cyan,
                    ForeColor = Color.Black,
                    UseVisualStyleBackColor = false
                };
                var description = new Label
                {
                    Text = parameterName + "｜" + definition.Description,
                    Location = new Point(9, 64),
                    Size = new Size(279, 32),
                    ForeColor = Color.FromArgb(205, 215, 220),
                    AutoEllipsis = true
                };
                apply.Click += (sender, args) =>
                {
                    if (choices != null && choices.SelectedItem == null)
                        return;
                    var value = bitmaskButton != null
                        ? GetBitmaskValue()
                        : choices != null
                        ? Convert.ToSingle(((SafetyOption)choices.SelectedItem).Value)
                        : Convert.ToSingle(number.Value);
                    ApplyRequested?.Invoke(this, ParameterName, value);
                };

                Controls.Add(title);
                Controls.Add(current);
                Controls.Add(apply);
                Controls.Add(description);
                SetCurrentValue(MainV2.comPort.MAV.param[parameterName].Value);

                if (!canWrite)
                {
                    if (choices != null)
                        choices.Enabled = false;
                    if (number != null)
                        number.Enabled = false;
                    if (bitmaskButton != null)
                        bitmaskButton.Enabled = false;
                }

                if (!string.IsNullOrEmpty(writeBlockedReason))
                {
                    current.Text = "目前：未啟用";
                    current.ForeColor = Color.FromArgb(255, 174, 72);
                    description.Text = parameterName + "｜" + writeBlockedReason;
                }
            }

            internal string Title { get; }
            internal string ParameterName { get; }
            internal event Action<SafetyParameterCard, string, float> ApplyRequested;

            internal void SetCurrentValue(double value)
            {
                current.Text = "目前：" + value.ToString("0.###", CultureInfo.InvariantCulture);
                if (choices != null)
                    choices.SelectedValue = Convert.ToInt32(value);
                else if (bitmaskButton != null)
                {
                    var mask = Convert.ToInt32(value);
                    foreach (ToolStripItem rawItem in bitmaskMenu.Items)
                    {
                        var item = rawItem as ToolStripMenuItem;
                        if (item?.Tag is int)
                            item.Checked = (mask & (1 << (int)item.Tag)) != 0;
                    }
                    RefreshBitmaskButton();
                }
                else if (number != null)
                    number.Value = Math.Max(number.Minimum, Math.Min(number.Maximum, Convert.ToDecimal(value)));
            }

            private float GetBitmaskValue()
            {
                var value = 0;
                foreach (ToolStripItem rawItem in bitmaskMenu.Items)
                {
                    var item = rawItem as ToolStripMenuItem;
                    if (item?.Tag is int && item.Checked)
                        value |= 1 << (int)item.Tag;
                }
                return value;
            }

            private void RefreshBitmaskButton()
            {
                if (bitmaskButton == null)
                    return;
                var selected = bitmaskMenu.Items.OfType<ToolStripMenuItem>()
                    .Count(item => item.Tag is int && item.Checked);
                bitmaskButton.Text = selected == 0
                    ? "未選擇例外（0） ▼"
                    : "已選 " + selected + " 項（" + GetBitmaskValue().ToString("0") + "） ▼";
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    bitmaskMenu?.Dispose();
                base.Dispose(disposing);
            }

            private static decimal SafeDecimal(double value, decimal fallback)
            {
                try { return Convert.ToDecimal(value); }
                catch { return fallback; }
            }
        }

        private sealed class SafetyOption
        {
            public int Value { get; set; }
            public string Text { get; set; }
        }
    }
}
