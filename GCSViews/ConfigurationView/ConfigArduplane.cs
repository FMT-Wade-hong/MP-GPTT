using MissionPlanner.ArduPilot;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigArduplane : MyUserControl, IActivate
    {
        // from http://stackoverflow.com/questions/2512781/winforms-big-paragraph-tooltip/2512895#2512895
        private const int maximumSingleLineTooltipLength = 50;
        private static readonly Hashtable tooltips = new Hashtable();
        private readonly Hashtable changes = new Hashtable();
        private GroupBox fmtTuningGuide;
        internal bool startup = true;

        public ConfigArduplane()
        {
            InitializeComponent();
            ApplyFmtTraditionalChineseText();
            ApplyFmtTraditionalChineseTooltips();
            CreateFmtTuningGuide();
            Resize += (sender, args) => LayoutFmtTuningGuide();
        }

        private void ApplyFmtTraditionalChineseText()
        {
            groupBox8.Text = "橫滾舵機 PID";
            groupBox9.Text = "俯仰舵機 PID";
            groupBox10.Text = "偏航舵機控制";
            groupBox4.Text = "L1 航跡轉向控制";
            groupBox5.Text = "TECS 高度／空速控制";
            groupBox3.Text = "油門（0–100%）";
            groupBox1.Text = "空速（m/s）";
            groupBox2.Text = "導航姿態限制（度）";
            groupBox16.Text = "其他混控";
            groupBox12.Text = "空速至俯仰 PID";
            groupBox13.Text = "高度至俯仰 PID";
            groupBox14.Text = "能量至油門 PID";

            label52.Text = "比例 P";
            label51.Text = "積分 I";
            label50.Text = "微分 D";
            label49.Text = "積分上限";
            label56.Text = "比例 P";
            label55.Text = "積分 I";
            label54.Text = "微分 D";
            label53.Text = "積分上限";
            label60.Text = "偏航→滾轉";
            label59.Text = "積分";
            label58.Text = "阻尼";
            label57.Text = "積分上限";

            label10.Text = "反應週期";
            label9.Text = "阻尼";
            label15.Text = "最大下降率";
            label14.Text = "時間常數";
            label13.Text = "俯仰阻尼";
            label11.Text = "最小下降率";
            label12.Text = "最大爬升率";

            label5.Text = "變化速率";
            label6.Text = "最大";
            label7.Text = "最小";
            label8.Text = "巡航";
            label1.Text = "校正比率";
            label2.Text = "FBW 最大";
            label3.Text = "FBW 最小";
            label4.Text = "巡航空速";
            label37.Text = "最大滾轉角";
            label38.Text = "最大俯仰角";
            label39.Text = "最小俯仰角";
            label78.Text = "方向舵混控";
            label83.Text = "俯仰↔油門";

            label68.Text = "比例 P";
            label67.Text = "積分 I";
            label66.Text = "微分 D";
            label65.Text = "積分上限";
            label72.Text = "比例 P";
            label71.Text = "積分 I";
            label70.Text = "微分 D";
            label69.Text = "積分上限";
            label76.Text = "比例 P";
            label75.Text = "積分 I";
            label74.Text = "微分 D";
            label73.Text = "積分上限";

            BUT_writePIDS.Text = "寫入參數";
            BUT_rerequestparams.Text = "重新讀取參數";
            BUT_refreshpart.Text = "更新畫面";

            foreach (var label in FmtParameterLabels())
            {
                // The original resource labels are only 13 px high. Traditional
                // Chinese glyphs can be clipped completely at that height when the
                // application uses Windows display scaling, leaving only the value
                // editors visible. Give every caption a fixed readable area.
                label.AutoSize = false;
                label.Width = 100;
                label.Height = 20;
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.ForeColor = ThemeManager.TextColor;
                label.BackColor = Color.Transparent;
                label.AutoEllipsis = true;
                label.Visible = true;
                label.BringToFront();
            }
        }

        private Label[] FmtParameterLabels()
        {
            return new[]
            {
                label1, label2, label3, label4, label5, label6, label7, label8,
                label9, label10, label11, label12, label13, label14, label15,
                label37, label38, label39, label49, label50, label51, label52,
                label53, label54, label55, label56, label57, label58, label59,
                label60, label65, label66, label67, label68, label69, label70,
                label71, label72, label73, label74, label75, label76, label78,
                label83
            };
        }

        private void ApplyFmtTraditionalChineseTooltips()
        {
            SetFmtTooltip(RLL2SRV_P, "橫滾比例增益（P）：提高可加快滾轉反應；過高可能造成左右振盪。");
            SetFmtTooltip(RLL2SRV_I, "橫滾積分增益（I）：修正長時間的姿態偏差；過高可能造成慢速擺動。");
            SetFmtTooltip(RLL2SRV_D, "橫滾微分增益（D）：抑制快速振盪；過高會放大震動與感測器雜訊。");
            SetFmtTooltip(RLL2SRV_IMAX, "橫滾積分上限：限制積分補償量，避免舵面長時間打滿。");
            SetFmtTooltip(PTCH2SRV_P, "俯仰比例增益（P）：提高可加快抬頭／低頭反應；過高可能造成俯仰振盪。");
            SetFmtTooltip(PTCH2SRV_I, "俯仰積分增益（I）：修正長時間的俯仰偏差；過高可能造成慢速擺動。");
            SetFmtTooltip(PTCH2SRV_D, "俯仰微分增益（D）：增加俯仰阻尼；過高會放大震動與雜訊。");
            SetFmtTooltip(PTCH2SRV_IMAX, "俯仰積分上限：限制積分補償量，避免升降舵長時間打滿。");
            SetFmtTooltip(YAW2SRV_RLL, "偏航至滾轉協調量：用於改善轉彎協調，減少側滑。");
            SetFmtTooltip(YAW2SRV_INT, "偏航積分增益：修正持續的偏航／側滑誤差。");
            SetFmtTooltip(YAW2SRV_DAMP, "偏航阻尼：抑制荷蘭滾與偏航擺動；過高可能使反應遲鈍。");
            SetFmtTooltip(YAW2SRV_IMAX, "偏航積分上限：限制方向舵的積分補償量。");

            SetFmtTooltip(NAVL1_PERIOD, "L1 反應週期：數值較小時轉向更積極，較大時航跡較平順但轉彎半徑通常較大。");
            SetFmtTooltip(NAVL1_DAMPING, "L1 阻尼：控制航跡轉向的穩定程度；過低容易蛇行，過高會使轉向遲鈍。");
            SetFmtTooltip(TECS_CLMB_MAX, "TECS 最大爬升率（m/s）：應依飛機在安全空速下可持續達成的爬升能力設定。");
            SetFmtTooltip(TECS_SINK_MIN, "TECS 最小下降率（m/s）：正常緩降時使用的下降能力設定。");
            SetFmtTooltip(TECS_PTCH_DAMP, "TECS 俯仰阻尼：抑制高度與空速控制中的俯仰振盪。");
            SetFmtTooltip(TECS_TIME_CONST, "TECS 時間常數：較小反應較快，較大反應較平順；過小可能造成高度與空速振盪。");
            SetFmtTooltip(TECS_SINK_MAX, "TECS 最大下降率（m/s）：限制自動飛行可要求的最大下降速度。");

            SetFmtTooltip(TRIM_THROTTLE, "巡航油門（%）：平飛巡航時的基準油門。");
            SetFmtTooltip(THR_MIN, "最小油門（%）：自動模式允許輸出的最低油門。");
            SetFmtTooltip(THR_MAX, "最大油門（%）：自動模式允許輸出的最高油門。");
            SetFmtTooltip(THR_SLEWRATE, "油門變化速率：限制油門每秒變化量，使動力輸出更平順。");
            SetFmtTooltip(ARSPD_RATIO, "空速校正比率：用於校正差壓感測器換算出的空速；需配合可靠的空速校正程序。");
            SetFmtTooltip(ARSPD_FBW_MIN, "FBW 最小空速（m/s）：固定翼姿態控制使用的最低目標空速，必須高於安全失速速度並保留裕度。");
            SetFmtTooltip(ARSPD_FBW_MAX, "FBW 最大空速（m/s）：固定翼姿態控制允許的最高目標空速，不得超過機體安全限制。");
            SetFmtTooltip(TRIM_ARSPD_CM, "巡航空速（m/s）：自動飛行與巡航使用的基準空速。");
            SetFmtTooltip(LIM_ROLL_CD, "最大滾轉角（度）：限制導航轉彎時的傾斜角；角度越大，轉彎越急且失速風險可能增加。");
            SetFmtTooltip(LIM_PITCH_MAX, "最大俯仰角（度）：限制自動模式可要求的最大抬頭角。");
            SetFmtTooltip(LIM_PITCH_MIN, "最小俯仰角（度）：通常為負值，用來限制自動模式可要求的最大低頭角。");
            SetFmtTooltip(KFF_RDDRMIX, "方向舵混控：轉彎時依滾轉輸入加入方向舵，以改善協調轉彎。");
            SetFmtTooltip(KFF_PTCH2THR, "俯仰／油門前饋混控：依韌體實際參數名稱補償俯仰與油門的耦合。");

            SetFmtTooltip(BUT_writePIDS, "將本頁已修改的數值寫入飛控。寫入前請確認參數與機型相符。");
            SetFmtTooltip(BUT_rerequestparams, "重新向飛控下載完整參數列表。");
            SetFmtTooltip(BUT_refreshpart, "重新讀取並更新本頁顯示的參數。");
        }

        private void SetFmtTooltip(Control control, string text)
        {
            toolTip1.SetToolTip(control, AddNewLinesForTooltip(text));
        }

        private void CreateFmtTuningGuide()
        {
            fmtTuningGuide = new GroupBox
            {
                Name = "FMT_TuningGuide",
                Text = "固定翼基本調校說明",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(18, 36, 45),
                Padding = new Padding(12, 26, 12, 12),
                TabStop = false
            };

            var guideText = new RichTextBox
            {
                Name = "FMT_TuningGuideText",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = false,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(18, 36, 45),
                ForeColor = Color.Gainsboro,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                TabStop = false,
                Text =
                    "調整原則\n" +
                    "• 先保存原始參數，每次只小幅調整一個項目，並在安全空域驗證。\n" +
                    "• PID 的 P 控制反應速度、I 修正長期偏差、D 抑制快速振盪；數值過高都可能造成不穩定。\n" +
                    "• QuadPlane／VTOL 必須分別驗證垂直起降模式與固定翼模式。\n\n" +
                    "橫滾／俯仰舵機 PID\n" +
                    "控制固定翼姿態反應。若出現快速振盪，先降低 P 或 D；若長時間無法維持目標姿態，再小幅調整 I。\n\n" +
                    "偏航舵機控制\n" +
                    "用於協調轉彎與抑制側滑。調整時觀察機尾擺動、側滑與轉彎協調性。\n\n" +
                    "L1 航跡轉向控制\n" +
                    "反應週期決定轉向積極程度，阻尼決定航跡是否平順。週期過小或阻尼過低可能造成蛇行。\n\n" +
                    "TECS 高度／空速控制\n" +
                    "協調油門與俯仰以控制高度和空速。爬升／下降率應依實際飛行能力設定，避免命令超出飛機性能。\n\n" +
                    "油門、空速與姿態限制\n" +
                    "巡航值應以穩定平飛資料為基準；最低空速需高於失速速度並保留安全裕度。最大滾轉角越大，轉彎負載與失速風險越高。\n\n" +
                    "操作\n" +
                    "修改後按「寫入參數」才會送至飛控；「重新讀取參數」會重新下載飛控參數。將滑鼠停在任一數值欄位可查看個別說明。"
            };

            fmtTuningGuide.Controls.Add(guideText);
            Controls.Add(fmtTuningGuide);
            fmtTuningGuide.BringToFront();
            LayoutFmtTuningGuide();
        }

        private void LayoutFmtTuningGuide()
        {
            if (fmtTuningGuide == null)
                return;

            const int guideLeft = 620;
            fmtTuningGuide.Visible = ClientSize.Width > guideLeft + 100;
            if (!fmtTuningGuide.Visible)
                return;

            fmtTuningGuide.SetBounds(
                guideLeft,
                15,
                Math.Max(360, ClientSize.Width - guideLeft - 16),
                Math.Max(300, ClientSize.Height - 30));
        }

        public void Activate()
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
            {
                Enabled = false;
                return;
            }
            if (MainV2.comPort.MAV.cs.firmware == Firmwares.ArduPlane)
            {
                Enabled = true;
            }
            else
            {
                Enabled = false;
                return;
            }

            startup = true;

            THR_SLEWRATE.setup(0, 0, 1, 0, "THR_SLEWRATE", MainV2.comPort.MAV.param);
            THR_MAX.setup(0, 0, 1, 0, "THR_MAX", MainV2.comPort.MAV.param);
            THR_MIN.setup(0, 0, 1, 0, "THR_MIN", MainV2.comPort.MAV.param);
            TRIM_THROTTLE.setup(0, 0, 1, 0, "TRIM_THROTTLE", MainV2.comPort.MAV.param);

            ARSPD_RATIO.setup(0, 2.5f, 1, 0.005f, "ARSPD_RATIO", MainV2.comPort.MAV.param);
            ARSPD_FBW_MAX.setup(0, 0, 1, 1, new string[] { "AIRSPEED_MAX", "ARSPD_FBW_MAX" }, MainV2.comPort.MAV.param);
            ARSPD_FBW_MIN.setup(0, 0, 1, 1, new string[] { "AIRSPEED_MIN", "ARSPD_FBW_MIN" }, MainV2.comPort.MAV.param);
            TRIM_ARSPD_CM.setup(0, 50, 1, 1, "AIRSPEED_CRUISE", MainV2.comPort.MAV.param);

            LIM_PITCH_MIN.setup(0, 0, 1, 1, "PTCH_LIM_MIN_DEG", MainV2.comPort.MAV.param);
            LIM_PITCH_MAX.setup(0, 0, 1, 1, "PTCH_LIM_MAX_DEG", MainV2.comPort.MAV.param);
            LIM_ROLL_CD.setup(0, 0, 1, 1, "ROLL_LIMIT_DEG", MainV2.comPort.MAV.param);

            KFF_PTCH2THR.setup(0, 0, 1, 0, new string[] { "KFF_THR2PTCH","KFF_PTCH2THR"}, MainV2.comPort.MAV.param);
            KFF_RDDRMIX.setup(0, 0, 1, 0, "KFF_RDDRMIX", MainV2.comPort.MAV.param);

            ENRGY2THR_IMAX.setup(0, 0, 100, 0, "ENRGY2THR_IMAX", MainV2.comPort.MAV.param);
            ENRGY2THR_D.setup(0, 0, 1, 0, "ENRGY2THR_D", MainV2.comPort.MAV.param);
            ENRGY2THR_I.setup(0, 0, 1, 0, "ENRGY2THR_I", MainV2.comPort.MAV.param);
            ENRGY2THR_P.setup(0, 0, 1, 0, "ENRGY2THR_P", MainV2.comPort.MAV.param);

            ALT2PTCH_IMAX.setup(0, 0, 100, 0, "ALT2PTCH_IMAX", MainV2.comPort.MAV.param);
            ALT2PTCH_D.setup(0, 0, 1, 0, "ALT2PTCH_D", MainV2.comPort.MAV.param);
            ALT2PTCH_I.setup(0, 0, 1, 0, "ALT2PTCH_I", MainV2.comPort.MAV.param);
            ALT2PTCH_P.setup(0, 0, 1, 0, "ALT2PTCH_P", MainV2.comPort.MAV.param);

            ARSP2PTCH_IMAX.setup(0, 0, 100, 0, "ARSP2PTCH_IMAX", MainV2.comPort.MAV.param);
            ARSP2PTCH_D.setup(0, 0, 1, 0, "ARSP2PTCH_D", MainV2.comPort.MAV.param);
            ARSP2PTCH_I.setup(0, 0, 1, 0, "ARSP2PTCH_I", MainV2.comPort.MAV.param);
            ARSP2PTCH_P.setup(0, 0, 1, 0, "ARSP2PTCH_P", MainV2.comPort.MAV.param);

            YAW2SRV_IMAX.setup(0, 0, 100, 0, "YAW2SRV_IMAX", MainV2.comPort.MAV.param);
            YAW2SRV_DAMP.setup(0, 0, 1, 0, "YAW2SRV_DAMP", MainV2.comPort.MAV.param);
            YAW2SRV_INT.setup(0, 0, 1, 0, "YAW2SRV_INT", MainV2.comPort.MAV.param);
            YAW2SRV_RLL.setup(0, 0, 1, 0, "YAW2SRV_RLL", MainV2.comPort.MAV.param);

            PTCH2SRV_IMAX.setup(0, 0, 100, 0, new String[] {"PTCH2SRV_IMAX","PTCH_RATE_IMAX"}, MainV2.comPort.MAV.param);
            PTCH2SRV_D.setup(0, 0, 1, 0, new String[] {"PTCH2SRV_D","PTCH_RATE_D"}, MainV2.comPort.MAV.param);
            PTCH2SRV_I.setup(0, 0, 1, 0, new String[] {"PTCH2SRV_I","PTCH_RATE_I"}, MainV2.comPort.MAV.param);
            PTCH2SRV_P.setup(0, 0, 1, 0, new String[] {"PTCH2SRV_P","PTCH_RATE_P"}, MainV2.comPort.MAV.param);

            RLL2SRV_IMAX.setup(0, 0, 100, 0, new String[] {"RLL2SRV_IMAX","RLL_RATE_IMAX"}, MainV2.comPort.MAV.param);
            RLL2SRV_D.setup(0, 0, 1, 0, new String[] {"RLL2SRV_D","RLL_RATE_D"}, MainV2.comPort.MAV.param);
            RLL2SRV_I.setup(0, 0, 1, 0, new String[] {"RLL2SRV_I","RLL_RATE_I"}, MainV2.comPort.MAV.param);
            RLL2SRV_P.setup(0, 0, 1, 0, new String[] {"RLL2SRV_P","RLL_RATE_P"}, MainV2.comPort.MAV.param);

            NAVL1_DAMPING.setup(0, 0, 1, 0, "NAVL1_DAMPING", MainV2.comPort.MAV.param);
            NAVL1_PERIOD.setup(0, 0, 1, 0, "NAVL1_PERIOD", MainV2.comPort.MAV.param);

            TECS_SINK_MAX.setup(0, 0, 1, 0, "TECS_SINK_MAX", MainV2.comPort.MAV.param);
            TECS_TIME_CONST.setup(0, 0, 1, 0, "TECS_TIME_CONST", MainV2.comPort.MAV.param);
            TECS_PTCH_DAMP.setup(0, 0, 1, 0, "TECS_PTCH_DAMP", MainV2.comPort.MAV.param);
            TECS_SINK_MIN.setup(0, 0, 1, 0, "TECS_SINK_MIN", MainV2.comPort.MAV.param);
            TECS_CLMB_MAX.setup(0, 0, 1, 0, "TECS_CLMB_MAX", MainV2.comPort.MAV.param);

            changes.Clear();

            // add tooltips to all controls
            foreach (Control control1 in Controls)
            {
                foreach (Control control2 in control1.Controls)
                {
                    if (control2 is MavlinkNumericUpDown)
                    {
                        var ParamName = ((MavlinkNumericUpDown)control2).ParamName;
                        toolTip1.SetToolTip(control2,
                            ParameterMetaDataRepository.GetParameterMetaData(ParamName,
                                ParameterMetaDataConstants.Description, MainV2.comPort.MAV.cs.firmware.ToString()));
                    }
                    if (control2 is MavlinkComboBox)
                    {
                        var ParamName = ((MavlinkComboBox)control2).ParamName;
                        toolTip1.SetToolTip(control2,
                            ParameterMetaDataRepository.GetParameterMetaData(ParamName,
                                ParameterMetaDataConstants.Description, MainV2.comPort.MAV.cs.firmware.ToString()));
                    }
                }
            }

            // Parameter metadata is normally English. Restore the FMT Traditional Chinese
            // descriptions after metadata loading so the page remains fully localized.
            ApplyFmtTraditionalChineseText();
            ApplyFmtTraditionalChineseTooltips();

            startup = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                BUT_writePIDS_Click(null, null);
                return true;
            }

            return false;
        }

        private static string AddNewLinesForTooltip(string text)
        {
            if (text.Length < maximumSingleLineTooltipLength)
                return text;
            var lineLength = (int)Math.Sqrt(text.Length) * 2;
            var sb = new StringBuilder();
            var currentLinePosition = 0;
            for (var textIndex = 0; textIndex < text.Length; textIndex++)
            {
                // If we have reached the target line length and the next
                // character is whitespace then begin a new line.
                if (currentLinePosition >= lineLength &&
                    char.IsWhiteSpace(text[textIndex]))
                {
                    sb.Append(Environment.NewLine);
                    currentLinePosition = 0;
                }
                // If we have just started a new line, skip all the whitespace.
                if (currentLinePosition == 0)
                    while (textIndex < text.Length && char.IsWhiteSpace(text[textIndex]))
                        textIndex++;
                // Append the next character.
                if (textIndex < text.Length) sb.Append(text[textIndex]);
                currentLinePosition++;
            }
            return sb.ToString();
        }

        private void ComboBox_Validated(object sender, EventArgs e)
        {
            EEPROM_View_float_TextChanged(sender, e);
        }

        private void Configuration_Validating(object sender, CancelEventArgs e)
        {
            EEPROM_View_float_TextChanged(sender, e);
        }

        internal void EEPROM_View_float_TextChanged(object sender, EventArgs e)
        {
            float value = 0;
            var name = ((Control)sender).Name;

            // do domainupdown state check
            try
            {
                if (sender.GetType() == typeof(MavlinkNumericUpDown))
                {
                    value = ((MAVLinkParamChanged)e).value;
                    changes[name] = value;
                }
                else if (sender.GetType() == typeof(MavlinkComboBox))
                {
                    value = ((MAVLinkParamChanged)e).value;
                    changes[name] = value;
                }
                ((Control)sender).BackColor = Color.Green;
            }
            catch (Exception)
            {
                ((Control)sender).BackColor = Color.Red;
            }
        }

        private void BUT_writePIDS_Click(object sender, EventArgs e)
        {
            var temp = (Hashtable)changes.Clone();

            foreach (string value in temp.Keys)
            {
                try
                {
                    if ((float)changes[value] > (float)MainV2.comPort.MAV.param[value] * 2.0f)
                        if (
                            CustomMessageBox.Show(value + " has more than doubled the last input. Are you sure?",
                                "Large Value", MessageBoxButtons.YesNo) == (int)DialogResult.No)
                        {
                            try
                            {
                                // set control as well
                                var textControls = Controls.Find(value, true);
                                if (textControls.Length > 0)
                                {
                                    // restore old value
                                    textControls[0].Text = MainV2.comPort.MAV.param[value].Value.ToString();
                                    textControls[0].BackColor = Color.FromArgb(0x43, 0x44, 0x45);
                                }
                            }
                            catch
                            {
                            }
                            return;
                        }

                    if (MainV2.comPort.BaseStream == null || !MainV2.comPort.BaseStream.IsOpen)
                    {
                        CustomMessageBox.Show("Your are not connected", Strings.ERROR);
                        return;
                    }

                    MainV2.comPort.setParam(value, (float)changes[value]);

                    changes.Remove(value);

                    try
                    {
                        // set control as well
                        var textControls = Controls.Find(value, true);
                        if (textControls.Length > 0)
                        {
                            textControls[0].BackColor = Color.FromArgb(0x43, 0x44, 0x45);
                        }
                    }
                    catch
                    {
                    }
                }
                catch
                {
                    CustomMessageBox.Show(string.Format(Strings.ErrorSetValueFailed, value), Strings.ERROR);
                }
            }
        }

        /// <summary>
        ///     Handles the Click event of the BUT_rerequestparams control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs" /> instance containing the event data.</param>
        protected void BUT_rerequestparams_Click(object sender, EventArgs e)
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
                return;

            ((Control)sender).Enabled = false;

            try
            {
                MainV2.comPort.getParamList();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error: getting param list " + ex, Strings.ERROR);
            }


            ((Control)sender).Enabled = true;

            Activate();
        }

        private void BUT_refreshpart_Click(object sender, EventArgs e)
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
                return;

            ((Control)sender).Enabled = false;


            updateparam(this);

            ((Control)sender).Enabled = true;


            Activate();
        }

        private void updateparam(Control parentctl)
        {
            foreach (Control ctl in parentctl.Controls)
            {
                if (typeof(NumericUpDown) == ctl.GetType() || typeof(ComboBox) == ctl.GetType())
                {
                    try
                    {
                        MainV2.comPort.GetParam(ctl.Name);
                    }
                    catch
                    {
                    }
                }

                if (ctl.Controls.Count > 0)
                {
                    updateparam(ctl);
                }
            }
        }

        private void numeric_ValueUpdated(object sender, EventArgs e)
        {
            EEPROM_View_float_TextChanged(sender, e);
        }
    }
}
