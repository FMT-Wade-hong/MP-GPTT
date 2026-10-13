using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner.FMT
{
    internal static class FmtMissionEstimate
    {
        internal sealed class Result
        {
            public double Metres, Seconds;
            public bool Uncertain;
            public string StopReason;
            public readonly List<string[]> Rows = new List<string[]>();
        }

        internal static Result Calculate(IList<Locationwp> items, PointLatLngAlt home,
            double speed, double climb, double descent, double transition, bool copter)
        {
            if (items == null || home == null ||
                new[] {home.Lat, home.Lng, home.Alt}.Any(v => double.IsNaN(v) || double.IsInfinity(v)) ||
                Math.Abs(home.Lat) > 90 || Math.Abs(home.Lng) > 180)
                throw new ArgumentException("任務或 HOME 座標無效，無法估算。");
            if (new[] {speed, climb, descent}.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v <= 0) ||
                double.IsNaN(transition) || double.IsInfinity(transition) || transition < 0)
                throw new ArgumentException("估算速度必須大於零，轉換時間不可為負值。");
            var result = new Result();
            var previous = home;
            var initialSpeed = speed;
            var jumpCounts = new Dictionary<int, int>();
            int steps = 0;
            Func<Locationwp, bool> changeSpeed = command => {
                if (new[] {command.p1, command.p2, command.p3, command.p4}.Any(v => float.IsNaN(v) || float.IsInfinity(v))) return false;
                if (command.p3 > 0 || (command.p1 != 0 && command.p1 != 1) || command.p4 != 0) return false;
                if (command.p2 > 0) speed = command.p2;
                else if (command.p2 == -2) speed = initialSpeed;
                else if (command.p2 != -1) return false;
                return true;
            };
            for (int index = 0; index < items.Count; index++)
            {
                // Bound expansion so an invalid/huge mission cannot freeze the planner UI.
                if (++steps > 20000)
                {
                    result.Uncertain = true;
                    result.StopReason = "展開航線超過 20,000 步；不提供總時間";
                    break;
                }
                var wp = items[index];
                var name = ((MAVLink.MAV_CMD)wp.id).ToString();
                double distance = 0, seconds = 0;
                string note = "";
                bool stop = false;
                int nextIndex = index + 1;
                if (new[] {wp.lat, wp.lng, wp.alt, wp.p1, wp.p2, wp.p3, wp.p4}.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                { note = "無效數值，停止估算"; stop = true; }
                else if (wp.id == 177) // Mission sequence includes HOME (0); items does not.
                {
                    if (items.Count(w => w.id == 177) > 1)
                    { stop = true; note = "多重跳轉可能受 MIS_OPTIONS／韌體影響，停止估算"; }
                    else if (wp.p2 == -1)
                    { stop = true; note = "無限重複航線：無有限總時間"; }
                    else if (wp.p1 < 1 || wp.p1 > items.Count || wp.p1 != Math.Truncate(wp.p1) ||
                             wp.p2 < 0 || wp.p2 > 32767 || wp.p2 != Math.Truncate(wp.p2))
                    { stop = true; note = "跳轉序號／重複次數無效（HOME 不可作為目標）"; }
                    else
                    {
                        int count;
                        jumpCounts.TryGetValue(index, out count);
                        if (count < (int)wp.p2)
                        {
                            jumpCounts[index] = count + 1;
                            nextIndex = (int)wp.p1 - 1;
                            note = string.Format("跳至 #{0}；第 {1}/{2} 次重複", wp.p1, count + 1, wp.p2);
                        }
                        else note = "重複次數完成，繼續下一項";
                    }
                }
                else if (wp.id == 178) // DO_CHANGE_SPEED
                {
                    if (!changeSpeed(wp)) { note = "變速含油門／相對值或其他類型，無法完整估算"; stop = true; }
                    else note = "估算速度 " + speed.ToString("0.##") + " m/s（無風、瞬時變速假設）";
                }
                else if (wp.id == 93) // NAV_DELAY, unlike CONDITION_DELAY this really pauses navigation
                {
                    if (wp.p1 >= 0) { seconds = wp.p1; note = "任務等待"; }
                    else { note = "等待指定時刻：時間不確定"; stop = true; }
                }
                else if (wp.id == 3000)
                {
                    if (copter || (wp.p1 != 3 && wp.p1 != 4))
                    { stop = true; note = "VTOL 轉換機型／目標狀態無效；只接受 3（多旋翼）或 4（固定翼）"; }
                    else { seconds = transition; note = "VTOL 轉換：採手動估算秒數"; }
                }
                else if (wp.id == 16 || wp.id == 17 || wp.id == 18 || wp.id == 19 || wp.id == 21 || wp.id == 22 || wp.id == 31)
                {
                    if (wp.frame != 0 && wp.frame != 3 && wp.frame != 5 && wp.frame != 6)
                    { note = "地形／不支援高度框架，停止估算"; stop = true; }
                    else if (Math.Abs(wp.lat) > 90 || Math.Abs(wp.lng) > 180)
                    { note = "座標超出範圍"; stop = true; }
                    else
                    {
                        // Intervening DO commands are processed after the previous NAV completes.
                        // Never look ahead and apply a later speed to an already flown incoming leg.
                        // Zero coordinates in mission navigation may mean current position.
                        var target = new PointLatLngAlt(wp.lat == 0 && wp.lng == 0 ? previous.Lat : wp.lat,
                            wp.lat == 0 && wp.lng == 0 ? previous.Lng : wp.lng,
                            wp.alt + ((wp.frame == 3 || wp.frame == 6) ? home.Alt : 0));
                        distance = previous.GetDistance(target);
                        double dz = target.Alt - previous.Alt;
                        seconds = Math.Max(distance / speed, Math.Abs(dz) / (dz >= 0 ? climb : descent));
                        if (wp.id == 31)
                        {
                            // Explicit planning approximation: approach at level, then change altitude.
                            // Heading-required exit cannot be inferred from altitude and speed alone.
                            if (copter || wp.p1 != 0 || wp.p4 != 0)
                            { stop = true; note = "盤旋至高度的機型／離圈條件尚無模型"; }
                            else
                            {
                                var verticalSeconds = Math.Abs(dz) / (dz >= 0 ? climb : descent);
                                seconds = distance / speed + verticalSeconds;
                                distance += verticalSeconds * speed;
                                note = "近似：水平進場後盤旋升降；不含入圈／離圈轉彎，非實測值";
                            }
                        }
                        previous = target;
                        if (wp.id == 16 && copter) seconds += Math.Max(0, wp.p1);
                        if (wp.id == 19) { seconds += Math.Max(0, wp.p1); if (!copter) distance += Math.Max(0, wp.p1) * speed; note = copter ? "定時懸停：不增加水平航程" : "定時盤旋；圈內航程以速度估算"; }
                        if ((wp.id == 19 || (wp.id == 16 && copter)) && wp.p1 < 0)
                        { stop = true; note = "等待秒數不可為負值，停止估算"; }
                        if (wp.id == 18)
                        {
                            if (wp.p1 <= 0 || wp.p3 == 0) { stop = true; note = "盤旋圈數／半徑未明確指定"; }
                            else { var orbit = 2 * Math.PI * Math.Abs(wp.p3) * wp.p1; distance += orbit; seconds += orbit / speed; note = "理想圓形盤旋估算"; }
                        }
                        if (wp.id == 17) { note = "無限盤旋：無有限總時間"; stop = true; }
                        if (wp.id == 21) { note = "估至降落點，不含拉平／滑跑；總時間不確定"; stop = true; }
                    }
                }
                else if (new ushort[] {183, 181, 203, 206, 2000, 201, 205, 209, 211}.Contains(wp.id))
                    note = "相機／輸出動作：不另加時間";
                else { stop = true; note = "此命令涉及路徑／條件或尚無時間模型，停止估算"; }
                result.Metres += distance; result.Seconds += seconds;
                result.Rows.Add(new[] {(index + 1).ToString(), name, distance.ToString("0.0"), seconds.ToString("0.0"), note});
                if (stop) { result.Uncertain = true; result.StopReason = "#" + (index + 1) + " " + name + "：" + note; break; }
                index = nextIndex - 1;
            }
            return result;
        }

        internal static string Summary(Result result, int count)
        {
            if (count == 0) return "沒有任務項目。";
            if (result.Uncertain) return "無法估算完整任務時間。" + result.StopReason +
                "\r\n表格僅供檢查已處理航段，不提供局部合計冒充總時間。";
            return string.Format("任務指令段估算：航程 {0:0.00} km，時間 {1:0.0} 分鐘（{2:0} 秒）\r\n不含任務結束後的自動返航／待命；不是起飛至落地總時間或續航保證。",
                result.Metres / 1000, result.Seconds / 60, result.Seconds);
        }
    }

    internal sealed class FmtMissionEstimateForm : Form
    {
        internal FmtMissionEstimateForm(IList<Locationwp> items, PointLatLngAlt home)
        {
            Text = "預估任務飛行時間（無風）"; Size = new Size(960, 620); MinimumSize = new Size(780, 500);
            StartPosition = FormStartPosition.CenterParent;
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 110, AutoScroll = true };
            var profile = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            profile.Items.AddRange(new object[] {"固定翼／VTOL", "多旋翼／直升機"}); profile.SelectedIndex = 0;
            top.Controls.Add(profile);
            Func<string, decimal, NumericUpDown> input = (label, value) => {
                top.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(8, 6, 0, 0) });
                var control = new NumericUpDown { Minimum = 0.1m, Maximum = 300, DecimalPlaces = 1, Value = value, Width = 65 };
                top.Controls.Add(control); return control;
            };
            var speed = input("巡航 m/s", 15); var climb = input("爬升 m/s", 2); var descent = input("下降 m/s", 2);
            var transition = input("每次 VTOL 轉換秒", 10); transition.Minimum = 0;
            var calculate = new Button { Text = "計算", Width = 90 }; top.Controls.Add(calculate);
            var note = new Label { Text = "數值為示例，請依機型修改；只估算目前任務快照，不寫入飛控。\r\n從 HOME 起算，包含單一有限跳轉的重複航段；不含風、加減速、轉彎。VTOL 起降／返航等未建模指令不顯示總時間。", AutoSize = true };
            top.SetFlowBreak(calculate, true); top.Controls.Add(note);
            var summary = new Label { Dock = DockStyle.Bottom, Height = 55, Text = "請確認估算速度與機型，再按計算。", Padding = new Padding(8) };
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, RowHeadersVisible = false };
            foreach (var header in new[] {"序號", "命令", "航程 m", "時間 s", "說明"}) grid.Columns.Add(header, header);
            Controls.Add(grid); Controls.Add(summary); Controls.Add(top);
            calculate.Click += (sender, args) => {
                try
                {
                var result = FmtMissionEstimate.Calculate(items, home, (double)speed.Value, (double)climb.Value,
                    (double)descent.Value, (double)transition.Value, profile.SelectedIndex == 1);
                grid.Rows.Clear(); foreach (var row in result.Rows) grid.Rows.Add(row);
                summary.Text = FmtMissionEstimate.Summary(result, items.Count);
                }
                catch (ArgumentException ex)
                {
                    grid.Rows.Clear();
                    summary.Text = ex.Message;
                }
            };
        }
    }
}
