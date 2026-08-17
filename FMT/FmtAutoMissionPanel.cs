using log4net;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionPlanner.FMT
{
    internal sealed class FmtAutoMissionPanel : UserControl
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Color Background = Color.FromArgb(12, 27, 36);
        private static readonly Color CardBackground = Color.FromArgb(7, 22, 32);
        private static readonly Color FieldBackground = Color.FromArgb(29, 48, 59);
        private static readonly Color SkyBlue = Color.FromArgb(45, 169, 220);
        private static readonly Color CardBorder = Color.FromArgb(27, 77, 101);
        private static readonly Color Separator = Color.FromArgb(38, 67, 82);
        private static readonly Color CaptionText = Color.FromArgb(181, 197, 207);
        private static readonly Color InactiveText = Color.FromArgb(175, 187, 194);
        private static readonly Color ActiveGreen = Color.FromArgb(46, 190, 92);
        private static readonly Color Warning = Color.FromArgb(242, 156, 45);
        private const float MissionValueFontSize = 9.5F;

        private readonly Panel layout;
        private readonly Control[] missionRowControls;
        private readonly int[] missionColumnWidths = new int[9];
        private readonly Label currentActionLabel;
        private readonly Panel missionComboHost;
        private readonly Label missionComboCaption;
        private readonly ComboBox missionCombo;
        private readonly Button executeButton;
        private readonly Label progressTextLabel;
        private readonly Label progressPercentLabel;
        private readonly Label nextActionLabel;
        private readonly Label distanceLabel;
        private readonly Label homeDistanceLabel;
        private readonly Label etaLabel;
        private readonly ToolTip toolTip;
        private readonly List<FmtMissionItem> missionItems = new List<FmtMissionItem>();
        private readonly List<int> missionPacketSubscriptions = new List<int>();
        private MAVLinkInterface missionSubscriptionPort;

        private int currentMissionSequence = -1;
        private int lastObservedMissionCount = -1;
        private int missionSignature;
        private int missionRefreshQueued;
        private int reportedMissionItemCount = -1;
        private bool busy;
        private int responsiveLayoutMode = -1;
        private int lastComboHighlightSequence = int.MinValue;
        private bool missionSelectionActive;
        private bool pendingVehicleRefresh;
        private bool rebuildingMissionCombo;
        private int operatorSelectedMissionSequence = -1;
        private int renderedMissionSequence = int.MinValue;
        private int renderedMissionTotal = -1;
        private int renderedMissionProgress = -1;
        private int missionVisualRefreshQueued;
        private int vehicleUpdateInvokeQueued;
        private int lastNormalVehicleUpdateTick = unchecked(Environment.TickCount - 500);
        private string displaySignature = string.Empty;
        private string executeStateSignature = string.Empty;

        internal FmtAutoMissionPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Name = "fmtAutoMissionPanel";
            Dock = DockStyle.Fill;
            BackColor = Background;
            ForeColor = Color.White;
            Margin = Padding.Empty;
            Padding = new Padding(5, 3, 5, 3);
            MinimumSize = new Size(0, 53);

            toolTip = new ToolTip();

            currentActionLabel = CreateLabel("fmtMissionCurrent", "目前執行\r\n○ 未啟用", FontStyle.Bold);
            currentActionLabel.ForeColor = InactiveText;
            currentActionLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily,
                MissionValueFontSize, FontStyle.Bold);
            ((MissionFieldLabel) currentActionLabel).ShowTargetIcon = true;

            missionCombo = new ComboBox
            {
                Name = "fmtMissionContents",
                Dock = DockStyle.None,
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                IntegralHeight = false,
                DropDownHeight = 330,
                BackColor = FieldBackground,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, MissionValueFontSize),
                Margin = Padding.Empty
            };
            missionCombo.DrawItem += MissionComboDrawItem;
            missionCombo.DropDown += MissionComboDropDown;
            missionCombo.DropDownClosed += MissionComboDropDownClosed;
            missionCombo.Enter += MissionComboEnter;
            missionCombo.Leave += MissionComboLeave;
            missionCombo.SelectionChangeCommitted += MissionComboSelectionChangeCommitted;
            missionCombo.SelectedIndexChanged += (sender, args) =>
            {
                if (!rebuildingMissionCombo)
                    UpdateExecuteState();
            };

            missionComboCaption = new Label
            {
                Name = "fmtMissionContentsCaption",
                Text = "任務航點",
                AutoSize = false,
                ForeColor = CaptionText,
                BackColor = CardBackground,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 7.5F, FontStyle.Bold),
                Margin = Padding.Empty
            };
            missionComboHost = new MissionStripPanel
            {
                Name = "fmtMissionContentsHost",
                BackColor = CardBackground,
                Margin = new Padding(4, 1, 6, 1)
            };
            missionComboHost.Controls.Add(missionComboCaption);
            missionComboHost.Controls.Add(missionCombo);
            missionComboHost.SizeChanged += (sender, args) => LayoutMissionComboHost();

            executeButton = new RoundedActionButton
            {
                Name = "fmtMissionExecute",
                Text = "跳轉航點",
                Dock = DockStyle.None,
                Margin = new Padding(0, 2, 7, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(10, 39, 52),
                ForeColor = Color.White,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, MissionValueFontSize, FontStyle.Bold),
                Image = CreateJumpWaypointIcon(),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Enabled = false
            };
            executeButton.FlatAppearance.BorderColor = Color.FromArgb(95, 205, 240);
            executeButton.Click += ExecuteButtonClick;

            progressTextLabel = CreateLabel("fmtMissionProgressText", "Mission Item\r\n-- / --", FontStyle.Bold);
            progressTextLabel.TextAlign = ContentAlignment.MiddleCenter;
            progressTextLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily,
                MissionValueFontSize, FontStyle.Bold);

            progressPercentLabel = CreateLabel("fmtMissionProgressPercent", "進度\r\n--%", FontStyle.Bold);
            progressPercentLabel.TextAlign = ContentAlignment.MiddleCenter;
            progressPercentLabel.ForeColor = SkyBlue;
            progressPercentLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily,
                MissionValueFontSize, FontStyle.Bold);

            nextActionLabel = CreateLabel("fmtMissionNext", "下一任務\r\n--", FontStyle.Regular);
            nextActionLabel.ForeColor = Color.Gainsboro;
            nextActionLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily,
                MissionValueFontSize, FontStyle.Bold);

            distanceLabel = CreateLabel("fmtMissionDistance", "距離\r\n--", FontStyle.Regular);
            distanceLabel.TextAlign = ContentAlignment.MiddleCenter;
            distanceLabel.ForeColor = Color.Gainsboro;
            distanceLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily,
                MissionValueFontSize, FontStyle.Bold);
            distanceLabel.Visible = false;

            homeDistanceLabel = CreateLabel("fmtMissionHomeDistance", "離家距離\r\n--", FontStyle.Regular);
            homeDistanceLabel.TextAlign = ContentAlignment.MiddleCenter;
            homeDistanceLabel.ForeColor = Color.Gainsboro;
            homeDistanceLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily,
                MissionValueFontSize, FontStyle.Bold);

            etaLabel = CreateLabel("fmtMissionEta", "ETA\r\n--:--:--", FontStyle.Regular);
            etaLabel.TextAlign = ContentAlignment.MiddleLeft;
            etaLabel.ForeColor = Color.Gainsboro;
            etaLabel.Font = new Font(SystemFonts.MessageBoxFont.FontFamily,
                MissionValueFontSize, FontStyle.Bold);

            layout = new MissionStripPanel
            {
                Name = "fmtMissionLayout",
                Dock = DockStyle.Fill,
                BackColor = CardBackground,
                Margin = Padding.Empty,
                Padding = new Padding(10, 2, 10, 2),
                DrawCardFrame = true
            };
            missionRowControls = new Control[]
            {
                currentActionLabel,
                missionComboHost,
                executeButton,
                progressTextLabel,
                progressPercentLabel,
                nextActionLabel,
                distanceLabel,
                homeDistanceLabel,
                etaLabel
            };
            layout.Controls.AddRange(missionRowControls);
            layout.SizeChanged += (sender, args) => LayoutMissionControls();

            Controls.Add(layout);
            SizeChanged += (sender, args) => ApplyResponsiveLayout();
            ApplyResponsiveLayout(true);
            RebuildMissionCombo(-1);
            SubscribeMissionPackets();
        }

        internal void UpdateFromVehicle(bool forceMissionRefresh = false)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                if (forceMissionRefresh)
                {
                    try
                    {
                        BeginInvoke((Action) (() => UpdateFromVehicle(true)));
                    }
                    catch (InvalidOperationException)
                    {
                        // A packet can arrive while the parent Flight Data page is
                        // closing. The next activation will rebuild the snapshot.
                    }
                }
                else if (Interlocked.Exchange(ref vehicleUpdateInvokeQueued, 1) == 0)
                {
                    try
                    {
                        BeginInvoke((Action) (() =>
                        {
                            Interlocked.Exchange(ref vehicleUpdateInvokeQueued, 0);
                            UpdateFromVehicle();
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Exchange(ref vehicleUpdateInvokeQueued, 0);
                    }
                }
                return;
            }

            // Flight Data runs its fast telemetry pass at 10 Hz. Mission progress,
            // distance and ETA are normal-cadence values; repainting them faster than
            // 2 Hz only adds layout/paint pressure and makes the native ComboBox less
            // stable while a mission is changing. Packet-driven refreshes bypass this.
            if (!forceMissionRefresh)
            {
                var now = Environment.TickCount;
                if (unchecked(now - lastNormalVehicleUpdateTick) < 500)
                    return;
                lastNormalVehicleUpdateTick = now;
            }

            // Keep an operator's open Mission Item list completely stable. Telemetry
            // (especially ETA and distance) is refreshed several times per second; a
            // TableLayoutPanel pass while the native ComboBox drop-down is open can
            // move/close the list or lose its highlighted selection. Defer that visual
            // refresh until the operator has finished selecting an item.
            if (missionSelectionActive || missionCombo.DroppedDown || missionCombo.Focused)
            {
                pendingVehicleRefresh = true;
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
            var missionVisualChanged = currentMissionSequence != renderedMissionSequence ||
                                       total != renderedMissionTotal ||
                                       progress != renderedMissionProgress;

            var currentDescription = currentItem == null
                ? "任務狀態未知"
                : FmtMissionCommandFormatter.Describe(currentItem);
            var inAuto = string.Equals(mode, "AUTO", StringComparison.OrdinalIgnoreCase);
            var inRtl = string.Equals(mode, "RTL", StringComparison.OrdinalIgnoreCase);
            var executionActive = connected && (inAuto || inRtl);
            string currentValue;
            string currentToolTip;
            if (!connected)
            {
                currentValue = "○ 尚未連線";
                currentToolTip = "尚未連線飛控。";
            }
            else if (inAuto)
            {
                currentValue = "● " + currentDescription;
                currentToolTip = "目前執行：" + currentDescription;
            }
            else if (inRtl)
            {
                currentValue = "● 返航 RTL";
                currentToolTip = "目前執行：返航 RTL";
            }
            else
            {
                currentValue = "○ 未啟用";
                currentToolTip = "目前執行僅在 AUTO 或 RTL 模式啟用。";
            }
            var currentText = "目前執行\r\n" + currentValue;
            var nextText = nextItem == null
                ? "下一任務\r\n--"
                : "下一任務\r\n" + FmtMissionCommandFormatter.Describe(nextItem);
            var nextToolTip = nextItem == null
                ? "下一任務：--"
                : "下一任務：" + FmtMissionCommandFormatter.Describe(nextItem);

            var distanceText = "距離\r\n--";
            var homeDistanceText = "離家距離\r\n--";
            var etaText = "ETA\r\n--:--:--";
            if (nextItem != null)
            {
                PointLatLngAlt nextLocation;
                var vehicleLocation = mav == null || mav.cs == null ? PointLatLngAlt.Zero : mav.cs.Location;
                if (nextItem.TryGetLocation(out nextLocation) && vehicleLocation != PointLatLngAlt.Zero)
                {
                    var distanceMeters = vehicleLocation.GetDistance(nextLocation);
                    if (!double.IsNaN(distanceMeters) && !double.IsInfinity(distanceMeters) && distanceMeters >= 0)
                    {
                        distanceText = "距離\r\n" + FormatDistance(distanceMeters);
                        var speed = GetGroundSpeedMetersPerSecond();
                        if (speed >= 0.5)
                            etaText = "ETA\r\n" + FormatEtaValue(distanceMeters / speed);
                    }
                }
            }

            var vehicleState = mav == null ? null : mav.cs;
            if (connected && vehicleState != null && vehicleState.TrackerLocation != PointLatLngAlt.Zero &&
                CurrentState.multiplierdist > 0)
            {
                var homeDistanceMeters = vehicleState.DistToHome / CurrentState.multiplierdist;
                if (!double.IsNaN(homeDistanceMeters) && !double.IsInfinity(homeDistanceMeters) &&
                    homeDistanceMeters >= 0)
                    homeDistanceText = "離家距離\r\n" + FormatDistance(homeDistanceMeters);
            }

            var signature = connected + "|" + mode + "|" + currentMissionSequence + "|" + total + "|" +
                            progress + "|" + currentText + "|" + nextText + "|" + distanceText + "|" +
                            homeDistanceText + "|" + etaText;
            if (!string.Equals(signature, displaySignature, StringComparison.Ordinal))
            {
                displaySignature = signature;
                // This strip uses fixed child bounds rather than a TableLayoutPanel.
                // Telemetry may change text and colour only; it cannot trigger a new
                // column measurement or move any neighbouring field.
                layout.SuspendLayout();
                SetLabelText(currentActionLabel, currentText);
                currentActionLabel.ForeColor = executionActive ? ActiveGreen : InactiveText;
                toolTip.SetToolTip(currentActionLabel, currentToolTip);
                SetLabelText(progressTextLabel, total == 0
                    ? "Mission Item\r\n-- / --"
                    : currentItem == null
                        ? "Mission Item\r\n-- / " + total
                        : "Mission Item\r\n" + currentListPosition + " / " + total);
                SetLabelText(progressPercentLabel,
                    "進度\r\n" + (total == 0 || currentItem == null ? "--%" : progress + "%"));
                SetLabelText(nextActionLabel, nextText);
                toolTip.SetToolTip(nextActionLabel, nextToolTip);
                SetLabelText(distanceLabel, distanceText);
                SetLabelText(homeDistanceLabel, homeDistanceText);
                SetLabelText(etaLabel, etaText);
                layout.ResumeLayout(false);
                InvalidateMissionRow(false);
                if (lastComboHighlightSequence != currentMissionSequence)
                {
                    lastComboHighlightSequence = currentMissionSequence;
                    missionCombo.Invalidate();
                }

                if (missionVisualChanged)
                {
                    renderedMissionSequence = currentMissionSequence;
                    renderedMissionTotal = total;
                    renderedMissionProgress = progress;
                    QueueMissionVisualRefresh();
                }
            }

            UpdateExecuteState();
        }

        private void SubscribeMissionPackets()
        {
            var port = MainV2.comPort;
            if (port == null)
                return;

            missionSubscriptionPort = port;

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
            rebuildingMissionCombo = true;
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
                    var preferred = missionItems.FirstOrDefault(
                                        item => item.Sequence == operatorSelectedMissionSequence) ??
                                    missionItems.FirstOrDefault(item => item.Sequence == preferredSequence) ??
                                    missionItems.FirstOrDefault(item => item.Sequence == GetCurrentSequence()) ??
                                    missionItems[0];
                    missionCombo.SelectedItem = preferred;
                }
            }
            finally
            {
                missionCombo.EndUpdate();
                rebuildingMissionCombo = false;
                UpdateExecuteState();
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
                var changed = await SetCurrentMissionItemSafelyAsync(target.Sequence);
                if (!changed)
                    ShowFailure("飛控未在時限內確認航點跳轉，原任務保持不變。\r\n" +
                                "請確認飛控已載入任務且 MAVLink 連線正常後再試一次。");
            }
            catch (Exception ex)
            {
                Log.Error("FMT AUTO mission item change failed", ex);
                ShowFailure("任務切換失敗。\r\n" + ex.Message);
            }
            finally
            {
                SetBusy(false);
                try
                {
                    UpdateFromVehicle(true);
                }
                catch (Exception ex)
                {
                    Log.Warn("FMT AUTO mission panel refresh failed after Mission Item change", ex);
                }
            }
        }

        private static async Task<bool> SetCurrentMissionItemSafelyAsync(int sequence)
        {
            if (sequence <= 0 || sequence > ushort.MaxValue)
                return false;

            var port = MainV2.comPort;
            var mav = port == null ? null : port.MAV;
            if (port == null || port.BaseStream == null || !port.BaseStream.IsOpen || mav == null ||
                mav.sysid == 0)
                return false;

            var request = new MAVLink.mavlink_mission_set_current_t
            {
                target_system = mav.sysid,
                target_component = mav.compid,
                seq = (ushort) sequence
            };

            // Do not call setWPCurrentAsync here. That legacy method temporarily takes
            // ownership of the shared serial receive loop and can time out or leave the
            // port locked when Flight Data is already consuming packets. Send the
            // standard request through the normal writer and let the existing telemetry
            // reader publish MISSION_CURRENT into CurrentState.wpno.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (port.BaseStream == null || !port.BaseStream.IsOpen)
                    return false;

                port.generatePacket(MAVLink.MAVLINK_MSG_ID.MISSION_SET_CURRENT, request,
                    mav.sysid, mav.compid);

                for (var sample = 0; sample < 15; sample++)
                {
                    await Task.Delay(100);
                    if (GetCurrentSequence() == sequence)
                        return true;
                    if (port.BaseStream == null || !port.BaseStream.IsOpen)
                        return false;
                }
            }

            return GetCurrentSequence() == sequence;
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
                ? "選擇任務項目後，按下「跳轉航點」並完成一次確認。"
                : GetDisabledReason(connected, target);
            var signature = busy + "|" + connected + "|" + missionItems.Count + "|" +
                            (target == null ? -1 : target.Sequence) + "|" + GetCurrentSequence() + "|" +
                            enabled + "|" + reason;
            if (string.Equals(signature, executeStateSignature, StringComparison.Ordinal))
                return;

            executeStateSignature = signature;
            executeButton.Enabled = enabled;
            executeButton.BackColor = enabled
                ? Color.FromArgb(10, 39, 52)
                : Color.FromArgb(42, 55, 62);
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
            executeButton.Text = value ? "跳轉中…" : "跳轉航點";
            UpdateExecuteState();
        }

        private void MissionComboDropDown(object sender, EventArgs e)
        {
            missionSelectionActive = true;
            RefreshMissionSnapshot();
            currentMissionSequence = GetCurrentSequence();
            missionCombo.DropDownWidth = Math.Max(missionCombo.Width, 420);
            missionCombo.Invalidate();
            UpdateExecuteState();
        }

        private void MissionComboDropDownClosed(object sender, EventArgs e)
        {
            UpdateExecuteState();

            // A WinForms ComboBox remains focused after its native list closes. Keep
            // telemetry frozen so the operator's committed choice cannot be disturbed.
            missionSelectionActive = missionCombo.Focused;
            if (!missionSelectionActive)
                FlushDeferredVehicleRefresh();
        }

        private void MissionComboEnter(object sender, EventArgs e)
        {
            missionSelectionActive = true;
        }

        private void MissionComboLeave(object sender, EventArgs e)
        {
            missionSelectionActive = false;
            FlushDeferredVehicleRefresh();
        }

        private void MissionComboSelectionChangeCommitted(object sender, EventArgs e)
        {
            var selected = missionCombo.SelectedItem as FmtMissionItem;
            operatorSelectedMissionSequence = selected == null ? -1 : selected.Sequence;
            UpdateExecuteState();
        }

        private void FlushDeferredVehicleRefresh()
        {
            if (!pendingVehicleRefresh || IsDisposed || Disposing)
                return;

            pendingVehicleRefresh = false;
            BeginInvoke((Action) (() => UpdateFromVehicle()));
        }

        private void QueueMissionVisualRefresh()
        {
            if (!IsHandleCreated || IsDisposed || Disposing)
                return;
            if (Interlocked.Exchange(ref missionVisualRefreshQueued, 1) != 0)
                return;

            try
            {
                BeginInvoke((Action) (() =>
                {
                    Interlocked.Exchange(ref missionVisualRefreshQueued, 0);
                    if (IsDisposed || Disposing)
                        return;
                    if (missionSelectionActive || missionCombo.DroppedDown || missionCombo.Focused)
                    {
                        renderedMissionSequence = int.MinValue;
                        pendingVehicleRefresh = true;
                        return;
                    }

                    // Run after the current telemetry/binding pass. Mission updates must
                    // repaint values only: recalculating bounds here made the complete row
                    // jump after MISSION_CURRENT changed.
                    InvalidateMissionRow(true);
                }));
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref missionVisualRefreshQueued, 0);
            }
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

        private void ApplyResponsiveLayout(bool force = false)
        {
            if (layout == null)
                return;

            var mode = ClientSize.Width >= 1500 ? 2 : ClientSize.Width >= 1050 ? 1 : 0;
            if (!force && responsiveLayoutMode == mode)
                return;

            responsiveLayoutMode = mode;
            if (ClientSize.Width >= 1500)
            {
                ConfigureColumns(205, 220, 108, 105, 72, 185, 95, 110, 110);
                etaLabel.Visible = true;
                distanceLabel.Visible = true;
                homeDistanceLabel.Visible = true;
            }
            else if (ClientSize.Width >= 1050)
            {
                ConfigureColumns(160, 175, 96, 92, 62, 150, 90, 95, 100);
                etaLabel.Visible = true;
                distanceLabel.Visible = true;
                homeDistanceLabel.Visible = true;
            }
            else
            {
                ConfigureColumns(135, 155, 88, 82, 54, 130, 0, 0, 90);
                distanceLabel.Visible = false;
                homeDistanceLabel.Visible = false;
                etaLabel.Visible = true;
            }
            LayoutMissionControls();
            InvalidateMissionRow(false);
        }

        private void ConfigureColumns(int current, int combo, int execute, int progressText,
            int percent, int nextWidth, int distance,
            int homeDistance, int eta)
        {
            var widths = new[]
            {
                current, combo, execute, progressText, percent,
                nextWidth, distance, homeDistance, eta
            };
            Array.Copy(widths, missionColumnWidths, missionColumnWidths.Length);
        }

        private void LayoutMissionControls()
        {
            if (layout == null || missionRowControls == null)
                return;

            layout.SuspendLayout();
            try
            {
                var widths = (int[]) missionColumnWidths.Clone();
                // Keep every field at a fixed pixel position. Spare horizontal room stays
                // at the right edge instead of being redistributed across the mission row;
                // therefore MISSION_CURRENT, progress, distance and ETA updates cannot
                // move any neighbouring control.

                var x = layout.Padding.Left;
                var rowTop = layout.Padding.Top;
                var rowHeight = Math.Max(0, layout.ClientSize.Height - layout.Padding.Vertical);
                var separators = new List<int>();
                for (var index = 0; index < missionRowControls.Length; index++)
                {
                    var control = missionRowControls[index];
                    var cellWidth = Math.Max(0, widths[index]);
                    var margin = control.Margin;
                    var bounds = new Rectangle(
                        x + margin.Left,
                        rowTop + margin.Top,
                        Math.Max(0, cellWidth - margin.Horizontal),
                        Math.Max(0, rowHeight - margin.Vertical));
                    if (control.Bounds != bounds)
                        control.Bounds = bounds;
                    x += cellWidth;

                    if (cellWidth > 0 && control.Visible &&
                        (index == 0 || index == 2 || index == 3 || index == 4 ||
                         index == 5 || index == 6 || index == 7))
                        separators.Add(x);
                }
                ((MissionStripPanel) layout).SetSeparators(separators);
            }
            finally
            {
                layout.ResumeLayout(false);
            }
        }

        private void LayoutMissionComboHost()
        {
            if (missionComboHost == null || missionComboCaption == null || missionCombo == null)
                return;

            var width = missionComboHost.ClientSize.Width;
            var height = missionComboHost.ClientSize.Height;
            missionComboCaption.SetBounds(0, 0, Math.Max(0, width), 14);
            missionCombo.SetBounds(0, 16, Math.Max(0, width), Math.Max(23, height - 16));
        }

        private void InvalidateMissionRow(bool paintImmediately)
        {
            if (layout == null || layout.IsDisposed)
                return;

            // Changed labels invalidate themselves. Repaint only
            // the card/separator surface here; invalidating and synchronously updating
            // every child produced avoidable CPU spikes and visible white flashes.
            layout.Invalidate();
        }

        private static Label CreateLabel(string name, string text, FontStyle style)
        {
            return new MissionFieldLabel
            {
                Name = name,
                Text = text,
                Dock = DockStyle.None,
                // Captions and values are painted independently. Long mission descriptions
                // wrap below their caption instead of being replaced by an ellipsis.
                AutoEllipsis = false,
                ForeColor = Color.White,
                BackColor = CardBackground,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, MissionValueFontSize, style),
                Margin = new Padding(4, 0, 4, 0)
            };
        }

        private static void SetLabelText(Label label, string text)
        {
            if (!string.Equals(label.Text, text, StringComparison.Ordinal))
            {
                label.Text = text;
                label.Invalidate();
            }
        }

        private static Image CreateJumpWaypointIcon()
        {
            var image = new Bitmap(25, 25);
            using (var graphics = Graphics.FromImage(image))
            using (var routePen = new Pen(SkyBlue, 2.2F))
            using (var nodeBrush = new SolidBrush(Background))
            using (var nodePen = new Pen(SkyBlue, 2F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                routePen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                routePen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                var points = new[]
                {
                    new PointF(5F, 20F), new PointF(10F, 14F),
                    new PointF(7F, 8F), new PointF(15F, 5F)
                };
                graphics.DrawLines(routePen, points);
                foreach (var point in points)
                {
                    graphics.FillEllipse(nodeBrush, point.X - 2.3F, point.Y - 2.3F, 4.6F, 4.6F);
                    graphics.DrawEllipse(nodePen, point.X - 2.3F, point.Y - 2.3F, 4.6F, 4.6F);
                }

                // Direction arrow identifies this as a jump-to-waypoint action.
                graphics.DrawLine(routePen, 14F, 13F, 21F, 18F);
                graphics.DrawLine(routePen, 21F, 18F, 16F, 19F);
                graphics.DrawLine(routePen, 21F, 18F, 20F, 13F);
            }
            return image;
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

        private static string FormatEtaValue(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > 359999)
                return "--:--:--";
            var eta = TimeSpan.FromSeconds(seconds);
            return ((int) eta.TotalHours).ToString("00") + eta.ToString(@"\:mm\:ss");
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
                    !left[index].Param1.Equals(right[index].Param1) ||
                    !left[index].Param2.Equals(right[index].Param2) ||
                    !left[index].Param3.Equals(right[index].Param3) ||
                    !left[index].Param4.Equals(right[index].Param4) ||
                    !left[index].Latitude.Equals(right[index].Latitude) ||
                    !left[index].Longitude.Equals(right[index].Longitude) ||
                    !left[index].Altitude.Equals(right[index].Altitude) ||
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
                var port = missionSubscriptionPort;
                if (port != null)
                {
                    foreach (var subscription in missionPacketSubscriptions)
                        port.UnSubscribeToPacketType(subscription);
                }
                missionPacketSubscriptions.Clear();
                missionSubscriptionPort = null;
                if (executeButton != null && executeButton.Image != null)
                {
                    var image = executeButton.Image;
                    executeButton.Image = null;
                    image.Dispose();
                }
                toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class MissionStripPanel : Panel
        {
            private int[] separators = new int[0];

            internal bool DrawCardFrame { get; set; }

            internal MissionStripPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (!DrawCardFrame)
                {
                    e.Graphics.Clear(BackColor);
                    return;
                }

                e.Graphics.Clear(Background);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var bounds = new Rectangle(1, 1, Math.Max(0, ClientSize.Width - 3),
                    Math.Max(0, ClientSize.Height - 3));
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    return;

                using (var path = CreateRoundedPath(bounds, 10))
                using (var brush = new SolidBrush(BackColor))
                using (var pen = new Pen(CardBorder, 1.2F))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            }

            internal void SetSeparators(IEnumerable<int> positions)
            {
                var next = positions == null ? new int[0] : positions.Distinct().ToArray();
                if (separators.SequenceEqual(next))
                    return;
                separators = next;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (!DrawCardFrame || separators.Length == 0)
                    return;

                using (var pen = new Pen(Separator, 1F))
                {
                    foreach (var x in separators)
                    {
                        if (x <= Padding.Left || x >= ClientSize.Width - Padding.Right)
                            continue;
                        e.Graphics.DrawLine(pen, x, 13, x, Math.Max(13, ClientSize.Height - 13));
                    }
                }
            }
        }

        private sealed class MissionFieldLabel : Label
        {
            private Font captionFont;

            internal bool ShowTargetIcon { get; set; }

            internal MissionFieldLabel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            }

            protected override void OnFontChanged(EventArgs e)
            {
                base.OnFontChanged(e);
                if (captionFont != null)
                    captionFont.Dispose();
                captionFont = new Font(Font.FontFamily, Math.Max(7.5F, Font.Size - 2F),
                    FontStyle.Regular);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (var background = new SolidBrush(BackColor))
                    e.Graphics.FillRectangle(background, ClientRectangle);

                var bounds = new Rectangle(
                    Padding.Left,
                    Padding.Top,
                    Math.Max(0, ClientSize.Width - Padding.Horizontal),
                    Math.Max(0, ClientSize.Height - Padding.Vertical));
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    return;

                var rawText = Text ?? string.Empty;
                var separator = rawText.IndexOf('\n');
                var caption = separator < 0
                    ? string.Empty
                    : rawText.Substring(0, separator).TrimEnd('\r');
                var value = separator < 0
                    ? rawText
                    : rawText.Substring(separator + 1).TrimStart('\r', '\n');

                var iconInset = ShowTargetIcon ? 34 : 0;
                if (ShowTargetIcon)
                    DrawTargetIcon(e.Graphics, new Point(bounds.Left + 14,
                        bounds.Top + bounds.Height / 2));

                var captionHeight = string.IsNullOrEmpty(caption) ? 0 : 15;
                if (captionHeight > 0)
                {
                    var captionBounds = new Rectangle(bounds.Left + iconInset, bounds.Top,
                        Math.Max(0, bounds.Width - iconInset),
                        Math.Min(captionHeight, bounds.Height));
                    TextRenderer.DrawText(e.Graphics, caption, captionFont ?? Font, captionBounds,
                        CaptionText, BackColor,
                        TextFormatFlags.SingleLine | TextFormatFlags.Top |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                        TextFormatFlags.PreserveGraphicsClipping);
                }

                var valueBounds = new Rectangle(bounds.Left + iconInset,
                    bounds.Top + captionHeight, Math.Max(0, bounds.Width - iconInset),
                    Math.Max(0, bounds.Height - captionHeight));
                if (valueBounds.Width <= 0 || valueBounds.Height <= 0)
                    return;

                var flags = TextFormatFlags.WordBreak | TextFormatFlags.Top |
                            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                            TextFormatFlags.PreserveGraphicsClipping;
                switch (TextAlign)
                {
                    case ContentAlignment.TopCenter:
                    case ContentAlignment.MiddleCenter:
                    case ContentAlignment.BottomCenter:
                        flags |= TextFormatFlags.HorizontalCenter;
                        break;
                    case ContentAlignment.TopRight:
                    case ContentAlignment.MiddleRight:
                    case ContentAlignment.BottomRight:
                        flags |= TextFormatFlags.Right;
                        break;
                    default:
                        flags |= TextFormatFlags.Left;
                        break;
                }

                TextRenderer.DrawText(e.Graphics, value, Font, valueBounds,
                    ForeColor, BackColor, flags);
            }

            private static void DrawTargetIcon(Graphics graphics, Point center)
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(SkyBlue, 1.8F))
                {
                    graphics.DrawEllipse(pen, center.X - 10, center.Y - 10, 20, 20);
                    graphics.DrawEllipse(pen, center.X - 4, center.Y - 4, 8, 8);
                    graphics.DrawLine(pen, center.X, center.Y - 14, center.X, center.Y - 7);
                    graphics.DrawLine(pen, center.X, center.Y + 7, center.X, center.Y + 14);
                    graphics.DrawLine(pen, center.X - 14, center.Y, center.X - 7, center.Y);
                    graphics.DrawLine(pen, center.X + 7, center.Y, center.X + 14, center.Y);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && captionFont != null)
                {
                    captionFont.Dispose();
                    captionFont = null;
                }
                base.Dispose(disposing);
            }
        }

        private sealed class RoundedActionButton : Button
        {
            internal RoundedActionButton()
            {
                SetStyle(ControlStyles.ResizeRedraw, true);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                    return;

                var oldRegion = Region;
                using (var path = CreateRoundedPath(new Rectangle(0, 0, ClientSize.Width,
                           ClientSize.Height), 6))
                    Region = new Region(path);
                if (oldRegion != null)
                    oldRegion.Dispose();
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return path;

            var diameter = Math.Max(1, Math.Min(radius * 2,
                Math.Min(bounds.Width, bounds.Height)));
            var arc = new Rectangle(bounds.Left, bounds.Top, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
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
