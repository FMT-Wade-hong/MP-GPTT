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

            CMB_paramfiles.Enabled = false;
            BUT_paramfileload.Enabled = false;
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

                            // Keep the parameter table and metadata in their original English form,
                            // but show the same Traditional Chinese explanation from every cell in
                            // the row. This prevents the tooltip content from changing according to
                            // which column happens to be under the pointer.
                            foreach (DataGridViewCell cell in row.Cells)
                                cell.ToolTipText = tooltipDescription;

                            var range = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Range, MainV2.comPort.MAV.cs.firmware.ToString());
                            var options = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Values, MainV2.comPort.MAV.cs.firmware.ToString());
                            var units = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Units, MainV2.comPort.MAV.cs.firmware.ToString());

                            row.Cells[Units.Index].Value = units;
                            row.Cells[Options.Index].Value = (range + "\n" + options.Replace(",", "\n")).Trim();
                            if (options.Length > 0)
                                row.Cells[Options.Index].ToolTipText = tooltipDescription;
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
                                row.Cells[Options.Index].ToolTipText = tooltipDescription;
                            }
                            row.Cells[Desc.Index].Value = metaDataDescription;
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
                            var tooltip = row.Cells[Options.Index].ToolTipText;
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
            if (!CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return englishDescription;

            string exact;
            if (FmtTraditionalChineseParameterDescriptions.TryGetValue(parameterName, out exact))
                return exact;

            var parts = (parameterName ?? string.Empty).Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            var translated = new List<string>();
            foreach (var part in parts)
            {
                string text;
                translated.Add(FmtParameterTerms.TryGetValue(part, out text) ? text : part);
            }

            var topic = translated.Count == 0 ? "飛控" : string.Join("／", translated);
            return "用途：設定「" + topic + "」相關功能。\r\n" +
                   "參數名稱：" + parameterName + "（名稱保留英文以對應飛控）。\r\n" +
                   "提醒：修改前請確認數值範圍、單位及目前飛行器構型；不確定時請保留預設值。";
        }

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

                var choices = new List<GitHubContent.FileInfo>
                {
                    new GitHubContent.FileInfo { name = "H420.param", path = "fmt:H420" }
                };
                if (paramfiles != null)
                    choices.AddRange(paramfiles);
                BeginInvoke((Action)delegate
               {
                   CMB_paramfiles.DataSource = choices.ToArray();
                   CMB_paramfiles.DisplayMember = "name";
                   CMB_paramfiles.Enabled = true;
                   BUT_paramfileload.Enabled = true;
               });
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
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

            var mcb = new MavlinkCheckBoxBitMask();
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
                cmb.DataSource = options;
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
