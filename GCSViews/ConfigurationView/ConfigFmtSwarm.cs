using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public sealed class ConfigFmtSwarm : MyUserControl, IActivate, IDeactivate
    {
        private readonly DataGridView vehicles = new DataGridView
        {
            ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            Dock = DockStyle.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(950, 0) };
        private readonly Button formation = new Button { Text = "相對位置編隊", AutoSize = true };
        private Form formationWindow;
        private readonly TabControl methods = new TabControl { Dock = DockStyle.Fill };
        private TabPage controllerPage;

        internal bool ControllerRunning =>
            (formationWindow as MissionPlanner.Swarm.FormationControl)?.IsRunning == true ||
            (formationWindow as MissionPlanner.Swarm.WaypointLeader.WPControl)?.IsRunning == true;

        public ConfigFmtSwarm()
        {
            AutoScroll = true;
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 5, Padding = new Padding(8)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label
            {
                Text = "群飛管理", Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14, FontStyle.Bold), AutoSize = true
            }, 0, 0);
            layout.Controls.Add(new Label
            {
                Text = "已連線機體總覽（快取快照）｜目標規模 5–10 台；支援查看多旋翼、固定翼、船／車。\r\n" +
                    "此頁不會自動連線、切換機體、解鎖或發送飛行指令。",
                AutoSize = true, MaximumSize = new Size(950, 0), Margin = new Padding(0, 8, 0, 8)
            }, 0, 1);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var refresh = new Button { Text = "重新整理機體清單", AutoSize = true };
            refresh.Click += (s, e) => RefreshVehicles();
            formation.Click += OpenFormation;
            actions.Controls.AddRange(new Control[] { refresh, formation });
            layout.Controls.Add(actions, 0, 2);
            foreach (var title in new[] { "連線", "SYSID", "COMPID", "機型", "飛行模式", "解鎖狀態", "鏈路" })
                vehicles.Columns.Add(title, title);
            layout.Controls.Add(vehicles, 0, 3);
            layout.Controls.Add(status, 0, 4);
            var overview = new TabPage("機體總覽／使用說明");
            overview.Controls.Add(layout);
            methods.TabPages.Add(overview);
            methods.TabPages.Add(new TabPage("相對位置編隊") { AutoScroll = true });
            methods.TabPages.Add(new TabPage("航點編隊／V 字隊形") { AutoScroll = true });
            methods.Selecting += (s, e) =>
            {
                if (ControllerRunning && e.TabPage != controllerPage)
                {
                    e.Cancel = true;
                    MessageBox.Show(this, "目前編隊仍在執行或停止中。請先停止發送，待控制迴圈結束後再切換方式。停止發送不等於降落。", "控制方式切換保護");
                }
            };
            methods.SelectedIndexChanged += (s, e) => LoadController();
            Controls.Add(methods);
        }

        public void Activate() { RefreshVehicles(); }
        public void Deactivate() { } // Never stop an external formation silently on page navigation.

        private void RefreshVehicles()
        {
            vehicles.Rows.Clear();
            int connected = 0;
            try
            {
                foreach (var link in MainV2.Comports.ToArray())
                {
                    bool open = link.BaseStream != null && link.BaseStream.IsOpen;
                    foreach (var mav in link.MAVlist.ToArray())
                    {
                        // Show autopilot components only; cameras/gimbals are not extra vehicles.
                        if (mav.compid != (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_AUTOPILOT1) continue;
                        if (open) connected++;
                        vehicles.Rows.Add(link.BaseStream?.PortName ?? "未知", mav.sysid, mav.compid,
                            mav.aptype.ToString(), mav.cs.mode, mav.cs.armed ? "已解鎖" : "未解鎖",
                            open ? "埠已開啟（非心跳驗證）" : "未連線／快取");
                    }
                }
                status.Text = "更新時間：" + DateTime.Now.ToString("HH:mm:ss") + "；開啟鏈路上的飛控：" + connected +
                    "（同一機體可能經不同鏈路重複列出）。\r\n" +
                    "相對位置編隊：領機＋拖曳偏移；航點編隊：地面主機＋空中領機、間距／前置量、V 字與高度交錯。\r\n" +
                    "同一頁一次只載入一套控制。請勿另外從 Ctrl+F 啟動第二套群飛控制。\r\n" +
                    "沿用原版控制範圍，並非勾選清單批次控制；尚未完成跨機型同步任務與 5–10 台 SITL／實機驗證。";
                formation.Enabled = connected > 0;
            }
            catch (Exception ex)
            {
                formation.Enabled = false;
                status.Text = "連線清單正在變更，請重新整理：" + ex.Message;
            }
        }

        private void OpenFormation(object sender, EventArgs e)
        {
            methods.SelectedIndex = 1;
        }

        private void LoadController()
        {
            if (ControllerRunning) return;
            if (formationWindow != null)
            {
                formationWindow.Close();
                formationWindow.Dispose();
                formationWindow = null;
            }
            controllerPage = null;
            if (methods.SelectedIndex == 0) { RefreshVehicles(); return; }
            try
            {
                formationWindow = methods.SelectedIndex == 1
                    ? (Form)new MissionPlanner.Swarm.FormationControl()
                    : new MissionPlanner.Swarm.WaypointLeader.WPControl();
                formationWindow.TopLevel = false;
                formationWindow.FormBorderStyle = FormBorderStyle.None;
                formationWindow.TopMost = false;
                formationWindow.MinimumSize = new Size(1000, 680);
                formationWindow.Dock = DockStyle.Fill;
                controllerPage = methods.SelectedTab;
                controllerPage.Controls.Add(formationWindow);
                MissionPlanner.Utilities.ThemeManager.ApplyThemeTo(formationWindow);
                formationWindow.Show();
            }
            catch (Exception ex) { MessageBox.Show(this, "無法載入編隊控制：" + ex.Message); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && formationWindow != null)
            {
                formationWindow.Close();
                formationWindow.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
