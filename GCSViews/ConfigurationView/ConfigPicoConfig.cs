using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public sealed class ConfigPicoConfig : UserControl, IActivate
    {
        private const string PathSetting = "fmt_picoconfig_path";
        private static Process launchedProcess;
        private readonly TextBox executable = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill };

        public ConfigPicoConfig()
        {
            Dock = DockStyle.Fill;
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1,
                Padding = new Padding(12)
            };
            layout.Controls.Add(new Label
            {
                AutoSize = true, Dock = DockStyle.Fill,
                Text = "PicoConfig 數傳設定\r\n以原廠 PicoConfig 開啟獨立視窗。請先中斷導控連線並釋放 SIK 等設定頁使用的序列埠，僅於地面安全狀態設定。"
            });
            layout.Controls.Add(executable);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var browse = new Button { Text = "選擇 PicoConfig.exe…", AutoSize = true };
            var launch = new Button { Text = "開啟 PicoConfig", AutoSize = true };
            actions.Controls.Add(browse);
            actions.Controls.Add(launch);
            layout.Controls.Add(actions);
            layout.Controls.Add(status);
            Controls.Add(layout);
            browse.Click += (sender, args) => ChooseExecutable();
            launch.Click += (sender, args) => Launch();
            Activate();
        }

        public void Activate()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var candidates = new[]
            {
                Settings.Instance[PathSetting],
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "PicoConfig", "PicoConfig.exe"),
                Path.Combine(desktop, "P400", "PicoConfig_1_10", "PicoConfig.exe"),
                Path.Combine(desktop, "P400PicoConfig_1_10", "PicoConfig.exe")
            };
            executable.Text = candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)) ?? "";
            status.Text = executable.Text.Length == 0
                ? "未找到 PicoConfig，請選擇原廠程式；請保留同資料夾內的 DLL 與設定檔。"
                : "已找到 PicoConfig；按「開啟 PicoConfig」啟動，不會自動連線或寫入參數。";
        }

        private void ChooseExecutable()
        {
            using (var dialog = new OpenFileDialog { Title = "選擇 PicoConfig.exe", Filter = "PicoConfig|PicoConfig.exe", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                executable.Text = dialog.FileName;
                Settings.Instance[PathSetting] = dialog.FileName;
                status.Text = "已選擇程式，按「開啟 PicoConfig」啟動。";
            }
        }

        private void Launch()
        {
            try
            {
                if (launchedProcess != null && !launchedProcess.HasExited)
                {
                    status.Text = "PicoConfig 已開啟，請從工作列切換至其視窗。";
                    return;
                }
                if (MainV2.comPort?.BaseStream?.IsOpen == true ||
                    (MainV2.Comports != null && MainV2.Comports.ToArray().Any(p => p?.BaseStream?.IsOpen == true)))
                {
                    status.Text = "請先中斷導控連線再開啟 PicoConfig；不會自動中斷目前連線。";
                    return;
                }
                if (!File.Exists(executable.Text))
                {
                    status.Text = "程式不存在，請重新選擇 PicoConfig.exe。";
                    return;
                }
                launchedProcess?.Dispose();
                launchedProcess = Process.Start(new ProcessStartInfo(executable.Text)
                {
                    WorkingDirectory = Path.GetDirectoryName(executable.Text),
                    UseShellExecute = true
                });
                status.Text = "已啟動 PicoConfig 獨立視窗；完成設定後請在 PicoConfig 中斷線，再恢復導控連線。";
            }
            catch (Exception ex)
            {
                status.Text = "無法啟動 PicoConfig：" + ex.Message;
            }
        }
    }
}
