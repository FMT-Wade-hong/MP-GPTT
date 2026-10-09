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
        private string fmtMetadataVehicle;

        internal static string GetFmtMetadataVehicle(string firmware, string versionText)
        {
            string family;
            switch (firmware)
            {
                case "ArduCopter2": case "ArduCopter": family = "Copter"; break;
                case "ArduPlane": family = "Plane"; break;
                case "ArduRover": case "Rover": family = "Rover"; break;
                case "ArduSub": family = "Sub"; break;
                default: return firmware;
            }
            try
            {
                var version = VersionDetection.GetVersion(versionText);
                if (version.Major == 4 && version.Minor >= 5 && version.Minor <= 7 && version.Build >= 0)
                    return family + version.ToString(3);
            }
            catch { /* Unknown firmware version: keep the existing lookup path. */ }
            return firmware;
        }

        public ConfigRawParams()
        {
            InitializeComponent();
            BUT_rerequestparams.Text = "重新載入全部參數";
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
            var metadataVehicle = GetFmtMetadataVehicle(MainV2.comPort.MAV.cs.firmware.ToString(), MainV2.comPort.MAV.VersionString);
            if (fmtMetadataVehicle != metadataVehicle) startup = true;
            fmtMetadataVehicle = metadataVehicle;
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
                    if (ParameterMetaDataRepository.GetParameterRebootRequired(value, (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString())))
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
                    CustomMessageBox.Show("可用參數數量已變更。不會自動下載整張參數表；新增參數可於安全停機後重新連線載入。", "參數");
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

                    Form paramCompareForm = new ParamCompare(Params, MainV2.comPort.MAV.param, param2)
                    { StageParameter = StageComparedParameter };

                    ThemeManager.ApplyThemeTo(paramCompareForm);
                    paramCompareForm.ShowDialog();
                }
            }
        }

        private void BUT_rerequestparams_Click(object sender, EventArgs e)
        {
            FmtPageParameterRefresh.ReloadAll(this, BUT_rerequestparams, () =>
                {
                    _changes.Clear();
                    startup = true;
                    try { processToScreen(); FilterTimerOnElapsed(null, null); }
                    finally { startup = false; }
                }, _changes.Count > 0);
        }

        private void Params_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex == -1 || startup || e.ColumnIndex != Value.Index)
                return;
            ValidateParameterEdit(e);
        }

        internal bool StageComparedParameter(string name, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            var row = Params.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => !r.IsNewRow &&
                string.Equals(Convert.ToString(r.Cells[Command.Index].Value).Trim(), name, StringComparison.Ordinal));
            if (row == null) return false;
            Params.EndEdit();
            cellEditValue = Convert.ToString(row.Cells[Value.Index].Value);
            Params.CellValueChanged -= Params_CellValueChanged;
            try { row.Cells[Value.Index].Value = value.ToString("R", CultureInfo.InvariantCulture); }
            finally { Params.CellValueChanged += Params_CellValueChanged; }
            // A programmatic compare must validate and queue explicitly, even when startup
            // suppresses the grid event. Never write to the vehicle from this path.
            ValidateParameterEdit(new DataGridViewCellEventArgs(Value.Index, row.Index));
            return _changes.ContainsKey(name) && (double)_changes[name] == value &&
                row.Cells[Value.Index].Style.BackColor == Color.Green;
        }

        private void ValidateParameterEdit(DataGridViewCellEventArgs e)
        {
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
                    ParameterMetaDataConstants.ReadOnly, (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString()));
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
                    ref min, ref max, (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString())))
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
                            ParameterMetaDataConstants.Description, (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString()));
                        if (!string.IsNullOrEmpty(metaDataDescription))
                        {
                            var tooltipDescription = AddNewLinesForTooltip(
                                GetFmtTooltipDescription(value, metaDataDescription));

                            // Localize display only; parameter identifiers and wire values stay unchanged.
                            foreach (DataGridViewCell cell in row.Cells)
                                cell.ToolTipText = tooltipDescription;

                            var range = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Range, (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString()));
                            var options = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Values, (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString()));
                            var units = ParameterMetaDataRepository.GetParameterMetaData(value,
                                ParameterMetaDataConstants.Units, (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString()));

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
                        else if (fmtMetadataVehicle != MainV2.comPort.MAV.cs.firmware.ToString())
                        {
                            row.Cells[Desc.Index].Value = "缺少對應版本參數說明：" + fmtMetadataVehicle;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }

                    try
                    {
                        var bitmask = ParameterMetaDataRepository.GetParameterBitMaskInt(value,
                            (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString()));
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

            // IDs can retain their spelling while meaning, units or warnings change.
            // Only complete source text is a safe translation key across firmware.
            var reviewed = MissionPlanner.FMT.FmtParameterDrafts.Translate(englishDescription);
            if (reviewed != englishDescription) return reviewed;

            string exact;
            if (FmtDescriptionTranslations.TryGetValue(englishDescription ?? string.Empty, out exact))
                return exact;

            return MissionPlanner.FMT.FmtParameterDrafts.Translate(englishDescription);
        }

        private static string LocalizeFmtOption(string label)
        {
            if (!CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return label;
            // Translate the action, not protocol names. Full source match only; retain
            // the exact original option so firmware-specific labels remain identifiable.
            string reviewed;
            if (FmtReviewedOptionTranslations.TryGetValue((label ?? string.Empty).Trim(), out reviewed))
                return reviewed + " (" + label + ")";
            // Technical names remain verbatim, including compound option labels.
            if (System.Text.RegularExpressions.Regex.IsMatch(label ?? string.Empty,
                @"\b(Roll|Pitch|YawD?|AutoTune|VFF|PID|EKF\d*|GPS|GNSS|IMU|AHRS|RTL|SmartRTL|Loiter|Stabilize|AltHold|Guided|Acro|Brake|PosHold|Circle|Land|Auto|Manual|CRSF|MAVLink|ESC|RCIN|NMEA|Rate [PID]|Angle P|Max Gain|Tune Check|HDoP|NSats|AGL|KF)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch((label ?? string.Empty).Trim(), @"^[A-Z][A-Z0-9_/+.-]+$"))
                return label;
            string translated;
            return FmtOptionTranslations.TryGetValue((label ?? string.Empty).Trim(), out translated)
                ? translated + " (" + label + ")" : label;
        }

        private static string LocalizeFmtOptions(string options)
        {
            return string.Join(",", (options ?? string.Empty).Split(',').Select(option =>
            {
                var separator = option.IndexOf(':');
                return separator < 0 ? option : option.Substring(0, separator + 1) + LocalizeFmtOption(option.Substring(separator + 1));
            }));
        }

        private static readonly Dictionary<string, string> FmtReviewedOptionTranslations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "0 PWM when disarmed", "上鎖時輸出 0 PWM" },
            { "2D Fix", "2D 定位" },
            { "2nd Sensor", "第二個感測器" },
            { "2ndBaro", "第二個 Baro" },
            { "2ndSensor", "第二個感測器" },
            { "2-position switch", "兩段開關" },
            { "3D DGPS Fix", "3D DGPS 定位" },
            { "3D Fix", "3D 定位" },
            { "3D RTK Fixed", "3D RTK Fixed 解" },
            { "3D RTK Float", "3D RTK Float 解" },
            { "3-position switch", "三段開關" },
            { "3rd Sensor", "第三個感測器" },
            { "3rdBaro", "第三個 Baro" },
            { "4th Sensor", "第四個感測器" },
            { "5th Sensor", "第五個感測器" },
            { "6th Sensor", "第六個感測器" },
            { "After first climb yaw reset", "首次爬升 Yaw 重設後" },
            { "AGL KF for optflow scaling", "使用 AGL KF 換算 optical flow" },
            { "On ground and after first climb yaw reset", "地面及首次爬升 Yaw 重設後" },
            { "External yaw sensor with compass fallback (Deprecated in 4.1+ see EK3_SRCn_YAW)", "使用外部 Yaw 感測器，失效時改用 compass（4.1 起棄用，參閱 EK3_SRCn_YAW）" },
            { "Use external yaw sensor (Deprecated in 4.1+ see EK3_SRCn_YAW)", "使用外部 Yaw 感測器（4.1 起棄用，參閱 EK3_SRCn_YAW）" },
            { "GPS 2D pos", "GPS 二維位置" },
            { "GPS 2D vel and 2D pos", "GPS 二維速度與二維位置" },
            { "GPS 3D Vel and 2D Pos", "GPS 三維速度與二維位置" },
            { "GPS with Compass Fallback", "GPS，失效時改用 compass" },
            { "Use GPS", "使用 GPS" },
            { "No GPS", "不使用 GPS" },
            { "yaw error", "Yaw 誤差" },
            { "ESC Telemetry", "ESC 遙測" },
            { "NMEA Output", "NMEA 輸出" },
            { "MAVLink High Latency", "MAVLink 高延遲鏈路" },
            { "SmartRTL or Land", "SmartRTL；無法使用時 Land" },
            { "SmartRTL or RTL", "SmartRTL；無法使用時 RTL" },
            { "Default+IMU", "預設加 IMU" },
            { "Default+Motors", "預設加馬達" },
            { "Default+RCIN", "預設加 RCIN" },
            { "DefaultBus", "預設匯流排" },
            { "DifferentialSpoilerLeft1", "左 differential spoiler 1" },
            { "DifferentialSpoilerLeft2", "左 differential spoiler 2" },
            { "DifferentialSpoilerRight1", "右 differential spoiler 1" },
            { "DifferentialSpoilerRight2", "右 differential spoiler 2" },
            { "Digital", "數位" },
            { "Portable", "可攜式" },
            { "Pedestrian", "步行" },
            { "Automotive", "車輛" },
            { "Sea", "海上" },
            { "Aviation", "航空" },
            { "cylinder", "圓柱形" },
            { "cone", "圓錐形" },
            { "sphere", "球形" },
            { "Direct Drive Fixed Pitch (Tail RSC only)", "Direct Drive Fixed Pitch（僅 Tail RSC）" },
            { "DroneCAN-out on AP_Periph", "AP_Periph 的 DroneCAN 輸出" },
            { "COM1(RS232) on GSOF", "GSOF 的 COM1（RS232）" },
            { "COM2(TTL) on GSOF", "GSOF 的 COM2（TTL）" },
            { "External PCA9685", "外接 PCA9685" },
            { "External ToshibaLED", "外接 ToshibaLED" },
            { "Internal ToshibaLED", "內建 ToshibaLED" },
            { "IS31FL3195 External", "外接 IS31FL3195" },
            { "IS31FL3195 Internal", "內建 IS31FL3195" },
            { "LP5562 External", "外接 LP5562" },
            { "LP5562 Internal", "內建 LP5562" },
            { "NCP5623 External", "外接 NCP5623" },
            { "NCP5623 Internal", "內建 NCP5623" },
            { "HighGain (-20C to 150C)", "高增益（-20°C～150°C）" },
            { "LowGain (50C to 550C)", "低增益（50°C～550°C）" },
            { "INS(INertial Sensors - accels & gyros)", "INS（慣性感測器：accelerometer 與 gyro）" },
            { "ICE Starter", "ICE 啟動器" },
            { "InvertedFlight", "倒飛" },
            { "Loweheiser starter", "Loweheiser 啟動器" },
            { "Loweheiser throttle", "Loweheiser 油門" },
            { "MAX31865 2 or 4 wire", "MAX31865 二線或四線" },
            { "MAX31865 3 wire", "MAX31865 三線" },
            { "NMEA AIVDM message", "NMEA AIVDM 訊息" },
            { "On", "開啟" },
            { "Performance", "效能" },
            { "TIBQ76952-I2C (Periph only)", "TIBQ76952-I2C（僅 Periph）" },
            { "TelemetryRadioRSSI", "遙測電台 RSSI" },
            { "TiltMotorFrontLeft", "左前馬達傾轉" },
            { "TiltMotorFrontRight", "右前馬達傾轉" },
            { "TiltMotorRearLeft", "左後馬達傾轉" },
            { "TiltMotorRearRight", "右後馬達傾轉" },
            { "TiltMotorsFront", "前方馬達傾轉" },
            { "TiltMotorsRear", "後方馬達傾轉" },
            { "TrafficLight", "紅黃綠狀態燈" },
            { "Trigger", "觸發" },
            { "TCP client", "TCP 用戶端" },
            { "TCP discard test", "TCP 丟棄測試" },
            { "TCP echo test", "TCP echo 測試" },
            { "TCP reflect test", "TCP reflect 測試" },
            { "TCP server", "TCP 伺服端" },
            { "UDP client", "UDP 用戶端" },
            { "UDP echo test", "UDP echo 測試" },
            { "UDP server", "UDP 伺服端" },
            { "Up", "向上" },
            { "V1 Protocol", "V1 協定" },
            { "V2 Protocol", "V2 協定" },
            { "1st harmonic", "第 1 次諧波" },
            { "2nd harmonic", "第 2 次諧波" },
            { "3rd harmonic", "第 3 次諧波" },
            { "4th harmonic", "第 4 次諧波" },
            { "5th harmonic", "第 5 次諧波" },
            { "6th harmonic", "第 6 次諧波" },
            { "7th harmonic", "第 7 次諧波" },
            { "8th harmonic", "第 8 次諧波" },
            { "9th harmonic", "第 9 次諧波" },
            { "10th harmonic", "第 10 次諧波" },
            { "11th harmonic", "第 11 次諧波" },
            { "12th harmonic", "第 12 次諧波" },
            { "13th harmonic", "第 13 次諧波" },
            { "14th harmonic", "第 14 次諧波" },
            { "15th harmonic", "第 15 次諧波" },
            { "16th harmonic", "第 16 次諧波" },
            { "4th hamronic", "第 4 次諧波" },
            { "absolute", "絕對" },
            { "ActiveForSafetyDisable", "允許操作以解除 safety" },
            { "ActiveForSafetyEnable", "允許操作以啟用 safety" },
            { "ActiveWhenArmed", "解鎖時有效" },
            { "AddAtToMessages", "在訊息加上 AT" },
            { "ADSBMavlinkProcessing", "處理 ADSB MAVLink" },
            { "Aileron", "副翼" },
            { "Airbrakes", "減速板" },
            { "Aircraft", "航空器" },
            { "Alarm", "警報" },
            { "All+FastATT", "全部加高速姿態資料" },
            { "All+FastIMU", "全部加高速 IMU" },
            { "All+FastIMU+PID", "全部加高速 IMU 與 PID" },
            { "All+FullIMU", "全部加完整 IMU" },
            { "All+MotBatt", "全部加 MotBatt" },
            { "AllIMUs", "全部 IMU" },
            { "AllowDescentSpeedup", "允許加快下降" },
            { "AllowNonGPSPosition", "允許非 GPS 位置" },
            { "AllowSpeedMismatchRecovery", "允許速度不一致後恢復" },
            { "Analog", "類比" },
            { "AnalogPin", "類比腳位" },
            { "Any", "任一" },
            { "AppliedBySensor", "由感測器套用" },
            { "arm", "解鎖" },
            { "arm_toggle", "切換解鎖／上鎖" },
            { "Arm-Land-RTL", "解鎖／Land／RTL" },
            { "ArmDisarm", "解鎖／上鎖" },
            { "ArmingOnly", "僅解鎖時" },
            { "ArmOnHighThrottle", "高油門時解鎖" },
            { "ArmOrDisarm", "解鎖或上鎖" },
            { "ARMVtol", "僅指定 VTOL 模式解鎖" },
            { "Assist", "輔助" },
            { "AutoAlways", "自動，持續" },
            { "AutoEnableDisableFloorOnLanding", "自動啟用，降落時停用最低高度限制" },
            { "AutoEnableOff", "停用自動啟用" },
            { "AutoEnableOnlyWhenArmed", "僅解鎖時自動啟用" },
            { "AutoEnableOnTakeoff", "起飛時自動啟用" },
            { "AutoLanding", "自動降落" },
            { "AutoThrottle", "自動油門" },
            { "AuxAuth", "輔助解鎖授權" },
            { "AviationStyleAH", "航空式人工地平儀" },
            { "Back", "後方" },
            { "Back-Left", "左後" },
            { "Back-Right", "右後" },
            { "backward", "向後" },
            { "Barometer", "氣壓計" },
            { "Battery", "電池" },
            { "Binary", "兩段式" },
            { "Blend", "融合" },
            { "Boat", "船" },
            { "BodyFrameRoll", "機體座標 Roll" },
            { "BoostThrottle", "提升油門" },
            { "Brushed", "有刷" },
            { "BrushedBiPolar", "有刷雙極" },
            { "BrushedWithRelay", "有刷搭配 relay" },
            { "BrushlessPWM", "無刷 PWM" },
            { "CalRequireGPS", "校正需要 GPS" },
            { "Camera", "相機" },
            { "camera_source_toggle", "切換相機來源" },
            { "camera_trigger", "觸發相機" },
            { "CameraAperture", "相機光圈" },
            { "CameraFocus", "相機對焦" },
            { "CameraISO", "相機 ISO" },
            { "CameraShutterSpeed", "相機快門速度" },
            { "CameraTrigger", "觸發相機" },
            { "CameraZoom", "相機變焦" },
            { "CardinalDirections", "基本方位" },
            { "Ceiling", "頂部" },
            { "Center", "中央" },
            { "CheckAndFix", "檢查並修正" },
            { "CheckOnly", "僅檢查" },
            { "ClearDNADatabase", "清除 DNA 資料庫" },
            { "Clockwise", "順時針" },
            { "ClockwiseX", "順時針 X" },
            { "CompanionComputer", "伴隨電腦" },
            { "Console", "主控台" },
            { "ConstantThrust", "固定推力" },
            { "Continue", "繼續" },
            { "ContinueAfterLand", "降落後繼續" },
            { "Continuous", "連續" },
            { "ContinuousRotation", "連續旋轉" },
            { "Counter-Clockwise", "逆時針" },
            { "Crisp", "反應俐落" },
            { "Custom", "自訂" },
            { "Scale FF by the ratio of VTOL to plane angle P gains in Position 1 phase of transition into VTOL flight as well as reducing VTOL angle P based on airspeed.", "轉入 VTOL 的 Position 1 階段，依 VTOL／固定翼 Angle P 比例縮放 FF，同時依空速降低 VTOL Angle P" },
            { "Scale FF by the ratio of VTOL/plane angle P gains in VTOL modes rather than reducing VTOL angle P based on airspeed.", "VTOL 模式依 VTOL／固定翼 Angle P 比例縮放 FF，而非依空速降低 VTOL Angle P" },
            { "Suppress speed scaling during auto takeoffs to be 1 or less to prevent oscillations without airspeed sensor.", "自動起飛將速度縮放限制在 1 以下或等於 1，避免無空速計時振盪" },
            { "Supress speed scaling during auto takeoffs to be 1 or less to prevent oscillations without airspeed sensor.", "自動起飛將速度縮放限制在 1 以下或等於 1，避免無空速計時振盪" },
            { "Surpress speed scaling during auto takeoffs to be 1 or less to prevent oscillations without airpseed sensor.", "自動起飛將速度縮放限制在 1 以下或等於 1，避免無空速計時振盪" },
            { "Tilt rotor tilt motors up when disarmed in FW modes (except manual) to prevent ground strikes", "固定翼模式（MANUAL 除外）上鎖時，傾轉旋翼朝上以避免碰地" },
            { "Tilt rotor-tilt motors up when disarmed in FW modes (except manual) to prevent ground strikes.", "固定翼模式（MANUAL 除外）上鎖時，傾轉旋翼朝上以避免碰地" },
            { "When set if there is a healthy compass in use the compass heading will be captured at arming and used for the AUTOLAND mode's initial takeoff direction instead of capturing ground course in NAV_TAKEOFF or Mode TAKEOFF or other modes.", "啟用且有健康 compass 時，記錄解鎖時 compass 航向供 AUTOLAND 初始起飛方向使用，而不在 NAV_TAKEOFF、TAKEOFF 或其他模式記錄地面航跡方向" },
            { "When unset the maximum allowed throttle is always used (THR_MAX or TKOFF_THR_MAX) during takeoff. When set TECS is allowed to operate between a minimum (THR_MIN or TKOFF_THR_MIN) and a maximum (THR_MAX or TKOFF_THR_MAX) limit. Applicable only when using an airspeed sensor.", "未啟用時起飛一律使用最高允許油門 THR_MAX 或 TKOFF_THR_MAX；啟用後 TECS 可在 THR_MIN／TKOFF_THR_MIN 與 THR_MAX／TKOFF_THR_MAX 之間控制。僅適用有空速計時" },
            { "actuator_1_center", "actuator 1 回中" },
            { "actuator_1_dec", "actuator 1 降低輸出" },
            { "actuator_1_inc", "actuator 1 提高輸出" },
            { "actuator_1_max", "actuator 1 最大輸出" },
            { "actuator_1_max_momentary", "actuator 1 暫時最大輸出" },
            { "actuator_1_max_toggle", "actuator 1 切換最大輸出" },
            { "actuator_1_min", "actuator 1 最小輸出" },
            { "actuator_1_min_momentary", "actuator 1 暫時最小輸出" },
            { "actuator_1_min_toggle", "actuator 1 切換最小輸出" },
            { "actuator_2_center", "actuator 2 回中" },
            { "actuator_2_dec", "actuator 2 降低輸出" },
            { "actuator_2_inc", "actuator 2 提高輸出" },
            { "actuator_2_max", "actuator 2 最大輸出" },
            { "actuator_2_max_momentary", "actuator 2 暫時最大輸出" },
            { "actuator_2_max_toggle", "actuator 2 切換最大輸出" },
            { "actuator_2_min", "actuator 2 最小輸出" },
            { "actuator_2_min_momentary", "actuator 2 暫時最小輸出" },
            { "actuator_2_min_toggle", "actuator 2 切換最小輸出" },
            { "actuator_3_center", "actuator 3 回中" },
            { "actuator_3_dec", "actuator 3 降低輸出" },
            { "actuator_3_inc", "actuator 3 提高輸出" },
            { "actuator_3_max", "actuator 3 最大輸出" },
            { "actuator_3_max_momentary", "actuator 3 暫時最大輸出" },
            { "actuator_3_max_toggle", "actuator 3 切換最大輸出" },
            { "actuator_3_min", "actuator 3 最小輸出" },
            { "actuator_3_min_momentary", "actuator 3 暫時最小輸出" },
            { "actuator_3_min_toggle", "actuator 3 切換最小輸出" },
            { "actuator_4_center", "actuator 4 回中" },
            { "actuator_4_dec", "actuator 4 降低輸出" },
            { "actuator_4_inc", "actuator 4 提高輸出" },
            { "actuator_4_max", "actuator 4 最大輸出" },
            { "actuator_4_max_momentary", "actuator 4 暫時最大輸出" },
            { "actuator_4_max_toggle", "actuator 4 切換最大輸出" },
            { "actuator_4_min", "actuator 4 最小輸出" },
            { "actuator_4_min_momentary", "actuator 4 暫時最小輸出" },
            { "actuator_4_min_toggle", "actuator 4 切換最小輸出" },
            { "actuator_5_center", "actuator 5 回中" },
            { "actuator_5_dec", "actuator 5 降低輸出" },
            { "actuator_5_inc", "actuator 5 提高輸出" },
            { "actuator_5_max", "actuator 5 最大輸出" },
            { "actuator_5_max_momentary", "actuator 5 暫時最大輸出" },
            { "actuator_5_max_toggle", "actuator 5 切換最大輸出" },
            { "actuator_5_min", "actuator 5 最小輸出" },
            { "actuator_5_min_momentary", "actuator 5 暫時最小輸出" },
            { "actuator_5_min_toggle", "actuator 5 切換最小輸出" },
            { "actuator_6_center", "actuator 6 回中" },
            { "actuator_6_dec", "actuator 6 降低輸出" },
            { "actuator_6_inc", "actuator 6 提高輸出" },
            { "actuator_6_max", "actuator 6 最大輸出" },
            { "actuator_6_max_momentary", "actuator 6 暫時最大輸出" },
            { "actuator_6_max_toggle", "actuator 6 切換最大輸出" },
            { "actuator_6_min", "actuator 6 最小輸出" },
            { "actuator_6_min_momentary", "actuator 6 暫時最小輸出" },
            { "actuator_6_min_toggle", "actuator 6 切換最小輸出" },
            { "servo_1_center", "servo 1 回中" },
            { "servo_1_dec", "servo 1 降低輸出" },
            { "servo_1_inc", "servo 1 提高輸出" },
            { "servo_1_max", "servo 1 最大輸出" },
            { "servo_1_max_momentary", "servo 1 暫時最大輸出" },
            { "servo_1_max_toggle", "servo 1 切換最大輸出" },
            { "servo_1_min", "servo 1 最小輸出" },
            { "servo_1_min_momentary", "servo 1 暫時最小輸出" },
            { "servo_1_min_toggle", "servo 1 切換最小輸出" },
            { "servo_2_center", "servo 2 回中" },
            { "servo_2_dec", "servo 2 降低輸出" },
            { "servo_2_inc", "servo 2 提高輸出" },
            { "servo_2_max", "servo 2 最大輸出" },
            { "servo_2_max_momentary", "servo 2 暫時最大輸出" },
            { "servo_2_max_toggle", "servo 2 切換最大輸出" },
            { "servo_2_min", "servo 2 最小輸出" },
            { "servo_2_min_momentary", "servo 2 暫時最小輸出" },
            { "servo_2_min_toggle", "servo 2 切換最小輸出" },
            { "servo_3_center", "servo 3 回中" },
            { "servo_3_dec", "servo 3 降低輸出" },
            { "servo_3_inc", "servo 3 提高輸出" },
            { "servo_3_max", "servo 3 最大輸出" },
            { "servo_3_max_momentary", "servo 3 暫時最大輸出" },
            { "servo_3_max_toggle", "servo 3 切換最大輸出" },
            { "servo_3_min", "servo 3 最小輸出" },
            { "servo_3_min_momentary", "servo 3 暫時最小輸出" },
            { "servo_3_min_toggle", "servo 3 切換最小輸出" },
            { "relay_1_momentary", "relay 1 暫時開啟" },
            { "relay_1_off", "relay 1 關閉" },
            { "relay_1_on", "relay 1 開啟" },
            { "relay_1_toggle", "relay 1 切換" },
            { "relay_2_momentary", "relay 2 暫時開啟" },
            { "relay_2_off", "relay 2 關閉" },
            { "relay_2_on", "relay 2 開啟" },
            { "relay_2_toggle", "relay 2 切換" },
            { "relay_3_momentary", "relay 3 暫時開啟" },
            { "relay_3_off", "relay 3 關閉" },
            { "relay_3_on", "relay 3 開啟" },
            { "relay_3_toggle", "relay 3 切換" },
            { "relay_4_momentary", "relay 4 暫時開啟" },
            { "relay_4_off", "relay 4 關閉" },
            { "relay_4_on", "relay 4 開啟" },
            { "relay_4_toggle", "relay 4 切換" },
            { "Parachute 3pos", "降落傘三段開關" },
            { "Parachute Enable", "啟用降落傘" },
            { "Parachute Release", "釋放降落傘" },
            { "Parachute release", "釋放降落傘" },
            { "Parameters", "參數" },
            { "ParseTelemetry", "解析遙測" },
            { "Passthrough", "直通" },
            { "Pause Stream Logging", "暫停串流記錄" },
            { "PersistAccels", "保留 accelerometer 校正" },
            { "PersistParams", "保留參數" },
            { "PersistTemps", "保留溫度設定" },
            { "Ping200X Send GPS", "Ping200X 傳送 GPS" },
            { "pitch control", "Pitch 控制" },
            { "Pitch input", "Pitch 輸入" },
            { "Pitmode until armed", "解鎖前使用 Pitmode" },
            { "Pitmode when disarmed", "上鎖時使用 Pitmode" },
            { "Plane AUTO Mode Landing Abort", "中止 Plane AUTO 降落" },
            { "Plane landing abort for VTOL Payload Place or glide-slope go-around", "中止 Plane 降落，用於 VTOL Payload Place 或沿滑翔斜坡重飛" },
            { "PointObstacle", "點狀障礙物" },
            { "Polygon", "多邊形" },
            { "Pos only", "僅位置" },
            { "Position", "位置" },
            { "Post filter", "濾波後" },
            { "Power Button", "電源按鈕" },
            { "PrecLoiter Enable", "啟用 PrecLoiter" },
            { "Prefix LQ with RF Mode", "在 LQ 前顯示 RF Mode" },
            { "Progressive crow", "漸進式 crow" },
            { "Progressive crow flaps", "漸進式 crow flaps" },
            { "Progressive crow flaps only first (0-50% flap in) then crow flaps (50 - 100% flap in)", "襟翼輸入 0～50% 先使用漸進式襟翼，再於 50～100% 使用 crow flaps" },
            { "Proximity Avoidance", "近距離避障" },
            { "Proximity Avoidance Enable", "啟用近距離避障" },
            { "PSC Angle Max", "PSC 最大角度" },
            { "PWM disabled while disarmed", "上鎖時停用 PWM" },
            { "PWM enabled while disarmed", "上鎖時啟用 PWM" },
            { "PWM Input", "PWM 輸入" },
            { "PWMInputPin", "PWM 輸入腳位" },
            { "QAssist 3pos", "QAssist 三段開關" },
            { "QRTL Always", "一律 QRTL" },
            { "Quadplane Fwd Throttle Override enable", "允許覆寫 QuadPlane 前推油門" },
            { "quadruple loop rate", "四倍迴圈速率" },
            { "Quintuple notch", "五重 notch" },
            { "RangeFinder Enable", "啟用 RangeFinder" },
            { "RangeFinder Gain", "RangeFinder gain" },
            { "Rate Loop Only", "僅 Rate 迴路" },
            { "Raw Voltage", "量測電壓（未補償負載壓降）" },
            { "RawSensor", "原始感測器資料" },
            { "RC Channels", "RC 通道" },
            { "RC Feel", "RC 操控反應" },
            { "RC Input", "RC 輸入" },
            { "RC input", "RC 輸入" },
            { "RC input offset heading when armed", "RC 輸入相對解鎖時航向的偏移" },
            { "RC Input-Output", "RC 輸入／輸出" },
            { "RC lock state from previous mode", "沿用上一模式的 RC 鎖定狀態" },
            { "RC output", "RC 輸出" },
            { "RC Output", "RC 輸出" },
            { "RC Override Enable", "啟用 RC Override" },
            { "RC Passthrough", "RC 直通" },
            { "RC pitch and roll control radius and rate", "RC Pitch／Roll 控制半徑與速率" },
            { "RC Targeting", "RC 指向控制" },
            { "RCChannelPwmValue", "RC 通道 PWM 值" },
            { "RCPassThru", "RC 直通" },
            { "Read data from file", "從檔案讀取資料" },
            { "Read data from file and stop on EOF", "從檔案讀取，到 EOF 停止" },
            { "Readings stopped", "停止量測讀值" },
            { "ReceiverProtocol", "接收機協定" },
            { "Recording Starts at arming and stops at disarming", "解鎖開始錄影，上鎖停止" },
            { "RecordOrigin", "記錄原點" },
            { "Recovery Pitch Angle", "恢復 Pitch 角" },
            { "Recovery Roll Angle", "恢復 Roll 角" },
            { "Recovery Yaw Angle", "恢復 Yaw 角" },
            { "relative", "相對" },
            { "Relative to alternate GPS instance", "相對另一個 GPS instance" },
            { "Relative to Home", "相對 Home" },
            { "Relative to lead vehicle heading", "相對領航機航向" },
            { "RelativeToCustomBase", "相對自訂基準" },
            { "Relaxed", "寬鬆" },
            { "Relay On/Off", "Relay 開／關" },
            { "Release gripper on thrust loss", "推力喪失時釋放 Gripper" },
            { "Remain in AVOID_ADSB", "維持 AVOID_ADSB" },
            { "Report", "回報" },
            { "Report Only", "僅回報" },
            { "Report only", "僅回報" },
            { "ReportOffset", "回報 offset" },
            { "ReportOnly", "僅回報" },
            { "Repositioning", "重定位" },
            { "Require Location", "要求位置資訊" },
            { "Restart Mission", "重新開始任務" },
            { "Resume if AUTO else Loiter", "若為 AUTO 則恢復，否則 Loiter" },
            { "Resume Mission", "恢復任務" },
            { "Resume previous flight mode", "恢復先前飛行模式" },
            { "Retract", "收回" },
            { "Retract after Takeoff", "起飛後收回" },
            { "Retract after Takeoff AND deploy during Land", "起飛後收回，Land 期間展開" },
            { "Retract Mount", "收回 Mount" },
            { "Retract Mount1", "收回 Mount1" },
            { "Retract Mount2", "收回 Mount2" },
            { "Retracted", "已收回" },
            { "Retry if stuck (Daiwa only)", "卡住時重試（僅 Daiwa）" },
            { "Retry Landing(Normal Strictness)", "重試降落（一般嚴格程度）" },
            { "Return to neutral angles on RC failsafe", "RC failsafe 時返回中立角度" },
            { "Reverse", "反向" },
            { "reverse", "反向" },
            { "Reverse Throttle", "反向油門" },
            { "right", "右" },
            { "Right", "右" },
            { "Right side up", "正面朝上" },
            { "Right Wheel", "右輪" },
            { "Right2m", "右側 2 m" },
            { "Right4m", "右側 4 m" },
            { "Right6m", "右側 6 m" },
            { "roll_pitch_toggle", "切換 Roll／Pitch" },
            { "Roll/Pitch stabilised", "Roll／Pitch 穩定式" },
            { "RPM library", "RPM 函式庫" },
            { "RPM Sensor", "RPM 感測器" },
            { "RS-485 Driver enable RTS pin", "RS-485 driver 使用 RTS 啟用腳位" },
            { "RTL or Hold", "先切入 RTL；無法切入時改用 Hold" },
            { "RTL or Land", "先切入 RTL；無法切入時改用 Land" },
            { "RTLStickCancel", "以搖桿取消 RTL" },
            { "Rudder", "方向舵" },
            { "Rudder mixing in direct flight modes only (Manual / Stabilize / Acro)", "僅在 Manual／Stabilize／Acro 直接控制模式混入方向舵" },
            { "RunCam Control", "RunCam 控制" },
            { "RunCam OSD Control", "RunCam OSD 控制" },
            { "Running", "執行中" },
            { "Rx UAT and 1090ES", "接收 UAT 與 1090ES" },
            { "Rx UAT only", "僅接收 UAT" },
            { "Rx&Tx UAT and 1090ES", "收發 UAT 與 1090ES" },
            { "Sag Compensated Voltage", "負載壓降補償後的估計電壓" },
            { "Sagetech MXS use External Config", "Sagetech MXS 使用外部配置" },
            { "Sail Mast Rotation", "桅杆旋轉" },
            { "Sailboat Heel", "帆船橫傾" },
            { "Sailboat motoring 3pos", "帆船馬達三段控制" },
            { "Sailboat Tack", "帆船換舷" },
            { "SailMastRotation", "桅杆旋轉" },
            { "Saltwater", "海水" },
            { "Same as Lead vehicle", "與領航機相同" },
            { "Save config", "儲存配置" },
            { "Save only when needed", "僅必要時儲存" },
            { "Save Trim", "儲存 trim" },
            { "Save Trim (4.1 and lower)", "儲存 trim（4.1 及以前）" },
            { "Save WP", "儲存 WP" },
            { "SaveWaypoint", "儲存航點" },
            { "SBG uses EKF as GNSS", "SBG 使用 EKF 作為 GNSS" },
            { "SBus servo out", "SBus servo 輸出" },
            { "Scan for unknown target", "掃描未知目標" },
            { "Scan for unknown target in auto mode", "自動模式掃描未知目標" },
            { "Scripting Matrix", "腳本矩陣" },
            { "SCurves used for navigation", "導航使用 SCurves" },
            { "Second battery", "第二顆電池" },
            { "Second driver", "第二個 driver" },
            { "Second interface", "第二個介面" },
            { "Second Relay", "第二個 relay" },
            { "Second RPM Sensor", "第二個 RPM 感測器" },
            { "SecondaryAllowExtendedFrames", "次要介面允許 extended frames" },
            { "SecondCAN", "第二個 CAN" },
            { "SecondGPS", "第二個 GPS" },
            { "select screen based on pwm ranges specified for each screen", "依各畫面指定的 PWM 範圍選擇畫面" },
            { "Send all items", "傳送全部項目" },
            { "Send debug to GCS", "傳送除錯資料至 GCS" },
            { "Send HIGH and NORMAL importance items", "傳送 HIGH 與 NORMAL 重要性項目" },
            { "Send only HIGH importance items", "僅傳送 HIGH 重要性項目" },
            { "Send resistance compensated voltage to GCS", "向 GCS 傳送電阻壓降補償電壓" },
            { "send to 2nd GPS", "傳送至第二個 GPS" },
            { "send to all", "傳送至全部" },
            { "send to first GPS", "傳送至第一個 GPS" },
            { "SendGNSS", "傳送 GNSS" },
            { "SendPosAsNamedValueFloat", "以 NamedValueFloat 傳送位置" },
            { "Sends moving baseline data on all ports", "全部連接埠傳送 moving baseline 資料" },
            { "SendServoAsPWM", "以 PWM 傳送 servo 指令" },
            { "SensAItion used as AHRS", "SensAItion 作為 AHRS" },
            { "Sensor-Rate Logging (sample at full sensor rate seen by AP)", "以 AP 接收的完整感測器速率取樣記錄" },
            { "Servo only", "僅 servo" },
            { "Servo with ExtGyro", "servo 搭配外部 gyro" },
            { "Servos to Neutral", "控制舵面 servo 中立輸出" },
            { "Servos to Zero PWM", "控制舵面 servo 輸出 0 PWM（非中立位置）" },
            { "Set as Proximity sensor", "設為 Proximity 感測器" },
            { "Set as Rangefinder", "設為 Rangefinder" },
            { "set roll pitch and yaw trim to current servo and RC", "以目前 servo 與 RC 設定 Roll／Pitch／Yaw trim" },
            { "Set roll pitch and yaw trim to current servo and RC", "以目前 servo 與 RC 設定 Roll／Pitch／Yaw trim" },
            { "Set steering trim to current servo and RC", "以目前 servo 與 RC 設定轉向 trim" },
            { "SetAttitudeTarget interprets Thrust As Thrust", "SetAttitudeTarget 將 Thrust 解讀為推力" },
            { "SetAttitudeTarget_ThrustAsThrust", "SetAttitudeTarget 的 Thrust 為推力" },
            { "Settings Access", "設定存取" },
            { "Seven PWMs", "7 路 PWM" },
            { "SeventhIMU", "第七個 IMU" },
            { "Shallow", "淺" },
            { "Shallow (45deg)", "淺（45 度）" },
            { "Show free stack space", "顯示 stack 可用空間" },
            { "ShowOverruns", "顯示執行超時" },
            { "ShowSlips", "顯示排程延遲" },
            { "Side into wind", "機體側面迎風" },
            { "SIMPLE heading reset", "重設 SIMPLE 航向" },
            { "simple heading reset", "重設 SIMPLE 航向" },
            { "Single/Dual", "單／雙" },
            { "Six PWMs", "6 路 PWM" },
            { "Skip board validation", "略過控制板驗證" },
            { "skip disarm before parachute release", "釋放降落傘前略過上鎖" },
            { "Skip IMU consistency checks when ICE motor running", "ICE 運轉時略過 IMU 一致性檢查" },
            { "Skip the arming check for neutral Roll/Pitch/Yay sticks", "略過解鎖前 Roll／Pitch／Yaw 搖桿回中檢查" },
            { "Slow", "慢" },
            { "Small", "小型" },
            { "SmartRTL or Hold", "SmartRTL；無法使用時 Hold" },
            { "SmartRTL or RTL or Hold", "依序嘗試 SmartRTL、RTL、Hold" },
            { "SmartRTL or RTL or Land", "依序嘗試 SmartRTL、RTL、Land" },
            { "Soaring Enable", "啟用 Soaring" },
            { "Soft", "柔和" },
            { "Speed", "速度" },
            { "SpeedMismatchDisable", "速度不一致時停用" },
            { "SPI Priority", "SPI 優先序" },
            { "Spin freely on startup", "啟動時自由旋轉" },
            { "Sprayer Enable", "啟用噴灑器" },
            { "SprayerPump", "噴灑泵浦" },
            { "SprayerSpinner", "噴灑旋轉器" },
            { "square law", "平方律" },
            { "Squawk 7400 on GCS failsafe", "GCS failsafe 時使用 Squawk 7400" },
            { "Squawk 7400 on RC failsafe", "RC failsafe 時使用 Squawk 7400" },
            { "Standard", "標準" },
            { "Standard Glide Slope", "標準滑翔斜坡" },
            { "Start at center rather than on perimeter", "由圓心而非圓周開始" },
            { "Start Recording", "開始錄影" },
            { "Start-up and automatically calibrate ESCs", "啟動並自動校正 ESC" },
            { "Start-up in ESC Calibration mode if throttle high", "高油門時以 ESC 校正模式啟動" },
            { "Start-up in ESC Calibration mode regardless of throttle", "不論油門位置，一律以 ESC 校正模式啟動" },
            { "Start-up only", "僅啟動時" },
            { "Starter", "啟動器" },
            { "Stationary", "靜止" },
            { "Steep", "陡" },
            { "Steep (72deg)", "陡（72 度）" },
            { "Steering", "轉向" },
            { "Stick roll right", "Roll 搖桿向右" },
            { "Stick yaw right", "Yaw 搖桿向右" },
            { "Stop", "停止" },
            { "Stop logging when disarmed (SBF only)", "上鎖時停止記錄（僅 SBF）" },
            { "Stop Recording", "停止錄影" },
            { "Stop writing data", "停止寫入資料" },
            { "Stop-Restart Scripting", "停止／重新啟動腳本" },
            { "Stopped", "已停止" },
            { "Storage Priority", "儲存優先序" },
            { "Strict", "嚴格" },
            { "Sum monitor measures minimum voltage instead of average", "SUM monitor 使用最低電壓而非平均電壓" },
            { "Sum Of Selected Monitors", "彙整選定電池監測器（電流加總，電壓預設取平均）" },
            { "SumOfFollowing", "彙整後續電池監測器（電流加總，電壓預設取平均）" },
            { "Suppress logging scripts to dataflash", "不將腳本內容寫入 dataflash" },
            { "Suppress Maintenance-Required Warnings", "抑制需要維護的警告" },
            { "Supress Maintenance-Required Warnings", "抑制需要維護的警告" },
            { "Surface and hold on surface on failsafe", "failsafe 時上浮並保持水面" },
            { "SurfaceTrackingUpDown", "向上／下表面追蹤" },
            { "Swap", "交換" },
            { "Swap first 4 and last 4 servos (for quadplane testing)", "交換前 4 與後 4 個 servo（QuadPlane 測試用）" },
            { "Swapped", "已交換" },
            { "SwapTXRX", "交換 TX／RX" },
            { "Switch to AltHold mode if current mode requires position", "目前模式需位置時切換至 AltHold" },
            { "Switch to Land mode from all modes", "任何模式都切換至 Land" },
            { "Switch to Land mode if current mode requires position", "目前模式需位置時切換至 Land" },
            { "switch to next screen after low to high transition and every 1s while channel value is high", "通道由低轉高時換下一頁，維持高值則每秒再換頁" },
            { "switch to next screen if channel value was changed", "通道值變更時切換下一頁" },
            { "SwitchExternalAHRS", "切換 External AHRS" },
            { "Synthetic Current and Analog Voltage", "估算電流及類比電壓" },
            { "SysID Magnitude", "SysID 振幅" },
            { "SysID Target", "SysID 目標" },
            { "System Performance", "系統效能" },
            { "System ID Chirp", "System ID 掃頻" },
            { "System ID Chirp (Quadplane only)", "System ID 掃頻（僅 QuadPlane）" },
            { "tail into wind", "機尾迎風" },
            { "TakeoffAndLanding", "起飛與降落" },
            { "TakePhoto", "拍照" },
            { "Terminate", "緊急終止（非一般返航或降落）" },
            { "Test autotuned gains after tune is complete", "調整完成後測試 AutoTune 增益" },
            { "Thermal Range Enabled", "啟用熱影像溫度範圍" },
            { "Third driver", "第三個 driver" },
            { "Third Relay", "第三個 relay" },
            { "THR_MIN PWM when disarmed", "上鎖時輸出 THR_MIN PWM" },
            { "Three PWMs", "3 路 PWM" },
            { "ThrLandControl", "油門控制降落速率" },
            { "ThrLandControl-enable throttle stick control of landing rate", "允許油門搖桿控制降落速率" },
            { "Throttle", "油門" },
            { "Throttle at or below min", "油門位於或低於最低值" },
            { "Throttle control in MANUAL while disarmed with safety off", "MANUAL 上鎖且 safety 解除時允許油門控制" },
            { "Throttle Curve", "油門曲線" },
            { "Throttle while disarmed", "上鎖時允許油門" },
            { "Throttle within a dead zone of RC trim value", "油門在 RC trim 死區內" },
            { "ThrottleInput", "油門輸入" },
            { "ThrottleLeft", "左油門" },
            { "ThrottleRight", "右油門" },
            { "ThrottleWhileDisarmed", "上鎖時允許油門" },
            { "Timer Priority", "計時器優先序" },
            { "ToggleMode", "切換模式" },
            { "ToggleSimpleMode", "切換 Simple Mode" },
            { "ToggleSuperSimpleMode", "切換 Super Simple Mode" },
            { "ToggleVideo", "切換影像" },
            { "Torqeedo Clear Err", "清除 Torqeedo 錯誤" },
            { "Transceiever disable", "停用收發器" },
            { "Transceiever enable", "啟用收發器" },
            { "TranslateArrows", "轉換箭頭" },
            { "Transmit in traditional Mode 3A/C only and inhibit Mode-S and ES (ADSB) transmissions", "僅發送傳統 Mode 3A/C，禁止 Mode-S 與 ES（ADSB）發送" },
            { "Transmitter Tuning", "發射機調整" },
            { "Transverse", "橫向" },
            { "Treat MS5611 as MS5607", "將 MS5611 視為 MS5607" },
            { "Trigger re-reading of mode switch", "觸發重新讀取模式開關" },
            { "trigger re-reading of mode switch", "觸發重新讀取模式開關" },
            { "TriggerHigh", "高電位觸發" },
            { "TriggerLow", "低電位觸發" },
            { "trim_pitch_dec", "降低 Pitch trim" },
            { "trim_pitch_inc", "提高 Pitch trim" },
            { "trim_roll_dec", "降低 Roll trim" },
            { "trim_roll_inc", "提高 Roll trim" },
            { "triple loop-rate", "三倍迴圈速率" },
            { "Triple notch", "三重 notch" },
            { "Turbine Start(heli)", "渦輪啟動（Heli）" },
            { "Two Paddles Input", "雙槳輸入" },
            { "Two PWMs", "2 路 PWM" },
            { "TX_NoDMA", "TX 不使用 DMA" },
            { "TX_PullDown", "TX 下拉" },
            { "TX_PullUp", "TX 上拉" },
            { "UART Priority", "UART 優先序" },
            { "UBlox GPS is F9P", "UBlox GPS 為 F9P" },
            { "Undefined", "未定義" },
            { "Unknown", "未知" },
            { "Unlock flash on reboot", "重啟時解除 flash 鎖定" },
            { "Unlocked", "已解除鎖定" },
            { "unused", "未使用" },
            { "Update at loop rate", "以迴圈速率更新" },
            { "UpgradeToLoiter", "條件成立後切入 Loiter" },
            { "Upper-Shoulder Frequency", "較高側峰頻率" },
            { "Upside Down", "倒置" },
            { "Upside down", "倒置" },
            { "Upward Throw", "向上拋出" },
            { "Use", "使用" },
            { "use", "使用" },
            { "Use 1 stop-bit in SmartAudio", "SmartAudio 使用 1 個 stop bit" },
            { "Use base station for GPS yaw on SBF", "SBF 使用 base station 作為 GPS Yaw" },
            { "Use baudrate 115200", "使用 115200 baud" },
            { "Use baudrate 115200 on ublox", "uBlox 使用 115200 baud" },
            { "use both control surfaces on each wing for roll", "每側機翼兩個舵面都用於 Roll" },
            { "Use centered throttle in Cruise or FBWB to indicate AIRSPEED_CRUISE", "Cruise／FBWB 中位油門對應 AIRSPEED_CRUISE" },
            { "Use centered throttle in Cruise or FBWB to indicate trim airspeed", "Cruise／FBWB 中位油門對應配平空速" },
            { "Use Current", "使用電流" },
            { "use Custom Controller", "使用 Custom Controller" },
            { "Use dedicated CAN port b/w GPSes for moving baseline", "使用 GPS 之間專用 CAN 連接埠傳送 moving baseline" },
            { "Use distance to land calc on battery failsafe", "電池 failsafe 時使用降落距離計算" },
            { "Use ellipsoid height instead of AMSL", "使用橢球高而非 AMSL" },
            { "Use ellipsoid height instead of AMSL for uBlox driver", "uBlox driver 使用橢球高而非 AMSL" },
            { "Use Governor", "使用 governor" },
            { "Use Governor-use ICE Idle Governor in MANUAL for forward motor", "MANUAL 前推引擎使用 ICE Idle Governor" },
            { "Use GPS for DCM position", "DCM 位置使用 GPS" },
            { "Use GPS for DCM position and height", "DCM 位置與高度使用 GPS" },
            { "Use Leaky I", "使用 Leaky I" },
            { "Use min freq on RPM source failure", "RPM 來源失效時使用最低頻率" },
            { "Use pitch when nose or tail-in for faster weathervaning", "機頭或機尾迎風時使用 Pitch 加快 weathervaning" },
            { "Use primary", "使用主要來源" },
            { "Use primary if 3D fix or better", "主要來源達 3D fix 或更佳時使用" },
            { "Use QRTL", "使用 QRTL" },
            { "USE QRTL-instead of QLAND for rc failsafe when in VTOL modes", "VTOL 模式 RC failsafe 使用 QRTL 取代 QLAND" },
            { "use same tolerance to auto rotate 45 deg rotations", "自動判定 45 度旋轉時使用相同容差" },
            { "Use Throttle", "使用油門" },
            { "Use UART2 for moving baseline on ublox", "uBlox 使用 UART2 傳送 moving baseline" },
            { "UseBeaconFence", "使用 Beacon fence" },
            { "UseBest", "使用最佳來源" },
            { "UseDecimalPack", "使用 DecimalPack" },
            { "UseEkf3Consistency", "使用 EKF3 一致性檢查" },
            { "UseFence", "使用 fence" },
            { "UseFence and UseProximitySensor", "使用 fence 及 Proximity Sensor" },
            { "UseHimarkServo", "使用 Himark servo" },
            { "UseHobbyWingESC", "使用 HobbyWing ESC" },
            { "UseProximitySensor", "使用 Proximity Sensor" },
            { "UseRecordedOriginForNonGPS", "非 GPS 導航使用記錄的原點" },
            { "UseRTLOnAbort", "中止時使用 RTL" },
            { "UseTCP", "使用 TCP" },
            { "UseTwoPositionSwitch", "使用兩段開關" },
            { "UseWhenZeroThrottle", "油門為 0 時使用" },
            { "Vector Nav use uncompensated values for accel gyro and mag", "Vector Nav 使用未補償的 accel／gyro／mag 值" },
            { "Vector Nav use uncompensated values for accel gyro and mag.", "Vector Nav 使用未補償的 accel／gyro／mag 值" },
            { "Vel only", "僅速度" },
            { "Velocity", "速度" },
            { "Velocity East", "向東速度" },
            { "Velocity North", "向北速度" },
            { "Verbose output", "詳細輸出" },
            { "VerboseSignalInfoGCS", "向 GCS 傳送詳細訊號資訊" },
            { "Vert Pos", "垂直位置" },
            { "Vertical search", "垂直搜尋" },
            { "Very Crisp", "反應非常俐落" },
            { "Very High", "非常高" },
            { "Very Low", "非常低" },
            { "Very Soft", "非常柔和" },
            { "Very Strict", "非常嚴格" },
            { "VerySlow", "非常慢" },
            { "Vicon Failed", "Vicon 失效" },
            { "Vicon Healthy", "Vicon 健康" },
            { "Video Stabilization", "影像穩定" },
            { "VideoSwitch", "影像切換" },
            { "Viso Align", "Viso 對齊" },
            { "VisOdom Align", "VisOdom 對齊" },
            { "VisoOdom Align", "VisoOdom 對齊" },
            { "Voltage compensation uses raw voltage", "電壓補償使用原始電壓" },
            { "Volz servo out", "Volz servo 輸出" },
            { "VTailLeft", "左 V-Tail" },
            { "VTailRight", "右 V-Tail" },
            { "VTOL approach", "VTOL 進場" },
            { "VTOL Forward Throttle", "VTOL 前推油門" },
            { "VTOL Input Pitch Angle", "VTOL 輸入 Pitch 角" },
            { "VTOL Input Roll Angle", "VTOL 輸入 Roll 角" },
            { "VTOL Input Yaw Angle", "VTOL 輸入 Yaw 角" },
            { "VTOL Land", "VTOL 降落" },
            { "VTOL Mixer Pitch", "VTOL 混控 Pitch" },
            { "VTOL Mixer Roll", "VTOL 混控 Roll" },
            { "VTOL Mixer Thrust", "VTOL 混控推力" },
            { "VTOL Mixer Yaw", "VTOL 混控 Yaw" },
            { "VTOL Recovery Pitch Angle", "VTOL 恢復 Pitch 角" },
            { "VTOL Recovery Roll Angle", "VTOL 恢復 Roll 角" },
            { "VTOL Recovery Yaw Angle", "VTOL 恢復 Yaw 角" },
            { "VTOL Takeoff", "VTOL 起飛" },
            { "Vtol Takeoff Frame", "VTOL 起飛座標基準" },
            { "Vtol Takeoff Frame-command NAV_VTOL_TAKEOFF alt set by the command's reference frame not above current location", "NAV_VTOL_TAKEOFF 高度依指令座標基準，而非相對目前位置" },
            { "VTX Power", "VTX 功率" },
            { "WaitForPilotInput", "等待飛行員輸入" },
            { "Walking Height", "步行高度" },
            { "Warn Only", "僅警告" },
            { "Warning", "警告" },
            { "Waypoint navigation used for position targets", "位置目標使用航點導航" },
            { "Weathervane Enable", "啟用 weathervaning" },
            { "WiFi Button", "WiFi 按鈕" },
            { "Winch Clutch", "絞盤離合器" },
            { "Winch Control", "絞盤控制" },
            { "Winch Enable", "啟用絞盤" },
            { "Windvane home heading direction offset", "風向標相對 Home 航向的方向偏移" },
            { "Wing Sail Elevator", "翼帆升降舵" },
            { "WingSailElevator", "翼帆升降舵" },
            { "WP Speed", "WP 速度" },
            { "WP Speed (4.6 and earlier)", "WP 速度（4.6 及以前）" },
            { "WP Speed (m/s)", "WP 速度（m/s）" },
            { "Write configuration once", "只寫入配置一次" },
            { "Write data to a file", "將資料寫入檔案" },
            { "Write protect bootloader flash on reboot", "重啟時對 bootloader flash 啟用寫入保護" },
            { "Write protect firmware flash on reboot", "重啟時對韌體 flash 啟用寫入保護" },
            { "Yes(0 PWM when disarmed)", "是，上鎖時輸出 0 PWM" },
            { "Yes(minimum PWM when disarmed)", "是，上鎖時輸出最低 PWM" },
            { "Zero thrust collective", "零推力 collective" },
            { "ZigZag SaveWP", "儲存 ZigZag 航點" },
            { "HalfDuplex", "半雙工" },
            { "Hardware safety switch", "硬體 safety switch" },
            { "Heading when armed", "解鎖時的航向" },
            { "HeartbeatAndAUTO", "Heartbeat 與 AUTO" },
            { "HeartbeatAndREMRSSI", "Heartbeat 與 REMRSSI" },
            { "High Speed", "高速" },
            { "High throttle cancels landing", "高油門取消降落" },
            { "HighIsOn", "高電位為開啟" },
            { "Hold in Auto Mode", "Auto 模式中保持" },
            { "hold open forever after release", "釋放後持續保持開啟" },
            { "Hold Position", "保持位置" },
            { "HoldAndDisarm", "保持並上鎖" },
            { "Home Location", "Home 位置" },
            { "Horiz Pos", "水平位置" },
            { "Horizontal search", "水平搜尋" },
            { "Hover", "懸停" },
            { "I term management based landed state", "依落地狀態管理 I 項" },
            { "I2C Priority", "I2C 優先序" },
            { "ICEngine start / stop", "內燃機啟動／停止" },
            { "Ignition", "點火" },
            { "Ignore", "忽略" },
            { "Ignore CRC in SmartAudio", "忽略 SmartAudio CRC" },
            { "Ignore DroneCAN SoC", "忽略 DroneCAN SoC" },
            { "Ignore forward flight angle limits in Qmodes", "Q 模式忽略前飛角度限制" },
            { "Ignore pilot yaw", "忽略飛行員 Yaw 輸入" },
            { "Ignore status updates in CRSF and blindly set VTX options", "忽略 CRSF 狀態更新，直接設定 VTX 選項" },
            { "IgnoreDNANodeConflicts", "忽略 DNA node 衝突" },
            { "IgnoreDNANodeUnhealthy", "忽略 DNA node 不健康狀態" },
            { "IgnoreUAVCAN SoC", "忽略 UAVCAN SoC" },
            { "Imperial", "英制" },
            { "IncludeHome", "包含 Home" },
            { "Inclusion/Exclusion Circles+Polygons", "包含／排除區域的圓形與多邊形" },
            { "InFlight-Learning", "飛行中學習" },
            { "Info", "資訊" },
            { "InitialClimb", "初始爬升" },
            { "InitialHeading", "初始航向" },
            { "Input Lateral Velocity", "輸入橫向速度" },
            { "Input Longitudinal Velocity", "輸入縱向速度" },
            { "Input Pitch Angle", "輸入 Pitch 角" },
            { "Input Roll Angle", "輸入 Roll 角" },
            { "Input Yaw Angle", "輸入 Yaw 角" },
            { "input_hold_set", "設定輸入保持" },
            { "Internal", "內部" },
            { "Internal-Learning", "內部學習" },
            { "Inverted", "反向" },
            { "InvertedAHRoll", "反轉人工地平儀 Roll" },
            { "InvertedFlight Enable", "啟用倒飛" },
            { "InvertedWindArrow", "反轉風向箭頭" },
            { "InvertedWindPointer", "反轉風向指針" },
            { "InvertInput", "反轉輸入" },
            { "InvertRX", "反轉 RX" },
            { "InvertTX", "反轉 TX" },
            { "IO Priority", "IO 優先序" },
            { "IR + EO1 Picture-in-picture", "IR 與 EO1 子母畫面" },
            { "IR thermal", "IR 熱影像" },
            { "KillIMU1", "停用 IMU1" },
            { "KillIMU2", "停用 IMU2" },
            { "KillIMU3", "停用 IMU3" },
            { "Land (4.0+ Only)", "Land（僅 4.0 起）" },
            { "Land even in MANUAL", "即使 MANUAL 也執行 Land" },
            { "Land even in Stabilize", "即使 Stabilize 也執行 Land" },
            { "Land Vertically (Not strict)", "垂直降落（Not strict）" },
            { "Landing", "降落" },
            { "Landing Flare", "降落拉平" },
            { "Landing Gear", "起落架" },
            { "LandingGear", "起落架" },
            { "Large", "大型" },
            { "LastFiltered", "最後一組濾波後資料" },
            { "LastRaw", "最後一組原始資料" },
            { "Leaky I(4.0 and earlier)", "Leaky I（4.0 及以前）" },
            { "Learn", "學習" },
            { "Learn and Save", "學習並儲存" },
            { "Learn Calibration", "學習校正" },
            { "LearnCalibration", "學習校正" },
            { "LearnCruise Speed", "學習巡航速度" },
            { "LearnCruiseSpeed", "學習巡航速度" },
            { "Leave as currently configured", "保留目前配置" },
            { "Leave Unchanged", "維持不變" },
            { "Left", "左" },
            { "left", "左" },
            { "Left Wheel", "左輪" },
            { "Left2m", "左側 2 m" },
            { "Left4m", "左側 4 m" },
            { "Left6m", "左側 6 m" },
            { "Level Transition", "水平轉換" },
            { "Leveling", "回正水平" },
            { "Leveling and Limited", "回正水平並限制角度" },
            { "lights1_brighter", "燈 1 增亮" },
            { "lights1_cycle", "燈 1 循環切換" },
            { "lights1_dimmer", "燈 1 調暗" },
            { "lights2_brighter", "燈 2 增亮" },
            { "lights2_cycle", "燈 2 循環切換" },
            { "lights2_dimmer", "燈 2 調暗" },
            { "Linear", "線性" },
            { "linear-see WIND_T_COEF", "線性，參閱 WIND_T_COEF" },
            { "LockUASIDOnFirstBasicIDRx", "收到第一個 Basic ID 後鎖定 UAS ID" },
            { "Log", "記錄" },
            { "log all", "全部記錄" },
            { "Log all AIVDM messages", "記錄全部 AIVDM 訊息" },
            { "Log all gyros", "記錄全部 gyro" },
            { "Log all instances", "記錄全部 instances" },
            { "Log decoded messages", "記錄解碼訊息" },
            { "Log Error", "記錄 Error" },
            { "Log Everything", "記錄全部" },
            { "Log Info and below", "記錄 Info 及以下等級" },
            { "Log None", "不記錄" },
            { "Log only instances with sensor source set to None", "僅記錄感測器來源為 None 的 instances" },
            { "log only special ID", "僅記錄指定 ID" },
            { "Log only unsupported AIVDM messages", "僅記錄不支援的 AIVDM 訊息" },
            { "Log primary gyro only", "僅記錄主要 gyro" },
            { "Log Warning and below", "記錄 Warning 及以下等級" },
            { "LogAllCanPackets", "記錄全部 CAN 封包" },
            { "LogAllData", "記錄全部資料" },
            { "LogAllFrames", "記錄全部 frames" },
            { "Logging Available", "記錄功能可用" },
            { "LoggingAvailable", "記錄功能可用" },
            { "Loiter in Auto Mode", "Auto 模式中 Loiter" },
            { "Loiter or Hold", "先切入 Loiter；無法切入時改用 Hold" },
            { "Loiter Speed", "Loiter 速度" },
            { "Loiter to QLand", "由 Loiter 轉 QLand" },
            { "Longitudinal", "縱向" },
            { "loop-rate", "迴圈速率" },
            { "Lost Copter Sound", "Copter 尋機音" },
            { "Lost Plane Sound", "Plane 尋機音" },
            { "Lost Rover Sound", "Rover 尋機音" },
            { "Low Speed", "低速" },
            { "Lower-Shoulder Frequency", "較低側峰頻率" },
            { "LowIsOn", "低電位為開啟" },
            { "Lua Scripts", "Lua 腳本" },
            { "Lua_Scripting", "Lua 腳本" },
            { "Main Priority", "主執行緒優先序" },
            { "Main Sail", "主帆" },
            { "MainSail", "主帆" },
            { "Make Mount ROI the center of the circle", "將 Mount ROI 設為圓心" },
            { "manual control", "手動控制" },
            { "MANUAL ONLY", "僅 MANUAL" },
            { "MavLink Targeting", "MAVLink 指向控制" },
            { "Max altitude", "最高高度" },
            { "Max altitude and Circle", "最高高度及圓形" },
            { "Max altitude and Polygon", "最高高度及多邊形" },
            { "Max altitude circle and Polygon", "最高高度、圓形及多邊形" },
            { "Max collective", "最大 collective" },
            { "Measured Lateral Position", "量測橫向位置" },
            { "Measured Lateral Velocity", "量測橫向速度" },
            { "Measured Longitudinal Position", "量測縱向位置" },
            { "Measured Longitudinal Velocity", "量測縱向速度" },
            { "Medium", "中等" },
            { "Medium Attitude", "中速率姿態資料" },
            { "Metric", "公制" },
            { "Mid collective", "中間 collective" },
            { "Min altitude", "最低高度" },
            { "Min collective", "最小 collective" },
            { "minimum PWM when disarmed", "上鎖時輸出最低 PWM" },
            { "Mission", "任務" },
            { "Mission Commands", "任務指令" },
            { "Mixer Pitch", "混控 Pitch" },
            { "Mixer Roll", "混控 Roll" },
            { "Mixer Thrust", "混控推力" },
            { "Mixer Yaw", "混控 Yaw" },
            { "Motor Detect", "馬達偵測" },
            { "Motor Emergency Stop", "馬達緊急停止" },
            { "Motor Yaw Headroom", "馬達 Yaw 控制餘裕" },
            { "Motor(not implemented yet)", "馬達（尚未實作）" },
            { "MotorLoadTest", "馬達負載測試" },
            { "Mount Follows lead vehicle on mode enter", "進入模式時 Mount 跟隨領航機" },
            { "Mount follows lead vehicle on mode enter", "進入模式時 Mount 跟隨領航機" },
            { "Mount Lock", "鎖定 Mount" },
            { "Mount LRF enable", "啟用 Mount LRF" },
            { "Mount POI Lock", "鎖定 Mount POI" },
            { "Mount Roll/Pitch Lock", "鎖定 Mount Roll／Pitch" },
            { "Mount Yaw Lock", "鎖定 Mount Yaw" },
            { "mount_center", "Mount 回中" },
            { "mount_open", "展開 Mount" },
            { "mount_pan", "Mount 水平轉動" },
            { "mount_pan_left", "Mount 向左轉" },
            { "mount_pan_right", "Mount 向右轉" },
            { "mount_roll", "Mount Roll" },
            { "mount_tilt", "Mount 俯仰" },
            { "mount_tilt_down", "Mount 向下俯" },
            { "mount_tilt_up", "Mount 向上仰" },
            { "Mount1Retract", "收回 Mount1" },
            { "mount2_open", "展開 Mount2" },
            { "mount2_pan", "Mount2 水平轉動" },
            { "mount2_roll", "Mount2 Roll" },
            { "mount2_tilt", "Mount2 俯仰" },
            { "Mount2Retract", "收回 Mount2" },
            { "Move Horizontally", "水平移動" },
            { "Move Perpendicularly in 3D", "在三維中垂直於目標方向移動" },
            { "MPPT Powered off at boot", "開機時關閉 MPPT 電源" },
            { "MPPT Powered off when disarmed", "上鎖時關閉 MPPT 電源" },
            { "MPPT Powered on at boot", "開機時啟用 MPPT 電源" },
            { "MPPT Powered on when armed", "解鎖時啟用 MPPT 電源" },
            { "MPPT reports input voltage and current", "MPPT 回報輸入電壓與電流" },
            { "Mtrs_Only_Qassist", "Qassist 僅使用馬達" },
            { "Multi-Source", "多來源" },
            { "Navigation Tuning", "導航調整" },
            { "Nearest Rally Point", "最近的 rally point" },
            { "NearlyAll", "幾乎全部" },
            { "NearlyAll-AC315", "幾乎全部，AC315" },
            { "Neutral", "中立" },
            { "Never change yaw", "不改變 Yaw" },
            { "Never reset", "不重設" },
            { "Night", "夜間" },
            { "NMEA water speed", "NMEA 水速" },
            { "No change in camera selection", "不改變相機選擇" },
            { "No Fix", "未定位" },
            { "No GPS connected", "未連接 GPS" },
            { "no logging", "不記錄" },
            { "No override", "不覆寫" },
            { "No PWMs", "無 PWM" },
            { "No RC pusles", "無 RC 脈波" },
            { "No repositioning", "不允許重定位" },
            { "No(AutoArmOnce after checks are passed)", "否，檢查通過後自動解鎖一次" },
            { "NoChange", "不變更" },
            { "NoData", "無資料" },
            { "NoFlybar", "無 flybar" },
            { "NoInfo", "無資訊" },
            { "Non Auto Terrain Follow Disable", "停用非 Auto 模式的地形跟隨" },
            { "None (0x0000)", "無（0x0000）" },
            { "Normal Start-up", "正常啟動" },
            { "North-East-Down", "北／東／下座標" },
            { "Nose into wind", "機頭迎風" },
            { "Nose or tail into wind", "機頭或機尾迎風" },
            { "Not Used", "不使用" },
            { "NotDisabled", "未停用" },
            { "NotEnforced", "不強制" },
            { "Nothing", "無動作" },
            { "Notice", "通知" },
            { "Notify on margin breaches", "超過裕度邊界時通知" },
            { "Off", "關閉" },
            { "On in all position controlled Q modes", "所有位置控制 Q 模式啟用" },
            { "On in all Q modes except QAUTOTUNE and QACRO", "除 QAUTOTUNE、QACRO 外全部 Q 模式啟用" },
            { "One PWMs", "1 路 PWM" },
            { "Only log every five samples (uBlox only)", "每 5 筆取樣只記錄一次（僅 uBlox）" },
            { "Only use during takeoffs or landing see weathervane takeoff and land override parameters", "僅起降時使用，參閱 weathervane 起降覆寫參數" },
            { "Only when in AUTO", "僅 AUTO 模式" },
            { "OnlyForGoAround", "僅重飛時" },
            { "OnOff", "開／關" },
            { "Optflow Calibration", "Optical Flow 校正" },
            { "Override GPS satellite health of L5 band from L1 health", "以 L1 健康狀態覆寫 GPS L5 衛星健康狀態" },
            { "MAG1 Failure", "MAG1 故障" },
            { "MAG2 Failure", "MAG2 故障" },
            { "MAG3 Failure", "MAG3 故障" },
            { "Debug", "除錯" },
            { "Declination", "磁偏角" },
            { "Deploy", "展開" },
            { "Direction of Flight", "飛行方向" },
            { "Direction reversed when backing up", "倒退時反轉方向" },
            { "Direction unchanged when backing up", "倒退時方向不變" },
            { "DirectMixing", "直接混控" },
            { "Disable Airspeed Use", "停用空速資料使用" },
            { "Disable automatic full RTCM parsing when RTCM seen on more than one channel", "多個通道收到 RTCM 時，不自動啟用完整 RTCM 解析" },
            { "Disable board arming gpio output change on arm/disarm", "解鎖／上鎖時不改變板上解鎖 GPIO 輸出" },
            { "Disable Disk", "停用磁碟" },
            { "Disable Download", "停用下載" },
            { "Disable Ground Effect Compensation", "停用地面效應補償" },
            { "Disable ignition in RC failsafe", "RC failsafe 時關閉點火" },
            { "Disable MAVftp", "停用 MAVftp" },
            { "Disable mode change following fence action until fence breach is cleared", "fence 動作後禁止切換模式，直到清除越界狀態" },
            { "Disable prearm display", "停用解鎖前資訊顯示" },
            { "disable Qassist based on synthetic airspeed", "停用依合成空速觸發的 Qassist" },
            { "Disable redline governor", "停用 redline governor" },
            { "Disable thrust loss check", "停用推力喪失檢查" },
            { "Disable while disarmed", "上鎖時停用" },
            { "Disable yaw imbalance warning", "停用 Yaw 不平衡警告" },
            { "DisableApproach", "停用進場階段" },
            { "DisableCrosshair", "停用準星" },
            { "Disabled (30fps)", "停用（30 fps）" },
            { "Disabled on USB connection", "連接 USB 時停用" },
            { "DisableDCMFallbackFW", "固定翼前飛時不退回 DCM" },
            { "DisableDCMFallbackVTOL", "VTOL 飛行時不退回 DCM" },
            { "DisableExternalNavigation", "停用 External Navigation" },
            { "DisableIgnitionRCFailsafe", "RC failsafe 時關閉點火" },
            { "DisableMultiplexing", "停用多工" },
            { "DisablePPPEchoLimit", "停用 PPP Echo 次數限制" },
            { "DisablePPPTimeout", "停用 PPP 逾時" },
            { "DisableRedineGovernor", "停用 redline governor" },
            { "Disables automatic configuration", "停用自動配置" },
            { "DisableSignalQueries", "停用訊號查詢" },
            { "DisableVoltageCorrection", "停用電壓修正" },
            { "Disarm", "上鎖" },
            { "disarm", "上鎖" },
            { "Disarm on land detection", "偵測落地後上鎖" },
            { "Disarmed Yaw Tilt", "上鎖時允許 Yaw 傾轉" },
            { "DisarmOnLowThrottle", "低油門時上鎖" },
            { "Discard log on reboot if never armed", "從未解鎖的日誌於重啟時捨棄" },
            { "Do not calibrate on start up. Manual calibration must be performed once per boot.", "開機不自動校正，每次開機須手動校正一次" },
            { "Do not land (just Hover) (Very Strict)", "不降落，只懸停（Very Strict）" },
            { "Do not require location", "不要求位置資訊" },
            { "Do not require offset calibration before flight. Manual calibration should be performed during initial setup.", "飛行前不強制 offset 校正；初始設定時仍應手動校正" },
            { "Do not save config", "不儲存配置" },
            { "Do not send status text on state change", "狀態變更時不傳送文字訊息" },
            { "Do not stabilize PositionXY", "不穩定控制 PositionXY" },
            { "Do not stabilize VelocityXY", "不穩定控制 VelocityXY" },
            { "Do not track", "不追蹤" },
            { "Do Nothing", "不執行動作" },
            { "Don't adjust the trims", "不調整 trim" },
            { "Don't Use", "不使用" },
            { "DoNotIncludeHome", "不包含 Home" },
            { "DoNotUse", "不使用" },
            { "DontDisableAirspeedUsingEKF", "不依 EKF 一致性檢查停用空速" },
            { "DontZeroCounter", "不將計數器歸零" },
            { "double loop-rate", "兩倍迴圈速率" },
            { "Double notch", "雙重 notch" },
            { "Down", "向下" },
            { "Down and Yaw only", "僅向下及 Yaw" },
            { "Drop", "釋放" },
            { "DualAircraftSynchronised", "雙機同步" },
            { "Dynamic FFT", "動態 FFT" },
            { "Dynamic harmonic", "動態諧波" },
            { "Dynamic Scripting Matrix", "動態腳本矩陣" },
            { "Eight PWMs", "8 路 PWM" },
            { "EKF lane switch attempt", "嘗試切換 EKF lane" },
            { "EKF Pos Source", "EKF 位置來源" },
            { "EKF Reset", "重設 EKF" },
            { "EKF Source Set", "EKF 來源組" },
            { "EKF yaw reset", "重設 EKF Yaw" },
            { "EKF-Learning", "由 EKF 學習" },
            { "Elevator", "升降舵" },
            { "ElevonLeft", "左 elevon" },
            { "ElevonRight", "右 elevon" },
            { "Emergency(PreArm)", "Emergency 等級（解鎖前）" },
            { "EmergencySurface", "緊急上浮" },
            { "Emit HDT", "輸出 HDT" },
            { "Emit THS", "輸出 THS" },
            { "Empty", "空白" },
            { "Enable Always", "持續啟用" },
            { "Enable automatic configuration", "啟用自動配置" },
            { "Enable automatic configuration for DroneCAN as well", "DroneCAN 也啟用自動配置" },
            { "Enable automatic configuration for Serial GPSes only", "只自動配置串列 GPS" },
            { "Enable CAN1 multicast bridged", "啟用 CAN1 multicast bridge" },
            { "Enable CAN1 multicast endpoint", "啟用 CAN1 multicast endpoint" },
            { "Enable CAN2 multicast bridged", "啟用 CAN2 multicast bridge" },
            { "Enable CAN2 multicast endpoint", "啟用 CAN2 multicast endpoint" },
            { "Enable Debug Pins", "啟用除錯腳位" },
            { "Enable EKF2", "啟用 EKF2" },
            { "Enable EKF3", "啟用 EKF3" },
            { "Enable FW Autotune", "啟用固定翼 AutoTune" },
            { "Enable hardware watchdog", "啟用硬體 watchdog" },
            { "Enable per-task perf info", "啟用各 task 效能資訊" },
            { "Enable post-filter FFT", "啟用濾波後 FFT" },
            { "Enable RTCM full parse even for a single channel", "即使僅一個通道，也啟用 RTCM 完整解析" },
            { "Enable sending stats", "啟用統計傳送" },
            { "Enable set of internal parameters", "允許設定內部參數" },
            { "Enable steering speed scaling", "啟用轉向隨速度縮放" },
            { "Enable VTOL AUTO", "啟用 VTOL AUTO" },
            { "EnableAirspeedAndGroundspeed", "啟用空速與地速" },
            { "EnableAndLearn", "啟用並學習" },
            { "EnableBTFLFonts", "啟用 Betaflight 字型" },
            { "EnableCanfd", "啟用 CAN FD" },
            { "EnableCANLogging", "啟用 CAN 記錄" },
            { "Enabled (25 fps)", "啟用（25 fps）" },
            { "Enabled Auto DO_LAND_START or RTL", "啟用，自動 DO_LAND_START，否則 RTL" },
            { "Enabled Auto DO_LAND_START/DO_RETURN_PATH_START or RTL", "啟用，自動 DO_LAND_START／DO_RETURN_PATH_START，否則 RTL" },
            { "Enabled Continue with Mission in Auto", "啟用，在 Auto 繼續任務" },
            { "Enabled including attitude reporting", "啟用，包含姿態回報" },
            { "Enabled on telemetry loss", "遙測中斷時啟用" },
            { "Enabled with attitude reporting", "啟用並回報姿態" },
            { "Enabled-Dynamic", "啟用，動態" },
            { "Enabled-Fixed", "啟用，固定" },
            { "Enabled-FixedWhenArmed", "啟用，解鎖時固定" },
            { "EnabledNoFailsafe", "啟用，不含 failsafe" },
            { "EnableFixedWingAutotune", "啟用固定翼 AutoTune" },
            { "EnableFlexDebug", "啟用 FlexDebug" },
            { "EnableINAVFonts", "啟用 INAV 字型" },
            { "EnableLandReposition", "允許降落期間重定位" },
            { "EnableLogging", "啟用記錄" },
            { "EnableNoFWUpdate", "啟用，但不更新韌體" },
            { "EnableOnAllIMUs", "對全部 IMU 啟用" },
            { "EnablePPP Ethernet gateway", "啟用 PPP Ethernet gateway" },
            { "EnableStats", "啟用統計" },
            { "EnableTelemetryMode", "啟用遙測模式" },
            { "EnableVersion1", "啟用版本 1" },
            { "EnableVersion2", "啟用版本 2" },
            { "EnforceArming", "強制要求解鎖" },
            { "Enforced", "強制執行" },
            { "EnforcePreArmChecks", "強制解鎖前檢查" },
            { "EngineRunEnable", "允許引擎運轉" },
            { "Enter depth hold mode", "進入深度保持模式" },
            { "Enter surface mode", "進入上浮模式" },
            { "EO1 + IR Picture-in-picture", "EO1 與 IR 子母畫面" },
            { "Error(PreArm)", "Error 等級（解鎖前）" },
            { "ESC Telemetry Motors Bitmask", "ESC Telemetry 馬達 bitmask" },
            { "External", "外接" },
            { "External Gov SetPoint", "外部 governor 設定點" },
            { "External only (0xFF00)", "僅外接（0xFF00）" },
            { "Face along GPS course", "朝向 GPS 航跡方向" },
            { "Face direction of travel", "朝向移動方向" },
            { "face direction of travel", "朝向移動方向" },
            { "Face Lead Vehicle", "朝向領航機" },
            { "Face next waypoint", "朝向下一航點" },
            { "Face next waypoint except RTL", "朝向下一航點，RTL 除外" },
            { "Failsafe enabled in Hold mode", "Hold 模式仍啟用 failsafe" },
            { "Fast", "快速" },
            { "Fast Attitude", "高速姿態資料" },
            { "Fast harmonic notch logging", "高速 harmonic notch 記錄" },
            { "Fast IMU", "高速 IMU" },
            { "FBW style", "FBW 式控制" },
            { "FBW style (no pitch)", "FBW 式控制，不含 Pitch" },
            { "FBWA at zero throttle", "油門為 0 時使用 FBWA" },
            { "FBWA taildragger takeoff mode", "FBWA 後三點起落架起飛模式" },
            { "FBWMixing", "FBW 混控" },
            { "Feedback from mid stick", "以搖桿中位為基準的回饋" },
            { "Fence Enable", "啟用 fence" },
            { "Fence Return Point", "fence 返回點" },
            { "FFT Tune", "FFT 調整" },
            { "File", "檔案" },
            { "File and MAVLink", "檔案及 MAVLink" },
            { "First battery", "第一顆電池" },
            { "First driver", "第一個 driver" },
            { "First IMU", "第一個 IMU" },
            { "First interface", "第一個介面" },
            { "First Relay", "第一個 relay" },
            { "FirstAndSecondIMU", "第一與第二個 IMU" },
            { "FirstBaro", "第一個 Baro" },
            { "FirstCAN", "第一個 CAN" },
            { "FirstFiltered", "第一組濾波後資料" },
            { "FirstGPS", "第一個 GPS" },
            { "FirstIMUOnly", "僅第一個 IMU" },
            { "FirstRaw", "第一組原始資料" },
            { "FirstSecondAndThirdIMU", "第一、第二與第三個 IMU" },
            { "FirstSensor", "第一個感測器" },
            { "Five PWMs", "5 路 PWM" },
            { "Fixed", "固定" },
            { "Flap", "襟翼" },
            { "Flap_auto", "自動襟翼" },
            { "FlapAuto", "自動襟翼" },
            { "FlaperonLeft", "左 flaperon" },
            { "FlaperonRight", "右 flaperon" },
            { "FlightMode Pause/Resume", "暫停／恢復飛行模式" },
            { "Fly HOME then land", "先飛至 HOME 再降落" },
            { "Fly HOME then land via DO_LAND_START mission item", "先飛至 HOME，再由 DO_LAND_START 任務項目進入降落" },
            { "Force effective control inputs to trim positions and prevent arming", "強制有效控制輸入為 trim 位置並禁止解鎖" },
            { "Force Flying", "強制視為飛行中" },
            { "Force FPV (bf) lock on roll and pitch", "強制 FPV 機體座標 Roll／Pitch 鎖定" },
            { "Force FS Action to FBWA", "強制 failsafe 動作為 FBWA" },
            { "Force IS_Flying", "強制 IS_Flying" },
            { "Force Qassist", "強制啟用 Qassist" },
            { "Force safety on when the aircraft disarms", "機體上鎖時強制啟用 safety" },
            { "Force UBlox Config Get/Set for configuration then automatic configuration for Serial GPSes only", "強制以 UBlox Config Get/Set 配置，之後僅自動配置串列 GPS" },
            { "ForcedExternal", "強制外接" },
            { "ForceVTXHighPower", "強制 VTX 高功率" },
            { "forward", "向前" },
            { "Forward", "向前" },
            { "forward mavlink packets that don't pass CRC", "轉送未通過 CRC 的 MAVLink 封包" },
            { "Forward or reverse to target point", "前進或倒退至目標點" },
            { "Forward Throttle", "前推油門" },
            { "Forward-Left", "左前" },
            { "Forward-Right", "右前" },
            { "Four PWMs", "4 路 PWM" },
            { "Fourth Relay", "第四個 relay" },
            { "Freshwater", "淡水" },
            { "Front", "前方" },
            { "Front & Right only", "僅前方與右方" },
            { "FuelFlow", "燃油流量" },
            { "FuelLevelAnalog", "類比油位" },
            { "FuelLevelPWM", "PWM 油位" },
            { "full span", "全跨度" },
            { "FullInput", "完整輸入" },
            { "Fullrate Attitude", "全速率姿態資料" },
            { "Fullrate Notch", "全速率 notch" },
            { "FW Input Pitch Angle", "固定翼輸入 Pitch 角" },
            { "FW Input Roll Angle", "固定翼輸入 Roll 角" },
            { "FW Mixer Pitch", "固定翼混控 Pitch" },
            { "FW Mixer Roll", "固定翼混控 Roll" },
            { "gain_dec", "降低 gain" },
            { "gain_inc", "提高 gain" },
            { "gain_toggle", "切換 gain" },
            { "Generator-Elec", "發電機電力" },
            { "Generator-Fuel", "發電機燃油" },
            { "Glide", "滑翔" },
            { "Glider", "滑翔機" },
            { "GliderOnly", "僅滑翔機" },
            { "Go directly to landing sequence", "直接進入降落程序" },
            { "Go directly to landing sequence via DO_LAND_START mission item", "由 DO_LAND_START 任務項目直接進入降落程序" },
            { "Go directly to landing sequence via DO_RETURN_PATH_START mission item", "由 DO_RETURN_PATH_START 任務項目直接進入降落程序" },
            { "Go to the last location where landing target was detected", "返回最後偵測到降落目標時的機體位置" },
            { "Go towards the approximate location of the detected landing target", "前往估計的降落目標位置" },
            { "GPS configuration", "GPS 配置" },
            { "GPS Configuration", "GPS 配置" },
            { "GPS Disable", "停用 GPS" },
            { "GPS Disable Yaw", "停用 GPS Yaw" },
            { "GPS Disabled", "GPS 已停用" },
            { "GPS Lock", "GPS 定位" },
            { "GPS lock", "GPS 定位" },
            { "GPS Point", "GPS 位置點" },
            { "GPS vehicle only", "僅機體 GPS" },
            { "Gripper Release", "釋放 Gripper" },
            { "Ground", "地面" },
            { "GroundSteering", "地面轉向" },
            { "GuidedThrottlePass", "Guided 油門直通" },
            { "Above Home", "相對 Home 高度" },
            { "Above Origin", "相對原點高度" },
            { "Above sea level", "海拔高度" },
            { "Above Terrain", "相對地形高度" },
            { "Accept MAVLink only from system IDs given by MAV_SYSID_GCS and MAV_SYSID_GCS_HI", "僅接受 MAV_SYSID_GCS 至 MAV_SYSID_GCS_HI 指定 system ID 的 MAVLink" },
            { "Accept Old Terrain Data", "接受舊地形資料（可能含已知資料錯誤）" },
            { "Accept unsigned MAVLink2 messages", "接受未簽章的 MAVLink2 訊息" },
            { "Add leading zero byte to requests", "在請求前加上 0 byte" },
            { "Adjust mid-throttle to be TRIM_THROTTLE in non-auto throttle modes except MANUAL", "除 MANUAL 外的非自動油門模式，將中位油門調整為 TRIM_THROTTLE" },
            { "ADSB Avoidance En", "啟用 ADSB 避碰" },
            { "ADSB Avoidance Enable", "啟用 ADSB 避碰" },
            { "Airspeed library", "空速感測器函式庫" },
            { "Airspeed Ratio Calibration", "空速比例校正" },
            { "Alarm Inverted", "警報輸出反向" },
            { "Alert(PreArm)", "Alert 等級（解鎖前）" },
            { "All (0xFFFF)", "全部（0xFFFF）" },
            { "All Channels neutral except Throttle is 950us", "全部通道回中，油門例外設為 950 微秒" },
            { "All enabled", "全部啟用" },
            { "Allow Arming", "允許解鎖" },
            { "Allow Arming from Transmitter", "允許由發射機解鎖" },
            { "Allow DroneCAN dynamic node update on hot-swap", "允許 DroneCAN 熱插拔時動態更新 node" },
            { "Allow DroneCAN InfoAux to be from a different CAN node", "允許不同 CAN node 傳送 DroneCAN InfoAux" },
            { "Allow fast waypoints (Dijkastras only)", "允許不停點的 fast waypoints（僅 Dijkstra）" },
            { "Allow FW Land", "允許固定翼降落" },
            { "Allow FW Takeoff", "允許固定翼起飛" },
            { "Allow Takeoff Without Raising Throttle", "允許不提高油門即起飛" },
            { "Allow union of inclusion areas", "允許 inclusion areas 的聯集" },
            { "Allow weathervaning", "允許 weathervaning" },
            { "Altitude correction", "高度修正" },
            { "Always face bow towards target point", "船首始終朝向目標點" },
            { "Always face stern towards target point", "船尾始終朝向目標點" },
            { "Always Land", "一律使用 Land" },
            { "Always log", "持續記錄" },
            { "Always reset", "一律重設" },
            { "Always use FW spiral approach", "一律採固定翼螺旋進場" },
            { "Analog Current Only", "類比，僅電流" },
            { "Analog Voltage and Current", "類比，電壓與電流" },
            { "Analog Voltage Only", "類比，僅電壓" },
            { "Arm/Emergency Motor Stop", "解鎖／緊急停止馬達" },
            { "ArmDisarm (4.1 and lower)", "解鎖／上鎖（4.1 及以前）" },
            { "ArmDisarm (4.2 and higher)", "解鎖／上鎖（4.2 起）" },
            { "ArmDisarm with AirMode  (4.2 and higher)", "解鎖／上鎖，並啟用 AirMode（4.2 起）" },
            { "ArmDisarm with Quadplane AirMode (4.2 and higher)", "解鎖／上鎖，並啟用 QuadPlane AirMode（4.2 起）" },
            { "Assume ACC_BODYFIX is perfectly aligned to the vehicle", "假設 ACC_BODYFIX 與機體完全對齊" },
            { "Assume first orientation was level", "假設第一個方向為水平" },
            { "AttCon Accel Limits", "姿態控制加速度限制" },
            { "AttCon Feed Forward", "姿態控制 Feedforward" },
            { "Auto Detect", "自動偵測" },
            { "Auto DO_LAND_START or RTL", "自動執行 DO_LAND_START，否則 RTL" },
            { "Auto DO_LAND_START/DO_RETURN_PATH_START or RTL", "自動使用 DO_LAND_START／DO_RETURN_PATH_START，否則 RTL" },
            { "Auto Mission Reset", "自動重設任務" },
            { "Auto Reboot after 15sec", "15 秒後自動重啟" },
            { "Auto RTL", "自動 RTL" },
            { "auto select remaining port for transmitting Moving baseline Data", "自動選擇剩餘連接埠傳送 Moving baseline 資料" },
            { "AUTOLAND if possible else RTL", "可用時 AUTOLAND，否則 RTL" },
            { "AUTOLAND or RTL", "先切入 AUTOLAND；無法切入時改用 RTL" },
            { "Autorecording enabled", "啟用自動錄影" },
            { "Battery ID/SerialNumber", "電池 ID／序號" },
            { "Battery Index", "電池索引" },
            { "Battery is for internal autopilot use only", "電池資料僅供 autopilot 內部使用" },
            { "Battery Level", "電池電量" },
            { "Battery Monitor", "電池監測器" },
            { "Battery MPPT Enable", "啟用電池 MPPT" },
            { "Be Moving Baseline Base", "作為 Moving Baseline Base" },
            { "Block and MAVLink", "Block 及 MAVLink" },
            { "Board voltage", "控制板電壓" },
            { "Boost Priority", "提高優先序" },
            { "Brake or Land", "Brake；無法使用時 Land" },
            { "Built-in buzzer", "內建蜂鳴器" },
            { "Built-in LED", "內建 LED" },
            { "Bus0(internal)", "Bus0（內部）" },
            { "Bus1(external)", "Bus1（外部）" },
            { "Bus2(auxiliary)", "Bus2（輔助）" },
            { "Bushed motor reverse 1 throttle or throttle-left or omni motor 1", "有刷馬達反轉 1：油門、左油門或 omni motor 1" },
            { "Bushed motor reverse 2 throttle-right or omni motor 2", "有刷馬達反轉 2：右油門或 omni motor 2" },
            { "Bushed motor reverse 3 omni motor 3", "有刷馬達反轉 3：omni motor 3" },
            { "Bushed motor reverse 4 omni motor 4", "有刷馬達反轉 4：omni motor 4" },
            { "Calibrate Compasses", "校正 compass" },
            { "Calibrate direction", "校正方向" },
            { "Calibrate speed", "校正速度" },
            { "Camera Auto Focus", "相機自動對焦" },
            { "Camera Image Tracking", "相機影像追蹤" },
            { "Camera Lens", "相機鏡頭" },
            { "Camera Manual Focus", "相機手動對焦" },
            { "Camera Mode Toggle", "切換相機模式" },
            { "Camera Record Video", "相機錄影" },
            { "Camera Trigger", "觸發相機" },
            { "Camera Zoom", "相機變焦" },
            { "CAN based Pitot tube", "透過 CAN 的 Pitot tube" },
            { "Capture to file", "擷取至檔案" },
            { "Center Frequency", "中心頻率" },
            { "Change Mode", "變更模式" },
            { "Check and update if needed", "檢查，必要時更新" },
            { "Check motor noise", "檢查馬達雜訊" },
            { "Circle and Polygon", "圓形與多邊形" },
            { "Circle Centered on Home", "以 Home 為中心的圓形" },
            { "Circle Rate", "Circle 轉動速率" },
            { "CIRCLE/no change(if already in AUTO|GUIDED|LOITER)", "CIRCLE；已在 AUTO／GUIDED／LOITER 則不變更" },
            { "Clear all configurations not set by ardupilot (UBlox only)", "清除非 ArduPilot 設定的全部配置（僅 UBlox）" },
            { "Clear Mission on reboot", "重啟時清除任務" },
            { "Clear Waypoints", "清除航點" },
            { "Climb Or Descend", "爬升或下降" },
            { "Compass Learn", "compass 學習" },
            { "Continue if in Auto on GCS failsafe only", "僅 GCS failsafe 時，若處於 Auto 則繼續" },
            { "Continue if in Auto on RC and/or GCS failsafe", "RC 與／或 GCS failsafe 時，若處於 Auto 則繼續" },
            { "Continue if in Auto on RC and/or GCS failsafe and continue if in pilot controlled modes on GCS failsafe", "RC 與／或 GCS failsafe 時繼續 Auto；GCS failsafe 時也繼續飛行員操控模式" },
            { "Continue if in Auto on RC failsafe only", "僅 RC failsafe 時，若處於 Auto 則繼續" },
            { "Continue if in Guided on RC failsafe only", "僅 RC failsafe 時，若處於 Guided 則繼續" },
            { "Control Tuning", "控制調整" },
            { "Convert feet to miles at 5280ft instead of 10000ft", "在 5280 ft 而非 10000 ft 切換為英里顯示" },
            { "Correct crosstrack error", "修正橫向航跡誤差" },
            { "Crank direction Reverse", "反轉曲柄方向" },
            { "Crash Dump arming check active", "啟用 Crash Dump 解鎖前檢查" },
            { "Crash Dump arming check deactivated", "停用 Crash Dump 解鎖前檢查" },
            { "Critical(PreArm)", "Critical 等級（解鎖前）" },
            { "Crow Select", "選擇 Crow" },
            { "CRSF flight mode disarm star", "上鎖時在 CRSF 模式加星號" },
            { "CRSF RSSI shows Link Quality", "CRSF RSSI 顯示 Link Quality" },
            { "Custom 1", "自訂旋轉 1" },
            { "Custom 2", "自訂旋轉 2" },
            { "Custom 4.1 and older", "自訂旋轉（4.1 及以前）" },
            { "D spoilers have pitch input", "Differential spoilers 混入 Pitch 輸入" },
            { "DDVP with external governor", "DDVP 搭配外部 governor" },
            { "Delay Spoolup", "延遲啟動旋翼" },
            { "Deploy during Land", "Land 期間展開" },
            { "Deploy Parachute", "釋放降落傘" },
            { "ACCEL1 Failure", "ACCEL1 故障" },
            { "ACCEL2 Failure", "ACCEL2 故障" },
            { "ACCEL3 Failure", "ACCEL3 故障" },
            { "ACCEL4 Failure", "ACCEL4 故障" },
            { "ACCEL5 Failure", "ACCEL5 故障" },
            { "Allow FW Land-if bit is not set then NAV_LAND command on quadplanes will instead perform a NAV_VTOL_LAND", "允許固定翼降落；未設定時 QuadPlane 將 NAV_LAND 改為 NAV_VTOL_LAND" },
            { "Allow FW Takeoff-if bit is not set then NAV_TAKEOFF command on quadplanes will instead perform a NAV_VTOL takeoff", "允許固定翼起飛；未設定時 QuadPlane 將 NAV_TAKEOFF 改為 NAV_VTOL 起飛" },
            { "Allow missing DroneCAN compasses to be automaticaly replaced (calibration still required)", "允許自動替換遺失的 DroneCAN compass，仍需校正" },
            { "Allow Precision Landing after manual reposition", "手動重定位後仍允許 Precision Landing" },
            { "Always use FW spiral approach-always use Use a fixed wing spiral approach for VTOL landings", "VTOL 降落一律使用固定翼螺旋進場" },
            { "ARMVTOL-arm only in VTOL modes (or AUTO mode when current nav cmd is VTOL Takeoff)", "僅在 VTOL 模式，或 AUTO 目前導航指令為 VTOL Takeoff 時允許解鎖" },
            { "ARMVTOL-arm only in VTOL or AUTO modes", "僅在 VTOL 或 AUTO 模式允許解鎖" },
            { "Climb to ALT_HOLD_RTL before turning for RTL", "先爬升至 ALT_HOLD_RTL，再轉向 RTL" },
            { "Climb to RTL_ALTITUDE before turning for RTL", "先爬升至 RTL_ALTITUDE，再轉向 RTL" },
            { "CompleteTransition if Q_TRANS_FAIL", "Q_TRANS_FAIL 逾時時完成轉換" },
            { "CompleteTransition-to fixed wing if Q_TRANS_FAIL timer times out instead of QLAND", "Q_TRANS_FAIL 逾時後完成固定翼轉換，而非 QLAND" },
            { "Continue the mission even after comms are recovered (does not go to the mission item at the time comms were lost)", "通訊恢復後繼續目前任務進度，不返回斷訊時的任務項目" },
            { "Delay Spoolup-delay VTOL spoolup for 2 seconds after arming", "解鎖後延遲 2 秒啟動 VTOL 馬達" },
            { "Demix heli servos and send roll/pitch/collective/yaw", "解除直升機 servo 混控，傳送 Roll／Pitch／collective／Yaw" },
            { "Disable attitude check for takeoff arming", "停用起飛解鎖的姿態檢查" },
            { "Disable FLTD update by Autotune", "禁止 AutoTune 更新 FLTD" },
            { "Disable FLTT update by Autotune", "禁止 AutoTune 更新 FLTT" },
            { "Disable force fixed wing controller recovery", "停用強制固定翼控制器恢復" },
            { "Disable Ground Effect Compensation-on baro altitude reports", "停用氣壓高度的地面效應補償" },
            { "Disable heap expansion on allocation failure", "記憶體配置失敗時不擴充 heap" },
            { "Disable pre-arm check", "停用解鎖前檢查" },
            { "Disable Qassist-based on synthetic airspeed even if airspeed sensor is used", "即使使用空速計，也停用依合成空速觸發的 Qassist" },
            { "Disable quadplane spin recovery", "停用 QuadPlane spin recovery" },
            { "Disable speed based Qassist when using synthethic airspeed estimates", "使用合成空速估計時，停用速度條件的 Qassist" },
            { "Disable speed based Qassist when using synthetic airspeed estimates", "使用合成空速估計時，停用速度條件的 Qassist" },
            { "Disable suppression of fixed wing rate gains in ground mode", "停用地面模式對固定翼 Rate gains 的抑制" },
            { "Disable thrust loss detection in transtions and fixed wing modes. Thrust loss detection will only run in VTOL modes.", "停用轉換與固定翼模式的推力喪失偵測，僅在 VTOL 偵測" },
            { "Disable thrust loss detection.", "停用推力喪失偵測" },
            { "DisableApproach-disable use of approach and airbrake stages in VTOL landing", "VTOL 降落跳過進場與 airbrake 階段" },
            { "DisableDJIWorkarounds", "停用 DJI 相容性處理" },
            { "DisableFIFO", "停用 FIFO" },
            { "Disarmed Yaw Tilt-enable motor tilt for yaw when disarmed", "上鎖時允許馬達傾轉控制 Yaw" },
            { "Don't forward mavlink to/from", "不向此鏈路轉送 MAVLink，也不轉送來自此鏈路的 MAVLink" },
            { "Don't forward mavlink to/from (moved to MAVn_OPTIONS >4.7)", "不雙向轉送 MAVLink（原文標示 >4.7 移至 MAVn_OPTIONS）" },
            { "Don't log Dt stats", "不記錄 Dt 統計" },
            { "Don't print frame rate stats", "不輸出 frame rate 統計" },
            { "Enable AFS for all autonomous modes (not just AUTO)", "所有自主模式都啟用 AFS，不限 AUTO" },
            { "Enable all modes", "啟用全部模式" },
            { "Enable autoflap in manual modes and use minimum of target and actual speed for flap setting", "手動模式啟用自動襟翼，以目標與實際速度的較小值設定襟翼" },
            { "Enable continuous sensor probe", "持續探測感測器" },
            { "Enable Coordinated turns", "啟用 coordinated turn" },
            { "Enable FBWB style loiter altitude control", "啟用 FBWB 式 Loiter 高度控制" },
            { "Enable full aerodynamic load factor-based roll limits when an airspeed sensor is enabled and AIRSPEED_STALL is set", "啟用空速計且已設 AIRSPEED_STALL 時，依氣動負載因子完整限制 Roll" },
            { "Enable target distance", "啟用目標距離" },
            { "Enable yaw damper in acro mode", "Acro 模式啟用 Yaw damper" },
            { "EnableDefaultAirspeed for takeoff", "起飛時啟用預設空速" },
            { "EnableLandResposition-enable pilot controlled repositioning in AUTO land.Descent will pause while repositioning", "AUTO 降落允許飛行員重定位，重定位時暫停下降" },
            { "Force Qassist-on always", "強制持續啟用 Qassist" },
            { "Force RTL mode on VTOL failsafes overriding bit 5(USE QRTL)", "VTOL failsafe 強制使用 RTL，覆寫位元 5 USE QRTL" },
            { "Force RTL mode-forces RTL mode on rc failsafe in VTOL modes overriding bit 5(USE_QRTL)", "VTOL 的 RC failsafe 強制使用 RTL，覆寫位元 5 USE_QRTL" },
            { "Force target airspeed to AIRSPEED_CRUISE in Cruise or FBWB", "Cruise／FBWB 的目標空速固定為 AIRSPEED_CRUISE" },
            { "Force target airspeed to trim airspeed in Cruise or FBWB", "Cruise／FBWB 的目標空速固定為配平空速" },
            { "honor min throttle during landing flare", "降落拉平階段仍遵守最低油門" },
            { "Ignore forward flight angle limits-in Qmodes and use Q_A_ANGLE_MAX exclusively", "Q 模式忽略前飛角度限制，僅使用 Q_A_ANGLE_MAX" },
            { "Ignore forward flight angle limits-in Qmodes and use Q_ANGLE_MAX exclusively", "Q 模式忽略前飛角度限制，僅使用 Q_ANGLE_MAX" },
            { "Ignore Streamrate", "忽略串流速率設定" },
            { "Ignore Streamrate (moved to MAVn_OPTIONS >4.7)", "忽略串流速率設定（原文標示 >4.7 移至 MAVn_OPTIONS）" },
            { "In AUTO - climb to next waypoint altitude immediately instead of linear climb", "AUTO 立即爬升至下一航點高度，而非沿航段線性爬升" },
            { "Increase Target landing airspeed constraint From AIRSPEED_CRUISE to AIRSPEED_MAX", "將降落目標空速上限由 AIRSPEED_CRUISE 提高至 AIRSPEED_MAX" },
            { "Increase Target landing airspeed constraint From Trim Airspeed to AIRSPEED_MAX", "將降落目標空速上限由配平空速提高至 AIRSPEED_MAX" },
            { "Increase Target landing airspeed constraint From Trim Airspeed to ARSPD_FBW_MAX", "將降落目標空速上限由配平空速提高至 ARSPD_FBW_MAX" },
            { "Indicate takeoff waiting for neutral rudder with flight control surfaces", "以舵面動作提示起飛正等待方向舵回中" },
            { "Level Transition-keep wings within LEVEL_ROLL_LIMIT and only use forward motor(s) for climb during transition", "轉換時機翼保持於 LEVEL_ROLL_LIMIT 內，僅以前推馬達爬升" },
            { "log Dijkstra points", "記錄 Dijkstra 路徑點" },
            { "log runtime memory usage and execution time", "記錄執行期記憶體用量與執行時間" },
            { "Maintain high speed in final descent", "最後下降階段維持高速" },
            { "Moving Landing Target", "移動中的降落目標" },
            { "Mtrs_Only_Qassist-in tailsitters only uses VTOL motors and not flying surfaces for QASSIST", "tailsitter 的 QASSIST 僅使用 VTOL 馬達，不使用舵面" },
            { "No Scripts to run message if all scripts have stopped", "全部腳本停止後顯示 No Scripts to run 訊息" },
            { "Option to not jump to AFS_WP_COMMS if already in the return path", "若已在返回路徑，不跳至 AFS_WP_COMMS" },
            { "Pre and post filter", "濾波前及濾波後" },
            { "Remove the PTCH_TRIM_DEG on the GCS horizon", "在 GCS 人工地平儀移除 PTCH_TRIM_DEG 補償" },
            { "Remove the PTCH_TRIM_DEG on the OSD horizon", "在 OSD 人工地平儀移除 PTCH_TRIM_DEG 補償" },
            { "Remove the TRIM_PITCH_CD on the GCS horizon", "在 GCS 人工地平儀移除 TRIM_PITCH_CD 補償" },
            { "Remove the TRIM_PITCH_CD on the OSD horizon", "在 OSD 人工地平儀移除 TRIM_PITCH_CD 補償" },
            { "Reset ALT_OFFSET on flight mode or AUTO waypoint changes", "飛行模式或 AUTO 航點變更時重設 ALT_OFFSET" },
            { "Reset position on startup", "啟動時重設位置" },
            { "Reset the origin of the waypoint to the present location", "將航段起點重設為目前位置" },
            { "Rudder mixing in direct flight modes only (Manual/Stabilize/Acro)", "只在 Manual／Stabilize／Acro 直接控制模式混入方向舵" },
            { "Runtime messages for memory usage and execution time", "以執行期訊息顯示記憶體用量與執行時間" },
            { "RX_NoDMA", "RX 不使用 DMA" },
            { "RX_PullDown", "RX 下拉" },
            { "RX_PullUp", "RX 上拉" },
            { "Sample post-filtering", "取樣濾波後資料" },
            { "Sample pre- and post-filter", "取樣濾波前與濾波後資料" },
            { "Save CRC of current scripts to loaded and running checksum parameters enabling pre-arm", "將目前腳本 CRC 存入載入與執行 checksum 參數，啟用相應解鎖前檢查" },
            { "Warn only", "僅警告" },
            { "Continue if in Auto on RC failsafe", "RC failsafe 時，若處於 Auto 則繼續任務" },
            { "Continue if in Auto on GCS failsafe", "GCS failsafe 時，若處於 Auto 則繼續任務" },
            { "Continue if in Guided on RC failsafe", "RC failsafe 時，若處於 Guided 則繼續" },
            { "Continue if landing on any failsafe", "任一 failsafe 發生時，若正在降落則繼續降落" },
            { "Continue if in pilot controlled modes on GCS failsafe", "GCS failsafe 時，若處於飛行員操控模式則繼續" },
            { "Release Gripper", "釋放 Gripper" },
            { "Enabled always RTL", "啟用，觸發時採用 RTL" },
            { "Enabled always Land", "啟用，觸發時採用 Land" },
            { "Enabled always SmartRTL or RTL", "啟用，採用 SmartRTL；無法使用時改採 RTL" },
            { "Enabled always SmartRTL or Land", "啟用，採用 SmartRTL；無法使用時改採 Land" },
            { "Enabled always Brake or Land", "啟用，採用 Brake；無法使用時改採 Land" },
            { "Enabled Continue with Mission in Auto Mode (Removed in 4.0+)", "啟用，在 Auto 繼續任務（4.0+ 已移除此選項）" },
            { "RTL or Continue with Mission in Auto Mode (Removed in 4.0+-see FS_OPTIONS)", "RTL，或在 Auto 繼續任務（4.0+ 已移除，請參閱 FS_OPTIONS）" },
            { "Ignore RC Receiver", "忽略 RC 接收機輸入" },
            { "Ignore MAVLink Overrides", "忽略 MAVLink Overrides 輸入" },
            { "Ignore Receiver Failsafe bit but allow other RC failsafes if setup", "忽略接收機 Failsafe bit；其他已設定的 RC failsafes 仍有效" },
            { "FPort Pad", "啟用 FPort padding" },
            { "Log RC input bytes", "記錄 RC 輸入的原始 bytes" },
            { "Arming check throttle for 0 input", "解鎖前檢查油門輸入是否為 0" },
            { "Skip the arming check for neutral Roll/Pitch/Yaw sticks", "略過解鎖前 Roll/Pitch/Yaw 搖桿回中檢查" },
            { "Allow Switch reverse", "允許反轉開關輸入" },
            { "Use passthrough for CRSF telemetry", "CRSF telemetry 使用 passthrough" },
            { "Suppress CRSF mode/rate message for ELRS systems", "ELRS 系統不顯示 CRSF mode/rate 訊息" },
            { "Enable multiple receiver support", "啟用多接收機支援" },
            { "Use Link Quality for RSSI with CRSF", "CRSF 以 Link Quality 作為 RSSI" },
            { "Annotate CRSF flight mode with * on disarm", "上鎖時在 CRSF flight mode 標示 *" },
            { "Use 420kbaud for ELRS protocol", "ELRS protocol 使用 420kbaud" },
            { "Clear MAVLink overrides on any stick input", "偵測到任何搖桿輸入時清除 MAVLink overrides" }
        };

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
            { "This is the altitude the vehicle will move to as the final stage of Returning to Launch or after completing a mission.  Set to zero to land.", "RTL 最後階段的目標高度，以 Home 為高度基準，不是相對返航途中地形的高度。0 表示降落；正值通常在此高度停留，而非繼續自動降落，但 RC failsafe 等安全處置仍可能要求降落。任務完成後若進入 RTL，亦使用此設定；不是所有任務結束都會套用。" },
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

                Form paramCompareForm = new ParamCompare(Params, MainV2.comPort.MAV.param, param2)
                { StageParameter = StageComparedParameter };

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
            string vehicle = (fmtMetadataVehicle ?? MainV2.comPort.MAV.cs.firmware.ToString());
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
