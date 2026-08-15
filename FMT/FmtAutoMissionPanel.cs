using log4net;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtAutoMissionPanel : UserControl
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Color Background = Color.FromArgb(12, 27, 36);
        private static readonly Color FieldBackground = Color.FromArgb(29, 48, 59);
        private static readonly Color SkyBlue = Color.FromArgb(45, 169, 220);
        private static readonly Color InactiveText = Color.FromArgb(175, 187, 194);
        private static readonly Color ActiveGreen = Color.FromArgb(46, 190, 92);
        private static readonly Color Warning = Color.FromArgb(242, 156, 45);

        private readonly TableLayoutPanel layout;
        private readonly Label currentActionLabel;
        private readonly ComboBox missionCombo;
        private readonly Button executeButton;
        private readonly Label progressTextLabel;
        private readonly MissionProgressBar progressBar;
        private readonly Label progressPercentLabel;
        private readonly Label nextActionLabel;
        private readonly Label distanceLabel;
        private readonly Label etaLabel;
        private readonly ToolTip toolTip;
        private readonly List<FmtMissionItem> missionItems = new List<FmtMissionItem>();
        private readonly List<int> missionPacketSubscriptions = new List<int>();

        private int currentMissionSequence = -1;
        private int lastObservedMissionCount = -1;
        private int missionSignature;
        private int missionRefreshQueued;
        private int reportedMissionItemCount = -1;
        private bool busy;
        private string displaySignature = string.Empty;
        private string executeStateSignature = string.Empty;

        internal FmtAutoMissionPanel()
        {
            Name = "fmtAutoMissionPanel";
            Dock = DockStyle.Fill;
            BackColor = Background;
            ForeColor = Color.White;
            Margin = Padding.Empty;
            Padding = new Padding(7, 6, 7, 6);
            MinimumSize = new Size(0, 54);

            toolTip = new ToolTip();

            currentActionLabel = CreateLabel("fmtMissionCurrent", "● 目前執行：任務狀態未知", FontStyle.Bold);
            currentActionLabel.ForeColor = InactiveText;

            missionCombo = new ComboBox
            {
                Name = "fmtMissionContents",
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                IntegralHeight = false,
                DropDownHeight = 330,
                BackColor = FieldBackground,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F),
                Margin = new Padding(4, 4, 6, 4)
            };
            missionCombo.DrawItem += MissionComboDrawItem;
            missionCombo.DropDown += MissionComboDropDown;
            missionCombo.SelectedIndexChanged += (sender, args) => UpdateExecuteState();

            executeButton = new Button
            {
                Name = "fmtMissionExecute",
                Text = "執行",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 7, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = SkyBlue,
                ForeColor = Color.White,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Enabled = false
            };
            executeButton.FlatAppearance.BorderColor = Color.FromArgb(95, 205, 240);
            executeButton.Click += ExecuteButtonClick;

            progressTextLabel = CreateLabel("fmtMissionProgressText", "Mission Item -- / --", FontStyle.Bold);
            progressTextLabel.TextAlign = ContentAlignment.MiddleCenter;

            progressBar = new MissionProgressBar
            {
                Name = "fmtMissionProgress",
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 12, 4, 12)
            };

            progressPercentLabel = CreateLabel("fmtMissionProgressPercent", "--%", FontStyle.Bold);
            progressPercentLabel.TextAlign = ContentAlignment.MiddleCenter;
            progressPercentLabel.ForeColor = SkyBlue;

            nextActionLabel = CreateLabel("fmtMissionNext", "下一任務：--", FontStyle.Regular);
            nextActionLabel.ForeColor = Color.Gainsboro;

            distanceLabel = CreateLabel("fmtMissionDistance", string.Empty, FontStyle.Regular);
            distanceLabel.TextAlign = ContentAlignment.MiddleCenter;
            distanceLabel.ForeColor = Color.Gainsboro;
            distanceLabel.Visible = false;

            etaLabel = CreateLabel("fmtMissionEta", "ETA --", FontStyle.Regular);
            etaLabel.TextAlign = ContentAlignment.MiddleCenter;
            etaLabel.ForeColor = Color.Gainsboro;

            layout = new TableLayoutPanel
            {
                Name = "fmtMissionLayout",
                Dock = DockStyle.Fill,
                BackColor = Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                RowCount = 1,
                ColumnCount = 9
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            ConfigureColumns(220, 230, 72, 100, 35F, 58, 65F, 90, 82);
            layout.Controls.Add(currentActionLabel, 0, 0);
            layout.Controls.Add(missionCombo, 1, 0);
            layout.Controls.Add(executeButton, 2, 0);
            layout.Controls.Add(progressTextLabel, 3, 0);
            layout.Controls.Add(progressBar, 4, 0);
            layout.Controls.Add(progressPercentLabel, 5, 0);
            layout.Controls.Add(nextActionLabel, 6, 0);
            layout.Controls.Add(distanceLabel, 7, 0);
            layout.Controls.Add(etaLabel, 8, 0);

            Controls.Add(layout);
            SizeChanged += (sender, args) => ApplyResponsiveLayout();
            RebuildMissionCombo(-1);
            SubscribeMissionPackets();
        }

        internal void UpdateFromVehicle(bool forceMissionRefresh = false)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action) (() => UpdateFromVehicle(forceMissionRefresh)));
                return;
            }

            var port = MainV2.comPort;
            var mav = port == null ? null : port.MAV;
            var connected = port != null && port.BaseStream != null && port.BaseStream.IsOpen;
            var observedCount = mav == null || mav.wps == null ? 0 : mav.wps.Count;
            if (forceMissionRefresh || observedCount != lastObservedMissionCount)
                RefreshMissionSnapshot();

            currentMissionSequence = GetCurrentSequence();
            var mode = mav == null || mav.cs == null ? string.Empty : mav.cs.mode;
            var currentItem = missionItems.FirstOrDefault(item => item.Sequence == currentMissionSequence);
            var currentIndex = currentItem == null ? -1 : missionItems.IndexOf(currentItem);
            var currentListPosition = currentIndex < 0 ? 0 : currentIndex + 1;
            var nextItem = currentIndex >= 0 && currentIndex + 1 < missionItems.Count
                ? missionItems[currentIndex + 1]
                : null;
            var reportedTotal = Volatile.Read(ref reportedMissionItemCount);
            var total = reportedTotal < 0
                ? missionItems.Count
                : Math.Max(missionItems.Count, reportedTotal);
            var progress = currentItem != null && total > 0
                ? Math.Max(0, Math.Min(100, (int) Math.Round(currentListPosition * 100.0 / total)))
                : 0;

            var currentDescription = currentItem == null
                ? "任務狀態未知"
                : FmtMissionCommandFormatter.Describe(currentItem);
            var inAuto = string.Equals(mode, "AUTO", StringComparison.OrdinalIgnoreCase);
            var currentText = inAuto
                ? "● 目前執行：" + currentDescription
                : "○ 目前模式：" + (string.IsNullOrWhiteSpace(mode) ? "--" : mode) +
                  "｜AUTO 任務暫停於 " + currentDescription;
            var nextText = nextItem == null
                ? "下一任務：--"
                : "下一任務：" + FmtMissionCommandFormatter.Describe(nextItem);

            var distanceText = string.Empty;
            var etaText = "ETA --";
            if (nextItem != null)
            {
                PointLatLngAlt nextLocation;
                var vehicleLocation = mav == null || mav.cs == null ? PointLatLngAlt.Zero : mav.cs.Location;
                if (nextItem.TryGetLocation(out nextLocation) && vehicleLocation != PointLatLngAlt.Zero)
                {
                    var distanceMeters = vehicleLocation.GetDistance(nextLocation);
                    if (!double.IsNaN(distanceMeters) && !double.IsInfinity(distanceMeters) && distanceMeters >= 0)
                    {
                        distanceText = "距離 " + FormatDistance(distanceMeters);
                        var speed = GetGroundSpeedMetersPerSecond();
                        if (speed >= 0.5)
                            etaText = FormatEta(distanceMeters / speed);
                    }
                }
            }

            var signature = connected + "|" + mode + "|" + currentMissionSequence + "|" + total + "|" +
                            progress + "|" + currentText + "|" + nextText + "|" + distanceText + "|" + etaText;
            if (!string.Equals(signature, displaySignature, StringComparison.Ordinal))
            {
                displaySignature = signature;
                currentActionLabel.Text = currentText;
                currentActionLabel.ForeColor = connected && inAuto ? ActiveGreen : InactiveText;
                progressTextLabel.Text = total == 0
                    ? "Mission Item -- / --"
                    : currentItem == null
                        ? "Mission Item -- / " + total
                    : "Mission Item " + currentListPosition + " / " + total;
                progressBar.Value = progress;
                progressPercentLabel.Text = total == 0 || currentItem == null ? "--%" : progress + "%";
                nextActionLabel.Text = nextText;
                distanceLabel.Text = distanceText;
                distanceLabel.Visible = !string.IsNullOrEmpty(distanceText) && ClientSize.Width >= 980;
                etaLabel.Text = etaText;
                missionCombo.Invalidate();
            }

            UpdateExecuteState();
        }

        private void SubscribeMissionPackets()
        {
            var port = MainV2.comPort;
            if (port == null)
                return;

            missionPacketSubscriptions.Add(port.SubscribeToPacketType(
                MAVLink.MAVLINK_MSG_ID.MISSION_COUNT, MissionPacketReceived, 0, 0));
            missionPacketSubscriptions.Add(port.SubscribeToPacketType(
                MAVLink.MAVLINK_MSG_ID.MISSION_ITEM, MissionPacketReceived, 0, 0));
            missionPacketSubscriptions.Add(port.SubscribeToPacketType(
                MAVLink.MAVLINK_MSG_ID.MISSION_ITEM_INT, MissionPacketReceived, 0, 0));
            missionPacketSubscriptions.Add(port.SubscribeToPacketType(
                MAVLink.MAVLINK_MSG_ID.MISSION_ACK, MissionPacketReceived, 0, 0));
        }

        private bool MissionPacketReceived(MAVLink.MAVLinkMessage message)
        {
            if (message == null || IsDisposed || Disposing)
                return true;

            // Ignore fence/rally transfers. The AUTO panel represents only the main Mission list.
            if (message.msgid == (uint) MAVLink.MAVLINK_MSG_ID.MISSION_COUNT)
            {
                var count = message.ToStructure<MAVLink.mavlink_mission_count_t>();
                if (count.mission_type != (byte) MAVLink.MAV_MISSION_TYPE.MISSION)
                    return true;

                // Mission Planner keeps HOME at sequence 0; AUTO starts at sequence 1.
                // Publish the usable Mission Item total immediately, before all items
                // have finished downloading into the local waypoint dictionary.
                Volatile.Write(ref reportedMissionItemCount, Math.Max(0, (int) count.count - 1));
            }
            else if (message.msgid == (uint) MAVLink.MAVLINK_MSG_ID.MISSION_ITEM)
            {
                var item = message.ToStructure<MAVLink.mavlink_mission_item_t>();
                if (item.mission_type != (byte) MAVLink.MAV_MISSION_TYPE.MISSION)
                    return true;
            }
            else if (message.msgid == (uint) MAVLink.MAVLINK_MSG_ID.MISSION_ITEM_INT)
            {
                var item = message.ToStructure<MAVLink.mavlink_mission_item_int_t>();
                if (item.mission_type != (byte) MAVLink.MAV_MISSION_TYPE.MISSION)
                    return true;
            }

            QueueMissionRefresh();
            return true;
        }

        private void QueueMissionRefresh()
        {
            if (Interlocked.Exchange(ref missionRefreshQueued, 1) != 0)
                return;

            try
            {
                if (!IsHandleCreated || IsDisposed || Disposing)
                {
                    Interlocked.Exchange(ref missionRefreshQueued, 0);
                    return;
                }

                BeginInvoke((Action) (() =>
                {
                    Interlocked.Exchange(ref missionRefreshQueued, 0);
                    UpdateFromVehicle(true);
                }));
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref missionRefreshQueued, 0);
            }
        }

        private void RefreshMissionSnapshot()
        {
            var port = MainV2.comPort;
            var mav = port == null ? null : port.MAV;
            var source = mav == null || mav.wps == null
                ? new KeyValuePair<int, MAVLink.mavlink_mission_item_int_t>[0]
                : mav.wps.ToArray();
            lastObservedMissionCount = source.Length;

            var nextItems = source
                .Where(pair => pair.Key > 0 && pair.Key <= ushort.MaxValue)
                .OrderBy(pair => pair.Key)
                .Select(pair => FmtMissionCommandFormatter.FromMissionItem(pair.Key, pair.Value))
                .ToList();
            var nextSignature = ComputeMissionSignature(nextItems);
            if (nextSignature == missionSignature && MissionListsEqual(missionItems, nextItems))
                return;

            var selected = missionCombo.SelectedItem as FmtMissionItem;
            var selectedSequence = selected == null ? -1 : selected.Sequence;
            missionItems.Clear();
            missionItems.AddRange(nextItems);
            missionSignature = nextSignature;
            RebuildMissionCombo(selectedSequence);
            displaySignature = string.Empty;
        }

        private void RebuildMissionCombo(int preferredSequence)
        {
            missionCombo.BeginUpdate();
            try
            {
                missionCombo.Items.Clear();
                foreach (var item in missionItems)
                    missionCombo.Items.Add(item);

                if (missionItems.Count == 0)
                {
                    missionCombo.Items.Add("尚未載入任務");
                    missionCombo.SelectedIndex = 0;
                }
                else
                {
                    var preferred = missionItems.FirstOrDefault(item => item.Sequence == preferredSequence) ??
                                    missionItems.FirstOrDefault(item => item.Sequence == GetCurrentSequence()) ??
                                    missionItems[0];
                    missionCombo.SelectedItem = preferred;
                }
            }
            finally
            {
                missionCombo.EndUpdate();
            }
        }

        private async void ExecuteButtonClick(object sender, EventArgs e)
        {
            var target = missionCombo.SelectedItem as FmtMissionItem;
            string error;
            if (!ValidateTarget(target, out error))
            {
                ShowFailure(error);
                UpdateFromVehicle(true);
                return;
            }

            var current = missionItems.FirstOrDefault(item => item.Sequence == GetCurrentSequence());
            using (var confirmation = new MissionConfirmationForm(current, target))
            {
                if (confirmation.ShowDialog(FindForm()) != DialogResult.Yes)
                    return;
            }

            // Revalidate after the operator closes the confirmation dialog. The mission may
            // have been downloaded, replaced or disconnected while the dialog was open.
            if (!ValidateTarget(target, out error))
            {
                ShowFailure(error);
                UpdateFromVehicle(true);
                return;
            }

            SetBusy(true);
            try
            {
                var changed = await MainV2.comPort.setWPCurrentAsync(
                    MainV2.comPort.MAV.sysid,
                    MainV2.comPort.MAV.compid,
                    (ushort) target.Sequence);
                if (!changed)
                    ShowFailure("飛控未確認任務切換，原任務保持不變。");
            }
            catch (Exception ex)
            {
                Log.Error("FMT AUTO mission item change failed", ex);
                ShowFailure("任務切換失敗。\r\n" + ex.Message);
            }
            finally
            {
                SetBusy(false);
                UpdateFromVehicle(true);
            }
        }

        private bool ValidateTarget(FmtMissionItem target, out string error)
        {
            error = null;
            var port = MainV2.comPort;
            if (port == null || port.BaseStream == null || !port.BaseStream.IsOpen)
            {
                error = "尚未連線飛控，無法變更 AUTO 任務。";
                return false;
            }
            if (target == null || target.Sequence <= 0 || target.Sequence > ushort.MaxValue)
            {
                error = "請先選擇有效的任務項目。";
                return false;
            }
            if (target.Sequence == GetCurrentSequence())
            {
                error = "此項目目前正在執行，請選擇其他任務項目。";
                return false;
            }
            if (port.MAV == null || port.MAV.wps == null || port.MAV.wps.Count <= 1)
            {
                error = "目前沒有已下載或已載入的 Mission。";
                return false;
            }

            MAVLink.mavlink_mission_item_int_t liveItem;
            if (!port.MAV.wps.TryGetValue(target.Sequence, out liveItem) ||
                liveItem.command != target.CommandId)
            {
                error = "任務內容已變更，請重新選擇 Mission Item。";
                return false;
            }
            if (port.MAV.sysid == 0)
            {
                error = "飛控尚未提供有效系統識別，無法切換任務。";
                return false;
            }

            return true;
        }

        private void UpdateExecuteState()
        {
            var target = missionCombo.SelectedItem as FmtMissionItem;
            var connected = MainV2.comPort != null && MainV2.comPort.BaseStream != null &&
                            MainV2.comPort.BaseStream.IsOpen;
            var enabled = !busy && connected && missionItems.Count > 0 && target != null &&
                          target.Sequence > 0 && target.Sequence != GetCurrentSequence();
            var reason = enabled
                ? "選擇任務項目後，按下執行並完成一次確認。"
                : GetDisabledReason(connected, target);
            var signature = busy + "|" + connected + "|" + missionItems.Count + "|" +
                            (target == null ? -1 : target.Sequence) + "|" + GetCurrentSequence() + "|" +
                            enabled + "|" + reason;
            if (string.Equals(signature, executeStateSignature, StringComparison.Ordinal))
                return;

            executeStateSignature = signature;
            executeButton.Enabled = enabled;
            executeButton.BackColor = enabled ? SkyBlue : Color.FromArgb(58, 70, 77);
            toolTip.SetToolTip(executeButton, reason);
        }

        private string GetDisabledReason(bool connected, FmtMissionItem target)
        {
            if (busy)
                return "正在等待飛控確認任務切換。";
            if (!connected)
                return "尚未連線飛控。";
            if (missionItems.Count == 0)
                return "尚未下載或載入 Mission。";
            if (target == null)
                return "請選擇有效的任務項目。";
            if (target.Sequence == GetCurrentSequence())
                return "此項目目前正在執行，請選擇其他任務項目。";
            return "目前無法執行任務切換。";
        }

        private void SetBusy(bool value)
        {
            busy = value;
            executeStateSignature = string.Empty;
            missionCombo.Enabled = !value;
            executeButton.Text = value ? "切換中…" : "執行";
            UpdateExecuteState();
        }

        private void MissionComboDropDown(object sender, EventArgs e)
        {
            RefreshMissionSnapshot();
            currentMissionSequence = GetCurrentSequence();
            missionCombo.DropDownWidth = Math.Max(missionCombo.Width, 420);
            missionCombo.Invalidate();
            UpdateExecuteState();
        }

        private void MissionComboDrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= missionCombo.Items.Count)
                return;

            var item = missionCombo.Items[e.Index] as FmtMissionItem;
            var isCurrent = item != null && item.Sequence == currentMissionSequence;
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var background = isCurrent
                ? Color.FromArgb(30, 112, 148)
                : selected ? Color.FromArgb(48, 73, 86) : FieldBackground;
            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, e.Bounds);

            var text = item == null
                ? Convert.ToString(missionCombo.Items[e.Index])
                : (isCurrent ? "✓ " : "  ") + item.Sequence.ToString().PadLeft(3) + "   " +
                  FmtMissionCommandFormatter.Describe(item);
            using (var brush = new SolidBrush(isCurrent ? Color.White : Color.Gainsboro))
                e.Graphics.DrawString(text, e.Font, brush, e.Bounds.Left + 4, e.Bounds.Top + 3);
            e.DrawFocusRectangle();
        }

        private void ApplyResponsiveLayout()
        {
            if (layout == null)
                return;

            if (ClientSize.Width >= 1200)
            {
                ConfigureColumns(220, 230, 72, 100, 35F, 58, 65F, 90, 82);
                etaLabel.Visible = true;
                distanceLabel.Visible = !string.IsNullOrEmpty(distanceLabel.Text);
            }
            else if (ClientSize.Width >= 930)
            {
                ConfigureColumns(185, 195, 68, 90, 35F, 52, 65F, 78, 70);
                etaLabel.Visible = true;
                distanceLabel.Visible = !string.IsNullOrEmpty(distanceLabel.Text);
            }
            else
            {
                ConfigureColumns(165, 170, 64, 88, 42F, 50, 58F, 0, 0);
                distanceLabel.Visible = false;
                etaLabel.Visible = false;
            }
        }

        private void ConfigureColumns(float current, float combo, float execute, float progressText,
            float progressPercentWidth, float percent, float nextPercentWidth, float distance, float eta)
        {
            if (layout == null)
                return;
            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, current));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, combo));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, execute));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, progressText));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, progressPercentWidth));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, percent));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, nextPercentWidth));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, distance));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, eta));
        }

        private static Label CreateLabel(string name, string text, FontStyle style)
        {
            return new Label
            {
                Name = name,
                Text = text,
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = Color.White,
                BackColor = Background,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9F, style),
                Margin = new Padding(4, 0, 4, 0)
            };
        }

        private static int GetCurrentSequence()
        {
            var state = MainV2.comPort == null || MainV2.comPort.MAV == null
                ? null
                : MainV2.comPort.MAV.cs;
            if (state == null || float.IsNaN(state.wpno) || float.IsInfinity(state.wpno))
                return -1;
            var value = (int) state.wpno;
            return value < 0 ? -1 : value;
        }

        private static double GetGroundSpeedMetersPerSecond()
        {
            var state = MainV2.comPort == null || MainV2.comPort.MAV == null
                ? null
                : MainV2.comPort.MAV.cs;
            if (state == null || CurrentState.multiplierspeed <= 0)
                return 0;
            return state.groundspeed / CurrentState.multiplierspeed;
        }

        private static string FormatDistance(double meters)
        {
            return meters >= 1000
                ? (meters / 1000.0).ToString("0.0") + " km"
                : Math.Round(meters).ToString("0") + " m";
        }

        private static string FormatEta(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > 359999)
                return "ETA --";
            var eta = TimeSpan.FromSeconds(seconds);
            return eta.TotalHours >= 1
                ? "ETA " + ((int) eta.TotalHours) + eta.ToString(@"\:mm\:ss")
                : "ETA " + eta.ToString(@"mm\:ss");
        }

        private static int ComputeMissionSignature(IEnumerable<FmtMissionItem> items)
        {
            unchecked
            {
                var hash = 17;
                foreach (var item in items)
                {
                    hash = hash * 31 + item.Sequence;
                    hash = hash * 31 + item.CommandId;
                    hash = hash * 31 + item.Param1.GetHashCode();
                    hash = hash * 31 + item.Param2.GetHashCode();
                    hash = hash * 31 + item.Param3.GetHashCode();
                    hash = hash * 31 + item.Param4.GetHashCode();
                    hash = hash * 31 + item.Latitude.GetHashCode();
                    hash = hash * 31 + item.Longitude.GetHashCode();
                    hash = hash * 31 + item.Altitude.GetHashCode();
                    hash = hash * 31 + item.Frame;
                }
                return hash;
            }
        }

        private static bool MissionListsEqual(IList<FmtMissionItem> left, IList<FmtMissionItem> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                if (left[index].Sequence != right[index].Sequence ||
                    left[index].CommandId != right[index].CommandId ||
                    left[index].Param1 != right[index].Param1 ||
                    left[index].Param2 != right[index].Param2 ||
                    left[index].Param3 != right[index].Param3 ||
                    left[index].Param4 != right[index].Param4 ||
                    left[index].Latitude != right[index].Latitude ||
                    left[index].Longitude != right[index].Longitude ||
                    left[index].Altitude != right[index].Altitude ||
                    left[index].Frame != right[index].Frame)
                    return false;
            }
            return true;
        }

        private static void ShowFailure(string message)
        {
            CustomMessageBox.Show(string.IsNullOrWhiteSpace(message) ? "任務切換失敗。" : message,
                "FMT AUTO 任務");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var port = MainV2.comPort;
                if (port != null)
                {
                    foreach (var subscription in missionPacketSubscriptions)
                        port.UnSubscribeToPacketType(subscription);
                }
                missionPacketSubscriptions.Clear();
                toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class MissionProgressBar : Control
        {
            private int value;

            internal int Value
            {
                get { return value; }
                set
                {
                    var next = Math.Max(0, Math.Min(100, value));
                    if (this.value == next)
                        return;
                    this.value = next;
                    Invalidate();
                }
            }

            internal MissionProgressBar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                BackColor = Color.FromArgb(47, 62, 70);
                Height = 14;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.Clear(BackColor);
                var fillWidth = (int) Math.Round(ClientSize.Width * value / 100.0);
                if (fillWidth > 0)
                {
                    using (var brush = new SolidBrush(SkyBlue))
                        e.Graphics.FillRectangle(brush, 0, 0, fillWidth, ClientSize.Height);
                }
                using (var border = new Pen(Color.FromArgb(86, 108, 119)))
                    e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, ClientSize.Width - 1),
                        Math.Max(0, ClientSize.Height - 1));
            }
        }

        private sealed class MissionConfirmationForm : Form
        {
            internal MissionConfirmationForm(FmtMissionItem current, FmtMissionItem target)
            {
                Text = "變更 AUTO 任務";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                ClientSize = new Size(540, 286);
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                AutoScaleMode = AutoScaleMode.Dpi;
                BackColor = Background;
                ForeColor = Color.White;
                Font = SystemFonts.MessageBoxFont;
                FmtBranding.ApplyApplicationIcon(this);

                var highRisk = target != null && target.IsHighRisk;
                var heading = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 58,
                    Padding = new Padding(20, 12, 20, 6),
                    Text = highRisk ? FmtMissionCommandFormatter.HighRiskWarning(target) : "變更 AUTO 任務",
                    ForeColor = highRisk ? Warning : SkyBlue,
                    BackColor = Background,
                    Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var currentText = current == null
                    ? "任務狀態未知"
                    : "WP" + current.Sequence + "｜" + FmtMissionCommandFormatter.Describe(current);
                var targetText = target == null
                    ? "任務狀態未知"
                    : "WP" + target.Sequence + "｜" + FmtMissionCommandFormatter.Describe(target);
                var details = new Label
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(24, 12, 24, 8),
                    Text = "目前：\r\n" + currentText + "\r\n\r\n切換至：\r\n" + targetText +
                           "\r\n\r\n是否確認執行？",
                    ForeColor = Color.White,
                    BackColor = FieldBackground,
                    Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10F),
                    TextAlign = ContentAlignment.TopLeft
                };

                var confirm = CreateDialogButton("確認執行", DialogResult.Yes,
                    highRisk ? Color.FromArgb(194, 79, 49) : SkyBlue);
                var cancel = CreateDialogButton("取消", DialogResult.Cancel, Color.FromArgb(65, 78, 86));
                var footer = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 62,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    Padding = new Padding(12, 11, 18, 9),
                    BackColor = Background
                };
                footer.Controls.Add(confirm);
                footer.Controls.Add(cancel);

                Controls.Add(details);
                Controls.Add(heading);
                Controls.Add(footer);
                AcceptButton = confirm;
                CancelButton = cancel;
            }

            private static Button CreateDialogButton(string text, DialogResult result, Color color)
            {
                return new Button
                {
                    Text = text,
                    DialogResult = result,
                    Size = new Size(132, 38),
                    Margin = new Padding(8, 0, 0, 0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = color,
                    ForeColor = Color.White,
                    UseVisualStyleBackColor = false
                };
            }
        }
    }
}
