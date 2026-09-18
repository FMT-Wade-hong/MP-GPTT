using log4net;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using org.mariuszgromada.math.mxparser;
using System.Runtime.CompilerServices;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigRawParams : MyUserControl, IActivate, IDeactivate
    {
        // from http://stackoverflow.com/questions/2512781/winforms-big-paragraph-tooltip/2512895#2512895
        private const int maximumSingleLineTooltipLength = 50;

        private static readonly ILog log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Hashtable tooltips = new Hashtable();
        // Changes made to the params between writing to the copter
        private readonly Hashtable _changes = new Hashtable();
        private static List<GitHubContent.FileInfo> paramfiles;
        private int fmtPresetLookupPending;
        private const string BitmaskButtonText = "設定位元遮罩";
        // ?
        internal static bool startup = true;
        internal static List<DataGridViewRow> rowlist = new List<DataGridViewRow>();

        // Used by Param Tree to filter by prefix
        private string filterPrefix = "";

        private NaturalStringComparer naturalsorter = new NaturalStringComparer();
        private bool fmtEditingEnabled;

        public ConfigRawParams()
        {
            InitializeComponent();
            if (CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                Desc.HeaderText = "功能說明";
                Options.HeaderText = "範圍／選項";
            }
            Params.CellClick += Params_FmtCellClick;
        }

        internal void ApplyFmtReadableTheme()
        {
            ThemeManager.ApplyThemeTo(this);

            BackColor = ThemeManager.BGColor;
            ForeColor = ThemeManager.TextColor;
            splitContainer1.BackColor = ThemeManager.BGColor;
            splitContainer1.Panel1.BackColor = ThemeManager.BGColor;
            splitContainer1.Panel2.BackColor = ThemeManager.BGColor;

            treeView1.BackColor = ThemeManager.ControlBGColor;
            treeView1.ForeColor = ThemeManager.TextColor;
            treeView1.LineColor = ThemeManager.TextColor;

            var selectionColor = ThemeManager.BannerColor1.IsEmpty
                ? Color.FromArgb(11, 111, 157)
                : ThemeManager.BannerColor1;
            var normalStyle = new DataGridViewCellStyle(Params.DefaultCellStyle)
            {
                BackColor = ThemeManager.ControlBGColor,
                ForeColor = ThemeManager.TextColor,
                SelectionBackColor = selectionColor,
                SelectionForeColor = Color.White
            };
            Params.DefaultCellStyle = normalStyle;
            Params.RowsDefaultCellStyle = new DataGridViewCellStyle(normalStyle);

            var alternatingStyle = new DataGridViewCellStyle(normalStyle)
            {
                BackColor = ThemeManager.BGColor
            };
            Params.AlternatingRowsDefaultCellStyle = alternatingStyle;

            var headerStyle = new DataGridViewCellStyle(Params.ColumnHeadersDefaultCellStyle)
            {
                BackColor = ThemeManager.BGColor,
                ForeColor = ThemeManager.TextColor,
                SelectionBackColor = selectionColor,
                SelectionForeColor = Color.White
            };
            Params.EnableHeadersVisualStyles = false;
            Params.BackgroundColor = ThemeManager.BGColor;
            Params.GridColor = ControlPaint.Light(ThemeManager.ControlBGColor, 0.25f);
            Params.ColumnHeadersDefaultCellStyle = headerStyle;
            Params.RowHeadersDefaultCellStyle = new DataGridViewCellStyle(headerStyle);

            treeView1.Invalidate();
            Params.Invalidate();
        }

        internal void EnableFmtEditing()
        {
            // The first activation builds the shared row cache while startup is true.
            // Complete that transition before accepting the user's first edit. Password
            // validation belongs to the parent Parameter Settings page, not this grid.
            startup = false;
            Params.ReadOnly = false;
            Value.ReadOnly = false;
            Params.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            // MAVLinkInterface.ReadOnly is also used by a few connection modes and was
            // incorrectly leaving a normally connected vehicle's value cells locked.
            fmtEditingEnabled = MainV2.comPort.BaseStream != null && MainV2.comPort.BaseStream.IsOpen;
            foreach (DataGridViewRow row in Params.Rows)
                row.Cells[Value.Index].ReadOnly = !fmtEditingEnabled;
            Params.Enabled = fmtEditingEnabled;
            BUT_writePIDS.Enabled = fmtEditingEnabled;
            Params.Focus();
        }

        private void Params_FmtCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (!fmtEditingEnabled || e.RowIndex < 0 || e.ColumnIndex != Value.Index)
                return;

            Params.CurrentCell = Params[e.ColumnIndex, e.RowIndex];
            Params.BeginEdit(true);
        }

        public void Activate()
        {
            if ((rowlist.Count == 0) || (!Settings.Instance.GetBoolean("SlowMachine", false))) startup = true;
            //If we connected to another vehicle the do a full refresh
            if (rowlist.Count != MainV2.comPort.MAV.param.Count()) startup = true;

            _changes.Clear();

            BUT_writePIDS.Enabled = MainV2.comPort.BaseStream.IsOpen;
            BUT_rerequestparams.Enabled = MainV2.comPort.BaseStream.IsOpen;
            BUT_reset_params.Enabled = MainV2.comPort.BaseStream.IsOpen;
            BUT_commitToFlash.Visible = MainV2.DisplayConfiguration.displayParamCommitButton;
            BUT_refreshTable.Visible = Settings.Instance.GetBoolean("SlowMachine", false);

            // Bundled presets must not wait for GitHub (VPN/offline connections may stall it).
            BindFramePresets(paramfiles);
            if (paramfiles == null && Interlocked.CompareExchange(ref fmtPresetLookupPending, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(updatedefaultlist);

            Params.Enabled = false;

            foreach (DataGridViewColumn col in Params.Columns)
            {
                // Don't need to size a fill column
                if (col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) continue;

                // Don't need to size a column that can't be resized
                if (col.Resizable == DataGridViewTriState.False) continue;

                if (!String.IsNullOrEmpty(Settings.Instance["rawparam_" + col.Name + "_width"]))
                {
                    col.Width = (int)Math.Max(5, Settings.Instance.GetInt32("rawparam_" + col.Name + "_width"));
                    log.InfoFormat("{0} to {1}", col.Name, col.Width);
                }
            }
            splitContainer1.SplitterDistance = Settings.Instance.GetInt32("rawparam_splitterdistance", 180);
            splitContainer1.Panel1Collapsed = Settings.Instance.GetBoolean("rawparam_panel1collapsed", false);
            but_collapse.Text = splitContainer1.Panel1Collapsed ? ">" : "<";

            processToScreen();

            Params.Enabled = true;

            Common.MessageShowAgain(Strings.RawParamWarning, Strings.RawParamWarningi);

            startup = false;

            txt_search.Focus();
        }

        public void Deactivate()
        {
            fmtEditingEnabled = false;
            Params.EndEdit();
            Params.Enabled = false;
            foreach (DataGridViewColumn col in Params.Columns)
            {
                // Don't need to save the width of a fill column
                if (col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) continue;

                // Don't need to save the width of a column that can't be resized
                if (col.Resizable == DataGridViewTriState.False) continue;

                Settings.Instance["rawparam_" + col.Name + "_width"] = col.Width.ToString("0", CultureInfo.InvariantCulture);
            }

            Settings.Instance["rawparam_splitterdistance"] = splitContainer1.SplitterDistance.ToString();
            Settings.Instance["rawparam_panel1collapsed"] = splitContainer1.Panel1Collapsed.ToString();
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

        private void BUT_load_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                AddExtension = true,
                DefaultExt = ".param",
                RestoreDirectory = true,
                Filter = ParamFile.FileMask
            })
            {
                var dr = ofd.ShowDialog();

                if (dr == DialogResult.OK)
                {
                    loadparamsfromfile(ofd.FileName, !MainV2.comPort.BaseStream.IsOpen);

                    if (!MainV2.comPort.BaseStream.IsOpen)
                        Activate();
                }
            }
        }

        private void loadparamsfromfile(string fn, bool offline = false)
        {
            var param2 = ParamFile.loadParamFile(fn);

            var loaded = 0;
            var missed = 0;
            List<string> missing = new List<string>();

            foreach (string name in param2.Keys)
            {
                var set = false;
                var value = param2[name].ToString();
                // set param table as well
                foreach (DataGridViewRow row in Params.Rows)
                {
                    if (name == "SYSID_SW_MREV")
                        continue;
                    if (name == "WP_TOTAL")
                        continue;
                    if (name == "CMD_TOTAL")
                        continue;
                    if (name == "FENCE_TOTAL")
                        continue;
                    if (name == "SYS_NUM_RESETS")
                        continue;
                    if (name == "ARSPD_OFFSET")
                        continue;
                    if (name == "GND_ABS_PRESS")
                        continue;
                    if (name == "GND_TEMP")
                        continue;
                    if (name == "CMD_INDEX")
                        continue;
                    if (name == "LOG_LASTFILE")
                        continue;
                    if (name == "FORMAT_VERSION")
                        continue;
                    if (row.Cells[Command.Index].Value != null && row.Cells[Command.Index].Value?.ToString() == name)
                    {
                        set = true;
                        if (row.Cells[Value.Index].Value.ToString() != value)
                            row.Cells[Value.Index].Value = value;
                        break;
                    }
                }

                if (offline && !set)
                {
                    set = true;
                    MainV2.comPort.MAV.param.Add(new MAVLink.MAVLinkParam(name, double.Parse(value),
                        MAVLink.MAV_PARAM_TYPE.REAL32));
                }

                if (set)
                {
                    loaded++;
                }
                else
                {
                    missed++;
                    missing.Add(name);
                }
            }

            if (missed > 0)
            {
                string list = "";
                foreach (var item in missing)
                {
                    list += item + " ";
                }
                CustomMessageBox.Show("Missing " + missed + " params\n" + list, "No matching Params", MessageBoxButtons.OK);
            }
        }

        private void BUT_save_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = ".param",
                RestoreDirectory = true,
                Filter = "Param List|*.param;*.parm"
            })
            {
                var dr = sfd.ShowDialog();
                if (dr == DialogResult.OK)
                {
                    var data = new Hashtable();
                    foreach (DataGridViewRow row in Params.Rows)
                    {
                        try
                        {
                            var value = double.Parse(row.Cells[Value.Index].Value.ToString());

                            data[row.Cells[Command.Index].Value.ToString()] = value;
                        }
                        catch (Exception)
                        {
                            CustomMessageBox.Show(Strings.InvalidNumberEntered + " " + row.Cells[Command.Index].Value);
                        }
                    }

                    ParamFile.SaveParamFile(sfd.FileName, data);
                }
            }
        }

        private void BUT_writePIDS_Click(object sender, EventArgs e)
        {
            // sort with enable at the bottom - this ensures params are set before the function is disabled
            var temp = _changes.Keys.Cast<string>().ToList();

            temp.SortENABLE();

            bool enable = temp.Any(a => a.EndsWith("_ENABLE"));

            int error = 0;
            bool reboot = false;
            int maxdisplay = 20;

            if (temp.Count > 0 && temp.Count <= maxdisplay)
            {
                // List to track successfully saved parameters
                List<string> savedParams = new List<string>();

                foreach (string value in temp)
                {
                    if (MainV2.comPort.BaseStream == null || !MainV2.comPort.BaseStream.IsOpen)
                    {
                        CustomMessageBox.Show("You are not connected", Strings.ERROR);
                        return;
                    }

                    // Get the previous value of the param to display in 'param change info'
                    // (a better way would be to get the value somewhere from inside the code, insted of recieving it over mavlink)
                    string previousValue = MainV2.comPort.MAV.param[value].ToString();
                    // new value of param
                    double newValue = (double)_changes[value];

                    // Add the parameter, previous and new values to the list for 'param change info'
                    // remember, the 'value' here is key of param, while prev and new are actual values of param
                    savedParams.Add($"{value}: {previousValue} -> {newValue}");
                }

                // Join the saved parameters list to a string
                string savedParamsMessage = string.Join(Environment.NewLine, savedParams);

                // Ask the user for confirmation showing detailed changes
                if (CustomMessageBox.Show($"You are about to change {savedParams.Count} parameters. Please review the changes below:\n\n{savedParamsMessage}\n\nDo you want to proceed?", "Confirm Parameter Changes",
        CustomMessageBox.MessageBoxButtons.YesNo, CustomMessageBox.MessageBoxIcon.Information) !=
    CustomMessageBox.DialogResult.Yes)
                    return;
            }
            else if (temp.Count > maxdisplay)
            {
                // Ask the user for confirmation without listing individual changes
                if (CustomMessageBox.Show($"You are about to change {temp.Count} parameters. Are you sure you want to proceed?", "Confirm Parameter Changes",
            CustomMessageBox.MessageBoxButtons.YesNo, CustomMessageBox.MessageBoxIcon.Information) !=
        CustomMessageBox.DialogResult.Yes)
                    return;
            }


            foreach (string value in temp)
            {
                try
                {
                    if (MainV2.comPort.BaseStream == null || !MainV2.comPort.BaseStream.IsOpen)
                    {
                        CustomMessageBox.Show("Your are not connected", Strings.ERROR);
                        return;
                    }

                    MainV2.comPort.setParam(value, (double)_changes[value]);
                    //check if reboot required
                    if (ParameterMetaDataRepository.GetParameterRebootRequired(value, MainV2.comPort.MAV.cs.firmware.ToString()))
                    {
                        reboot = true;
                    }
                    try
                    {
                        // set control as well
                        var textControls = Controls.Find(value, true);
                        if (textControls.Length > 0)
                        {
                            ThemeManager.ApplyThemeTo(textControls[0]);
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        // set param table as well
                        foreach (DataGridViewRow row in Params.Rows)
                        {
                            if (row.Cells[Command.Index].Value.ToString() == value)
                            {
                                row.Cells[Value.Index].Style.BackColor = ThemeManager.ControlBGColor;
                                _changes.Remove(value);
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                catch
                {
                    error++;
                    CustomMessageBox.Show("Set " + value + " Failed");
                }
            }

            if (error > 0)
                CustomMessageBox.Show("Not all parameters successfully saved.", "Saved");
            else if (temp.Count>0)
                CustomMessageBox.Show($"{temp.Count} parameters successfully saved.", "Saved");
            else
                CustomMessageBox.Show("No parameters were changed.", "No changes");

            //Check if reboot is required
            if (reboot)
            {
               CustomMessageBox.Show("Reboot is required for some parameters to take effect.", "Reboot Required");
            }

            if (MainV2.comPort.MAV.param.TotalReceived != MainV2.comPort.MAV.param.TotalReported )
            {
                if (MainV2.comPort.MAV.cs.armed)
                {
                    CustomMessageBox.Show("The number of available parameters changed, until full param refresh is done, some parameters will not be available.", "Params");
                    //Hack the number of reported params to keep params list available
                    MainV2.comPort.MAV.param.TotalReported = MainV2.comPort.MAV.param.TotalReceived;
                }
                else
                {
                    CustomMessageBox.Show("The number of available parameters changed. A full param refresh will be done to show all params.", "Params");
                    //Click on refresh button
                    BUT_rerequestparams_Click(BUT_rerequestparams, null);
                }
            }
        }

        private void BUT_compare_Click(object sender, EventArgs e)
        {
            var param2 = new Dictionary<string, double>();

            using (var ofd = new OpenFileDialog
            {
                AddExtension = true,
                DefaultExt = ".param",
                RestoreDirectory = true,
                Filter = ParamFile.FileMask
            })
            {
                var dr = ofd.ShowDialog();
                if (dr == DialogResult.OK)
                {
                    param2 = ParamFile.loadParamFile(ofd.FileName);

                    Form paramCompareForm = new ParamCompare(Params, MainV2.comPort.MAV.param, param2);

                    ThemeManager.ApplyThemeTo(paramCompareForm);
                    paramCompareForm.ShowDialog();
                }
            }
        }

        private void BUT_rerequestparams_Click(object sender, EventArgs e)
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
                return;

            if (!MainV2.comPort.MAV.cs.armed || DialogResult.OK ==
                Common.MessageShowAgain("Refresh Params", Strings.WarningUpdateParamList, true))
            {
                ((Control)sender).Enabled = false;

                try
                {
                    MainV2.comPort.getParamList();
                }
                catch (Exception ex)
                {
                    log.Error("Exception getting param list", ex);
                    CustomMessageBox.Show(Strings.ErrorReceivingParams, Strings.ERROR);
                }


                ((Control)sender).Enabled = true;

                startup = true;

                processToScreen();

                FilterTimerOnElapsed(null, null);

                startup = false;
            }
        }

        private void Params_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex == -1 || startup || e.ColumnIndex != Value.Index)
                return;
            try
            {
                if (Params[Command.Index, e.RowIndex].Value.ToString().EndsWith("_REV") &&
                    (Params[Command.Index, e.RowIndex].Value.ToString().StartsWith("RC") ||
                     Params[Command.Index, e.RowIndex].Value.ToString().StartsWith("HS")))
                {
                    if (Params[e.ColumnIndex, e.RowIndex].Value.ToString() == "0")
                        Params[e.ColumnIndex, e.RowIndex].Value = "-1";
                }

                double min = 0;
                double max = 0;

                var value = Params[e.ColumnIndex, e.RowIndex].Value.ToString();
                value = value.Replace(',', '.');

                var newvalue = (double) new Expression(value).calculate();
                if (double.IsNaN(newvalue) || double.IsInfinity(newvalue))
                {
                    throw new Exception();
                }

                var readonly1 = ParameterMetaDataRepository.GetParameterMetaData(
                    Params[Command.Index, e.RowIndex].Value.ToString(),
                    ParameterMetaDataConstants.ReadOnly, MainV2.comPort.MAV.cs.firmware.ToString());
                if (!String.IsNullOrEmpty(readonly1))
                {
                    var readonly2 = bool.Parse(readonly1);
                    if (readonly2)
                    {
                        CustomMessageBox.Show(
                            Params[Command.Index, e.RowIndex].Value +
                            " is marked as ReadOnly, and will not be changed", "ReadOnly",
                            MessageBoxButtons.OK);
                        Params.CellValueChanged -= Params_CellValueChanged;
                        Params[e.ColumnIndex, e.RowIndex].Value = cellEditValue;
                        Params.CellValueChanged += Params_CellValueChanged;
                        return;
                    }
                }

                if (ParameterMetaDataRepository.GetParameterRange(Params[Command.Index, e.RowIndex].Value.ToString(),
                    ref min, ref max, MainV2.comPort.MAV.cs.firmware.ToString()))
                {
                    if (newvalue > max || newvalue < min)
                    {
                        if (
                            CustomMessageBox.Show(
                                Params[Command.Index, e.RowIndex].Value +
                                " value is out of range. Do you want to continue?", "Out of range",
                                MessageBoxButtons.YesNo) == (int)DialogResult.No)
                        {
                            Params.CellValueChanged -= Params_CellValueChanged;
                            Params[e.ColumnIndex, e.RowIndex].Value = cellEditValue;
                            Params.CellValueChanged += Params_CellValueChanged;
                            return;
                        }
                    }
                }

                Params[e.ColumnIndex, e.RowIndex].Style.BackColor = Color.Green;
                log.InfoFormat("Queue change {0} = {1} ({2})", Params[Command.Index, e.RowIndex].Value, Params[e.ColumnIndex, e.RowIndex].Value, newvalue);
                _changes[Params[Command.Index, e.RowIndex].Value] = newvalue;

                Params.CellValueChanged -= Params_CellValueChanged;
                Params[e.ColumnIndex, e.RowIndex].Value = newvalue.ToString();
                Params.CellValueChanged += Params_CellValueChanged;
            }
            catch (Exception)
            {
                Params[e.ColumnIndex, e.RowIndex].Style.BackColor = Color.Red;
            }


            Params.Focus();
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

        internal void processToScreen()
        {
            toolTip1.RemoveAll();
            Params.Rows.Clear();
            log.Info("processToScreen");

            var list = new List<string>();

            // process hashdefines and update display
            // But only if startup is true, otherwise we assume that rowlist is valid and just put it back
            // to the gridview

            if (startup)
            {
                foreach (string item in MainV2.comPort.MAV.param.Keys)
                    list.Add(item);

                rowlist.Clear();

                bool has_defaults = false;

                Parallel.ForEach(list, value =>
                {
                    if (value == null || value == "")
                        return;

                    var row = new DataGridViewRow() { Height = 36 };
                    lock (rowlist)
                        rowlist.Add(row);
                    row.CreateCells(Params);
                    row.Cells[Command.Index].Value = value;
                    row.Cells[Value.Index].Value = MainV2.comPort.MAV.param[value].ToString();
                    var fav_params = Settings.Instance.GetList("fav_params");
                    row.Cells[Fav.Index].Value = fav_params.Contains(value);

                    if (MainV2.comPort.MAV.param[value].default_value.HasValue) {
                        has_defaults = true;
                        row.Cells[Default_value.Index].Value = MainV2.comPort.MAV.param[value].default_value_to_string();
                    } else {
                        row.Cells[Default_value.Index].Value = "NaN";
                    }
                    try
                    {
                        var metaDataDescription = ParameterMetaDataRepository.GetParameterMetaData(value,
                            ParameterMetaDataConstants.Description, MainV2.comPort.MAV.cs.firmware.ToString());
                        if (!string.IsNullOrEmpty(metaDataDescription))
                        {
                            var tooltipDescription = AddNewLinesForTooltip(
                                GetFmtTooltipDescription(value, metaDataDescription));

                            // Localize display only; parameter identifiers and wire values stay unchanged.
                            foreach (DataGridViewCell cell in row.Cells)
                                cell.ToolTipText = tooltipDescription;

                            var range = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Range, MainV2.comPort.MAV.cs.firmware.ToString());
                            var options = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Values, MainV2.comPort.MAV.cs.firmware.ToString());
                            var units = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Units, MainV2.comPort.MAV.cs.firmware.ToString());

                            row.Cells[Units.Index].Value = units;
                            row.Cells[Options.Index].Value = (range + "\n" + LocalizeFmtOptions(options).Replace(",", "\n")).Trim();
                            if (options.Length > 0)
                                row.Cells[Options.Index].ToolTipText = tooltipDescription + "\r\n原始選項：\r\n" + options;
                            int N = options.Count(c => c.Equals(','));
                            if (N > 50)
                            {
                                int columns = (N - 1) / 50 + 1;
                                StringBuilder ans = new StringBuilder();
                                var opts = options.Split(',');
                                int i = 0;
                                while(true)
                                {
                                    for(int j=0; j<columns; j++)
                                    {
                                        ans.Append(opts[i] + ", ");
                                        i++;
                                        if (i >= N) break;
                                    }
                                    if (i >= N) break;
                                    ans.Append("\n");
                                }
                                row.Cells[Options.Index].ToolTipText = tooltipDescription + "\r\n原始選項：\r\n" + options;
                            }
                            row.Cells[Desc.Index].Value = GetFmtDisplayDescription(value, metaDataDescription);
                            row.Cells[Desc.Index].ToolTipText = tooltipDescription;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }

                    try
                    {
                        var bitmask = ParameterMetaDataRepository.GetParameterBitMaskInt(value,
                            MainV2.comPort.MAV.cs.firmware.ToString());
                        if (bitmask.Count > 0)
                        {
                            var tooltip = row.Cells[Options.Index].ToolTipText + "\r\n原始位元選項：\r\n" +
                                string.Join("\r\n", bitmask.Select(item => item.Key + ":" + item.Value));
                            row.Cells[Options.Index] = new DataGridViewButtonCell
                            {
                                Value = BitmaskButtonText,
                                FlatStyle = FlatStyle.Flat,
                                ToolTipText = tooltip
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                });

                Default_value.Visible = has_defaults;
                chk_none_default.Visible = has_defaults;
            }
            //update values in rowlist
            if (!startup)
            {
                foreach (DataGridViewRow r in rowlist)
                {
                    r.Cells[Value.Index].Value = MainV2.comPort.MAV.param[r.Cells[Command.Index].Value.ToString()].ToString();
                }
            }


            log.Info("about to add all");

            Params.Visible = false;

            Params.Rows.AddRange(rowlist.ToArray());

            log.Info("about to sort");

            Params.SortCompare += OnParamsOnSortCompare;

            Params.Sort(Params.Columns[Command.Index], ListSortDirection.Ascending);

            Params.Visible = true;

            if (splitContainer1.Panel1Collapsed == false)
            {
                BuildTree();
            }

            log.Info("Done");
        }

        private void BuildTree()
        {
            treeView1.Nodes.Clear();
            var currentNode = treeView1.Nodes.Add("All");
            string currentPrefix = "";

            // Get command names from the gridview
            List<string> commands = new List<string>();
            foreach (DataGridViewRow row in Params.Rows)
            {
                // The protected parameter page can activate while the grid is still
                // creating its placeholder row.  That row has no command value and
                // must not be treated as a real parameter.
                if (row == null || row.IsNewRow || Command.Index < 0 ||
                    Command.Index >= row.Cells.Count || row.Cells[Command.Index].Value == null)
                    continue;

                string command = row.Cells[Command.Index].Value.ToString();
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                if (!commands.Contains(command))
                {
                    commands.Add(command);
                }
            }

            // Sort them again (because of the favorites, they may be out of order)
            commands.Sort(naturalsorter);

            for (int i = 0; i < commands.Count; i++)
            {
                string param = commands[i];

                // While param does not start with currentPrefix, step up a layer in the tree
                while (!param.StartsWith(currentPrefix) && currentNode?.Parent != null)
                {
                    currentPrefix = currentPrefix.RemoveFromEnd(currentNode.Text.Split('_').Last() + "_");
                    currentNode = currentNode.Parent;
                }

                if (currentNode == null)
                {
                    currentNode = treeView1.Nodes.Count > 0 ? treeView1.Nodes[0] : treeView1.Nodes.Add("All");
                    currentPrefix = "";
                }

                // If this is the last parameter, add it
                if (i == commands.Count - 1)
                {
                    currentNode.Nodes.Add(param);
                    break;
                }

                string next_param = commands[i + 1];
                // While the next parameter has a common prefix with this, add branch nodes
                string nodeToAdd = param.Substring(currentPrefix.Length).Split('_')[0] + "_";
                while (nodeToAdd.Length > 1 // While the currentPrefix is smaller than param
                    && param.StartsWith(currentPrefix + nodeToAdd) // And while this parameter starts with currentPrefix+nodeToAdd (needed for edge case where next_param starts with the full name of this param; see Q_PLT_Y_RATE and Q_PLT_Y_RATE_TC)
                    && next_param.StartsWith(currentPrefix + nodeToAdd)) // And the next parameter also starts with currentPrefix
                {
                    currentPrefix += nodeToAdd;
                    currentNode = currentNode.Nodes.Add(currentPrefix.Substring(0, currentPrefix.Length - 1));
                    nodeToAdd = param.Substring(currentPrefix.Length).Split('_')[0] + "_";
                }
                currentNode.Nodes.Add(param);
            }
            // TopNode can temporarily be null during the first activation/layout pass.
            // Expanding is cosmetic, so defer safely instead of crashing the page.
            treeView1.TopNode?.Expand();
        }

        private static string GetFmtTooltipDescription(string parameterName, string englishDescription)
        {
            var translated = GetFmtDisplayDescription(parameterName, englishDescription);
            return translated == englishDescription ? englishDescription : translated + "\r\n原文：" + englishDescription;
        }

        private static string GetFmtDisplayDescription(string parameterName, string englishDescription)
        {
            if (!CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return englishDescription;

            string exact;
            if (FmtTraditionalChineseParameterDescriptions.TryGetValue(parameterName, out exact))
                return exact;

            if (FmtDescriptionTranslations.TryGetValue(englishDescription ?? string.Empty, out exact))
                return exact;

            return MissionPlanner.FMT.FmtParameterDrafts.Translate(englishDescription);
        }

        private static string LocalizeFmtOption(string label)
        {
            if (!CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return label;
            string translated;
            return FmtOptionTranslations.TryGetValue((label ?? string.Empty).Trim(), out translated)
                ? translated : MissionPlanner.FMT.FmtParameterDrafts.Translate(label);
        }

        private static string LocalizeFmtOptions(string options)
        {
            return string.Join(",", (options ?? string.Empty).Split(',').Select(option =>
            {
                var separator = option.IndexOf(':');
                return separator < 0 ? option : option.Substring(0, separator + 1) + LocalizeFmtOption(option.Substring(separator + 1));
            }));
        }

        private static readonly Dictionary<string, string> FmtOptionTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "On ground and after first climb yaw reset", "地面及首次爬升偏航重設後" },
            { "JammingExpected", "預期 GPS 干擾" },
            { "ManualLaneSwitching", "手動通道切換（危險：停用自動切換）" },
            { "Optflow may use terrain alt", "光流可使用地形高度" },
            { "AGL KF for optflow scaling", "以離地高度卡爾曼濾波器換算光流" },
            { "AlignExtNavPosWhenUsingOptFlow", "使用光流時對準外部導航位置" },
            { "UsePerCoreEKFSources", "各 EKF 核心使用獨立來源" },
            { "EnableGPSAffinity", "啟用 GPS 對應" },
            { "EnableBaroAffinity", "啟用氣壓計對應" },
            { "EnableCompassAffinity", "啟用羅盤對應" },
            { "EnableAirspeedAffinity", "啟用空速計對應" },
            { "Use Baro", "使用氣壓計" },
            { "Use Range Finder", "使用測距儀" },
            { "Use GPS", "使用 GPS" },
            { "Use Range Beacon", "使用測距信標" },
            { "Always", "始終使用" },
            { "WhenNoYawSensor", "無偏航感測器時" },
            { "Navigation", "導航" },
            { "Terrain", "地形" },
            { "NSats", "衛星數量" },
            { "HDoP", "水平精度衰減因子" },
            { "speed error", "速度誤差" },
            { "position error", "位置誤差" },
            { "yaw error", "偏航誤差" },
            { "pos drift", "位置漂移" },
            { "vert speed", "垂直速度" },
            { "horiz speed", "水平速度" },
            { "GPS 3D Vel and 2D Pos", "GPS 三維速度與二維位置" },
            { "GPS 2D vel and 2D pos", "GPS 二維速度與二維位置" },
            { "GPS 2D pos", "GPS 二維位置" },
            { "No GPS", "不使用 GPS" },
            { "When flying", "飛行時" },
            { "When manoeuvring", "機動時" },
            { "Never", "不使用" },
            { "After first climb yaw reset", "首次爬升偏航重設後" },
            { "Use external yaw sensor (Deprecated in 4.1+ see EK3_SRCn_YAW)", "使用外部偏航感測器（4.1 以上已棄用，見 EK3_SRCn_YAW）" },
            { "External yaw sensor with compass fallback (Deprecated in 4.1+ see EK3_SRCn_YAW)", "外部偏航感測器，失效時回退羅盤（4.1 以上已棄用，見 EK3_SRCn_YAW）" },
            { "Correct when using Baro height", "使用氣壓高度時修正" },
            { "Correct when using range finder height", "使用測距高度時修正" },
            { "Apply corrections to local position", "將修正套用至局部位置" },
            { "FuseAllVelocities", "融合所有速度來源" },
            { "ExternalNav", "外部導航" },
            { "Baro", "氣壓計" },
            { "WheelEncoder", "輪編碼器" },
            { "Compass", "羅盤" },
            { "GPS with Compass Fallback", "GPS，失效時回退羅盤" },
            { "GPS", "衛星定位（GPS）" },
            { "GSF", "高斯和濾波器（GSF）" },
            { "FirstEKF", "第 1 個 EKF 核心" },
            { "FirstIMU", "第 1 個 IMU" },
            { "SecondEKF", "第 2 個 EKF 核心" },
            { "SecondIMU", "第 2 個 IMU" },
            { "ThirdEKF", "第 3 個 EKF 核心" },
            { "ThirdIMU", "第 3 個 IMU" },
            { "FourthEKF", "第 4 個 EKF 核心" },
            { "FourthIMU", "第 4 個 IMU" },
            { "FifthEKF", "第 5 個 EKF 核心" },
            { "FifthIMU", "第 5 個 IMU" },
            { "SixthEKF", "第 6 個 EKF 核心" },
            { "SixthIMU", "第 6 個 IMU" },
            { "Disabled", "停用" }, { "Enabled", "啟用" }, { "Disable", "停用" }, { "Enable", "啟用" },
            { "None", "無" }, { "Default", "預設" }, { "Low", "低" }, { "High", "高" },
            { "Normal", "一般" }, { "Reversed", "反向" }, { "No", "否" }, { "Yes", "是" },
            { "All", "全部" }, { "Auto", "自動" }, { "Manual", "手動" }, { "Land", "降落" },
            { "RTL", "返航" }, { "SmartRTL", "智慧返航" }, { "Loiter", "定點盤旋" },
            { "Stabilize", "自穩" }, { "AltHold", "定高" }, { "Guided", "導引" }, { "Acro", "特技" },
            { "Brake", "煞停" }, { "PosHold", "位置保持" }, { "Circle", "繞圈" },
            { "Disabled/NoAction", "停用／不動作" }, { "SmartRTL or RTL", "智慧返航或返航" },
            { "SmartRTL or Land", "智慧返航或降落" }, { "Rangefinder", "測距儀" },
            { "Beacon", "定位信標" }, { "ESC Telemetry", "電調遙測" }, { "OpticalFlow", "光流" },
            { "NMEA Output", "NMEA 輸出" }, { "WindVane", "風向計" }, { "RCIN", "遙控輸入" },
            { "Scripting", "腳本" }, { "Generator", "發電機" }, { "Winch", "絞盤" },
            { "AirSpeed", "空速" }, { "MAVLink High Latency", "MAVLink 高延遲鏈路" }
        };

        // Match the source description, not just the ID, to avoid applying a translation
        // to a different vehicle/firmware meaning. Units and numeric metadata are untouched.
        private static readonly Dictionary<string, string> FmtDescriptionTranslations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "This sets the percentage number of standard deviations applied to the GPS velocity measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted. If EK3_GLITCH_RAD set to 0 the velocity innovations will be clipped instead of rejected if they exceed the gate size and a smaller value of EK3_VEL_I_GATE not exceeding 300 is recommended to limit the effect of GPS transient errors.", "GPS 速度量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。 EK3_GLITCH_RAD=0 時，超過門檻的速度新息會被限幅而非拒絕；建議 EK3_VEL_I_GATE 不超過 300，以限制 GPS 短暫誤差的影響。" },
            { "This sets the percentage number of standard deviations applied to the GPS position measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted. If EK3_GLITCH_RAD has been set to 0 the horizontal position innovations will be clipped instead of rejected if they exceed the gate size so a smaller value of EK3_POS_I_GATE not exceeding 300 is recommended to limit the effect of GPS transient errors.", "GPS 位置量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。 EK3_GLITCH_RAD=0 時，超過門檻的水平位置新息會被限幅而非拒絕；建議 EK3_POS_I_GATE 不超過 300，以限制 GPS 短暫誤差的影響。" },
            { "This controls the maximum radial uncertainty in position between the value predicted by the filter and the value measured by the GPS before the filter position and velocity states are reset to the GPS. Making this value larger allows the filter to ignore larger GPS glitches but also means that non-GPS errors such as IMU and compass can create a larger error in position before the filter is forced back to the GPS position. If EK3_GLITCH_RAD set to 0 the GPS innovations will be clipped instead of rejected if they exceed the gate size set by EK3_VEL_I_GATE and EK3_POS_I_GATE which can be useful if poor quality sensor data is causing GPS rejection and loss of navigation but does make the EKF more susceptible to GPS glitches. If setting EK3_GLITCH_RAD to 0 it is recommended to reduce EK3_VEL_I_GATE and EK3_POS_I_GATE to 300.", "濾波器預測位置與 GPS 量測位置之間可容許的最大徑向位置不確定度；超過後會將位置與速度狀態重設至 GPS。增大可忽略較大的 GPS 突跳，但也容許 IMU、羅盤等非 GPS 誤差累積成更大的位置誤差，才被迫回到 GPS 位置。 設為 0 時，超過 EK3_VEL_I_GATE、EK3_POS_I_GATE 門檻的 GPS 新息會被限幅而非拒絕；感測器品質差導致拒收 GPS、失去導航時可能有用，但 EKF 也更容易受 GPS 突跳影響。設為 0 時，建議將上述兩個門檻降低至 300。" },
            { "This is the RMS value of noise in the altitude measurement. Increasing it reduces the weighting of the baro measurement and will make the filter respond more slowly to baro measurement errors, but will make it more sensitive to GPS and accelerometer errors. A larger value for EK3_ALT_M_NSE may be required when operating with EK3_SRCx_POSZ = 0. This parameter also sets the noise for the 'synthetic' zero height measurement that is used when EK3_SRCx_POSZ = 0.", "高度量測雜訊的均方根值。增大會降低氣壓計權重，對氣壓高度量測誤差的反應較慢，但對 GPS 與加速度計誤差更敏感。 EK3_SRCx_POSZ=0 時可能需要更大的值；此参数也設定該情況下使用的「合成」零高度量測雜訊。" },
            { "This sets the percentage number of standard deviations applied to the height measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.  If EK3_GLITCH_RAD set to 0 the vertical position innovations will be clipped instead of rejected if they exceed the gate size and a smaller value of EK3_HGT_I_GATE not exceeding 300 is recommended to limit the effect of height sensor transient errors.", "高度量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。 EK3_GLITCH_RAD=0 時，超過門檻的垂直位置新息會被限幅而非拒絕；建議 EK3_HGT_I_GATE 不超過 300，以限制高度感測器短暫誤差的影響。" },
            { "This determines when the filter will use the 3-axis magnetometer fusion model that estimates both earth and body fixed magnetic field states and when it will use a simpler magnetic heading fusion model that does not use magnetic field states. The 3-axis magnetometer fusion is only suitable for use when the external magnetic field environment is stable. EK3_MAG_CAL = 0 uses heading fusion on ground, 3-axis fusion in-flight, and is the default setting for Plane users. EK3_MAG_CAL = 1 uses 3-axis fusion only when manoeuvring. EK3_MAG_CAL = 2 uses heading fusion at all times, is recommended if the external magnetic field is varying and is the default for rovers. EK3_MAG_CAL = 3 uses heading fusion on the ground and 3-axis fusion after the first in-air field and yaw reset has completed, and is the default for copters. EK3_MAG_CAL = 4 uses 3-axis fusion at all times. EK3_MAG_CAL = 7 uses 3-axis fusion on the ground and after the first in-air field and yaw reset has completed. This allows the magnetic field to be learned on the ground before takeoff (useful when swapping batteries with different magnetic signatures) while inhibiting learning during the initial climb when motor magnetic interference is strongest. While disarmed and stationary the yaw is additionally anchored to the measured magnetic heading, since a stationary vehicle gives the 3-axis fusion no way to separate a yaw error from the body field states. A body field component perpendicular to the horizontal earth field is not separable from yaw by a heading measurement, so it appears as a heading offset rather than being learned. NOTE : Use of simple heading magnetometer fusion makes vehicle compass calibration and alignment errors harder for the EKF to detect which reduces the sensitivity of the Copter EKF failsafe algorithm. NOTE: The fusion mode can be forced to 2 for specific EKF cores using the EK3_MAG_MASK parameter. NOTE: limited operation without a magnetometer or any other yaw sensor is possible by setting all COMPASS_USE, COMPASS_USE2, COMPASS_USE3, etc parameters to 0 and setting COMPASS_ENABLE to 0. If this is done, the EK3_GSF_RUN and EK3_GSF_USE masks must be set to the same as EK3_IMU_MASK. A yaw angle derived from IMU and GPS velocity data using a Gaussian Sum Filter (GSF) will then be used to align the yaw when flight commences and there is sufficient movement.", "選擇三軸磁力計融合（估計地球與機體磁場狀態）或簡單磁航向融合（不估計磁場狀態）。三軸融合僅適合外部磁場穩定的環境。0：地面航向、飛行三軸，固定翼預設；1：僅機動時三軸；2：始終航向，適合磁場變化且為車輛預設；3：地面航向、首次空中磁場與偏航重設後三軸，多旋翼預設；4：始終三軸；7：地面與首次空中磁場／偏航重設後使用三軸，可於地面學習磁場（例如更換磁場特徵不同的電池），但在馬達磁干擾最強的初始爬升期間禁止學習。未解鎖且靜止時，偏航額外錨定於量測磁航向，因為靜止時三軸融合無法區分偏航誤差與機體磁場狀態；垂直於水平地球磁場的機體磁場分量無法由航向量測與偏航區分，會表現為航向偏移而非被學習。簡單航向融合較難偵測羅盤校正與對準誤差，降低多旋翼 EKF 失效保護的敏感度。EK3_MAG_MASK 可強制指定核心使用模式 2。將全部 COMPASS_USE 系列與 COMPASS_ENABLE 設為 0，可有限度地無磁力計／其他偏航感測器運作；EK3_GSF_RUN 與 EK3_GSF_USE 遮罩需同 EK3_IMU_MASK，開始飛行且移動量足夠後，使用 IMU、GPS 速度的高斯和濾波器（GSF）對準偏航。" },
            { "The maximum optical flow rate in rad/sec that will be accepted by the filter.  Flow rates above this value will not be fused.", "濾波器可接受的最大光流角速率，單位為弧度／秒；高於此值的光流不參與融合。" },
            { "Range finder can be used as the primary height source when below this percentage of its maximum range (see RNGFNDx_MAX) and the primary height source is Baro or GPS (see EK3_SRCx_POSZ).  This feature should not be used for terrain following as it is designed for vertical takeoff and landing with climb above the range finder use height before commencing the mission, and with horizontal position changes below that height being limited to a flat region around the takeoff and landing point.", "主要高度來源為氣壓計或 GPS（見 EK3_SRCx_POSZ），且高度低於測距儀最大量程（RNGFNDx_MAX）的此百分比時，可改以測距儀作主要高度來源。不可用於地形跟隨；此功能設計用於垂直起降，任務開始前應爬升超過測距儀使用高度，低於此高度時的水平移動應限於起降點周圍平坦區域。" },
            { "1 byte bitmap of which EKF3 instances run an independent EKF-GSF yaw estimator to provide a backup yaw estimate that doesn't rely on magnetometer data. This estimator uses IMU, GPS and, if available, airspeed data. EKF-GSF yaw estimator data for the primary EKF3 instance will be logged as GSF0 and GSF1 messages. Use of the yaw estimate generated by this algorithm is controlled by the EK3_GSF_USE_MASK and EK3_GSF_RST_MAX parameters. To run the EKF-GSF yaw estimator in ride-along and logging only, set EK3_GSF_USE to 0.", "1 位元組遮罩，指定哪些 EKF3 實例執行獨立的 EKF-GSF 偏航估測器，提供不依賴磁力計的備援偏航。估測器使用 IMU、GPS，以及可用時的空速資料；主 EKF3 實例的資料記錄為 GSF0、GSF1。是否採用其偏航由 EK3_GSF_USE_MASK 與 EK3_GSF_RST_MAX 控制；僅旁路運算及記錄時，將使用遮罩設為 0（原文寫作 EK3_GSF_USE）。" },
            { "Ratio of mass to drag coefficient measured along the X body axis. This parameter enables estimation of wind drift for vehicles with bluff bodies and without propulsion forces in the X and Y direction (eg multicopters). The drag produced by this effect scales with speed squared. Set to a positive value > 1.0 to enable. A starting value is the mass in Kg divided by the frontal area. The predicted drag from the rotors is specified separately by the EK3_DRAG_MCOEF parameter.", "機體 X 軸的質量與阻力係數比值，用於估計多旋翼等 X、Y 軸無推進力之鈍體的風漂移。此阻力與速度平方成正比；設為大於 1.0 的正值以啟用。初始值可取質量（公斤）除以正面面積。旋翼阻力另由 EK3_DRAG_MCOEF 設定。" },
            { "Ratio of mass to drag coefficient measured along the Y body axis. This parameter enables estimation of wind drift for vehicles with bluff bodies and without propulsion forces in the X and Y direction (eg multicopters). The drag produced by this effect scales with speed squared. Set to a positive value > 1.0 to enable. A starting value is the mass in Kg divided by the side area. The predicted drag from the rotors is specified separately by the EK3_DRAG_MCOEF parameter.", "機體 Y 軸的質量與阻力係數比值，用於估計多旋翼等 X、Y 軸無推進力之鈍體的風漂移。此阻力與速度平方成正比；設為大於 1.0 的正值以啟用。初始值可取質量（公斤）除以側面面積。旋翼阻力另由 EK3_DRAG_MCOEF 設定。" },
            { "This sets the amount of noise used when fusing X and Y acceleration as an observation that enables estimation of wind velocity for multi-rotor vehicles. This feature is enabled by the EK3_DRAG_BCOEF_X and EK3_DRAG_BCOEF_Y parameters", "融合 X、Y 軸加速度以估計多旋翼風速時使用的量測雜訊；此功能由 EK3_DRAG_BCOEF_X 與 EK3_DRAG_BCOEF_Y 啟用。" },
            { "This parameter is used to predict the drag produced by the rotors when flying a multi-copter, enabling estimation of wind drift. The drag produced by this effect scales with speed not speed squared and is produced because some of the air velocity normal to the rotors axis of rotation is lost when passing through the rotor disc which changes the momentum of the airflow causing drag. For unducted rotors the effect is roughly proportional to the area of the propeller blades when viewed side on and changes with different propellers. It is higher for ducted rotors. For example if flying at 15 m/s at sea level conditions produces a rotor induced drag acceleration of 1.5 m/s/s, then EK3_DRAG_MCOEF would be set to 0.1 = (1.5/15.0). Set EK3_MCOEF to a positive value to enable wind estimation using this drag effect. To account for the drag produced by the body which scales with speed squared, see documentation for the EK3_DRAG_BCOEF_X and EK3_DRAG_BCOEF_Y parameters.", "預測多旋翼的旋翼阻力以估計風漂移。此阻力與速度成正比，而非速度平方；氣流穿過旋翼盤時損失部分垂直於旋轉軸的速度，動量變化因而產生阻力。無涵道旋翼的效應約與槳葉側面投影面積成正比，會隨槳型改變，涵道旋翼的效應較大。例如海平面以 15 m/s 飛行時，旋翼阻力造成 1.5 m/s² 減速度，係數為 1.5/15=0.1。設為正值以使用此效應估計風速；與速度平方相關的機體阻力請參閱 EK3_DRAG_BCOEF_X、EK3_DRAG_BCOEF_Y。原始說明中的 EK3_MCOEF 為原文用字，保留於提示供核對。" },
            { "Determines how verbose the EKF3 streaming logging is. A value of 0 provides full logging(default), a value of 1 only XKF4 scaled innovations are logged, a value of 2 both XKF4 and GSF are logged, and a value of 3 disables all streaming logging of EKF3.", "EKF3 串流記錄的詳細程度。0：完整記錄（預設）；1：僅記錄 XKF4 縮放新息；2：記錄 XKF4 與 GSF；3：停用全部 EKF3 串流記錄。" },
            { "Vertical accuracy threshold for GPS as the altitude source. The GPS will not be used as an altitude source if the reported vertical accuracy of the GPS is larger than this threshold, falling back to baro instead. Set to zero to deactivate the threshold check.", "GPS 作為高度來源的垂直精度門檻；回報的垂直精度值大於此門檻時，不使用 GPS 高度，改回氣壓計。設為 0 會停用此門檻檢查。" },
            { "EKF optional behaviour. Bit 0 (JammingExpected): Setting JammingExpected will change the EKF behaviour such that if dead reckoning navigation is possible it will require the preflight alignment GPS quality checks controlled by EK3_GPS_CHECK and EK3_CHECK_SCALE to pass before resuming GPS use if GPS lock is lost for more than 2 seconds to prevent bad position estimate. Bit 1 (Manual lane switching): DANGEROUS – If enabled, this disables automatic lane switching. If the active lane becomes unhealthy, no automatic switching will occur. Users must manually set EK3_PRIMARY to change lanes. No health checks will be performed on the selected lane. Use with extreme caution.  Bit 2 (Optflow may use terrain alt): Terrain SRTM data will be used if the vehicle climbs above the rangefinder's range allowing optical flow to be used at higher altitudes. Bit 3 (AGL KF for optflow scaling): Use a 2-state IMU-aided AGL Kalman filter (height + vertical velocity, fused with rangefinder) to compute the height-above-ground used for optical flow velocity scaling, instead of terrainState-pd. This decouples optical flow scaling from errors in the main filter's vertical position state.", "EKF 可選行為。位元 0（預期干擾）：若可航位推算，GPS 失鎖超過 2 秒後，必須通過 EK3_GPS_CHECK、EK3_CHECK_SCALE 所控制的飛前對準 GPS 品質檢查，才恢復使用 GPS，以避免不良位置估計。位元 1（手動通道切換）：危險！停用自動切換；目前通道不健康時也不會自動切換，必須手動設定 EK3_PRIMARY，且不檢查所選通道健康狀態，務必極度謹慎。位元 2：高度超過測距儀量程時，可用 SRTM 地形高度，使光流可在更高處使用。位元 3：以 IMU 輔助、融合測距儀的二狀態離地高度卡爾曼濾波器（高度與垂直速度），取代 terrainState-pd 計算光流速度比例換算所用的離地高度，使換算不受主濾波器垂直位置狀態誤差影響。" },
            { "EKF Source Options. Bit 0: Fuse all velocity sources present in EK3_SRCx_VEL_. Bit 1: Align external navigation position when using optical flow. Bit 3: Use SRC per core. By default, EKF source selection is controlled via the EK3_SRC parameters, allowing only one source to be active at a time across all cores (switchable via MAVLink, Lua, or RC). Enabling this bit maps EKF core 1 to SRC1, core 2 to SRC2, etc., allowing each core to run independently with a dedicated source.", "EKF 資料來源選項。位元 0：融合 EK3_SRCx_VEL_ 中所有可用速度來源。位元 1：使用光流時對準外部導航位置。位元 3：各核心使用各自來源；預設所有核心同時只使用一組 EK3_SRC 來源，可由 MAVLink、Lua 或遙控切換。啟用後核心 1 對應 SRC1、核心 2 對應 SRC2，依此類推，各核心可獨立使用專屬來源。" },
            { "This noise controls the growth of the vertical accelerometer delta velocity bias state error estimate. Increasing it makes accelerometer bias estimation faster and noisier.", "控制垂直加速度計速度增量偏差狀態之估計誤差增長。增大會使加速度計偏差估計更快，但雜訊也更大。" },
            { "The accelerometer bias state will be limited to +- this value", "加速度計偏差狀態限制在此值的正負範圍內。" },
            { "This control disturbance noise controls the growth of estimated error due to accelerometer measurement errors excluding bias. Increasing it makes the flter trust the accelerometer measurements less and other measurements more.", "控制加速度計量測誤差（不含偏差）造成的估計誤差增長。增大會降低對加速度計量測的信任，並提高其他量測的權重。" },
            { "These options control the affinity between sensor instances and EKF cores", "控制感測器實例與 EKF 核心之間的對應關係。" },
            { "This is the RMS value of noise in the altitude measurement. Increasing it reduces the weighting of the baro measurement and will make the filter respond more slowly to baro measurement errors, but will make it more sensitive to GPS and accelerometer errors.", "高度量測雜訊的均方根值。增大會降低氣壓計權重，對氣壓高度量測誤差的反應較慢，但對 GPS 與加速度計誤差更敏感。" },
            { "This parameter controls the primary height sensor used by the EKF. If the selected option cannot be used, it will default to Baro as the primary height source. Setting 0 will use the baro altitude at all times. Setting 1 uses the range finder and is only available in combination with optical flow navigation (EK3_GPS_TYPE = 3). Setting 2 uses GPS. Setting 3 uses the range beacon data. NOTE - the EK3_RNG_USE_HGT parameter can be used to switch to range-finder when close to the ground.", "選擇 EKF 的主要高度感測器；所選來源無法使用時回退至氣壓計。0：氣壓高度；1：測距儀，僅可搭配光流導航（EK3_GPS_TYPE=3）；2：GPS；3：測距信標。接近地面時可透過 EK3_RNG_USE_HGT 切換至測距儀。" },
            { "This is the number of msec that the range beacon measurements lag behind the inertial measurements.", "測距信標量測相對於慣性量測的延遲，單位為毫秒。" },
            { "This sets the percentage number of standard deviations applied to the range beacon measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "測距信標量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "This is the RMS value of noise in the range beacon measurement. Increasing it reduces the weighting on this measurement.", "測距信標量測雜訊的均方根值；增大會降低該量測的權重。" },
            { "1 byte bitmap controlling use of sideslip angle fusion for estimation of non wind states during operation of 'fly forward' vehicle types such as fixed wing planes. By assuming that the angle of sideslip is small, the wind velocity state estimates are corrected  whenever the EKF is not dead reckoning (e.g. has an independent velocity or position sensor such as GPS). This behaviour is on by default and cannot be disabled. When the EKF is dead reckoning, the wind states are used as a reference, enabling use of the small angle of sideslip assumption to correct non wind velocity states (eg attitude, velocity, position, etc) and improve navigation accuracy. This behaviour is on by default and cannot be disabled. The behaviour controlled by this parameter is the use of the small angle of sideslip assumption to correct non wind velocity states when the EKF is NOT dead reckoning. This is primarily of benefit to reduce the buildup of yaw angle errors during straight and level flight without a yaw sensor (e.g. magnetometer or dual antenna GPS yaw) provided aerobatic flight maneuvers with large sideslip angles are not performed. The 'always' option might be used where the yaw sensor is intentionally not fitted or disabled. The 'WhenNoYawSensor' option might be used if a yaw sensor is fitted, but protection against in-flight failure and continual rejection by the EKF is desired. For vehicles operated within visual range of the operator performing frequent turning maneuvers, setting this parameter is unnecessary.", "此 1 位元組遮罩用於固定翼等向前飛行機型，控制非航位推算期間是否以小側滑角假設修正非風速狀態（姿態、速度、位置等）。具有 GPS 等獨立速度或位置量測時，以小側滑角假設修正風速；航位推算時則以風速為參考修正其他狀態，這兩種基本行為預設啟用且不可關閉。本參數控制的是未進行航位推算時的額外修正，可減少無偏航感測器時直線平飛累積的偏航誤差，但不適合大側滑角特技動作。Always 適用於未裝或刻意停用偏航感測器；WhenNoYawSensor 可在已裝感測器但飛行中失效或持續被 EKF 拒收時提供保護。目視範圍內頻繁轉彎的飛行通常不需設定此功能。" },
            { "This scales the thresholds that are used to check GPS accuracy before it is used by the EKF. A value of 100 is the default. Values greater than 100 increase and values less than 100 reduce the maximum GPS error the EKF will accept. A value of 200 will double the allowable GPS error.", "縮放 EKF 使用 GPS 前的精度檢查門檻。預設 100；大於 100 放寬可接受的 GPS 誤差，小於 100 則收緊；200 表示容許誤差加倍。" },
            { "Ratio of mass to drag coefficient measured along the X body axis. This parameter enables estimation of wind drift for vehicles with bluff bodies and without propulsion forces in the X and Y direction (eg multicopters). The drag produced by this effect scales with speed squared.  Set to a postive value > 1.0 to enable. A starting value is the mass in Kg divided by the frontal area. The predicted drag from the rotors is specified separately by the EK3_DRAG_MCOEF parameter.", "機體 X 軸的質量與阻力係數比值，用於估計多旋翼等 X、Y 軸無推進力之鈍體的風漂移。此阻力與速度平方成正比；設為大於 1.0 的正值以啟用。初始值可取質量（公斤）除以正面面積。旋翼阻力另由 EK3_DRAG_MCOEF 設定。" },
            { "Ratio of mass to drag coefficient measured along the Y body axis. This parameter enables estimation of wind drift for vehicles with bluff bodies and without propulsion forces in the X and Y direction (eg multicopters). The drag produced by this effect scales with speed squared.  Set to a postive value > 1.0 to enable. A starting value is the mass in Kg divided by the side area. The predicted drag from the rotors is specified separately by the EK3_DRAG_MCOEF parameter.", "機體 Y 軸的質量與阻力係數比值，用於估計多旋翼等 X、Y 軸無推進力之鈍體的風漂移。此阻力與速度平方成正比；設為大於 1.0 的正值以啟用。初始值可取質量（公斤）除以側面面積。旋翼阻力另由 EK3_DRAG_MCOEF 設定。" },
            { "This sets the amount of noise used when fusing X and Y acceleration as an observation that enables esitmation of wind velocity for multi-rotor vehicles. This feature is enabled by the EK3_DRAG_BCOEF_X and EK3_DRAG_BCOEF_Y parameters", "融合 X、Y 軸加速度以估計多旋翼風速時使用的量測雜訊；此功能由 EK3_DRAG_BCOEF_X 與 EK3_DRAG_BCOEF_Y 啟用。" },
            { "This parameter is used to predict the drag produced by the rotors when flying a multi-copter, enabling estimation of wind drift. The drag produced by this effect scales with speed not speed squared and is produced because some of the air velocity normal to the rotors axis of rotation is lost when passing through the rotor disc which changes the momentum of the airflow causing drag. For unducted rotors the effect is roughly proportional to the area of the propeller blades when viewed side on and changes with different propellers. It is higher for ducted rotors. For example if flying at 15 m/s at sea level conditions produces a rotor induced drag acceleration of 1.5 m/s/s, then EK3_DRAG_MCOEF would be set to 0.1 = (1.5/15.0). Set EK3_MCOEF to a postive value to enable wind estimation using this drag effect. To account for the drag produced by the body which scales with speed squared, see documentation for the EK3_DRAG_BCOEF_X and EK3_DRAG_BCOEF_Y parameters.", "預測多旋翼的旋翼阻力以估計風漂移。此阻力與速度成正比，而非速度平方；氣流穿過旋翼盤時損失部分垂直於旋轉軸的速度，動量變化因而產生阻力。無涵道旋翼的效應約與槳葉側面投影面積成正比，會隨槳型改變，涵道旋翼的效應較大。例如海平面以 15 m/s 飛行時，旋翼阻力造成 1.5 m/s² 減速度，係數為 1.5/15=0.1。設為正值以使用此效應估計風速；與速度平方相關的機體阻力請參閱 EK3_DRAG_BCOEF_X、EK3_DRAG_BCOEF_Y。原始說明中的 EK3_MCOEF 為原文用字，保留於提示供核對。" },
            { "This sets the percentage number of standard deviations applied to the airspeed measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "空速量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "This is the RMS value of noise in equivalent airspeed measurements used by planes. Increasing it reduces the weighting of airspeed measurements and will make wind speed estimates less noisy and slower to converge. Increasing also increases navigation errors when dead-reckoning without GPS measurements.", "固定翼使用的當量空速量測雜訊均方根值。增大會降低空速權重，使風速估計雜訊較小但收斂較慢；無 GPS 的航位推算導航誤差也會增大。" },
            { "This enables EKF3. Enabling EKF3 only makes the maths run, it does not mean it will be used for flight control. To use it for flight control set AHRS_EKF_TYPE=3. A reboot or restart will need to be performed after changing the value of EK3_ENABLE for it to take effect.", "啟用 EKF3 計算；啟用不代表會用於飛行控制。若要用於飛控，須設 AHRS_EKF_TYPE=3。變更 EK3_ENABLE 後必須重新啟動才生效。" },
            { "lanes have to be consistently better than the primary by at least this threshold to reduce their overall relativeCoreError, lowering this makes lane switching more sensitive to smaller error differences", "其他估算通道必須持續比主通道好至少此門檻，才會降低其相對核心誤差。降低門檻會使通道切換對較小的誤差差異更敏感。" },
            { "This is the number of msec that the optical flow measurements lag behind the inertial measurements. It is the time from the end of the optical flow averaging period and does not include the time delay due to the 100msec of averaging within the flow sensor.", "光流量測相對於慣性量測的延遲，單位為毫秒。從光流平均期間結束時計算，不包含光流感測器內部 100 毫秒平均所造成的延遲。" },
            { "This sets the percentage number of standard deviations applied to the optical flow innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "光流量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "This is the RMS value of noise and errors in optical flow measurements. Increasing it reduces the weighting on these measurements.", "光流量測雜訊與誤差的均方根值；增大會降低光流量測的權重。" },
            { "Controls if the optical flow data is fused into the 24-state navigation estimator OR the 1-state terrain height estimator.", "選擇將光流資料融合至 24 狀態導航估測器，或 1 狀態地形高度估測器。" },
            { "This state  process noise controls growth of the gyro delta angle bias state error estimate. Increasing it makes rate gyro bias estimation faster and noisier.", "控制陀螺儀角度增量偏差狀態之估計誤差增長的程序雜訊。增大會使角速度陀螺儀偏差估計更快，但雜訊也更大。" },
            { "This controls the maximum radial uncertainty in position between the value predicted by the filter and the value measured by the GPS before the filter position and velocity states are reset to the GPS. Making this value larger allows the filter to ignore larger GPS glitches but also means that non-GPS errors such as IMU and compass can create a larger error in position before the filter is forced back to the GPS position.", "濾波器預測位置與 GPS 量測位置之間可容許的最大徑向位置不確定度；超過後會將位置與速度狀態重設至 GPS。增大可忽略較大的 GPS 突跳，但也容許 IMU、羅盤等非 GPS 誤差累積成更大的位置誤差，才被迫回到 GPS 位置。" },
            { "This parameter sets the size of the dead zone that is applied to negative baro height spikes that can occur when taking off or landing when a vehicle with lift rotors is operating in ground effect ground effect. Set to about 0.5m less than the amount of negative offset in baro height that occurs just prior to takeoff when lift motors are spooling up. Set to 0 if no ground effect is present.", "起降時旋翼地面效應可能造成氣壓高度負向突波，此值設定對該突波的死區。可設為起飛前馬達加速時所見氣壓高度負偏移量減約 0.5 公尺；沒有地面效應時設為 0。" },
            { "This is a 1 byte bitmap controlling which GPS preflight checks are performed. Set to 0 to bypass all checks. Set to 255 perform all checks. Set to 3 to check just the number of satellites and HDoP. Set to 31 for the most rigorous checks that will still allow checks to pass when the copter is moving, eg launch from a boat.", "1 位元組遮罩，選擇 GPS 飛前檢查。0：略過全部；255：執行全部；3：只檢查衛星數與水平精度衰減因子（HDoP）；31：在機體移動時仍可通過的最嚴格檢查組合，例如從船上起飛。" },
            { "This controls use of GPS measurements : 0 = use 3D velocity & 2D position, 1 = use 2D velocity and 2D position, 2 = use 2D position, 3 = Inhibit GPS use - this can be useful when flying with an optical flow sensor in an environment where GPS quality is poor and subject to large multipath errors.", "選擇使用的 GPS 量測：0：三維速度與二維位置；1：二維速度與二維位置；2：僅二維位置；3：禁止使用 GPS。GPS 品質差或多路徑誤差嚴重、改以光流感測器飛行時，可使用不採用 GPS 的選項。" },
            { "Sets the maximum number of times the EKF3 will be allowed to reset its yaw to the estimate from the EKF-GSF yaw estimator. No resets will be allowed unless the use of the EKF-GSF yaw estimate is enabled via the EK3_GSF_USE_MASK parameter.", "EKF3 可將偏航重設為 EKF-GSF 偏航估計的最大次數。必須先透過 EK3_GSF_USE_MASK 啟用該估計，否則不允許重設。" },
            { "1 byte bitmap of which EKF3 instances run an independant EKF-GSF yaw estimator to provide a backup yaw estimate that doesn't rely on magnetometer data. This estimator uses IMU, GPS and, if available, airspeed data. EKF-GSF yaw estimator data for the primary EKF3 instance will be logged as GSF0 and GSF1 messages. Use of the yaw estimate generated by this algorithm is controlled by the EK3_GSF_USE_MASK and EK3_GSF_RST_MAX parameters. To run the EKF-GSF yaw estimator in ride-along and logging only, set EK3_GSF_USE to 0.", "1 位元組遮罩，指定哪些 EKF3 實例執行獨立的 EKF-GSF 偏航估測器，提供不依賴磁力計的備援偏航。估測器使用 IMU、GPS，以及可用時的空速資料；主 EKF3 實例的資料記錄為 GSF0、GSF1。是否採用其偏航由 EK3_GSF_USE_MASK 與 EK3_GSF_RST_MAX 控制；僅旁路運算及記錄時，將使用遮罩設為 0（原文寫作 EK3_GSF_USE）。" },
            { "A bitmask of which EKF3 instances will use the output from the EKF-GSF yaw estimator that has been turned on by the EK3_GSF_RUN_MASK parameter. If the inertial navigation calculation stops following the GPS, then the vehicle code can request EKF3 to attempt to resolve the issue, either by performing a yaw reset if enabled by this parameter by switching to another EKF3 instance.", "指定哪些 EKF3 實例可採用由 EK3_GSF_RUN_MASK 啟動的 EKF-GSF 偏航估測輸出。若慣性導航不再跟隨 GPS，機體程式可要求 EKF3 嘗試解決：依本參數允許偏航重設，或切換至另一 EKF3 實例。" },
            { "This control disturbance noise controls the growth of estimated error due to gyro measurement errors excluding bias. Increasing it makes the flter trust the gyro measurements less and other measurements more.", "控制陀螺儀量測誤差（不含偏差）造成的估計誤差增長。增大會降低陀螺儀量測的權重，並提高其他量測的權重。" },
            { "This is the number of msec that the Height measurements lag behind the inertial measurements.", "高度量測相對於慣性量測的延遲，單位為毫秒。" },
            { "This sets the percentage number of standard deviations applied to the height measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "高度量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "Specifies the crossover frequency of the complementary filter used to calculate the output predictor height rate derivative.", "計算輸出預測器高度變化率導數時，互補濾波器使用的交越頻率。" },
            { "1 byte bitmap of IMUs to use in EKF3. A separate instance of EKF3 will be started for each IMU selected. Set to 1 to use the first IMU only (default), set to 2 to use the second IMU only, set to 3 to use the first and second IMU. Additional IMU's can be used up to a maximum of 6 if memory and processing resources permit. There may be insufficient memory and processing resources to run multiple instances. If this occurs EKF3 will fail to start.", "1 位元組遮罩，指定 EKF3 使用的 IMU；每個選取的 IMU 都啟動獨立 EKF3 實例。1：第一個（預設）；2：第二個；3：第一與第二個。記憶體及運算資源足夠時最多可用 6 個；資源不足以執行多實例時，EKF3 將無法啟動。" },
            { "This sets the IMU mask of sensors to do full logging for", "選擇要完整記錄資料的 IMU 感測器遮罩。" },
            { "This determines when the filter will use the 3-axis magnetometer fusion model that estimates both earth and body fixed magnetic field states and when it will use a simpler magnetic heading fusion model that does not use magnetic field states. The 3-axis magnetometer fusion is only suitable for use when the external magnetic field environment is stable. EK3_MAG_CAL = 0 uses heading fusion on ground, 3-axis fusion in-flight, and is the default setting for Plane users. EK3_MAG_CAL = 1 uses 3-axis fusion only when manoeuvring. EK3_MAG_CAL = 2 uses heading fusion at all times, is recommended if the external magnetic field is varying and is the default for rovers. EK3_MAG_CAL = 3 uses heading fusion on the ground and 3-axis fusion after the first in-air field and yaw reset has completed, and is the default for copters. EK3_MAG_CAL = 4 uses 3-axis fusion at all times. EK3_MAG_CAL = 5 uses an external yaw sensor with simple heading fusion. NOTE : Use of simple heading magnetometer fusion makes vehicle compass calibration and alignment errors harder for the EKF to detect which reduces the sensitivity of the Copter EKF failsafe algorithm. NOTE: The fusion mode can be forced to 2 for specific EKF cores using the EK3_MAG_MASK parameter. EK3_MAG_CAL = 6 uses an external yaw sensor with fallback to compass when the external sensor is not available if we are flying. NOTE: The fusion mode can be forced to 2 for specific EKF cores using the EK3_MAG_MASK parameter. NOTE: limited operation without a magnetometer or any other yaw sensor is possible by setting all COMPASS_USE, COMPASS_USE2, COMPASS_USE3, etc parameters to 0 and setting COMPASS_ENABLE to 0. If this is done, the EK3_GSF_RUN and EK3_GSF_USE masks must be set to the same as EK3_IMU_MASK. A yaw angle derived from IMU and GPS velocity data using a Gaussian Sum Filter (GSF) will then be used to align the yaw when flight commences and there is sufficient movement.", "選擇何時使用三軸磁力計融合（估計地球與機體座標磁場狀態），或不估計磁場狀態的簡單磁航向融合。三軸融合僅適合外部磁場穩定的環境。0：地面用航向、飛行用三軸，為固定翼預設；1：僅機動時用三軸；2：始終用航向，適合外部磁場變動，為車輛預設；3：地面用航向，首次空中磁場與偏航重設完成後用三軸，為多旋翼預設；4：始終用三軸；5：外部偏航感測器搭配簡單航向融合；6：外部偏航感測器，飛行時若不可用則回退羅盤。簡單航向融合會使羅盤校正與對準誤差更難被 EKF 偵測，降低多旋翼 EKF 失效保護的敏感度。可用 EK3_MAG_MASK 強制特定核心採模式 2。若所有 COMPASS_USE 系列與 COMPASS_ENABLE 均為 0，可有限度地在無磁力計及其他偏航感測器下運作；EK3_GSF_RUN、EK3_GSF_USE 遮罩需與 EK3_IMU_MASK 相同，開始飛行且移動量足夠時，以 IMU 與 GPS 速度的高斯和濾波器（GSF）估計對準偏航。選項 5、6 在 4.1 以上已棄用，改參閱 EK3_SRCn_YAW。" },
            { "This limits the difference between the learned earth magnetic field and the earth field from the world magnetic model tables. A value of zero means to disable the use of the WMM tables.", "限制學習到的地球磁場與世界磁場模型（WMM）表格值之差；設為 0 表示不使用 WMM 表格。" },
            { "This sets the percentage number of standard deviations applied to the magnetometer measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "磁力計量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "This is the RMS value of noise in magnetometer measurements. Increasing it reduces the weighting on these measurements.", "磁力計量測雜訊的均方根值；增大會降低磁力計量測的權重。" },
            { "1 byte bitmap of EKF cores that will disable magnetic field states and use simple magnetic heading fusion at all times. This parameter enables specified cores to be used as a backup for flight into an environment with high levels of external magnetic interference which may degrade the EKF attitude estimate when using 3-axis magnetometer fusion. NOTE : Use of a different magnetometer fusion algorithm on different cores makes unwanted EKF core switches due to magnetometer errors more likely.", "1 位元組 EKF 核心遮罩；選取的核心停用磁場狀態，始終使用簡單磁航向融合。可作為飛入強外部磁干擾環境時的備援，避免三軸磁力計融合使姿態估計劣化。注意：不同核心使用不同磁力計融合演算法，會提高因磁力計誤差而發生非預期核心切換的機率。" },
            { "This state process noise controls the growth of body magnetic field state error estimates. Increasing it makes magnetometer bias error estimation faster and noisier.", "控制機體座標磁場狀態估計誤差增長的程序雜訊。增大會使磁力計偏差估計更快，但雜訊也更大。" },
            { "This state process noise controls the growth of earth magnetic field state error estimates. Increasing it makes earth magnetic field estimation faster and noisier.", "控制地球磁場狀態估計誤差增長的程序雜訊。增大會使地球磁場估計更快，但雜訊也更大。" },
            { "This sets the magnitude maximum optical flow rate in rad/sec that will be accepted by the filter", "濾波器可接受的最大光流角速率大小，單位為弧度／秒。" },
            { "This sets the amount of position variation that the EKF allows for when operating without external measurements (eg GPS or optical flow). Increasing this parameter makes the EKF attitude estimate less sensitive to vehicle manoeuvres but more sensitive to IMU errors.", "沒有 GPS 或光流等外部量測時，EKF 容許的位置變化量。增大會使姿態估計對機體機動較不敏感，但對 IMU 誤差更敏感。" },
            { "When a height sensor other than GPS is used as the primary height source by the EKF, the position of the zero height datum is defined by that sensor and its frame of reference. If a GPS height measurement is also available, then the height of the WGS-84 height datum used by the EKF can be corrected so that the height returned by the getLLH() function is compensated for primary height sensor drift and change in datum over time. The first two bit positions control when the height datum will be corrected. Correction is performed using a Bayes filter and only operates when GPS quality permits. The third bit position controls where the corrections to the GPS reference datum are applied. Corrections can be applied to the local vertical position or to the reported EKF origin height (default).", "以非 GPS 感測器作為主要高度來源時，零高度基準由該感測器及其參考座標決定。若有 GPS 高度，可修正 EKF 使用的 WGS-84 高度基準，使 getLLH() 回傳高度補償主要高度感測器的漂移與基準隨時間變化。前兩個位元控制何時修正；修正使用貝氏濾波器，僅在 GPS 品質允許時運作。第三個位元選擇將 GPS 基準修正套用於局部垂直位置，或回報的 EKF 原點高度（預設）。" },
            { "This parameter is adjust the sensitivity of the on ground not moving test which is used to assist with learning the yaw gyro bias and stopping yaw drift before flight when operating without a yaw sensor. Bigger values allow the detection of a not moving condition with noiser IMU data. Check the XKFM data logged when the vehicle is on ground not moving and adjust the value of OGNM_TEST_SF to be slightly higher than the maximum value of the XKFM.ADR, XKFM.ALR, XKFM.GDR and XKFM.GLR test levels.", "調整地面靜止判定的敏感度，用於無偏航感測器時學習偏航陀螺儀偏差，並抑制起飛前偏航漂移。較大值容許在更嘈雜的 IMU 資料下判定靜止。檢查地面靜止時的 XKFM 記錄，將此值設為略高於 XKFM.ADR、ALR、GDR、GLR 測試值的最大值。" },
            { "This sets the percentage number of standard deviations applied to the GPS position measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "GPS 位置量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "This sets the GPS horizontal position observation noise. Increasing it reduces the weighting of GPS horizontal position measurements.", "GPS 水平位置觀測雜訊；增大會降低 GPS 水平位置量測的權重。" },
            { "The core number (index in IMU mask) that will be used as the primary EKF core on startup. While disarmed the EKF will force the use of this core. A value of 0 corresponds to the first IMU in EK3_IMU_MASK.", "啟動時的主要 EKF 核心編號，依 IMU 遮罩中的索引編號。未解鎖時強制使用此核心；0 對應 EK3_IMU_MASK 中第一個 IMU。" },
            { "This sets the percentage number of standard deviations applied to the range finder innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "測距儀量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "This is the RMS value of noise in the range finder measurement. Increasing it reduces the weighting on this measurement.", "測距儀量測雜訊的均方根值；增大會降低該量測的權重。" },
            { "Range finder can be used as the primary height source when below this percentage of its maximum range (see RNGFNDx_MAX_CM) and the primary height source is Baro or GPS (see EK3_SRCx_POSZ).  This feature should not be used for terrain following as it is designed for vertical takeoff and landing with climb above the range finder use height before commencing the mission, and with horizontal position changes below that height being limited to a flat region around the takeoff and landing point.", "主要高度來源為氣壓計或 GPS（見 EK3_SRCx_POSZ），且高度低於測距儀最大量程（RNGFNDx_MAX_CM）的此百分比時，可改以測距儀作主要高度來源。不可用於地形跟隨；此功能設計用於垂直起降，任務開始前應爬升超過測距儀使用高度，低於此高度時的水平移動應限於起降點周圍平坦區域。" },
            { "The range finder will not be used as the primary height source when the horizontal ground speed is greater than this value.", "水平地速高於此值時，不以測距儀作為主要高度來源。" },
            { "EKF Source Options", "EKF 資料來源選項。" },
            { "Position Horizontal Source (Primary)", "第 1 組（主要）水平位置資料來源。" },
            { "Position Vertical Source", "第 1 組（主要）垂直位置資料來源。" },
            { "Velocity Horizontal Source", "第 1 組（主要）水平速度資料來源。" },
            { "Velocity Vertical Source", "第 1 組（主要）垂直速度資料來源。" },
            { "Yaw Source", "第 1 組（主要）偏航資料來源。" },
            { "Position Horizontal Source (Secondary)", "第 2 組（次要）水平位置資料來源。" },
            { "Position Vertical Source (Secondary)", "第 2 組（次要）垂直位置資料來源。" },
            { "Velocity Horizontal Source (Secondary)", "第 2 組（次要）水平速度資料來源。" },
            { "Velocity Vertical Source (Secondary)", "第 2 組（次要）垂直速度資料來源。" },
            { "Yaw Source (Secondary)", "第 2 組（次要）偏航資料來源。" },
            { "Position Horizontal Source (Tertiary)", "第 3 組（第三組）水平位置資料來源。" },
            { "Position Vertical Source (Tertiary)", "第 3 組（第三組）垂直位置資料來源。" },
            { "Velocity Horizontal Source (Tertiary)", "第 3 組（第三組）水平速度資料來源。" },
            { "Velocity Vertical Source (Tertiary)", "第 3 組（第三組）垂直速度資料來源。" },
            { "Yaw Source (Tertiary)", "第 3 組（第三組）偏航資料來源。" },
            { "Sets the time constant of the output complementary filter/predictor in centi-seconds.", "輸出互補濾波器／預測器的時間常數，單位為百分之一秒。" },
            { "Specifies the maximum gradient of the terrain below the vehicle when it is using range finder as a height reference", "以測距儀作為高度參考時，飛行器下方地形的最大坡度。" },
            { "This sets the percentage number of standard deviations applied to the GPS velocity measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "GPS 速度量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "This sets a lower limit on the speed accuracy reported by the GPS receiver that is used to set vertical velocity observation noise. If the model of receiver used does not provide a speed accurcy estimate, then the parameter value will be used. Increasing it reduces the weighting of the GPS vertical velocity measurements.", "用來設定垂直速度觀測雜訊的 GPS 回報速度精度下限；若接收器不提供速度精度估計，則直接使用此參數。增大會降低 GPS 垂直速度量測的權重。" },
            { "This sets a lower limit on the speed accuracy reported by the GPS receiver that is used to set horizontal velocity observation noise. If the model of receiver used does not provide a speed accurcy estimate, then the parameter value will be used. Increasing it reduces the weighting of the GPS horizontal velocity measurements.", "用來設定水平速度觀測雜訊的 GPS 回報速度精度下限；若接收器不提供速度精度估計，則直接使用此參數。增大會降低 GPS 水平速度量測的權重。" },
            { "This is the 1-STD odometry velocity observation error that will be assumed when minimum quality is reported by the sensor. When quality is between max and min, the error will be calculated using linear interpolation between VIS_VERR_MIN and VIS_VERR_MAX.", "感測器回報最低品質時採用的里程計速度觀測誤差（1 個標準差）。品質介於最高與最低之間時，在 VIS_VERR_MIN 與 VIS_VERR_MAX 間線性內插。" },
            { "This is the 1-STD odometry velocity observation error that will be assumed when maximum quality is reported by the sensor. When quality is between max and min, the error will be calculated using linear interpolation between VIS_VERR_MIN and VIS_VERR_MAX.", "感測器回報最高品質時採用的里程計速度觀測誤差（1 個標準差）。品質介於最高與最低之間時，在 VIS_VERR_MIN 與 VIS_VERR_MAX 間線性內插。" },
            { "This is the 1-STD odometry velocity observation error that will be assumed when wheel encoder data is being fused.", "融合輪編碼器資料時採用的里程計速度觀測誤差（1 個標準差）。" },
            { "This state process noise controls the growth of wind state error estimates. Increasing it makes wind estimation faster and noisier.", "控制風速狀態估計誤差增長的程序雜訊。增大會使風速估計更快，但雜訊也更大。" },
            { "This controls how much the process noise on the wind states is increased when gaining or losing altitude to take into account changes in wind speed and direction with altitude. Increasing this parameter increases how rapidly the wind states adapt when changing altitude, but does make wind velocity estimation noiser.", "高度改變時增加風速狀態程序雜訊的程度，以反映風速與風向隨高度變化。增大會使風速狀態在升降時更快適應，但風速估計雜訊也會增大。" },
            { "This sets the percentage number of standard deviations applied to the magnetometer yaw measurement innovation consistency check. Decreasing it makes it more likely that good measurements will be rejected. Increasing it makes it more likely that bad measurements will be accepted.", "磁力計偏航量測的新息一致性檢查門檻，以標準差的百分比表示。降低會增加良好量測被拒絕的機率；提高會增加不良量測被接受的機率。" },
            { "This is the RMS value of noise in yaw measurements from the magnetometer. Increasing it reduces the weighting on these measurements.", "磁力計偏航量測雜訊的均方根值；增大會降低該量測的權重。" },
            { "Capacity of the battery in mAh when full", "電池充滿時的容量，單位為 mAh。" },
            { "The PWM level in microseconds on channel 3 below which throttle failsafe triggers", "第 3 通道的油門失效保護 PWM 門檻，單位為微秒；低於此值時觸發油門失效保護。" },
            { "The minimum alt above home the vehicle will climb to before returning.  If the vehicle is flying higher than this value it will return at its current altitude.", "返航前爬升至相對 Home 點的最低高度；若目前已高於此高度，則保持目前高度返航。" },
            { "This is the altitude the vehicle will move to as the final stage of Returning to Launch or after completing a mission.  Set to zero to land.", "返航最後階段或任務完成後要到達的高度；設為 0 表示降落。" },
            { "The vehicle will climb this many cm during the initial climb portion of the RTL", "返航初始爬升階段的爬升高度，單位為公分。" },
            { "Time (in milliseconds) to loiter above home before beginning final descent", "開始最後下降前，在 Home 點上方停留的時間，單位為毫秒。" },
            { "The descent speed for the final stage of landing in cm/s", "降落最後階段的下降速度，單位為公分／秒。" },
            { "The descent speed for the first stage of landing in cm/s. If this is zero then WPNAV_SPEED_DN is used", "降落第一階段的下降速度，單位為公分／秒；設為 0 時使用 WPNAV_SPEED_DN。" },
            { "Defines the speed in cm/s which the aircraft will attempt to maintain horizontally during a WP mission", "航點任務中嘗試維持的水平速度，單位為公分／秒。" },
            { "Defines the speed in cm/s which the aircraft will attempt to maintain while climbing during a WP mission", "航點任務中嘗試維持的爬升速度，單位為公分／秒。" },
            { "Defines the speed in cm/s which the aircraft will attempt to maintain while descending during a WP mission", "航點任務中嘗試維持的下降速度，單位為公分／秒。" },
            { "Defines the horizontal acceleration in cm/s/s used during missions", "任務飛行使用的水平加速度，單位為公分／秒平方。" },
            { "Defines the distance from a waypoint, that when crossed indicates the wp has been hit.", "航點到達判定距離；進入此距離內即視為已到達航點。" },
            { "The maximum vertical ascending velocity the pilot may request in cm/s", "操作者可要求的最大垂直爬升速度，單位為公分／秒。" },
            { "The maximum vertical descending velocity the pilot may request in cm/s.  If 0 PILOT_SPEED_UP value is used.", "操作者可要求的最大垂直下降速度，單位為公分／秒；設為 0 時使用 PILOT_SPEED_UP。" },
            { "Maximum lean angle in all flight modes", "所有飛行模式的最大傾斜角度；數值單位以單位欄為準。" },
            { "Point at which the motors start to spin expressed as a number from 0 to 1 in the entire output range.  Should be lower than MOT_SPIN_MIN.", "馬達開始旋轉的輸出比例，以整個輸出範圍的 0～1 表示；應低於 MOT_SPIN_MIN。" },
            { "Point at which the thrust starts expressed as a number from 0 to 1 in the entire output range.  Should be higher than MOT_SPIN_ARM.", "開始產生推力的輸出比例，以整個輸出範圍的 0～1 表示；應高於 MOT_SPIN_ARM。" },
            { "Point at which the thrust saturates expressed as a number from 0 to 1 in the entire output range", "推力達到飽和時的輸出比例，以整個輸出範圍的 0～1 表示。" },
            { "Motor thrust needed to hover expressed as a number from 0 to 1", "懸停所需的馬達推力，以 0～1 的比例表示。" },
            { "RC input options", "遙控輸入選項。" },
            { "Filter applied to acceleration to reduce noise.  Lower values reduce noise but add delay.", "降低加速度雜訊的濾波設定；較低的數值可減少雜訊，但會增加延遲。" }
        };

        private static readonly Dictionary<string, string> FmtTraditionalChineseParameterDescriptions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ACRO_LOCKING", "放開搖桿時啟用姿態鎖定。設為 2 時使用以四元數為基礎的姿態鎖定；啟用偏航速率控制或四元數鎖定時，可保持任意姿態。" },
                { "ACRO_BAL_PITCH", "設定特技與運動模式中，俯仰角回正至水平的速度。數值越大，飛行器回正越快；直升機使用此值設定俯仰軸虛擬平衡桿的衰減速率，數值越大，期望姿態與實際姿態間的差異衰減越快。" },
                { "ACRO_BAL_ROLL", "設定特技與運動模式中，橫滾角回正至水平的速度。數值越大，飛行器回正越快；直升機使用此值設定橫滾軸虛擬平衡桿的衰減速率，數值越大，期望姿態與實際姿態間的差異衰減越快。" },
                { "ACRO_OPTIONS", "設定特技模式的附加行為。Air-mode 會持續套用 ATC_THR_MIX_MAN（直升機不受影響）；僅速率迴路會停用角度穩定，只使用角速度穩定控制。" },
                { "ACRO_PITCH_RATE", "設定特技模式下俯仰軸的最大旋轉速率。數值越大，滿舵時的俯仰反應越快。" },
                { "ACRO_ROLL_RATE", "設定特技模式下橫滾軸的最大旋轉速率。數值越大，滿舵時的橫滾反應越快。" },
                { "ACRO_RP_EXPO", "設定特技模式橫滾與俯仰的指數曲線，使搖桿接近行程邊緣時可獲得更快的旋轉反應。" },
                { "ACRO_RP_RATE", "設定特技模式的最大橫滾與俯仰角速度。數值越大，旋轉反應越快。" },
                { "ACRO_RP_RATE_TC", "設定特技模式橫滾與俯仰角速度控制輸入的時間常數。數值較小時反應較直接銳利；數值較大時反應較柔和。" },
                { "ACRO_THR_MID", "設定特技模式的油門中點，用於調整搖桿中位所對應的油門輸出。" },
                { "ACRO_TRAINER", "選擇特技模式使用的輔助訓練功能，包括停用、自動回正，以及自動回正並限制傾角。" },
                { "ACRO_Y_EXPO", "設定特技模式偏航的指數曲線，使搖桿接近行程邊緣時可獲得更快的旋轉反應。" },
                { "ACRO_YAW_RATE", "設定特技模式下偏航軸的最大旋轉速率。數值越大，滿舵時的偏航反應越快。" },
                { "ACRO_Y_RATE", "設定特技模式的最大偏航角速度。數值越大，偏航旋轉反應越快。" },
                { "ACRO_Y_RATE_TC", "設定特技模式偏航角速度控制輸入的時間常數。數值較小時反應較直接銳利；數值較大時反應較柔和。" },
                { "ADSB_TYPE", "選擇 ADS-B 硬體或通訊類型；未安裝 ADS-B 裝置時應維持停用。" },
                { "AFS_ENABLE", "啟用進階失效保護系統。啟用前必須完成相關失效保護參數設定與實際測試。" },
                { "AHRS_COMP_BETA", "設定 AHRS 使用空速與 GPS 地速交叉修正時的時間常數；數值越大，越偏重 GPS 資料。" },
                { "AHRS_EKF_TYPE", "選擇飛控用於姿態與位置估算的 EKF 類型。一般情況請使用韌體建議值。" },
                { "AHRS_GPS_GAIN", "設定 GPS 對 AHRS 姿態修正的影響程度。固定翼通常保留預設值。" },
                { "AHRS_GPS_MINSATS", "設定允許 GPS 參與速度與姿態修正所需的最低衛星數量。" },
                { "AHRS_GPS_USE", "設定 AHRS 是否使用 GPS 進行導航與位置修正。正常飛行不建議任意停用。" },
                { "AHRS_ORIENTATION", "設定飛控安裝方向。若飛控不是箭頭朝前且水平安裝，必須選擇正確旋轉方向。" },
                { "AIRSPEED_CRUISE", "設定自動油門模式下的目標巡航空速，單位依欄位顯示。" },
                { "AIRSPEED_MIN", "設定自動飛行允許的最低空速；通常應高於失速速度並保留安全裕度。" },
                { "AIRSPEED_MAX", "設定自動飛行允許的最高目標空速，不可超過機體與動力系統的安全限制。" }
            };

        private static readonly Dictionary<string, string> FmtParameterTerms =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ACRO", "特技模式" }, { "AHRS", "姿態航向參考系統" }, { "ADSB", "ADS-B 航空監視" },
                { "AFS", "進階失效保護" }, { "AIRSPEED", "空速" }, { "ALT", "高度" }, { "ARSPD", "空速計" },
                { "ARMING", "解鎖" }, { "ATT", "姿態" }, { "AUTO", "自動模式" }, { "AVOID", "避障" },
                { "BARO", "氣壓計" }, { "BATT", "電池" }, { "BAT", "電池" }, { "BRD", "飛控板" },
                { "CAN", "CAN 匯流排" }, { "COMPASS", "羅盤" }, { "EKF", "擴展卡爾曼濾波" },
                { "FENCE", "地理圍籬" }, { "FLTMODE", "飛行模式" }, { "FRAME", "機架構型" },
                { "GPS", "衛星定位" }, { "INS", "慣性導航" }, { "LAND", "降落" }, { "LOIT", "盤旋" },
                { "MOT", "馬達" }, { "NAV", "導航" }, { "PILOT", "手動操控" }, { "Q", "垂直起降" },
                { "RC", "遙控器" }, { "RTL", "返航" }, { "SERIAL", "序列埠" }, { "SERVO", "伺服輸出" },
                { "TECS", "總能量控制" }, { "THR", "油門" }, { "WP", "航點" }, { "WPNAV", "航點導航" },
                { "ENABLE", "啟用" }, { "TYPE", "類型" }, { "RATE", "速率" }, { "MAX", "最大值" },
                { "MIN", "最小值" }, { "GAIN", "增益" }, { "USE", "使用" }, { "OPTIONS", "選項" },
                { "LOCKING", "姿態鎖定" }, { "PITCH", "俯仰" }, { "ROLL", "橫滾" }, { "YAW", "偏航" }
            };


        // Based on https://gist.github.com/Nazardo/e42de483a03ec2e1ef9348e23bec4f95
        // (c) 2019 M. Levra

        public sealed class NaturalStringComparer : IComparer<string>
        {
            #region IComparer<string> Members

            public int Compare(string x, string y)
            {
                return NaturalCompare(x, y);
            }

            #endregion

            public int NaturalCompare(string x, string y)
            {
                int indexX = 0;
                int indexY = 0;
                while (true)
                {
                    // Handle the case when one string has ended.
                    if (indexX == x.Length)
                    {
                        return indexY == y.Length ? 0 : -1;
                    }
                    if (indexY == y.Length)
                    {
                        return 1;
                    }

                    char charX = x[indexX];
                    char charY = y[indexY];
                    if (char.IsDigit(charX) && char.IsDigit(charY))
                    {
                        // Skip leading zeroes in numbers.
                        while (indexX < x.Length && x[indexX] == '0')
                        {
                            indexX++;
                        }
                        while (indexY < y.Length && y[indexY] == '0')
                        {
                            indexY++;
                        }

                        // Find the end of numbers
                        int endNumberX = indexX;
                        int endNumberY = indexY;
                        while (endNumberX < x.Length && char.IsDigit(x[endNumberX]))
                        {
                            endNumberX++;
                        }
                        while (endNumberY < y.Length && char.IsDigit(y[endNumberY]))
                        {
                            endNumberY++;
                        }

                        int digitsLengthX = endNumberX - indexX;
                        int digitsLengthY = endNumberY - indexY;

                        // If the lengths are different, then the longer number is bigger
                        if (digitsLengthX != digitsLengthY)
                        {
                            return digitsLengthX - digitsLengthY;
                        }
                        // Compare numbers digit by digit
                        while (indexX < endNumberX)
                        {
                            if (x[indexX] != y[indexY])
                                return x[indexX] - y[indexY];
                            indexX++;
                            indexY++;
                        }
                    }
                    else
                    {
                        // Plain characters comparison
                        int compareResult = char.ToUpperInvariant(charX).CompareTo(char.ToUpperInvariant(charY));
                        if (compareResult != 0)
                        {
                            return compareResult;
                        }
                        indexX++;
                        indexY++;
                    }
                }
            }
        }



        private void OnParamsOnSortCompare(object sender, DataGridViewSortCompareEventArgs args)
        {
            var fav1obj = Params[Fav.Index, args.RowIndex1].Value;
            var fav2obj = Params[Fav.Index, args.RowIndex2].Value;

            var fav1 = fav1obj == null ? false : (bool)fav1obj;

            var fav2 = fav2obj == null ? false : (bool)fav2obj;

            if (args.CellValue1 == null)
                return;

            if (args.CellValue2 == null)
                return;

            args.SortResult = naturalsorter.NaturalCompare(args.CellValue1.ToString(), args.CellValue2.ToString());
            args.Handled = true;

            if (fav1 && fav2)
            {
                return;
            }

            if (fav1 || fav2)
                args.SortResult = fav1.CompareTo(fav2) * (Params.SortOrder == SortOrder.Ascending ? -1 : 1);
        }

        private void updatedefaultlist(object crap)
        {
            try
            {
                if (paramfiles == null)
                {
                    string subdir = "";
                    if (MainV2.comPort.MAV.param.ContainsKey("Q_ENABLE") &&
                        MainV2.comPort.MAV.param["Q_ENABLE"].Value >= 1.0)
                    {
                        subdir = "QuadPlanes/";
                    }
                    try
                    {
                        paramfiles = GitHubContent.GetDirContent("ardupilot", "ardupilot", "/Tools/Frame_params/" + subdir, ".param");
                    }
                    catch (Exception ex)
                    {
                        log.Warn("Unable to load online frame presets; bundled presets remain available", ex);
                    }
                }

                if (!IsDisposed && !Disposing && IsHandleCreated)
                    BeginInvoke((Action)(() =>
                    {
                        if (!IsDisposed && !Disposing)
                            BindFramePresets(paramfiles);
                    }));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                Interlocked.Exchange(ref fmtPresetLookupPending, 0);
            }
        }

        private void BindFramePresets(List<GitHubContent.FileInfo> onlinePresets)
        {
            var selectedPath = (CMB_paramfiles.SelectedItem as GitHubContent.FileInfo)?.path;
            var choices = new List<GitHubContent.FileInfo>
            {
                new GitHubContent.FileInfo { name = "H420.param", path = "fmt:H420" }
            };
            if (onlinePresets != null)
                choices.AddRange(onlinePresets);
            CMB_paramfiles.DisplayMember = "name";
            CMB_paramfiles.DataSource = choices.ToArray();
            var selectedIndex = choices.FindIndex(item => item.path == selectedPath);
            CMB_paramfiles.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            CMB_paramfiles.Enabled = true;
            BUT_paramfileload.Enabled = true;
        }

        void filterList(string searchfor)
        {
            DateTime start = DateTime.Now;
            Params.Visible = false;
            if (searchfor.Length >= 2 || searchfor.Length == 0)
            {
                Regex filter = new Regex(searchfor.Replace("*", ".*").Replace("..*", ".*"), RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

                foreach (DataGridViewRow row in Params.Rows)
                {
                    string name = row.Cells[Command.Index].Value.ToString();
                    if (name != filterPrefix.TrimEnd('_') && !name.StartsWith(filterPrefix))
                    {
                        row.Visible = false;
                        continue;
                    }
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Value != null && filter.IsMatch(cell.Value.ToString()))
                        {
                            row.Visible = true;
                            break;
                        }
                        row.Visible = false;
                    }
                }
            }

            if (chk_modified.Checked)
            {
                foreach (DataGridViewRow row in Params.Rows)
                {
                    // is it modified? - always show
                    if (_changes.ContainsKey(row.Cells[Command.Index].Value))
                    {
                        row.Visible = true;
                    }
                    else
                    {
                        row.Visible = false;
                    }
                }
            }

            if (chk_none_default.Checked)
            {
                foreach (DataGridViewRow row in Params.Rows)
                {
                    row.Visible = row.Cells[Default_value.Index].Value.ToString() != row.Cells[Value.Index].Value.ToString();
                }
            }

            Params.Visible = true;

            log.InfoFormat("Filter: {0}ms", (DateTime.Now - start).TotalMilliseconds);
        }

        private void BUT_paramfileload_Click(object sender, EventArgs e)
        {
            var filepath = Settings.GetUserDataDirectory() + CMB_paramfiles.Text;

            try
            {
                var selected = (GitHubContent.FileInfo)CMB_paramfiles.SelectedValue;
                byte[] data;
                if (selected.path == "fmt:H420")
                {
                    using (var stream = typeof(ConfigRawParams).Assembly.GetManifestResourceStream(
                        "MissionPlanner.FMT.FrameParams.H420.param"))
                    using (var buffer = new MemoryStream())
                    {
                        if (stream == null)
                            throw new InvalidOperationException("找不到 H420 基礎參數資源。");
                        stream.CopyTo(buffer);
                        data = buffer.ToArray();
                    }
                }
                else
                    data = GitHubContent.GetFileContent("ArduPilot", "ardupilot", selected.path);

                File.WriteAllBytes(filepath, data);

                var param2 = ParamFile.loadParamFile(filepath);

                Form paramCompareForm = new ParamCompare(Params, MainV2.comPort.MAV.param, param2);

                ThemeManager.ApplyThemeTo(paramCompareForm);
                if (paramCompareForm.ShowDialog() == DialogResult.OK)
                {
                    CustomMessageBox.Show("Loaded parameters, please make sure you write them!", "Loaded");
                }

                // no activate the user needs to click write.
                //this.Activate();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Failed to load file.\n" + ex);
            }
        }

        private void CMB_paramfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void BUT_reset_params_Click(object sender, EventArgs e)
        {
            if (
                CustomMessageBox.Show("Reset all parameters to default\nAre you sure!!", "Reset",
                    MessageBoxButtons.YesNo) == (int)DialogResult.Yes)
            {
                try
                {
                    MainV2.comPort.setParam(new[] { "FORMAT_VERSION", "SYSID_SW_MREV" }, 0);
                    Thread.Sleep(1000);
                    MainV2.comPort.doReboot(false, true);
                    MainV2.comPort.BaseStream.Close();


                    CustomMessageBox.Show(
                        "Your board is now rebooting, You will be required to reconnect to the autopilot.");
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    CustomMessageBox.Show(Strings.ErrorCommunicating + "\n" + ex, Strings.ERROR);
                }
            }
        }

        private readonly System.Timers.Timer _filterTimer = new System.Timers.Timer();
        private string cellEditValue;

        private void txt_search_TextChanged(object sender, EventArgs e)
        {
            _filterTimer.Elapsed -= FilterTimerOnElapsed;
            _filterTimer.Stop();
            _filterTimer.Interval = 500;
            _filterTimer.Elapsed += FilterTimerOnElapsed;
            _filterTimer.Start();
        }

        public void FilterTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            _filterTimer.Stop();
            Invoke((Action)delegate
           {
               filterList(txt_search.Text);
               optionsControlUpateBounds();
           });
        }

        private void Params_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Only process the Description column
            if (e.RowIndex == -1 || startup)
                return;

            if (e.ColumnIndex == Options.Index &&
                Params[e.ColumnIndex, e.RowIndex] is DataGridViewButtonCell)
            {
                ShowBitmaskEditor(e.RowIndex);
                return;
            }

            if (e.ColumnIndex == Desc.Index)
            {
                try
                {
                    string descStr = Params[e.ColumnIndex, e.RowIndex].Value.ToString();
                    CheckForUrlAndLaunchInBrowser(descStr);
                }
                catch
                {
                }
            }

            if (e.ColumnIndex == Fav.Index)
            {
                var check = Params[e.ColumnIndex, e.RowIndex].EditedFormattedValue;
                var name = Params[Command.Index, e.RowIndex].Value.ToString();

                if (check != null && (bool)check)
                {
                    // add entry
                    Settings.Instance.AppendList("fav_params", name);
                }
                else
                {
                    // remove entry
                    var list = Settings.Instance.GetList("fav_params");
                    Settings.Instance.SetList("fav_params", list.Where(s => s != name));
                }

                Params.Sort(Command, ListSortDirection.Ascending);
            }
        }

        public static void CheckForUrlAndLaunchInBrowser(string stringWithPossibleUrl)
        {
            if (stringWithPossibleUrl == null)
                return;

            foreach (string url in stringWithPossibleUrl.Split(' '))
            {
                Uri uriResult;
                if (Uri.TryCreate(url, UriKind.Absolute, out uriResult) &&
                    (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    try
                    {
                        // launch the URL in your default browser
                        System.Diagnostics.Process process = new System.Diagnostics.Process();
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.FileName = url;
                        process.Start();
                    }
                    catch { }

                    // only handle the first valid URL
                    return;
                }
            }
        }

        private void BUT_commitToFlash_Click(object sender, EventArgs e)
        {
            try
            {
                MainV2.comPort.doCommand((byte)MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.PREFLIGHT_STORAGE, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
            }
            catch
            {
                CustomMessageBox.Show("Invalid command");
                return;
            }

            CustomMessageBox.Show("Parameters committed to non-volatile memory");
            return;
        }

        private void chk_filter_CheckedChanged(object sender, EventArgs e)
        {
            FilterTimerOnElapsed(null, null);
        }

        private void Params_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            cellEditValue = Params[e.ColumnIndex, e.RowIndex].Value.ToString();
        }

        private void BUT_refreshTable_Click(object sender, EventArgs e)
        {
            startup = true;
            processToScreen();
            startup = false;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string txt = treeView1.SelectedNode.Text + "_";
            if (txt == "All_") txt = "";
            filterPrefix = txt;
            FilterTimerOnElapsed(null, null);
        }

        private void but_collapse_Click(object sender, EventArgs e)
        {
            if (splitContainer1.Panel1Collapsed)
            {
                but_collapse.Text = "<";
                splitContainer1.Panel1Collapsed = false;
                BuildTree();
            }
            else
            {
                but_collapse.Text = ">";
                splitContainer1.Panel1Collapsed = true;
                filterPrefix = "";
                FilterTimerOnElapsed(null, null);
            }
        }

        Control optionsControl;

        private void ShowBitmaskEditor(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Params.Rows.Count)
                return;

            var row = Params.Rows[rowIndex];
            var paramName = Convert.ToString(row.Cells[Command.Index].Value);
            if (string.IsNullOrWhiteSpace(paramName))
                return;

            var mcb = new MavlinkCheckBoxBitMask { OptionTextTranslator = LocalizeFmtOption };
            var list = new MAVLink.MAVLinkParamList();
            var type = MAVLink.MAV_PARAM_TYPE.INT32;
            if (MainV2.comPort.MAV.param.ContainsKey(paramName))
                type = MainV2.comPort.MAV.param[paramName].TypeAP;

            double value;
            if (!double.TryParse(Convert.ToString(row.Cells[Value.Index].Value),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return;

            list.Add(new MAVLink.MAVLinkParam(paramName, value, type));
            mcb.setup(paramName, list);
            mcb.myLabel1.Text = MissionPlanner.FMT.FmtParameterDrafts.Translate(mcb.myLabel1.Text);
            var originalDescription = mcb.label1.Text;
            mcb.label1.Text = GetFmtDisplayDescription(paramName, originalDescription);
            var originalTip = new ToolTip();
            originalTip.SetToolTip(mcb.label1, originalDescription);
            mcb.Disposed += (sender, args) => originalTip.Dispose();
            mcb.ValueChanged += (o, x, newValue) =>
            {
                if (rowIndex < Params.Rows.Count)
                    Params.Rows[rowIndex].Cells[Value.Index].Value = newValue;
                Params.InvalidateRow(rowIndex);
                mcb.Focus();
            };

            var frm = mcb.ShowUserControl();
            frm.TopMost = true;
        }

        // Create and place the relevant control in the options column when a row is entered
        private void Params_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (optionsControl != null)
            {
                try
                {
                    Params.Controls.Remove(optionsControl);
                    optionsControl.Dispose();
                } catch { }
                optionsControl = null;
            }

            string param_name = Params[Command.Index, e.RowIndex].Value.ToString();
            string vehicle = MainV2.comPort.MAV.cs.firmware.ToString();
            var options = ParameterMetaDataRepository.GetParameterOptionsInt(param_name, vehicle);
            var bitmask = ParameterMetaDataRepository.GetParameterBitMaskInt(param_name, vehicle);
            // If this is a bitmask, create a button to open the bitmask editor
            // (this is better than trying to cram the bitmask checkboxes into the small cell)
            if (bitmask.Count > 0)
            {
                if (!(Params[Options.Index, e.RowIndex] is DataGridViewButtonCell))
                {
                    Params[Options.Index, e.RowIndex] = new DataGridViewButtonCell
                    {
                        Value = BitmaskButtonText,
                        FlatStyle = FlatStyle.Flat,
                        ToolTipText = Params[Options.Index, e.RowIndex].ToolTipText
                    };
                }
            }
            // If there are options, create a combo box and populate it with the options
            else if (options.Count > 0)
            {
                ComboBox cmb = new ComboBox() { Dock = DockStyle.Fill };
                cmb.DropDownStyle = ComboBoxStyle.DropDownList;
                cmb.DataSource = options.Select(option => new KeyValuePair<int, string>(option.Key, LocalizeFmtOption(option.Value))).ToList();
                cmb.DisplayMember = "Value";
                cmb.ValueMember = "Key";

                // Widen the dropdown menu if the text is too long
                // https://www.codeproject.com/Articles/5801/Adjust-combo-box-drop-down-list-width-to-longest-s
                cmb.DropDown += (s, ev) =>
                {
                    ComboBox senderComboBox = (ComboBox)s;
                    int width = senderComboBox.DropDownWidth;
                    Graphics g = senderComboBox.CreateGraphics();
                    Font font = senderComboBox.Font;
                    int vertScrollBarWidth =
                        (senderComboBox.Items.Count > senderComboBox.MaxDropDownItems)
                        ? SystemInformation.VerticalScrollBarWidth : 0;

                    int newWidth;
                    foreach (KeyValuePair<int, string> item in ((ComboBox)s).Items)
                    {
                        newWidth = (int)g.MeasureString(item.Value, font).Width
                            + vertScrollBarWidth;
                        if (width < newWidth)
                        {
                            width = newWidth;
                        }
                    }
                    senderComboBox.DropDownWidth = width;
                };

                ThemeManager.ApplyThemeTo(cmb);

                // Create a blank panel to hold the combo box
                // (this blanks out the cell so that the text doesn't peak through)
                optionsControl = new Panel();
                ((Panel)optionsControl).BackColor = Params.Rows[e.RowIndex].InheritedStyle.BackColor;
                optionsControl.Controls.Add(cmb);
                optionsControl.Bounds = Params.GetCellDisplayRectangle(Options.Index, e.RowIndex, false);
                Params.Controls.Add(optionsControl);

                // Populate the current selection from the cell value, if it's valid
                int val = -1;
                if(int.TryParse(Params[Value.Index, e.RowIndex].Value.ToString(), out val))
                {
                    cmb.SelectedValue = val;
                }
                else
                {
                    cmb.SelectedIndex = -1;
                }

                // When the combo box selection changes, update the cell value
                cmb.SelectedIndexChanged += (s, a) =>
                {
                    Params.CurrentRow.Cells[Value.Index].Value = cmb.SelectedValue.ToString();
                    Params.Invalidate();
                };
            }

            // Otherwise, this is a simple numeric parameter
            else
            {
                double min = -32768.0;
                double max = 32768.0;
                if (ParameterMetaDataRepository.GetParameterRange(param_name, ref min, ref max, vehicle))
                {
                    // Default increment is the range divided by 1000, rounded to the nearest power of 10
                    double inc = Math.Pow(10, Math.Floor(Math.Log10((max - min) / 1000)));

                    ParameterMetaDataRepository.GetParameterIncrement(param_name, ref inc, vehicle);

                    NumericUpDown num = new NumericUpDown() { Dock = DockStyle.Fill };

                    // Set the number of decimal places based on the increment, or the minimum value if it's smaller (but not zero)
                    int decimalPlaces = (int)Math.Round(Math.Max(0, -Math.Log10(Math.Abs(inc))));
                    if (Math.Abs(min) < inc && Math.Abs(min) >= 1e-9)
                    {
                        decimalPlaces = (int)Math.Round(Math.Max(0, -Math.Log10(Math.Abs(min))));
                    }
                    num.DecimalPlaces = decimalPlaces;
                    num.Minimum = Math.Round((decimal)min, num.DecimalPlaces);
                    num.Maximum = Math.Round((decimal)max, num.DecimalPlaces);
                    num.Increment = Math.Round((decimal)inc, num.DecimalPlaces);

                    // Parse the cell. Clamp the value to the bounds.
                    decimal val = num.Minimum;
                    decimal.TryParse(Params[Value.Index, e.RowIndex].Value?.ToString(), out val);
                    val = Math.Min(val, num.Maximum);
                    val = Math.Max(val, num.Minimum);
                    num.Value = Math.Round(val, num.DecimalPlaces);

                    // Update the cell if the text in the box changes
                    num.TextChanged += (s, a) =>
                    {
                        Params.CurrentRow.Cells[Value.Index].Value = num.Text;
                        Params.Invalidate();
                    };

                    ThemeManager.ApplyThemeTo(num);

                    optionsControl = new Panel();
                    ((Panel)optionsControl).BackColor = Params.Rows[e.RowIndex].InheritedStyle.BackColor;
                    optionsControl.Controls.Add(num);
                    optionsControl.Bounds = Params.GetCellDisplayRectangle(Options.Index, e.RowIndex, false);
                    Params.Controls.Add(optionsControl);

                }

            }

        }

        // Upate the size and location of our options control whenever a scroll or resize happens
        private void optionsControlUpateBounds()
        {
            if (optionsControl != null)
            {
                if (Params.CurrentRow == null)
                {
                    Params.Controls.Remove(optionsControl);
                    optionsControl.Dispose();
                    optionsControl = null;
                    return;
                }
                var bounds = Params.GetCellDisplayRectangle(Options.Index, Params.CurrentRow.Index, false);
                optionsControl.Bounds = bounds;
                optionsControl.Visible = bounds.Height > 0;
            }
        }

        private void Params_Scroll(object sender, ScrollEventArgs e)
        {
            optionsControlUpateBounds();
        }

        private void Params_RowHeightChanged(object sender, DataGridViewRowEventArgs e)
        {
            if(e.Row == Params.CurrentRow || e.Row.Index + 1 == Params.CurrentRow.Index)
            {
                optionsControlUpateBounds();
            }
        }

        private void Params_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (e.Column.Index == Options.Index || e.Column.Index + 1 == Options.Index)
            {
                optionsControlUpateBounds();
            }
        }
    }

}
