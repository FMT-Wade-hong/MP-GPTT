using DeviceProgramming.Dfu;
using Microsoft.Scripting.Utils;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace MissionPlanner.Controls
{
    public partial class SerialOutputPass : Form
    {
        static TcpListener listener;
        // Thread signal.
        public static ManualResetEvent tcpClientConnected = new ManualResetEvent(false);
        private DataGridViewTextBoxColumn runtimeStatusColumn;

        private sealed class ForwardRuntime
        {
            internal bool Started;
            internal MAVLinkInterface.Mirror Mirror;
            internal TcpListener Listener;
        }

        public SerialOutputPass()
        {
            InitializeComponent();
            ConfigureRuntimeStatusColumn();

            chk_write.Checked = MainV2.comPort.MirrorStreamWrite;

            CMB_serialport.Items.AddRange(SerialPort.GetPortNames());
            CMB_serialport.Items.Add("TCP Host - 14550");
            CMB_serialport.Items.Add("TCP Client");
            CMB_serialport.Items.Add("UDP Host - 14550");
            CMB_serialport.Items.Add("UDP Client");

            if (MainV2.comPort.MirrorStream != null && MainV2.comPort.MirrorStream.IsOpen || listener != null)
            {
                BUT_connect.Text = Strings.Stop;
            }

            MissionPlanner.Utilities.Tracking.AddPage(this.GetType().ToString(), this.Text);

            try
            {
                Load();
            }
            catch (Exception ex) {
                CustomMessageBox.Show("Failed to load list: " + ex.Message);
            }
        }

        private void ConfigureRuntimeStatusColumn()
        {
            runtimeStatusColumn = new DataGridViewTextBoxColumn
            {
                Name = "RuntimeStatus",
                HeaderText = "狀態",
                ReadOnly = true,
                Width = 52,
                MinimumWidth = 52,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    ForeColor = Color.Gray,
                    SelectionForeColor = Color.Gray
                }
            };
            myDataGridView1.Columns.Insert(0, runtimeStatusColumn);
            myDataGridView1.RowsAdded += (sender, args) =>
            {
                for (var index = args.RowIndex; index < args.RowIndex + args.RowCount &&
                                                   index < myDataGridView1.Rows.Count; index++)
                    SetRuntimeStatus(myDataGridView1.Rows[index], false, null);
            };
            myDataGridView1.CellBeginEdit += (sender, args) =>
            {
                if (args.RowIndex >= 0 && IsStarted(myDataGridView1.Rows[args.RowIndex]))
                    args.Cancel = true;
            };
            myDataGridView1.UserDeletingRow += (sender, args) => StopForwardingRow(args.Row, false);
        }

        private static bool IsStarted(DataGridViewRow row)
        {
            var runtime = row == null ? null : row.Tag as ForwardRuntime;
            return runtime != null && runtime.Started;
        }

        private void SetRuntimeStatus(DataGridViewRow row, bool started, string error)
        {
            if (row == null || row.IsNewRow || runtimeStatusColumn == null)
                return;

            var color = !string.IsNullOrEmpty(error)
                ? Color.FromArgb(218, 55, 55)
                : started
                    ? Color.FromArgb(43, 190, 99)
                    : Color.Gray;
            var cell = row.Cells[runtimeStatusColumn.Name];
            cell.Value = "●";
            cell.Style.ForeColor = color;
            cell.Style.SelectionForeColor = color;
            cell.ToolTipText = !string.IsNullOrEmpty(error)
                ? error
                : started ? "MAVLink 轉發已啟用" : "MAVLink 轉發未啟用";
            row.Cells[Go.Name].Value = started ? "停止" : "啟動";
        }

        private void BUT_connect_Click(object sender, EventArgs e)
        {
            if (MainV2.comPort.MirrorStream != null && MainV2.comPort.MirrorStream.IsOpen || listener != null)
            {
                MainV2.comPort.MirrorStream.Close();
                BUT_connect.Text = Strings.Connect;
            }
            else
            {
                try
                {
                    switch (CMB_serialport.Text)
                    {
                        case "TCP Host - 14550":
                        case "TCP Host":
                            {
                                MainV2.comPort.MirrorStream = new TcpSerial();
                                CMB_baudrate.SelectedIndex = 0;
                                int port = 14550;
                                if (InputBox.Show("Port", "Enter port", ref port) != DialogResult.OK)
                                    return;
                                listener = new TcpListener(System.Net.IPAddress.Any, port);
                                listener.Start(0);
                                listener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClientCallback), listener);
                                BUT_connect.Text = Strings.Stop;
                                return;
                            }

                        case "TCP Client":

                            MainV2.comPort.MirrorStream = new TcpSerial() { retrys = 999999, autoReconnect = true, ConfigRef = "SerialOutputPassTCP" };
                            CMB_baudrate.SelectedIndex = 0;
                            break;
                        case "UDP Host - 14550":
                            {
                                int port = 14550;
                                if (InputBox.Show("Port", "Enter port", ref port) != DialogResult.OK)
                                    return;
                                MainV2.comPort.MirrorStream = new UdpSerial()
                                { ConfigRef = "SerialOutputPassUDP", Port = port.ToString() };
                                CMB_baudrate.SelectedIndex = 0;
                                break;
                            }

                        case "UDP Client":
                            MainV2.comPort.MirrorStream = new UdpSerialConnect() { ConfigRef = "SerialOutputPassUDPCL" };
                            CMB_baudrate.SelectedIndex = 0;
                            break;
                        default:
                            MainV2.comPort.MirrorStream = new SerialPort();
                            MainV2.comPort.MirrorStream.PortName = CMB_serialport.Text;
                            break;
                    }
                }
                catch
                {
                    CustomMessageBox.Show(Strings.InvalidPortName);
                    return;
                }

                try
                {
                    MainV2.comPort.MirrorStream.BaudRate = int.Parse(CMB_baudrate.Text);
                }
                catch
                {
                    CustomMessageBox.Show(Strings.InvalidBaudRate);
                    return;
                }
                try
                {
                    MainV2.comPort.MirrorStream.Open();
                }
                catch
                {
                    CustomMessageBox.Show("Error Connecting\nif using com0com please rename the ports to COM??");
                    return;
                }
            }
        }

        void DoAcceptTcpClientCallback(IAsyncResult ar)
        {
            try
            {
                var legacyListener = ar.AsyncState as TcpListener;
                if (legacyListener != null)
                {
                    TcpClient legacyClient = legacyListener.EndAcceptTcpClient(ar);
                    var legacyStream = MainV2.comPort.MirrorStream as TcpSerial;
                    if (legacyStream != null)
                        legacyStream.client = legacyClient;
                    legacyListener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClientCallback), legacyListener);
                    return;
                }

                var state = (ValueTuple<TcpListener, MAVLinkInterface.Mirror>)ar.AsyncState;
                TcpListener tcpListener = state.Item1;
                MAVLinkInterface.Mirror mirror = state.Item2;
                TcpClient client = tcpListener.EndAcceptTcpClient(ar);

                ((TcpSerial)mirror.MirrorStream).client = client;
                tcpListener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClientCallback), state);
            }
            catch (ObjectDisposedException)
            {
                // 使用者停止轉發時，待處理的非同步 Accept 會正常結束。
            }
            catch (SocketException)
            {
                // 監聽器關閉或網路介面變更時停止接受連線，避免背景執行緒崩潰。
            }
            catch (InvalidCastException) { }
        }

        private void chk_write_CheckedChanged(object sender, EventArgs e)
        {
            MainV2.comPort.MirrorStreamWrite = chk_write.Checked;
        }


        private void myDataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            Save();
        }

        private void Save()
        {
            List<string> ans = new List<string>();
            foreach (DataGridViewRow row in myDataGridView1.Rows)
            {
                if (row.IsNewRow)
                    continue;

                // 執行狀態屬於本次程式執行階段，不可寫入設定，避免重開後誤顯示已啟用。
                var line = new object[]
                {
                    row.Cells[Type.Name].Value,
                    row.Cells[Direction.Name].Value,
                    row.Cells[Port.Name].Value,
                    row.Cells[Extra.Name].Value,
                    row.Cells[Write.Name].Value,
                    string.Empty
                }.ToJSON(Formatting.None);
                ans.Add(line);
            }

            Settings.Instance.SetList(configlist, ans);
        }

        private void Load()
        {
            var ans = Settings.Instance.GetList(configlist);

            foreach (string row in ans)
            {
                if (row == null || row == "")
                    continue;
                var data = ((JArray)JsonConvert.DeserializeObject(row)).Select(a => ((JValue)a).Value).ToArray();
                var index = myDataGridView1.Rows.Add();
                var gridRow = myDataGridView1.Rows[index];
                if (data.Length > 0) gridRow.Cells[Type.Name].Value = data[0];
                if (data.Length > 1) gridRow.Cells[Direction.Name].Value = data[1];
                if (data.Length > 2) gridRow.Cells[Port.Name].Value = data[2];
                if (data.Length > 3) gridRow.Cells[Extra.Name].Value = data[3];
                if (data.Length > 4) gridRow.Cells[Write.Name].Value = data[4];
                SetRuntimeStatus(gridRow, false, null);
            }
        }

        string configlist = "serialpasslist";

        private void myDataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            
        }

        private void myDataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != Go.Index)
                return;

            var row = myDataGridView1.Rows[e.RowIndex];
            if (row.IsNewRow)
                return;

            if (IsStarted(row))
            {
                StopForwardingRow(row, true);
                return;
            }

            string validationError;
            if (!TryValidateForwardingRow(row, out validationError))
            {
                SetRuntimeStatus(row, false, validationError);
                CustomMessageBox.Show(validationError, "MAVLink 轉發設定");
                return;
            }

            MAVLinkInterface.Mirror mirror = null;
            TcpListener rowListener = null;
            try
            {
                mirror = new MAVLinkInterface.Mirror();

                var protocol = CellText(row, Type);
                var direction = CellText(row, Direction);
                var port = CellText(row, Port);
                var extra = CellText(row, Extra);
                var write = GetWriteValue(row);
                if (protocol == "TCP")
                {
                    if (direction == "Inbound")
                    {
                        mirror.MirrorStream = new TcpSerial();
                        mirror.MirrorStreamWrite = write;
                        CMB_baudrate.SelectedIndex = 0;
                        rowListener = new TcpListener(IPAddress.Any, int.Parse(port));
                        rowListener.Start(0);
                        rowListener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClientCallback),
                            (rowListener, mirror));
                    }
                    else
                    {
                        mirror.MirrorStream = new TcpSerial
                        {
                            retrys = 999999,
                            autoReconnect = true,
                            Host = extra,
                            Port = port,
                            ConfigRef = "SerialOutputPassTCP"
                        };
                        CMB_baudrate.SelectedIndex = 0;
                        mirror.MirrorStream.Open();
                        mirror.MirrorStreamWrite = write;
                    }
                }
                else if (protocol == "UDP")
                {
                    if (direction == "Inbound")
                    {
                        var udp = new UdpSerial { ConfigRef = "SerialOutputPassUDP", Port = port };
                        udp.client = new UdpClient(int.Parse(port));
                        mirror.MirrorStream = udp;
                        udp.IsOpen = true;
                        CMB_baudrate.SelectedIndex = 0;
                        mirror.MirrorStream.Open();
                        mirror.MirrorStreamWrite = write;
                    }
                    else
                    {
                        IPAddress targetAddress;
                        if (!IPAddress.TryParse(extra, out targetAddress))
                            targetAddress = Dns.GetHostAddresses(extra).First(address =>
                                address.AddressFamily == AddressFamily.InterNetwork ||
                                address.AddressFamily == AddressFamily.InterNetworkV6);
                        var udp = new UdpSerialConnect { ConfigRef = "SerialOutputPassUDPCL" };
                        udp.hostEndPoint = new IPEndPoint(targetAddress, int.Parse(port));
                        udp.client = new UdpClient();
                        udp.IsOpen = true;
                        mirror.MirrorStream = udp;
                        mirror.MirrorStreamWrite = write;
                        CMB_baudrate.SelectedIndex = 0;
                    }
                }
                else
                {
                    mirror.MirrorStream = new SerialPort
                    {
                        PortName = port,
                        BaudRate = int.Parse(extra)
                    };
                    mirror.MirrorStream.Open();
                    mirror.MirrorStreamWrite = write;
                }

                MainV2.comPort.Mirrors.Add(mirror);
                row.Tag = new ForwardRuntime { Started = true, Mirror = mirror, Listener = rowListener };
                SetRuntimeStatus(row, true, null);
                Save();
            }
            catch (Exception ex)
            {
                try { if (rowListener != null) rowListener.Stop(); } catch { }
                try { if (mirror != null && mirror.MirrorStream != null) mirror.MirrorStream.Close(); } catch { }
                if (mirror != null)
                    MainV2.comPort.Mirrors.Remove(mirror);
                row.Tag = null;
                var message = "無法啟用 MAVLink 轉發：" + ex.Message;
                SetRuntimeStatus(row, false, message);
                CustomMessageBox.Show(message, "MAVLink 轉發設定");
            }
        }

        private static string CellText(DataGridViewRow row, DataGridViewColumn column)
        {
            var value = row.Cells[column.Name].Value;
            return value == null ? string.Empty : value.ToString().Trim();
        }

        private static bool GetWriteValue(DataGridViewRow row)
        {
            var value = row.Cells["Write"].Value;
            if (value == null)
                return false;
            bool parsed;
            return value is bool ? (bool)value : bool.TryParse(value.ToString(), out parsed) && parsed;
        }

        private bool TryValidateForwardingRow(DataGridViewRow row, out string error)
        {
            error = null;
            var protocol = CellText(row, Type);
            var direction = CellText(row, Direction);
            var portText = CellText(row, Port);
            var extra = CellText(row, Extra);

            if (protocol != "Serial" && protocol != "TCP" && protocol != "UDP")
            {
                error = "請先選擇轉發協定。";
                return false;
            }

            if (protocol == "Serial")
            {
                int baudRate;
                if (string.IsNullOrWhiteSpace(portText))
                {
                    error = "請輸入序列埠名稱。";
                    return false;
                }
                if (!int.TryParse(extra, out baudRate) || baudRate <= 0)
                {
                    error = "序列埠鮑率必須是大於 0 的整數。";
                    return false;
                }
            }
            else
            {
                int portNumber;
                if (direction != "Inbound" && direction != "Outbound")
                {
                    error = "請選擇轉發方向。";
                    return false;
                }
                if (!int.TryParse(portText, out portNumber) || portNumber < 1 || portNumber > 65535)
                {
                    error = "連接埠必須介於 1 到 65535。";
                    return false;
                }
                if (direction == "Outbound" && !IsValidHost(extra))
                {
                    error = "目標 IP 或主機名稱格式不正確。";
                    return false;
                }
                if (direction == "Inbound" && IsPortInUse(protocol, portNumber))
                {
                    error = protocol + " 連接埠 " + portNumber + " 已被其他程式占用。";
                    return false;
                }
            }

            foreach (DataGridViewRow other in myDataGridView1.Rows)
            {
                if (other == row || other.IsNewRow || string.IsNullOrWhiteSpace(CellText(other, Type)))
                    continue;

                var otherProtocol = CellText(other, Type);
                var otherDirection = CellText(other, Direction);
                var otherPort = CellText(other, Port);
                var otherExtra = CellText(other, Extra);

                if (protocol == "Serial" && otherProtocol == "Serial" &&
                    string.Equals(portText, otherPort, StringComparison.OrdinalIgnoreCase))
                {
                    error = "序列埠 " + portText + " 已在另一筆轉發設定中使用。";
                    return false;
                }

                if (protocol == otherProtocol && direction == "Inbound" && otherDirection == "Inbound" &&
                    portText == otherPort)
                {
                    error = protocol + " 監聽連接埠 " + portText + " 與另一筆設定重複。";
                    return false;
                }

                if (protocol == otherProtocol && direction == "Outbound" && otherDirection == "Outbound" &&
                    portText == otherPort && string.Equals(extra, otherExtra, StringComparison.OrdinalIgnoreCase))
                {
                    error = "目標 " + extra + ":" + portText + " 與另一筆轉發設定重複。";
                    return false;
                }

                if (protocol == otherProtocol && direction == "Outbound" && otherDirection == "Inbound" &&
                    portText == otherPort && IsLocalAddress(extra))
                {
                    error = "目標指向本機的相同監聽連接埠，會形成轉發迴圈。";
                    return false;
                }

                if (protocol == otherProtocol && direction == "Inbound" && otherDirection == "Outbound" &&
                    portText == otherPort && IsLocalAddress(otherExtra))
                {
                    error = "此監聽連接埠已被另一筆本機轉發目標使用，會形成轉發迴圈。";
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;
            if (host.All(char.IsDigit))
                return false;
            IPAddress address;
            return IPAddress.TryParse(host, out address) || Uri.CheckHostName(host) != UriHostNameType.Unknown;
        }

        private static bool IsLocalAddress(string host)
        {
            IPAddress address;
            if (!IPAddress.TryParse(host, out address))
                return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                return true;
            try
            {
                return Dns.GetHostAddresses(Dns.GetHostName()).Any(local => local.Equals(address));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPortInUse(string protocol, int port)
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                return protocol == "TCP"
                    ? properties.GetActiveTcpListeners().Any(endpoint => endpoint.Port == port)
                    : properties.GetActiveUdpListeners().Any(endpoint => endpoint.Port == port);
            }
            catch
            {
                // 作業系統無法提供清單時，交由實際 Bind/Open 再回報錯誤。
                return false;
            }
        }

        private void StopForwardingRow(DataGridViewRow row, bool save)
        {
            var runtime = row == null ? null : row.Tag as ForwardRuntime;
            if (runtime != null)
            {
                try { if (runtime.Listener != null) runtime.Listener.Stop(); } catch { }
                try
                {
                    if (runtime.Mirror != null && runtime.Mirror.MirrorStream != null)
                        runtime.Mirror.MirrorStream.Close();
                }
                catch { }
                if (runtime.Mirror != null)
                    MainV2.comPort.Mirrors.Remove(runtime.Mirror);
            }

            if (row != null && !row.IsNewRow)
            {
                row.Tag = null;
                SetRuntimeStatus(row, false, null);
            }
            if (save)
                Save();
        }
    }
}
