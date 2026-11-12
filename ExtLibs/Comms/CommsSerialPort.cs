using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.Win32.SafeHandles;

namespace MissionPlanner.Comms
{
    public class SerialPort : ICommsSerial
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SerialPort));

        public static Func<SerialPort, string, int, ICommsSerial> DefaultType { get; set; } = (self, portname, baudrate) =>
        {
            return new WinSerialPort();
        };

        public ICommsSerial _baseport;

        public SerialPort()
        {
            _baseport = DefaultType.Invoke(this, null, 0);
        }

        public SerialPort(string portname, int baudrate)
        {
            _baseport = DefaultType.Invoke(this, portname, baudrate);
            PortName = portname;
            BaudRate = baudrate;
        }

        public SerialPort(string portname)
        {
            _baseport = DefaultType.Invoke(this, portname, 0);
            PortName = portname;
        }

        public void Dispose()
        {
            _baseport.Dispose();
        }

        public Stream BaseStream => _baseport.BaseStream;

        public int BaudRate
        {
            get
            {
                if (_baseport == null) return 0; return _baseport.BaudRate; }
            set
            {
                if (_baseport == null)
                    _baseport = DefaultType.Invoke(this, PortName, value);
                if(_baseport == null) // we are trying to set baudrate on a port that has no portname/doesnt exist
                    return;
                _baseport.BaudRate = value;
            }
        }

        public int BytesToRead => _baseport.BytesToRead;

        public int BytesToWrite => _baseport.BytesToWrite;

        public int DataBits
        {
            get => _baseport.DataBits;
            set => _baseport.DataBits = value;
        }

        public bool DtrEnable
        {
            get => _baseport.DtrEnable;
            set => _baseport.DtrEnable = value;
        }

        public bool IsOpen
        {
            get
            {
                if (_baseport == null) return false;
                return _baseport.IsOpen;
            }
        }

        public string PortName
        {
            get
            {
                if (_baseport == null) return "";
                return _baseport.PortName;
            }
            set
            {
                if (_baseport != null)
                    _baseport.PortName = value;
                if (_baseport == null)
                    _baseport = DefaultType.Invoke(this, value, 0);
            }
        }

        public int ReadBufferSize
        {
            get => _baseport.ReadBufferSize;
            set => _baseport.ReadBufferSize = value;
        }

        public int ReadTimeout
        {
            get => _baseport.ReadTimeout;
            set => _baseport.ReadTimeout = value;
        }

        public bool RtsEnable
        {
            get => _baseport.RtsEnable;
            set => _baseport.RtsEnable = value;
        }

        public int WriteBufferSize
        {
            get => _baseport.WriteBufferSize;
            set => _baseport.WriteBufferSize = value;
        }

        public int WriteTimeout
        {
            get => _baseport.WriteTimeout;
            set => _baseport.WriteTimeout = value;
        }

        public void Close()
        {
            _baseport.Close();
        }

        public void DiscardInBuffer()
        {
            _baseport.DiscardInBuffer();
        }

        public void Open()
        {
            if (espFix && _baseport is WinSerialPort)
            {
                ((WinSerialPort)_baseport).espFix = true;
            }
            _baseport.Open();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _baseport.Read(buffer, offset, count);
        }

        public int ReadByte()
        {
            return _baseport.ReadByte();
        }

        public int ReadChar()
        {
            return _baseport.ReadChar();
        }

        public string ReadExisting()
        {
            return _baseport.ReadExisting();
        }

        public string ReadLine()
        {
            return _baseport.ReadLine();
        }

        public void Write(string text)
        {
            _baseport.Write(text);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _baseport.Write(buffer, offset, count);
        }

        public void WriteLine(string text)
        {
            _baseport.WriteLine(text);
        }

        public void toggleDTR()
        {
            _baseport.toggleDTR();
        }


        public bool espFix = false;

        private static readonly object locker = new object();

        private static readonly ConcurrentDictionary<string, string> comportnamecache = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> nameRequests =
            new ConcurrentDictionary<string, Lazy<Task<string>>>();
        private static Task<string[]> portEnumeration;
        private static string[] lastSystemPorts = new string[0];
        internal static Func<string[]> SystemPortReader = System.IO.Ports.SerialPort.GetPortNames;
        public static event EventHandler DeviceNamesChanged;

        public new static string[] GetPortNames()
        {
            // prevent hammering
            lock (locker)
            {
                var allPorts = new List<string>();

                if (Directory.Exists("/dev/"))
                {
                    // cleanup now
                    GC.Collect();
                    // mono is failing in here on linux "too many open files"
                    try
                    {
                        if (Directory.Exists("/dev/serial/by-id/"))
                            allPorts.AddRange(Directory.GetFiles("/dev/serial/by-id/", "*"));
                    }
                    catch
                    {
                    }

                    try
                    {
                        allPorts.AddRange(Directory.GetFiles("/dev/", "ttyACM*"));
                    }
                    catch
                    {
                    }

                    try
                    {
                        allPorts.AddRange(Directory.GetFiles("/dev/", "ttyUSB*"));
                    }
                    catch
                    {
                    }

                    try
                    {
                        allPorts.AddRange(Directory.GetFiles("/dev/", "rfcomm*"));
                    }
                    catch
                    {
                    }

                    try
                    {
                        allPorts.AddRange(Directory.GetFiles("/dev/", "*usb*"));
                    }
                    catch
                    {
                    }

                    try
                    {
                        allPorts.AddRange(Directory.GetFiles("/dev/", "tty.*"));
                    }
                    catch
                    {
                    }

                    try
                    {
                        allPorts.AddRange(Directory.GetFiles("/dev/", "cu.*"));
                    }
                    catch
                    {
                    }
                }

                string[] ports = null;

                try
                {
                    // A driver/registry query must not block the UI indefinitely. Reuse one
                    // pending request instead of starting more workers on every dropdown click.
                    if (portEnumeration == null) portEnumeration = Task.Run(() => SystemPortReader());
                    if (portEnumeration.Wait(1000))
                    {
                        lastSystemPorts = NormalizePortNames(portEnumeration.Result);
                        portEnumeration = null;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    portEnumeration = null;
                }
                ports = lastSystemPorts;

                if (ports != null)
                    allPorts.AddRange(ports);

                if (GetCustomPorts != null)
                {
                    try
                    {
                        allPorts.AddRange(GetCustomPorts.Invoke());
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }

                return NormalizePortNames(allPorts);
            }
        }

        public static Func<List<string>> GetCustomPorts;

        public static string GetNiceName(string port)
        {
            if (string.IsNullOrWhiteSpace(port) || port == "AUTO" || port == "UDP" ||
                port == "UDPCl" || port == "TCP" || port == "WS") return "";
            if (comportnamecache.TryGetValue(port, out var name)) return name;
            // WMI can stall inside a Bluetooth driver. Never abort that worker or wait
            // for it from DrawItem. Results belong to their own port, even if they arrive late.
            var request = nameRequests.GetOrAdd(port, p => new Lazy<Task<string>>(() => Task.Run(() => GetName(p))));
            var task = request.Value;
            return task.Status == TaskStatus.RanToCompletion ? task.Result : "";
        }

        public delegate string EventArgsDeviceName(string port);

        public static event EventArgsDeviceName GetDeviceName;

        private static string GetName(string port)
        {
            var name = "";
            try
            {
                name = GetDeviceName?.Invoke(port) ?? "";
            }
            catch (Exception ex)
            {
                log.Debug("Unable to resolve serial port name " + port, ex);
            }
            comportnamecache[port] = name;
            try { DeviceNamesChanged?.Invoke(null, EventArgs.Empty); }
            catch (Exception ex) { log.Debug("Serial port name notification failed", ex); }
            return name;
        }

        internal static string[] NormalizePortNames(IEnumerable<string> ports)
        {
            return (ports ?? Enumerable.Empty<string>()).Select(FixBlueToothPortNameBug)
                .Where(p => !string.IsNullOrWhiteSpace(p) && !p.Any(char.IsControl))
                .Distinct(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                    StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).ToArray();
        }

        // .NET bug: sometimes bluetooth ports are enumerated with bogus characters
        // eg 'COM10' becomes 'COM10c' - one workaround is to remove the non numeric
        // char. Annoyingly, sometimes a numeric char is added, which means this
        // does not work in all cases.
        // See http://connect.microsoft.com/VisualStudio/feedback/details/236183/system-io-ports-serialport-getportnames-error-with-bluetooth
        private static string FixBlueToothPortNameBug(string portName)
        {
            if (portName == null)
                return null;
            portName = portName.Trim('\0', ' ', '\t', '\r', '\n');
            if (!portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                return portName;
            int end = 3;
            while (end < portName.Length && portName[end] >= '0' && portName[end] <= '9') end++;
            return end == 3 ? null : "COM" + portName.Substring(3, end - 3);
        }
    }

    public class WinSerialPort : System.IO.Ports.SerialPort, ICommsSerial
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WinSerialPort));

        public bool espFix = false;
        public WinSerialPort()
        {
        }

        public new bool DtrEnable
        {
            get => base.DtrEnable;
            set
            {
                log.Info(base.PortName + " DtrEnable " + value);
                if (base.DtrEnable == value) return;
                base.DtrEnable = value;
            }
        }

        public new bool RtsEnable
        {
            get => base.RtsEnable;
            set
            {
                log.Info(PortName + " RtsEnable " + value);
                if (base.RtsEnable == value) return;
                base.RtsEnable = value;
            }
        }

        /*
        protected override void Dispose(bool disposing)
        {
            try
            {
                try
                {
                    Type mytype = typeof(System.IO.Ports.SerialPort);
                    FieldInfo field = mytype.GetField("internalSerialStream", BindingFlags.Instance | BindingFlags.NonPublic);

                    if (field != null)
                    {
                        Stream stream = (Stream)field.GetValue(this);

                        if (stream != null)
                        {
                            try
                            {
                                stream.Dispose();
                            }
                            catch (Exception ex) { Console.WriteLine("1 " + ex.ToString()); }
                            stream = null;
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine("2 " + ex.ToString()); }

                base.Dispose(disposing);
            }
            catch (Exception ex) { Console.WriteLine("3 " + ex.ToString()); }
        }
        */

        public new void Open()
        {
            // 500ms write timeout - win32 api default
            base.WriteTimeout = 500;


            if (base.IsOpen)
                return;

            try
            {
                // this causes element not found with bluetooth devices.
                if (base.BaudRate >= 115200)
                {
                    //Console.WriteLine("Doing SerialPortFixer");
                    //SerialPortFixer.Execute(PortName);
                    //Console.WriteLine("Done SerialPortFixer");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            if (PortName.StartsWith("/"))
                if (!File.Exists(PortName))
                    throw new Exception("No such device");


            if (espFix)
                base.Handshake = System.IO.Ports.Handshake.RequestToSend;

            try
            {
                base.Open();
            }
            catch
            {
                try
                {
                    Close();
                }
                catch
                {
                }

                throw;
            }
            if (espFix)
                base.Handshake = System.IO.Ports.Handshake.None;
        }

        public new void Close()
        {
            log.Info("Closing port " + PortName);
            base.Close();
        }

        public void toggleDTR()
        {
            var open = base.IsOpen;
            Console.WriteLine("toggleDTR " + IsOpen);
            try
            {
                if (!open)
                    Open();
            }
            catch
            {
            }

            base.DtrEnable = false;
            base.RtsEnable = false;

            Thread.Sleep(50);

            base.DtrEnable = true;
            base.RtsEnable = true;

            Thread.Sleep(50);

            try
            {
                if (!open)
                    Close();
            }
            catch
            {
            }

            Console.WriteLine("toggleDTR done " + IsOpen);
        }
    }

    public sealed class SerialPortFixer : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SerialPortFixer));

        #region IDisposable Members

        public void Dispose()
        {
            if (m_Handle != null)
            {
                m_Handle.Dispose();
                m_Handle = null;
            }

            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Members

        public static void Execute(string portName)
        {
            using (new SerialPortFixer(portName))
            {
            }
        }

        #region Implementation

        private const int DcbFlagAbortOnError = 14;
        private const int CommStateRetries = 10;
        private SafeFileHandle m_Handle;

        private SerialPortFixer(string portName)
        {
            const int dwFlagsAndAttributes = 0x40000000;
            const int dwAccess = unchecked((int) 0xC0000000);
            if (portName == null || !portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Invalid Serial Port", "portName");
            var hFile = NativeMethods.CreateFile(@"\\.\" + portName, dwAccess, 0, IntPtr.Zero, 3, dwFlagsAndAttributes,
                IntPtr.Zero);
            if (hFile.IsInvalid) WinIoError();
            try
            {
                var fileType = NativeMethods.GetFileType(hFile);
                if (fileType != 2 && fileType != 0) throw new ArgumentException("Invalid Serial Port", "portName");
                m_Handle = hFile;
                InitializeDcb();
            }
            catch
            {
                hFile.Dispose();
                m_Handle = null;
                throw;
            }
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern int FormatMessage(int dwFlags, HandleRef lpSource, int dwMessageId, int dwLanguageId,
                StringBuilder lpBuffer, int nSize, IntPtr arguments);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern bool GetCommState(SafeFileHandle hFile, ref Dcb lpDcb);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern bool SetCommState(SafeFileHandle hFile, ref Dcb lpDcb);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern bool ClearCommError(SafeFileHandle hFile, ref int lpErrors, ref Comstat lpStat);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode,
                IntPtr securityAttrs, int dwCreationDisposition,
                int dwFlagsAndAttributes, IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern int GetFileType(SafeFileHandle hFile);
        }

        private void InitializeDcb()
        {
            var dcb = new Dcb();
            GetCommStateNative(ref dcb);
            log.Info("before dcb flags: " + dcb.Flags);
            dcb.Flags &= ~(1u << DcbFlagAbortOnError);
            log.Info("after dcb flags: " + dcb.Flags);
            SetCommStateNative(ref dcb);
        }

        private static string GetMessage(int errorCode)
        {
            var lpBuffer = new StringBuilder(0x200);
            if (
                NativeMethods.FormatMessage(0x3200, new HandleRef(null, IntPtr.Zero), errorCode, 0, lpBuffer,
                    lpBuffer.Capacity,
                    IntPtr.Zero) != 0)
                return lpBuffer.ToString();
            return "Unknown Error";
        }

        private static int MakeHrFromErrorCode(int errorCode)
        {
            return (int) (0x80070000 | (uint) errorCode);
        }

        private static void WinIoError()
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new IOException(GetMessage(errorCode), MakeHrFromErrorCode(errorCode));
        }

        private void GetCommStateNative(ref Dcb lpDcb)
        {
            var commErrors = 0;
            var comStat = new Comstat();

            for (var i = 0; i < CommStateRetries; i++)
            {
                if (!NativeMethods.ClearCommError(m_Handle, ref commErrors, ref comStat)) WinIoError();
                if (NativeMethods.GetCommState(m_Handle, ref lpDcb)) break;
                if (i == CommStateRetries - 1) WinIoError();
            }
        }

        private void SetCommStateNative(ref Dcb lpDcb)
        {
            var commErrors = 0;
            var comStat = new Comstat();
            for (var i = 0; i < CommStateRetries; i++)
            {
                if (!NativeMethods.ClearCommError(m_Handle, ref commErrors, ref comStat)) WinIoError();
                if (NativeMethods.SetCommState(m_Handle, ref lpDcb)) break;
                if (i == CommStateRetries - 1) WinIoError();
            }
        }

        #region Nested type: COMSTAT

        [StructLayout(LayoutKind.Sequential)]
        private struct Comstat
        {
            public readonly uint Flags;
            public readonly uint cbInQue;
            public readonly uint cbOutQue;
        }

        #endregion Nested type: COMSTAT

        #region Nested type: DCB

        /*
         * https://msdn.microsoft.com/en-us/library/windows/desktop/aa363214(v=vs.85).aspx
  DWORD fBinary  :1;
  DWORD fParity  :1;
  DWORD fOutxCtsFlow  :1;
  DWORD fOutxDsrFlow  :1;
  DWORD fDtrControl  :2;
  DWORD fDsrSensitivity  :1;
  DWORD fTXContinueOnXoff  :1;
  DWORD fOutX  :1;
  DWORD fInX  :1;
  DWORD fErrorChar  :1;
  DWORD fNull  :1;
  DWORD fRtsControl  :2;
  DWORD fAbortOnError  :1;
  DWORD fDummy2  :17;
         */

        [StructLayout(LayoutKind.Sequential)]
        public struct Dcb
        {
            public readonly uint DCBlength;
            public readonly uint BaudRate;
            public uint Flags;
            public readonly ushort wReserved;
            public readonly ushort XonLim;
            public readonly ushort XoffLim;
            public readonly byte ByteSize;
            public readonly byte Parity;
            public readonly byte StopBits;
            public readonly byte XonChar;
            public readonly byte XoffChar;
            public readonly byte ErrorChar;
            public readonly byte EofChar;
            public readonly byte EvtChar;
            public readonly ushort wReserved1;
        }

        #endregion Nested type: DCB

        #endregion Implementation
    }
}
