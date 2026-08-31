#if !LIB
extern alias Drawing;
#endif

using GMap.NET.WindowsForms;
using log4net;
using MissionPlanner.ArduPilot;
using MissionPlanner.Comms;
using MissionPlanner.Controls;
using MissionPlanner.FMT;
using MissionPlanner.GCSViews.ConfigurationView;
using MissionPlanner.Log;
using MissionPlanner.Maps;
using MissionPlanner.Utilities;

using MissionPlanner.Warnings;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner.ArduPilot.Mavlink;
using MissionPlanner.Utilities.HW;
using Transitions;
using System.Linq;
using MissionPlanner.Joystick;
using System.Net;
using Newtonsoft.Json;
using MissionPlanner;
using Flurl.Util;
using Org.BouncyCastle.Bcpg;
using log4net.Repository.Hierarchy;
using System.Numerics;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using static MAVLink;
using DroneCAN;

namespace MissionPlanner
{
    public partial class MainV2 : Form
    {
        private static readonly ILog log =
            LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly FMT.FmtConnectionCloseGuard fmtConnectionCloseGuard = new FMT.FmtConnectionCloseGuard();
        private bool fmtShutdownStarted;

        public static menuicons displayicons; //do not initialize to allow update of custom icons
        public static string running_directory = Settings.GetRunningDirectory();

        public abstract class menuicons
        {
            public abstract Image fd { get; }
            public abstract Image fp { get; }
            public abstract Image initsetup { get; }
            public abstract Image config_tuning { get; }
            public abstract Image sim { get; }
            public abstract Image terminal { get; }
            public abstract Image help { get; }
            public abstract Image donate { get; }
            public abstract Image connect { get; }
            public abstract Image disconnect { get; }
            public abstract Image bg { get; }
            public abstract Image wizard { get; }
        }


        public class burntkermitmenuicons : menuicons
        {
            public override Image fd
            {
                get
                {
                    if (File.Exists($"{running_directory}light_flightdata_icon.png"))
                        return Image.FromFile($"{running_directory}light_flightdata_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_flightdata_icon;
                }
            }

            public override Image fp
            {
                get
                {
                    if (File.Exists($"{running_directory}light_flightplan_icon.png"))
                        return Image.FromFile($"{running_directory}light_flightplan_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_flightplan_icon;
                }
            }

            public override Image initsetup
            {
                get
                {
                    if (File.Exists($"{running_directory}light_initialsetup_icon.png"))
                        return Image.FromFile($"{running_directory}light_initialsetup_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_initialsetup_icon;
                }
            }

            public override Image config_tuning
            {
                get
                {
                    if (File.Exists($"{running_directory}light_tuningconfig_icon.png"))
                        return Image.FromFile($"{running_directory}light_tuningconfig_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_tuningconfig_icon;
                }
            }

            public override Image sim
            {
                get
                {
                    if (File.Exists($"{running_directory}light_simulation_icon.png"))
                        return Image.FromFile($"{running_directory}light_simulation_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_simulation_icon;
                }
            }

            public override Image terminal
            {
                get
                {
                    if (File.Exists($"{running_directory}light_terminal_icon.png"))
                        return Image.FromFile($"{running_directory}light_terminal_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_terminal_icon;
                }
            }

            public override Image help
            {
                get
                {
                    if (File.Exists($"{running_directory}light_help_icon.png"))
                        return Image.FromFile($"{running_directory}light_help_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_help_icon;
                }
            }

            public override Image donate
            {
                get
                {
                    if (File.Exists($"{running_directory}light_donate_icon.png"))
                        return Image.FromFile($"{running_directory}light_donate_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.donate;
                }
            }

            public override Image connect
            {
                get
                {
                    if (File.Exists($"{running_directory}light_connect_icon.png"))
                        return Image.FromFile($"{running_directory}light_connect_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_connect_icon;
                }
            }

            public override Image disconnect
            {
                get
                {
                    if (File.Exists($"{running_directory}light_disconnect_icon.png"))
                        return Image.FromFile($"{running_directory}light_disconnect_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.light_disconnect_icon;
                }
            }

            public override Image bg
            {
                get
                {
                    if (File.Exists($"{running_directory}light_icon_background.png"))
                        return Image.FromFile($"{running_directory}light_icon_background.png");
                    else
                        return global::MissionPlanner.Properties.Resources.bgdark;
                }
            }

            public override Image wizard
            {
                get
                {
                    if (File.Exists($"{running_directory}light_wizard_icon.png"))
                        return Image.FromFile($"{running_directory}light_wizard_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.wizardicon;
                }
            }
        }

        public class highcontrastmenuicons : menuicons
        {
            private string running_directory = Settings.GetRunningDirectory();

            public override Image fd
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_flightdata_icon.png"))
                        return Image.FromFile($"{running_directory}dark_flightdata_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_flightdata_icon;
                }
            }

            public override Image fp
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_flightplan_icon.png"))
                        return Image.FromFile($"{running_directory}dark_flightplan_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_flightplan_icon;
                }
            }

            public override Image initsetup
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_initialsetup_icon.png"))
                        return Image.FromFile($"{running_directory}dark_initialsetup_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_initialsetup_icon;
                }
            }

            public override Image config_tuning
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_tuningconfig_icon.png"))
                        return Image.FromFile($"{running_directory}dark_tuningconfig_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_tuningconfig_icon;
                }
            }

            public override Image sim
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_simulation_icon.png"))
                        return Image.FromFile($"{running_directory}dark_simulation_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_simulation_icon;
                }
            }

            public override Image terminal
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_terminal_icon.png"))
                        return Image.FromFile($"{running_directory}dark_terminal_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_terminal_icon;
                }
            }

            public override Image help
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_help_icon.png"))
                        return Image.FromFile($"{running_directory}dark_help_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_help_icon;
                }
            }

            public override Image donate
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_donate_icon.png"))
                        return Image.FromFile($"{running_directory}dark_donate_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.donate;
                }
            }

            public override Image connect
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_connect_icon.png"))
                        return Image.FromFile($"{running_directory}dark_connect_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_connect_icon;
                }
            }

            public override Image disconnect
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_disconnect_icon.png"))
                        return Image.FromFile($"{running_directory}dark_disconnect_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.dark_disconnect_icon;
                }
            }

            public override Image bg
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_icon_background.png"))
                        return Image.FromFile($"{running_directory}dark_icon_background.png");
                    else
                        return null;
                }
            }

            public override Image wizard
            {
                get
                {
                    if (File.Exists($"{running_directory}dark_wizard_icon.png"))
                        return Image.FromFile($"{running_directory}dark_wizard_icon.png");
                    else
                        return global::MissionPlanner.Properties.Resources.wizardicon;
                }
            }
        }

        Controls.MainSwitcher MyView;

        private static DisplayView _displayConfiguration = File.Exists(DisplayViewExtensions.custompath)
            ? new DisplayView().Custom()
            : new DisplayView().Advanced();

        public static event EventHandler LayoutChanged;

        public static DisplayView DisplayConfiguration
        {
            get { return _displayConfiguration; }
            set
            {
                _displayConfiguration = value;
                Settings.Instance["displayview"] = _displayConfiguration.ConvertToString();
                LayoutChanged?.Invoke(null, EventArgs.Empty);
            }
        }


        public static bool ShowAirports { get; set; }
        public static bool ShowTFR { get; set; }

        private Utilities.adsb _adsb;

        public bool EnableADSB
        {
            get { return _adsb != null; }
            set
            {
                if (value == true)
                {
                    _adsb = new Utilities.adsb();

                    if (Settings.Instance["adsbserver"] != null)
                        Utilities.adsb.server = Settings.Instance["adsbserver"];
                    if (Settings.Instance["adsbport"] != null)
                        Utilities.adsb.serverport = int.Parse(Settings.Instance["adsbport"].ToString());
                }
                else
                {
                    Utilities.adsb.Stop();
                    _adsb = null;
                }
            }
        }

        //public static event EventHandler LayoutChanged;

        /// <summary>
        /// Active Comport interface
        /// </summary>
        public static MAVLinkInterface comPort
        {
            get { return _comPort; }
            set
            {
                if (_comPort == value)
                    return;

                var previousPort = _comPort;
                if (instance != null && previousPort != null)
                    previousPort.MavChanged -= instance.comPort_MavChanged;

                _comPort = value;
                if (instance == null)
                    return;
                _comPort.MavChanged += instance.comPort_MavChanged;
                instance.comPort_MavChanged(null, null);
            }
        }

        static MAVLinkInterface _comPort = new MAVLinkInterface();

        /// <summary>
        /// passive comports
        /// </summary>
        public static List<MAVLinkInterface> Comports = new List<MAVLinkInterface>();

        public delegate void WMDeviceChangeEventHandler(WM_DEVICECHANGE_enum cause);

        public event WMDeviceChangeEventHandler DeviceChanged;
        private readonly ConditionalWeakTable<Form, object> themedChildForms =
            new ConditionalWeakTable<Form, object>();
        private static readonly object ThemeAppliedMarker = new object();

        /// <summary>
        /// other planes in the area from adsb
        /// </summary>
        public object adsblock = new object();

        public ConcurrentDictionary<string, adsb.PointLatLngAltHdg> adsbPlanes =
            new ConcurrentDictionary<string, adsb.PointLatLngAltHdg>();

        public static string titlebar;

        /// <summary>
        /// Comport name
        /// </summary>
        public static string comPortName = "";

        public static int comPortBaud = 57600;

        /// <summary>
        /// mono detection
        /// </summary>
        public static bool MONO = false;

        public bool UseCachedParams { get; set; } = false;
        public static bool Android { get; set; }
        public static bool IOS { get; set; }
        public static bool OSX { get; set; }


        /// <summary>
        /// speech engine enable
        /// </summary>
        public static bool speechEnable
        {
            get { return speechEngine == null ? false : speechEngine.speechEnable; }
            set
            {
                if (speechEngine != null) speechEngine.speechEnable = value;
            }
        }

        public static bool speech_armed_only = false;
        public static bool speechEnabled()
        {
            if (speechEngine == null)
                return false;

            if (!speechEnable) {
                return false;
            }
            if (speech_armed_only) {
                return MainV2.comPort.MAV.cs.armed;
            }
            return true;
        }

        /// <summary>
        /// spech engine static class
        /// </summary>
        public static ISpeech speechEngine { get; set; }

        /// <summary>
        /// joystick static class
        /// </summary>
        public static Joystick.JoystickBase joystick { get; set; }

        /// <summary>
        /// track last joystick packet sent. used to control rate
        /// </summary>
        DateTime lastjoystick = DateTime.Now;

        /// <summary>
        /// determine if we are running sitl
        /// </summary>
        public static bool sitl
        {
            get
            {
                if (MissionPlanner.GCSViews.SITL.SITLSEND == null) return false;
                if (MissionPlanner.GCSViews.SITL.SITLSEND.Client.Connected) return true;
                return false;
            }
        }

        /// <summary>
        /// hud background image grabber from a video stream - not realy that efficent. ie no hardware overlays etc.
        /// </summary>
        public static WebCamService.Capture cam { get; set; }
        /// <summary>
        /// used for custom autoconnect for predefined endpoints
        /// </summary>
        public List<AutoConnect.ConnectionInfo> ExtraConnectionList { get; } = new List<AutoConnect.ConnectionInfo>();
        /// <summary>
        /// used for dynamic custom port types
        /// </summary>
        public Dictionary<Regex, Func<string, string, ICommsSerial>> CustomPortList { get; } = new Dictionary<Regex, Func<string, string, ICommsSerial>>();

        /// <summary>
        /// controls the main serial reader thread
        /// </summary>
        bool serialThread = false;

        bool pluginthreadrun = false;

        bool joystickthreadrun = false;

        bool adsbThread = false;

        Thread httpthread;
        Thread pluginthread;

        /// <summary>
        /// track the last heartbeat sent
        /// </summary>
        private DateTime heatbeatSend = DateTime.UtcNow;

        /// <summary>
        /// track the last ads-b send time
        /// </summary>
        private DateTime adsbSend = DateTime.Now;
        /// <summary>
        /// track the adsb plane index we're round-robin sending
        /// starts at -1 because it'll get incremented before sending
        /// </summary>
        private int adsbIndex = -1;

        /// <summary>
        /// used to call anything as needed.
        /// </summary>
        public static MainV2 instance = null;

        public static bool isHerelink = false;

        public static MainSwitcher View;

        /// <summary>
        /// store the time we first connect UTC
        /// </summary>
        DateTime connecttime = DateTime.UtcNow;
        /// <summary>
        /// no data repeat interval UTC
        /// </summary>
        DateTime nodatawarning = DateTime.UtcNow;

        /// <summary>
        /// update the connect button UTC
        /// </summary>
        DateTime connectButtonUpdate = DateTime.UtcNow;

        /// <summary>
        /// declared here if i want a "single" instance of the form
        /// ie configuration gets reloaded on every click
        /// </summary>
        public GCSViews.FlightData FlightData;

        public GCSViews.FlightPlanner FlightPlanner;
        GCSViews.SITL Simulation;
        private ToolStripButton MenuFeiMao;
        private ToolStripButton MenuFmtParameterSettings;
        private ToolStripButton MenuFmtPreflightCheck;
        private ToolStripButton MenuFmtArmDisarm;
        private ToolStripButton MenuFmtAirspeedZero;
        private ToolStripButton MenuFmtQnh;
        private ToolStripControlHost MenuFmtGpsStatus;
        private Label FmtGpsPrimaryLabel;
        private Label FmtGpsDopLabel;
        private ToolStripControlHost MenuFmtFlightTime;
        private FmtMqttTrafficIndicator MenuFmtMqttTraffic;
        private Label FmtFlightTimeLabel;
        private Label FmtTotalFlightTimeLabel;
        private ToolStripControlHost MenuFmtRotorRpm;
        private Label FmtRotorRpmLabel;
        private readonly Dictionary<string, Image> FmtQuickActionBackgrounds =
            new Dictionary<string, Image>(StringComparer.Ordinal);

        private sealed class FmtQuickActionToolStripButton : ToolStripButton
        {
            internal Color FillColor { get; set; } = Color.FromArgb(18, 50, 65);
            internal Color BorderColor { get; set; } = Color.FromArgb(65, 194, 235);

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.FromArgb(24, 24, 24));
                var bounds = new RectangleF(1.5F, 2.5F, Math.Max(1, Width - 4F), Math.Max(1, Height - 6F));
                using (var path = CreateFmtRoundedRectanglePath(bounds, 7F))
                using (var fill = new SolidBrush(FillColor))
                using (var border = new Pen(BorderColor, 1.4F))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }

                var textColor = Enabled ? Color.White : Color.FromArgb(205, 212, 216);
                TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Round(bounds), textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
        }

        private Form connectionStatsForm;
        private ConnectionStats _connectionStats;

        /// <summary>
        /// This 'Control' is the toolstrip control that holds the comport combo, baudrate combo etc
        /// Otiginally seperate controls, each hosted in a toolstip sqaure, combined into this custom
        /// control for layout reasons.
        /// </summary>
        public static ConnectionControl _connectionControl;

        public static bool TerminalTheming = true;

        public void updateLayout(object sender, EventArgs e)
        {
            MenuSimulation.Visible = DisplayConfiguration.displaySimulation;
            MenuHelp.Visible = DisplayConfiguration.displayHelp;
            MissionPlanner.Controls.BackstageView.BackstageView.Advanced = DisplayConfiguration.isAdvancedMode;

            // force autohide on
            if (DisplayConfiguration.autoHideMenuForce)
            {
                AutoHideMenu(true);
                Settings.Instance["menu_autohide"] = true.ToString();
                autoHideToolStripMenuItem.Visible = false;
            }
            else if (Settings.Instance.GetBoolean("menu_autohide"))
            {
                AutoHideMenu(Settings.Instance.GetBoolean("menu_autohide"));
                Settings.Instance["menu_autohide"] = Settings.Instance.GetBoolean("menu_autohide").ToString();
            }



            //Flight data page
            if (MainV2.instance.FlightData != null)
            {
                //hide menu items
                MainV2.instance.FlightData.updateDisplayView();
            }

            if (MainV2.instance.FlightPlanner != null)
            {
                //hide menu items
                MainV2.instance.FlightPlanner.updateDisplayView();
            }
        }


        public MainV2()
        {
            log.Info("Mainv2 ctor");

            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            // create one here - but override on load
            Settings.Instance["guid"] = Guid.NewGuid().ToString();

            //Check for -config argument, and if it is an xml extension filename then use that for config
            if (Program.args.Length > 0 && Program.args.Contains("-config"))
            {
                var cmds = ProcessCommandLine(Program
                    .args); //This will be called later as well, but we need it here and now
                if (cmds.ContainsKey("config") &&
                    (cmds["config"] != null) &&
                    (String.Compare(Path.GetExtension(cmds["config"]), ".xml", true) == 0))
                {
                    Settings.FileName = cmds["config"];
                }
            }

            // load config
            LoadConfig();

            speech_armed_only = Settings.Instance.GetBoolean("speech_armed_only", false);

            // force language to be loaded
            L10N.GetConfigLang();

            ShowAirports = true;

            // setup adsb
            Utilities.adsb.ApplicationVersion = System.Windows.Forms.Application.ProductVersion;
            Utilities.adsb.UpdatePlanePosition += adsb_UpdatePlanePosition;

            MAVLinkInterface.UpdateADSBPlanePosition += adsb_UpdatePlanePosition;

            MAVLinkInterface.UpdateADSBCollision += (sender, tuple) =>
            {
                lock (adsblock)
                {
                    if (MainV2.instance.adsbPlanes.ContainsKey(tuple.id))
                    {
                        // update existing
                        ((adsb.PointLatLngAltHdg) instance.adsbPlanes[tuple.id]).ThreatLevel = tuple.threat_level;
                    }
                }
            };

            MAVLinkInterface.gcssysid = (byte) Settings.Instance.GetByte("gcsid", MAVLinkInterface.gcssysid);

            Form splash = Program.Splash;

            splash?.Refresh();

            Application.DoEvents();

            instance = this;

            MyView = new MainSwitcher(this);

            View = MyView;

            if (Settings.Instance.ContainsKey("language") && !string.IsNullOrEmpty(Settings.Instance["language"]))
            {
                ApplyConfiguredLanguageAtStartup(CultureInfoEx.GetCultureInfo(Settings.Instance["language"]));
            }

            InitializeComponent();
            ConfigureFmtMainMenu();

            //Init Theme table and load BurntKermit as a default
            ThemeManager.thmColor = new ThemeColorTable(); //Init colortable
            ThemeManager.thmColor.InitColors(); //This fills up the table with BurntKermit defaults.
            ThemeManager.thmColor
                .SetTheme(); //Set the colors, this need to handle the case when not all colors are defined in the theme file



            Settings.Instance["theme"] = FMT.FmtAuthentication.ThemeName;

            ThemeManager.LoadTheme(Settings.Instance["theme"]);

            Utilities.ThemeManager.ApplyThemeTo(this);


            // define default basestream
            comPort.BaseStream = new SerialPort();
            comPort.BaseStream.BaudRate = 57600;
            ((SerialPort)comPort.BaseStream).espFix = Settings.Instance.GetBoolean("CHK_rtsresetesp32", false);

            _connectionControl = toolStripConnectionControl.ConnectionControl;
            _connectionControl.CMB_baudrate.TextChanged += this.CMB_baudrate_TextChanged;
            _connectionControl.CMB_serialport.SelectedIndexChanged += this.CMB_serialport_SelectedIndexChanged;
            _connectionControl.CMB_serialport.Click += this.CMB_serialport_Click;
            _connectionControl.cmb_sysid.Click += cmb_sysid_Click;

            _connectionControl.ShowLinkStats += (sender, e) => ShowConnectionStatsForm();
            srtm.datadirectory = $"{Settings.GetDataDirectory()}srtm";

            try
            {
                Directory.GetFiles(srtm.datadirectory).ToList().ForEach(x =>
                {
                    var fi = new FileInfo(x);
                    if (fi.Length == 0)
                        File.Delete(x);
                    // fix srtm3 bug cache - delete old files https://discuss.ardupilot.org/t/serious-terrain-data-error-and-how-to-fix-your-vehicle/142593
                    if (fi.LastWriteTimeUtc < new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc))
                        File.Delete(x);
                });
            }
            catch { }

            var t = Type.GetType("Mono.Runtime");
            MONO = (t != null);

            try
            {
                if (speechEngine == null)
                    speechEngine = new Speech();
                MAVLinkInterface.Speech = speechEngine;
                CurrentState.Speech = speechEngine;
            }
            catch
            {
            }

            // proxy loader - dll load now instead of on config form load
            new Transition(new TransitionType_EaseInEaseOut(2000));

            PopulateSerialportList();
            if (_connectionControl.CMB_serialport.Items.Count > 0)
            {
                _connectionControl.CMB_baudrate.SelectedIndex = 8;
                _connectionControl.CMB_serialport.SelectedIndex = 0;
            }
            // ** Done

            splash?.Refresh();
            Application.DoEvents();

            // load last saved connection settings
            string temp = Settings.Instance.ComPort;
            if (!string.IsNullOrEmpty(temp))
            {
                _connectionControl.CMB_serialport.SelectedIndex = _connectionControl.CMB_serialport.FindString(temp);
                if (_connectionControl.CMB_serialport.SelectedIndex == -1)
                {
                    _connectionControl.CMB_serialport.Text = temp; // allows ports that dont exist - yet
                }

                comPort.BaseStream.PortName = temp;
                comPortName = temp;
            }

            string temp2 = Settings.Instance.BaudRate;
            if (!string.IsNullOrEmpty(temp2))
            {
                var idx = _connectionControl.CMB_baudrate.FindString(temp2);
                if (idx == -1)
                {
                    _connectionControl.CMB_baudrate.Text = temp2;
                }
                else
                {
                    _connectionControl.CMB_baudrate.SelectedIndex = idx;
                }

                comPortBaud = int.Parse(temp2);
            }

            MissionPlanner.Utilities.Tracking.cid = new Guid(Settings.Instance["guid"].ToString());

            if (splash != null)
            {
                this.Text = splash?.Text;
                titlebar = splash?.Text;
            }

            if (!MONO) // windows only
            {
                if (Settings.Instance["showconsole"] != null && Settings.Instance["showconsole"].ToString() == "True")
                {
                }
                else
                {
                    NativeMethods.ShowWindow(NativeMethods.GetConsoleWindow(), NativeMethods.SW_HIDE);

                }

                // prevent system from sleeping while mp open
                var previousExecutionState =
                    NativeMethods.SetThreadExecutionState(
                        NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED);
            }

            ChangeUnits();

            if (Settings.Instance["showairports"] != null)
            {
                MainV2.ShowAirports = bool.Parse(Settings.Instance["showairports"]);
            }

            // set default
            ShowTFR = true;
            // load saved
            if (Settings.Instance["showtfr"] != null)
            {
                MainV2.ShowTFR = Settings.Instance.GetBoolean("showtfr", ShowTFR);
            }

            if (Settings.Instance["enableadsb"] != null)
            {
                MainV2.instance.EnableADSB = Settings.Instance.GetBoolean("enableadsb");
            }

            try
            {
                log.Debug(Process.GetCurrentProcess().Modules.ToJSON());
            }
            catch
            {
            }

            try
            {
                log.Info("Create FD");
                FlightData = new GCSViews.FlightData();
                log.Info("Create FP");
                FlightPlanner = new GCSViews.FlightPlanner();
                //Configuration = new GCSViews.ConfigurationView.Setup();
                //Firmware = new GCSViews.Firmware();
                //Terminal = new GCSViews.Terminal();

                FlightData.Width = MyView.Width;
                FlightPlanner.Width = MyView.Width;
            }
            catch (ArgumentException e)
            {
                //http://www.microsoft.com/en-us/download/details.aspx?id=16083
                //System.ArgumentException: Font 'Arial' does not support style 'Regular'.

                log.Fatal(e);
                CustomMessageBox.Show($"{e}\n\n Font Issues? Please install this http://www.microsoft.com/en-us/download/details.aspx?id=16083");
                //splash.Close();
                //this.Close();
                Application.Exit();
            }
            catch (Exception e)
            {
                log.Fatal(e);
                CustomMessageBox.Show($"A Major error has occured : {e}");
                Application.Exit();
            }

            //set first instance display configuration
            if (DisplayConfiguration == null)
            {
                DisplayConfiguration = DisplayConfiguration.Advanced();
            }

            // load old config
            if (Settings.Instance["advancedview"] != null)
            {
                if (Settings.Instance.GetBoolean("advancedview") == true)
                {
                    DisplayConfiguration = new DisplayView().Advanced();
                }

                // remove old config
                Settings.Instance.Remove("advancedview");
            } //// load this before the other screens get loaded

            if (Settings.Instance["displayview"] != null)
            {
                try
                {
                    DisplayConfiguration = Settings.Instance.GetDisplayView("displayview");
                    //Force new view in case of saved view in config.xml
                    DisplayConfiguration.displayAdvancedParams = false;
                    DisplayConfiguration.displayStandardParams = false;
                    DisplayConfiguration.displayFullParamList = true;
                }
                catch
                {
                    DisplayConfiguration = DisplayConfiguration.Advanced();
                }
            }

            LayoutChanged += updateLayout;
            LayoutChanged(null, EventArgs.Empty);

            if (Settings.Instance["CHK_GDIPlus"] != null)
                GCSViews.FlightData.myhud.opengl = !bool.Parse(Settings.Instance["CHK_GDIPlus"].ToString());

            if (Settings.Instance["CHK_hudshow"] != null)
                GCSViews.FlightData.myhud.hudon = bool.Parse(Settings.Instance["CHK_hudshow"].ToString());

            try
            {
                if (Settings.Instance["MainLocX"] != null && Settings.Instance["MainLocY"] != null)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    Point startpos = new Point(Settings.Instance.GetInt32("MainLocX"),
                        Settings.Instance.GetInt32("MainLocY"));

                    // fix common bug which happens when user removes a monitor, the app shows up
                    // offscreen and it is very hard to move it onscreen.  Also happens with
                    // remote desktop a lot.  So this only restores position if the position
                    // is visible.
                    foreach (Screen s in Screen.AllScreens)
                    {
                        if (s.WorkingArea.Contains(startpos))
                        {
                            this.Location = startpos;
                            break;
                        }
                    }

                }

                if (Settings.Instance["MainMaximised"] != null)
                {
                    this.WindowState =
                        (FormWindowState) Enum.Parse(typeof(FormWindowState), Settings.Instance["MainMaximised"]);
                    // dont allow minimised start state
                    if (this.WindowState == FormWindowState.Minimized)
                    {
                        this.WindowState = FormWindowState.Normal;
                        this.Location = new Point(100, 100);
                    }
                }

                if (Settings.Instance["MainHeight"] != null)
                    this.Height = Settings.Instance.GetInt32("MainHeight");
                if (Settings.Instance["MainWidth"] != null)
                    this.Width = Settings.Instance.GetInt32("MainWidth");

                // set presaved default telem rates
                if (Settings.Instance["CMB_rateattitude"] != null)
                    CurrentState.rateattitudebackup = Settings.Instance.GetInt32("CMB_rateattitude");
                if (Settings.Instance["CMB_rateposition"] != null)
                    CurrentState.ratepositionbackup = Settings.Instance.GetInt32("CMB_rateposition");
                if (Settings.Instance["CMB_ratestatus"] != null)
                    CurrentState.ratestatusbackup = Settings.Instance.GetInt32("CMB_ratestatus");
                if (Settings.Instance["CMB_raterc"] != null)
                    CurrentState.ratercbackup = Settings.Instance.GetInt32("CMB_raterc");
                if (Settings.Instance["CMB_ratesensors"] != null)
                    CurrentState.ratesensorsbackup = Settings.Instance.GetInt32("CMB_ratesensors");

                //Load customfield names from config

                for (short i = 0; i < 20; i++)
                {
                    var fieldname = "customfield" + i.ToString();
                    if (Settings.Instance.ContainsKey(fieldname))
                        CurrentState.custom_field_names.Add(fieldname, Settings.Instance[fieldname].ToUpper());
                }

                // make sure rates propogate
                MainV2.comPort.MAV.cs.ResetInternals();

                if (Settings.Instance["speechenable"] != null)
                    MainV2.speechEnable = Settings.Instance.GetBoolean("speechenable");

                if (Settings.Instance["analyticsoptout"] != null)
                    MissionPlanner.Utilities.Tracking.OptOut = Settings.Instance.GetBoolean("analyticsoptout");

                try
                {
                    if (Settings.Instance["TXT_homelat"] != null)
                        MainV2.comPort.MAV.cs.PlannedHomeLocation.Lat = Settings.Instance.GetDouble("TXT_homelat");

                    if (Settings.Instance["TXT_homelng"] != null)
                        MainV2.comPort.MAV.cs.PlannedHomeLocation.Lng = Settings.Instance.GetDouble("TXT_homelng");

                    if (Settings.Instance["TXT_homealt"] != null)
                        MainV2.comPort.MAV.cs.PlannedHomeLocation.Alt = Settings.Instance.GetDouble("TXT_homealt");

                    // remove invalid entrys
                    if (Math.Abs(MainV2.comPort.MAV.cs.PlannedHomeLocation.Lat) > 90 ||
                        Math.Abs(MainV2.comPort.MAV.cs.PlannedHomeLocation.Lng) > 180)
                        MainV2.comPort.MAV.cs.PlannedHomeLocation = new PointLatLngAlt();
                }
                catch
                {
                }
            }
            catch
            {
            }

            Warnings.CustomWarning.defaultsrc = comPort.MAV.cs;
            Warnings.WarningEngine.Start(speechEnable ? speechEngine : null);
            Warnings.WarningEngine.WarningMessage += WarningEngine_WarningMessage;
            Warnings.WarningEngine.QuickPanelColoring += WarningEngine_QuickPanelColoring;

            if (CurrentState.rateattitudebackup == 0) // initilised to 10, configured above from save
            {
                CustomMessageBox.Show("NOTE: your attitude rate is 0, the hud will not work\nChange in Configuration > Planner > Telemetry Rates");
            }

            // create log dir if it doesnt exist
            try
            {
                if (!Directory.Exists(Settings.Instance.LogDir))
                    Directory.CreateDirectory(Settings.Instance.LogDir);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
#if !NETSTANDARD2_0
#if !NETCOREAPP2_0
            Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

            // make sure new enough .net framework is installed
            if (!MONO)
            {
                try
                {
                    Microsoft.Win32.RegistryKey installed_versions =
                        Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP");
                    string[] version_names = installed_versions.GetSubKeyNames();
                    //version names start with 'v', eg, 'v3.5' which needs to be trimmed off before conversion
                    double Framework = Convert.ToDouble(version_names[version_names.Length - 1].Remove(0, 1),
                        CultureInfo.InvariantCulture);
                    int SP =
                        Convert.ToInt32(installed_versions.OpenSubKey(version_names[version_names.Length - 1])
                            .GetValue("SP", 0));

                    if (Framework < 4.0)
                    {
                        CustomMessageBox.Show("This program requires .NET Framework 4.0. You currently have " + Framework);
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

#endif
#endif

            FMT.FmtBranding.ApplyApplicationIcon(this);

            Application.DoEvents();

            Comports.Add(comPort);

            MainV2.comPort.MavChanged += comPort_MavChanged;

            // save config to test we have write access
            SaveConfig();
        }

        void cmb_sysid_Click(object sender, EventArgs e)
        {
            MainV2._connectionControl.UpdateSysIDS();
        }

        void comPort_MavChanged(object sender, EventArgs e)
        {
            log.Info($"Mav Changed {MainV2.comPort.MAV.sysid}");

            HUD.Custom.src = MainV2.comPort.MAV.cs;

            CustomWarning.defaultsrc = MainV2.comPort.MAV.cs;

            MissionPlanner.Controls.PreFlight.CheckListItem.defaultsrc = MainV2.comPort.MAV.cs;

            // when uploading a firmware we dont want to reload this screen.
            if (instance.MyView.current.Control != null &&
                instance.MyView.current.Control.GetType() == typeof(GCSViews.InitialSetup))
            {
                var page = ((GCSViews.InitialSetup) instance.MyView.current.Control).backstageView.SelectedPage;
                if (page != null && page.Text.Contains("Install Firmware"))
                {
                    return;
                }
            }
        }
#if !NETSTANDARD2_0
#if !NETCOREAPP2_0
        void SystemEvents_PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            // try prevent crash on resume
            if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
            {
                doDisconnect(MainV2.comPort);
            }
        }
#endif
#endif
        private void BGLoadAirports(object nothing)
        {
            // read airport list
            try
            {
                Utilities.Airports.ReadOurairports($"{running_directory}airports.csv");

                Utilities.Airports.checkdups = true;

                //Utilities.Airports.ReadOpenflights(Application.StartupPath + Path.DirectorySeparatorChar + "airports.dat");

                log.Info("Loaded " + Utilities.Airports.GetAirportCount + " airports");
            }
            catch
            {
            }
        }

        public void switchicons(menuicons icons)
        {
            //Check if we starting
            if (displayicons != null)
            {
                // dont update if no change
                if (displayicons.GetType() == icons.GetType())
                    return;
            }

            displayicons = icons;

            MainMenu.BackColor = SystemColors.MenuBar;

            MainMenu.BackgroundImage = displayicons.bg;

            MenuFlightData.Image = displayicons.fd;
            MenuFlightPlanner.Image = displayicons.fp;
            MenuInitConfig.Image = displayicons.initsetup;
            MenuSimulation.Image = displayicons.sim;
            MenuConfigTune.Image = displayicons.config_tuning;
            MenuConnect.Image = displayicons.connect;
            MenuHelp.Image = displayicons.help;


            MenuFlightData.ForeColor = ThemeManager.TextColor;
            MenuFlightPlanner.ForeColor = ThemeManager.TextColor;
            MenuInitConfig.ForeColor = ThemeManager.TextColor;
            MenuSimulation.ForeColor = ThemeManager.TextColor;
            MenuConfigTune.ForeColor = ThemeManager.TextColor;
            MenuConnect.ForeColor = ThemeManager.TextColor;
            MenuHelp.ForeColor = ThemeManager.TextColor;
        }

        void adsb_UpdatePlanePosition(object sender, MissionPlanner.Utilities.adsb.PointLatLngAltHdg adsb)
        {
            lock (adsblock)
            {
                var id = adsb.Tag;

                if (MainV2.instance.adsbPlanes.ContainsKey(id))
                {
                    var plane = (adsb.PointLatLngAltHdg)instance.adsbPlanes[id];
                    if (plane.Source == null && sender != null)
                    {
                        log.DebugFormat("Ignoring MAVLink-sourced ADSB_VEHICLE for locally-known aircraft {0}", adsb.Tag);
                        return;
                    }

                    // update existing
                    plane.Lat = adsb.Lat;
                    plane.Lng = adsb.Lng;
                    plane.Alt = adsb.Alt;
                    plane.Heading = adsb.Heading;
                    plane.Time = DateTime.Now;
                    plane.CallSign = adsb.CallSign;
                    plane.Squawk = adsb.Squawk;
                    plane.Raw = adsb.Raw;
                    plane.Speed = adsb.Speed;
                    plane.VerticalSpeed = adsb.VerticalSpeed;
                    plane.Source = sender;
                    instance.adsbPlanes[id] = plane;
                }
                else
                {
                    // create new plane
                    MainV2.instance.adsbPlanes[id] =
                        new adsb.PointLatLngAltHdg(adsb.Lat, adsb.Lng,
                                adsb.Alt, adsb.Heading, adsb.Speed, id,
                                DateTime.Now)
                            {CallSign = adsb.CallSign, Squawk = adsb.Squawk, Raw = adsb.Raw, Source = sender};
                }
            }
        }


        private void ResetConnectionStats()
        {
            log.Info("Reset connection stats");
            // If the form has been closed, or never shown before, we need do nothing, as
            // connection stats will be reset when shown
            if (this.connectionStatsForm != null && connectionStatsForm.Visible)
            {
                // else the form is already showing.  reset the stats
                this.connectionStatsForm.Controls.Clear();
                _connectionStats = new ConnectionStats(comPort);
                this.connectionStatsForm.Controls.Add(_connectionStats);
                ThemeManager.ApplyThemeTo(this.connectionStatsForm);
            }
        }

        private void ShowConnectionStatsForm()
        {
            if (this.connectionStatsForm == null || this.connectionStatsForm.IsDisposed)
            {
                // If the form has been closed, or never shown before, we need all new stuff
                this.connectionStatsForm = new Form
                {
                    Width = 430,
                    Height = 180,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    Text = Strings.LinkStats
                };
                // Change the connection stats control, so that when/if the connection stats form is showing,
                // there will be something to see
                this.connectionStatsForm.Controls.Clear();
                _connectionStats = new ConnectionStats(comPort);
                this.connectionStatsForm.Controls.Add(_connectionStats);
                this.connectionStatsForm.Width = _connectionStats.Width;
            }

            this.connectionStatsForm.Show();
            ThemeManager.ApplyThemeTo(this.connectionStatsForm);
        }

        private void CMB_serialport_Click(object sender, EventArgs e)
        {
            string oldport = _connectionControl.CMB_serialport.Text;
            PopulateSerialportList();
            if (_connectionControl.CMB_serialport.Items.Contains(oldport))
                _connectionControl.CMB_serialport.Text = oldport;
        }

        private void PopulateSerialportList()
        {
            _connectionControl.CMB_serialport.Items.Clear();

            _connectionControl.CMB_serialport.Items.Add("AUTO");
            _connectionControl.CMB_serialport.Items.AddRange(SerialPort.GetPortNames());

            _connectionControl.CMB_serialport.Items.Add("TCP");
            _connectionControl.CMB_serialport.Items.Add("UDP");
            _connectionControl.CMB_serialport.Items.Add("UDPCl");
            _connectionControl.CMB_serialport.Items.Add("WS");

            foreach (var item in ExtraConnectionList)
            {
                _connectionControl.CMB_serialport.Items.Add(item.Label);
            }
        }

        private void MenuFlightData_Click(object sender, EventArgs e)
        {
            MyView.ShowScreen("FlightData");

            // save config
            SaveConfig();
        }

        private void MenuFlightPlanner_Click(object sender, EventArgs e)
        {
            MyView.ShowScreen("FlightPlanner");

            // save config
            SaveConfig();
        }

        public void MenuSetup_Click(object sender, EventArgs e)
        {
            if (Settings.Instance.GetBoolean("password_protect") == false)
            {
                MyView.ShowScreen("HWConfig");
            }
            else
            {
                var pw = "";
                if (InputBox.Show("Enter Password", "Please enter your password", ref pw, true) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    bool ans = Password.ValidatePassword(pw);

                    if (ans == false)
                    {
                        CustomMessageBox.Show("Bad Password", "Bad Password");
                    }
                }

                if (Password.VerifyPassword(pw))
                {
                    MyView.ShowScreen("HWConfig");
                }
            }
        }

        private void MenuSimulation_Click(object sender, EventArgs e)
        {
            MyView.ShowScreen("Simulation");
        }

        private void MenuTuning_Click(object sender, EventArgs e)
        {
            if (Settings.Instance.GetBoolean("password_protect") == false)
            {
                MyView.ShowScreen("SWConfig");
            }
            else
            {
                var pw = "";
                if (InputBox.Show("Enter Password", "Please enter your password", ref pw, true) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    bool ans = Password.ValidatePassword(pw);

                    if (ans == false)
                    {
                        CustomMessageBox.Show("Bad Password", "Bad Password");
                    }
                }

                if (Password.VerifyPassword(pw))
                {
                    MyView.ShowScreen("SWConfig");
                }
            }
        }

        private void MenuTerminal_Click(object sender, EventArgs e)
        {
            MyView.ShowScreen("Terminal");
        }

        public void doDisconnect(MAVLinkInterface comPort)
        {
            log.Info("We are disconnecting");
            try
            {
                if (speechEngine != null) // cancel all pending speech
                    speechEngine.SpeakAsyncCancelAll();

                comPort.BaseStream.DtrEnable = false;
                comPort.Close();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            // now that we have closed the connection, cancel the connection stats
            // so that the 'time connected' etc does not grow, but the user can still
            // look at the now frozen stats on the still open form
            try
            {
                // if terminal is used, then closed using this button.... exception
                if (this.connectionStatsForm != null)
                    ((ConnectionStats) this.connectionStatsForm.Controls[0]).StopUpdates();
            }
            catch
            {
            }

            // refresh config window if needed
            if (MyView.current != null)
            {
                if (MyView.current.Name == "HWConfig")
                    MyView.ShowScreen("HWConfig");
                if (MyView.current.Name == "SWConfig")
                    MyView.ShowScreen("SWConfig");
            }

            try
            {
                System.Threading.ThreadPool.QueueUserWorkItem((WaitCallback) delegate
                    {
                        try
                        {
                            MissionPlanner.Log.LogSort.SortLogs(Directory.GetFiles(Settings.Instance.LogDir, "*.tlog"));
                        }
                        catch
                        {
                        }
                    }
                );
            }
            catch
            {
            }

            this.MenuConnect.Image = global::MissionPlanner.Properties.Resources.light_connect_icon;
        }

        public void doConnect(MAVLinkInterface comPort, string portname, string baud, bool getparams = true, bool showui = true)
        {
            bool skipconnectcheck = false;
            log.Info($"We are connecting to {portname} {baud}");
            switch (portname)
            {
                case "preset":
                    skipconnectcheck = true;
                    this.BeginInvokeIfRequired(() =>
                    {
                        if (comPort.BaseStream is TcpSerial)
                            _connectionControl.CMB_serialport.Text = "TCP";
                        if (comPort.BaseStream is UdpSerial)
                            _connectionControl.CMB_serialport.Text = "UDP";
                        if (comPort.BaseStream is UdpSerialConnect)
                            _connectionControl.CMB_serialport.Text = "UDPCl";
                        if (comPort.BaseStream is SerialPort)
                        {
                            _connectionControl.CMB_serialport.Text = comPort.BaseStream.PortName;
                            _connectionControl.CMB_baudrate.Text = comPort.BaseStream.BaudRate.ToString();
                            ((SerialPort)comPort.BaseStream).espFix = Settings.Instance.GetBoolean("CHK_rtsresetesp32", false);

                        }
                    });
                    break;
                case "TCP":
                    comPort.BaseStream = new TcpSerial();
                    _connectionControl.CMB_serialport.Text = "TCP";
                    break;
                case "UDP":
                    comPort.BaseStream = new UdpSerial();
                    _connectionControl.CMB_serialport.Text = "UDP";
                    break;
                case "WS":
                    comPort.BaseStream = new WebSocket();
                    _connectionControl.CMB_serialport.Text = "WS";
                    break;
                case "UDPCl":
                    comPort.BaseStream = new UdpSerialConnect();
                    _connectionControl.CMB_serialport.Text = "UDPCl";
                    break;
                case "AUTO":
                    // do autoscan
                    Comms.CommsSerialScan.Scan(true);
                    DateTime deadline = DateTime.Now.AddSeconds(50);
                    ProgressReporterDialogue prd = new ProgressReporterDialogue();
                    prd.UpdateProgressAndStatus(-1, "Waiting for ports");
                    prd.DoWork += sender =>
                    {
                        while (Comms.CommsSerialScan.foundport == false || Comms.CommsSerialScan.run == 1)
                        {
                            System.Threading.Thread.Sleep(500);
                            Console.WriteLine("wait for port " + CommsSerialScan.foundport + " or " +
                                              CommsSerialScan.run);
                            if (sender.doWorkArgs.CancelRequested)
                            {
                                sender.doWorkArgs.CancelAcknowledged = true;
                                return;
                            }

                            if (DateTime.Now > deadline)
                            {
                                _connectionControl.IsConnected(false);
                                throw new Exception(Strings.Timeout);
                            }
                        }
                    };
                    prd.RunBackgroundOperationAsync();
                    return;
                default:
                    var extraconfig = ExtraConnectionList.Any(a => a.Label == portname);
                    if (extraconfig)
                    {
                        var config = ExtraConnectionList.First(a => a.Label == portname);
                        config.Enabled = true;
                        AutoConnect.ProcessEntry(config);
                        return;
                    }

                    var customport = CustomPortList.Any(a => a.Key.IsMatch(portname));
                    if (customport)
                    {
                        comPort.BaseStream = CustomPortList.First(a => a.Key.IsMatch(portname)).Value(portname, baud);
                    }
                    else
                    {
                        comPort.BaseStream = new SerialPort();
                        ((SerialPort)comPort.BaseStream).espFix = Settings.Instance.GetBoolean("CHK_rtsresetesp32", false);

                    }
                    break;
            }

            // Tell the connection UI that we are now connected.
            this.BeginInvokeIfRequired(() =>
            {
                _connectionControl.IsConnected(true);

                // Here we want to reset the connection stats counter etc.
                this.ResetConnectionStats();
            });

            comPort.MAV.cs.ResetInternals();

            //cleanup any log being played
            comPort.logreadmode = false;
            if (comPort.logplaybackfile != null)
                comPort.logplaybackfile.Close();
            comPort.logplaybackfile = null;

            try
            {
                log.Info("Set Portname");
                // set port, then options
                if (portname.ToLower() != "preset")
                    comPort.BaseStream.PortName = portname;

                log.Info("Set Baudrate");
                try
                {
                    if (baud != "" && baud != "0" && baud.IsNumber())
                        comPort.BaseStream.BaudRate = int.Parse(baud);
                }
                catch (Exception exp)
                {
                    log.Error(exp);
                }

                // prevent serialreader from doing anything
                comPort.giveComport = true;

                log.Info("About to do dtr if needed");
                // reset on connect logic.
                if (Settings.Instance.GetBoolean("CHK_resetapmonconnect") == true)
                {
                    log.Info("set dtr rts to false");
                    comPort.BaseStream.DtrEnable = false;
                    comPort.BaseStream.RtsEnable = false;

                    comPort.BaseStream.toggleDTR();
                }

                comPort.giveComport = false;

                // setup to record new logs
                try
                {
                    Directory.CreateDirectory(Settings.Instance.LogDir);
                    lock (this)
                    {
                        // create log names
                        var dt = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                        var tlog = Settings.Instance.LogDir + Path.DirectorySeparatorChar +
                                   dt + ".tlog";
                        var rlog = Settings.Instance.LogDir + Path.DirectorySeparatorChar +
                                   dt + ".rlog";

                        // check if this logname already exists
                        int a = 1;
                        while (File.Exists(tlog))
                        {
                            Thread.Sleep(1000);
                            // create new names with a as an index
                            dt = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + "-" + a.ToString();
                            tlog = Settings.Instance.LogDir + Path.DirectorySeparatorChar +
                                   dt + ".tlog";
                            rlog = Settings.Instance.LogDir + Path.DirectorySeparatorChar +
                                   dt + ".rlog";
                        }

                        //open the logs for writing
                        comPort.logfile =
                            new BufferedStream(
                                File.Open(tlog, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None));
                        comPort.rawlogfile =
                            new BufferedStream(
                                File.Open(rlog, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None));
                        log.Info($"creating logfile {dt}.tlog");
                    }
                }
                catch (Exception exp2)
                {
                    log.Error(exp2);
                    CustomMessageBox.Show(Strings.Failclog);
                } // soft fail

                // reset connect time - for timeout functions
                connecttime = DateTime.UtcNow;

                // do the connect
                comPort.Open(false, skipconnectcheck, showui);

                if (!comPort.BaseStream.IsOpen)
                {
                    log.Info("comport is closed. existing connect");
                    try
                    {
                        _connectionControl.IsConnected(false);
                        UpdateConnectIcon();
                        comPort.Close();
                    }
                    catch
                    {
                    }

                    return;
                }

                //158	MAV_COMP_ID_PERIPHERAL	Generic autopilot peripheral component ID. Meant for devices that do not implement the parameter microservice.
                if (getparams && comPort.MAV.compid != (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_PERIPHERAL)
                {
                    if (UseCachedParams && File.Exists(comPort.MAV.ParamCachePath) &&
                        new FileInfo(comPort.MAV.ParamCachePath).LastWriteTime > DateTime.Now.AddHours(-1))
                    {
                        File.ReadAllText(comPort.MAV.ParamCachePath).FromJSON<MAVLink.MAVLinkParamList>()
                            .ForEach(a => comPort.MAV.param.Add(a));
                        comPort.MAV.param.TotalReported = comPort.MAV.param.TotalReceived;
                    }
                    else
                    {
                        if (Settings.Instance.GetBoolean("Params_BG", false))
                        {
                            Task.Run(() =>
                            {
                                try
                                {
                                    comPort.getParamListMavftp(comPort.MAV.sysid, comPort.MAV.compid);
                                }
                                catch
                                {

                                }
                            });
                        }
                        else
                        {
                            comPort.getParamList();
                        }
                    }
                }

                // check for newer firmware
                if (showui)
                    Task.Run(() =>
                    {
                        try
                        {
                            string[] fields1 = comPort.MAV.VersionString.Split(' ');

                            var softwares = APFirmware.GetReleaseNewest(APFirmware.RELEASE_TYPES.OFFICIAL);

                            foreach (var item in softwares)
                            {
                                // check primare firmware type. ie arudplane, arducopter
                                if (fields1[0].ToLower().Contains(item.VehicleType.ToLower()))
                                {
                                    Version ver1 = VersionDetection.GetVersion(comPort.MAV.VersionString);
                                    Version ver2 = item.MavFirmwareVersion;

                                    if (ver2 > ver1)
                                    {
                                        Common.MessageShowAgain(Strings.NewFirmware + "-" + item.VehicleType + " " + ver2,
                                            Strings.NewFirmwareA + item.VehicleType + " " + ver2 + Strings.Pleaseup +
                                            "[link;https://discuss.ardupilot.org/tags/stable-release;Release Notes]");
                                        break;
                                    }

                                    // check the first hit only
                                    break;
                                }
                            }

                            // load version specific config
                            ParameterMetaDataRepositoryAPMpdef.GetMetaDataVersioned(VersionDetection.GetVersion(comPort.MAV.VersionString));
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                        }
                    });

                this.BeginInvokeIfRequired(() =>
                {
                    _connectionControl.UpdateSysIDS();

                    FlightData.CheckBatteryShow();

                    // save the baudrate for this port
                    Settings.Instance[_connectionControl.CMB_serialport.Text.Replace(" ","_") + "_BAUD"] =
                        _connectionControl.CMB_baudrate.Text;

                    this.Text = titlebar;

                    // refresh config window if needed
                    if (MyView.current != null && showui)
                    {
                        if (MyView.current.Name == "HWConfig")
                            MyView.ShowScreen("HWConfig");
                        if (MyView.current.Name == "SWConfig")
                            MyView.ShowScreen("SWConfig");
                    }

                    // load wps on connect option.
                    if (Settings.Instance.GetBoolean("loadwpsonconnect") == true && showui)
                    {
                        // only do it if we are connected.
                        if (comPort.BaseStream.IsOpen)
                        {
                            MenuFlightPlanner_Click(null, null);
                            FlightPlanner.BUT_read_Click(null, null);
                        }
                    }

                    // get any rallypoints
                    if (MainV2.comPort.MAV.param.ContainsKey("RALLY_TOTAL") &&
                        int.Parse(MainV2.comPort.MAV.param["RALLY_TOTAL"].ToString()) > 0 && showui)
                    {
                        try
                        {
                            FlightPlanner.getRallyPointsToolStripMenuItem_Click(null, null);

                            double maxdist = 0;

                            foreach (var rally in comPort.MAV.rallypoints)
                            {
                                foreach (var rally1 in comPort.MAV.rallypoints)
                                {
                                    var pnt1 = new PointLatLngAlt(rally.Value.y / 10000000.0f, rally.Value.x / 10000000.0f);
                                    var pnt2 = new PointLatLngAlt(rally1.Value.y / 10000000.0f,
                                        rally1.Value.x / 10000000.0f);

                                    var dist = pnt1.GetDistance(pnt2);

                                    maxdist = Math.Max(maxdist, dist);
                                }
                            }

                            if (comPort.MAV.param.ContainsKey("RALLY_LIMIT_KM") &&
                                (maxdist / 1000.0) > (float)comPort.MAV.param["RALLY_LIMIT_KM"])
                            {
                                CustomMessageBox.Show(Strings.Warningrallypointdistance + " " +
                                                      (maxdist / 1000.0).ToString("0.00") + " > " +
                                                      (float)comPort.MAV.param["RALLY_LIMIT_KM"]);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Warn(ex);
                        }
                    }

                    // get any fences
                    if (MainV2.comPort.MAV.param.ContainsKey("FENCE_TOTAL") &&
                        int.Parse(MainV2.comPort.MAV.param["FENCE_TOTAL"].ToString()) > 1 &&
                        MainV2.comPort.MAV.param.ContainsKey("FENCE_ACTION") && showui)
                    {
                        try
                        {
                            FlightPlanner.GeoFencedownloadToolStripMenuItem_Click(null, null);
                        }
                        catch (Exception ex)
                        {
                            log.Warn(ex);
                        }
                    }

                    //Add HUD custom items source
                    HUD.Custom.src = MainV2.comPort.MAV.cs;

                    // set connected icon
                    this.MenuConnect.Image = displayicons.disconnect;
                });
            }
            catch (Exception ex)
            {
                log.Warn(ex);
                try
                {
                    _connectionControl.IsConnected(false);
                    UpdateConnectIcon();
                    comPort.Close();
                }
                catch (Exception ex2)
                {
                    log.Warn(ex2);
                }

                CustomMessageBox.Show($"Can not establish a connection\n\n{ex.Message}");
                return;
            }
        }


        private void MenuConnect_Click(object sender, EventArgs e)
        {
            Connect();

            // save config
            SaveConfig();
        }

        private void Connect()
        {
            comPort.giveComport = false;

            log.Info("MenuConnect Start");

            // sanity check
            if (comPort.BaseStream.IsOpen && comPort.MAV.cs.groundspeed > 4)
            {
                if ((int) DialogResult.No ==
                    CustomMessageBox.Show(Strings.Stillmoving, Strings.Disconnect, MessageBoxButtons.YesNo))
                {
                    return;
                }
            }

            try
            {
                log.Info("Cleanup last logfiles");
                // cleanup from any previous sessions
                if (comPort.logfile != null)
                    comPort.logfile.Close();

                if (comPort.rawlogfile != null)
                    comPort.rawlogfile.Close();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(Strings.ErrorClosingLogFile + ex.Message, Strings.ERROR);
            }

            comPort.logfile = null;
            comPort.rawlogfile = null;

            // decide if this is a connect or disconnect
            if (comPort.BaseStream.IsOpen)
            {
                doDisconnect(comPort);
            }
            else
            {
                doConnect(comPort, _connectionControl.CMB_serialport.Text, _connectionControl.CMB_baudrate.Text);
            }

            _connectionControl.UpdateSysIDS();

            if (comPort.BaseStream.IsOpen)
                loadph_serial();
        }

        void loadph_serial()
        {
            try
            {
                if (comPort.MAV.SerialString == "")
                    return;

                if (comPort.MAV.SerialString.Contains("CubeBlack") &&
                    !comPort.MAV.SerialString.Contains("CubeBlack+") &&
                    comPort.MAV.param.ContainsKey("INS_ACC3_ID") && comPort.MAV.param["INS_ACC3_ID"].Value == 0 &&
                    comPort.MAV.param.ContainsKey("INS_GYR3_ID") && comPort.MAV.param["INS_GYR3_ID"].Value == 0 &&
                    comPort.MAV.param.ContainsKey("INS_ENABLE_MASK") && comPort.MAV.param["INS_ENABLE_MASK"].Value >= 7)
                {
                    MissionPlanner.Controls.SB.Show("Param Scan");
                }
            }
            catch
            {
            }

            try
            {
                if (comPort.MAV.SerialString == "")
                    return;

                // brd type should be 3
                // devids show which sensor is not detected
                // baro does not list a devid

                //devop read spi lsm9ds0_ext_am 0 0 0x8f 1
                if (comPort.MAV.SerialString.Contains("CubeBlack") && !comPort.MAV.SerialString.Contains("CubeBlack+"))
                {
                    Task.Run(() =>
                    {
                        bool bad1 = false;
                        byte[] data = new byte[0];

                        comPort.device_op(comPort.MAV.sysid, comPort.MAV.compid, out data,
                            MAVLink.DEVICE_OP_BUSTYPE.SPI,
                            "lsm9ds0_ext_g", 0, 0, 0x8f, 1);
                        if (data.Length != 0 && (data[0] != 0xd4 && data[0] != 0xd7))
                            bad1 = true;

                        comPort.device_op(comPort.MAV.sysid, comPort.MAV.compid, out data,
                            MAVLink.DEVICE_OP_BUSTYPE.SPI,
                            "lsm9ds0_ext_am", 0, 0, 0x8f, 1);
                        if (data.Length != 0 && data[0] != 0x49)
                            bad1 = true;

                        if (bad1)
                            this.BeginInvoke(method: (Action) delegate
                            {
                                MissionPlanner.Controls.SB.Show("SPI Scan");
                            });
                    });
                }

            }
            catch
            {
            }
        }

        private void CMB_serialport_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_connectionControl.CMB_serialport.SelectedItem == _connectionControl.CMB_serialport.Text)
                return;

            comPortName = _connectionControl.CMB_serialport.Text;
            if (comPortName == "UDP" || comPortName == "UDPCl" || comPortName == "TCP" || comPortName == "AUTO")
            {
                _connectionControl.CMB_baudrate.Enabled = false;
            }
            else
            {
                _connectionControl.CMB_baudrate.Enabled = true;
            }

            try
            {
                // check for saved baud rate and restore
                if (Settings.Instance[_connectionControl.CMB_serialport.Text.Replace(" ", "_") + "_BAUD"] != null)
                {
                    _connectionControl.CMB_baudrate.Text =
                        Settings.Instance[_connectionControl.CMB_serialport.Text.Replace(" ", "_") + "_BAUD"];
                }
            }
            catch
            {
            }
        }


        /// <summary>
        /// Confirm before disposing views, stopping workers or closing transports.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            // Cleanup pumps messages; do not run it twice on reentry.
            if (fmtShutdownStarted) { e.Cancel = true; return; }
            if (fmtConnectionCloseGuard.ShouldCancel(e.CloseReason,
                HasFmtOpenTelemetryConnection(), FlightData?.HasRunningMqttBridge == true,
                message => MessageBox.Show(this, message, "連線中，確定要關閉？",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)))
            {
                e.Cancel = true;
                return;
            }
            fmtShutdownStarted = true;

            log.Info("MainV2_FormClosing");

            LayoutChanged -= updateLayout;
            MainV2.comPort.MavChanged -= comPort_MavChanged;
            Warnings.WarningEngine.WarningMessage -= WarningEngine_WarningMessage;
            Warnings.WarningEngine.QuickPanelColoring -= WarningEngine_QuickPanelColoring;
#if !NETSTANDARD2_0
#if !NETCOREAPP2_0
            Microsoft.Win32.SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
#endif
#endif

            log.Info("GMaps write cache");
            // speed up tile saving on exit
            GMap.NET.GMaps.Instance.CacheOnIdleRead = false;
            GMap.NET.GMaps.Instance.BoostCacheEngine = true;

            Settings.Instance["MainHeight"] = this.Height.ToString();
            Settings.Instance["MainWidth"] = this.Width.ToString();
            Settings.Instance["MainMaximised"] = this.WindowState.ToString();

            Settings.Instance["MainLocX"] = this.Location.X.ToString();
            Settings.Instance["MainLocY"] = this.Location.Y.ToString();

            log.Info("close logs");

            // close bases connection
            try
            {
                comPort.logreadmode = false;
                if (comPort.logfile != null)
                    comPort.logfile.Close();

                if (comPort.rawlogfile != null)
                    comPort.rawlogfile.Close();

                comPort.logfile = null;
                comPort.rawlogfile = null;
            }
            catch
            {
            }

            log.Info("close ports");
            // close all connections
            foreach (var port in Comports)
            {
                try
                {
                    port.logreadmode = false;
                    if (port.logfile != null)
                        port.logfile.Close();

                    if (port.rawlogfile != null)
                        port.rawlogfile.Close();

                    port.logfile = null;
                    port.rawlogfile = null;
                }
                catch
                {
                }
            }

            log.Info("stop adsb");
            Utilities.adsb.Stop();

            log.Info("stop WarningEngine");
            Warnings.WarningEngine.Stop();

            log.Info("stop GStreamer");
            GCSViews.FlightData.hudGStreamer.Stop();

            log.Info("closing vlcrender");
            try
            {
                while (vlcrender.store.Count > 0)
                    vlcrender.store[0].Stop();
            }
            catch
            {
            }

            log.Info("closing pluginthread");

            pluginthreadrun = false;

            if (pluginthread != null)
            {
                try
                {
                    while (!PluginThreadrunner.WaitOne(100)) Application.DoEvents();
                }
                catch
                {
                }

                pluginthread.Join();
            }

            log.Info("closing serialthread");

            serialThread = false;

            log.Info("closing adsbthread");

            adsbThread = false;

            log.Info("closing joystickthread");

            joystickthreadrun = false;

            log.Info("closing httpthread");

            // if we are waiting on a socket we need to force an abort
            httpserver.Stop();

            log.Info("sorting tlogs");
            try
            {
                System.Threading.ThreadPool.QueueUserWorkItem((WaitCallback) delegate
                    {
                        try
                        {
                            MissionPlanner.Log.LogSort.SortLogs(Directory.GetFiles(Settings.Instance.LogDir, "*.tlog"));
                        }
                        catch
                        {
                        }
                    }
                );
            }
            catch
            {
            }

            log.Info("closing MyView");

            // close all tabs
            MyView.Dispose();

            log.Info("closing fd");
            try
            {
                FlightData.Dispose();
            }
            catch
            {
            }

            log.Info("closing fp");
            try
            {
                FlightPlanner.Dispose();
            }
            catch
            {
            }

            log.Info("closing sim");
            try
            {
                Simulation?.Dispose();
            }
            catch
            {
            }

            try
            {
                if (comPort.BaseStream.IsOpen)
                    comPort.Close();
            }
            catch
            {
            } // i get alot of these errors, the port is still open, but not valid - user has unpluged usb

            // save config
            SaveConfig();

            Console.WriteLine(httpthread?.IsAlive);
            Console.WriteLine(pluginthread?.IsAlive);

            log.Info("MainV2_FormClosing done");

            if (MONO)
                this.Dispose();
        }

        private static bool HasFmtOpenTelemetryConnection()
        {
            try
            {
                if (comPort?.BaseStream?.IsOpen == true) return true;
                // Protect every active vehicle, not just the selected vehicle.
                return Comports != null && Comports.ToArray().Any(port => port?.BaseStream?.IsOpen == true);
            }
            catch (Exception ex)
            {
                log.Debug("Unable to inspect connection state before closing; requesting confirmation", ex);
                return true; // An uncertain transport state should still prompt.
            }
        }


        /// <summary>
        /// this happens after FormClosing...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            MenuFmtMqttTraffic?.Dispose();
            base.OnFormClosed(e);

            Console.WriteLine("MainV2_FormClosed");

            if (joystick != null)
            {
                while (!joysendThreadExited)
                    Thread.Sleep(10);

                joystick.Dispose(); //proper clean up of joystick.
            }

            foreach (var image in FmtQuickActionBackgrounds.Values)
                image.Dispose();
            FmtQuickActionBackgrounds.Clear();
        }

        private void LoadConfig()
        {
            try
            {
                log.Info("Loading config");

                Settings.Instance.Load();

                comPortName = Settings.Instance.ComPort;
            }
            catch (Exception ex)
            {
                log.Error("Bad Config File", ex);
            }
        }

        private void SaveConfig()
        {
            try
            {
                log.Info("Saving config");
                Settings.Instance.ComPort = comPortName;

                if (_connectionControl != null)
                    Settings.Instance.BaudRate = _connectionControl.CMB_baudrate.Text;

                Settings.Instance.APMFirmware = MainV2.comPort.MAV.cs.firmware.ToString();

                Settings.Instance.Save();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.ToString());
            }
        }

        /// <summary>
        /// needs to be true by default so that exits properly if no joystick used.
        /// </summary>
        volatile private bool joysendThreadExited = true;

        /// <summary>
        /// thread used to send joystick packets to the MAV
        /// </summary>
        private async void joysticksend()
        {
            float rate = 50; // 1000 / 50 = 20 hz
            int count = 0;

            DateTime lastratechange = DateTime.Now;

            joystickthreadrun = true;

            while (joystickthreadrun)
            {
                joysendThreadExited = false;
                //so we know this thread is stil alive.
                try
                {
                    if (MONO)
                    {
                        log.Error("Mono: closing joystick thread");
                        break;
                    }

                    if (!MONO)
                    {
                        //joystick stuff

                        if (joystick != null && joystick.enabled)
                        {
                            if (!joystick.manual_control)
                            {
                                MAVLink.mavlink_rc_channels_override_t
                                    rc = new MAVLink.mavlink_rc_channels_override_t();

                                rc.target_component = comPort.MAV.compid;
                                rc.target_system = comPort.MAV.sysid;

                                if (joystick.getJoystickAxis(1) == Joystick.joystickaxis.None)
                                    rc.chan1_raw = ushort.MaxValue;
                                if (joystick.getJoystickAxis(2) == Joystick.joystickaxis.None)
                                    rc.chan2_raw = ushort.MaxValue;
                                if (joystick.getJoystickAxis(3) == Joystick.joystickaxis.None)
                                    rc.chan3_raw = ushort.MaxValue;
                                if (joystick.getJoystickAxis(4) == Joystick.joystickaxis.None)
                                    rc.chan4_raw = ushort.MaxValue;
                                if (joystick.getJoystickAxis(5) == Joystick.joystickaxis.None)
                                    rc.chan5_raw = ushort.MaxValue;
                                if (joystick.getJoystickAxis(6) == Joystick.joystickaxis.None)
                                    rc.chan6_raw = ushort.MaxValue;
                                if (joystick.getJoystickAxis(7) == Joystick.joystickaxis.None)
                                    rc.chan7_raw = ushort.MaxValue;
                                if (joystick.getJoystickAxis(8) == Joystick.joystickaxis.None)
                                    rc.chan8_raw = ushort.MaxValue;
                                if (joystick.getJoystickAxis(9) == Joystick.joystickaxis.None)
                                    rc.chan9_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(10) == Joystick.joystickaxis.None)
                                    rc.chan10_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(11) == Joystick.joystickaxis.None)
                                    rc.chan11_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(12) == Joystick.joystickaxis.None)
                                    rc.chan12_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(13) == Joystick.joystickaxis.None)
                                    rc.chan13_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(14) == Joystick.joystickaxis.None)
                                    rc.chan14_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(15) == Joystick.joystickaxis.None)
                                    rc.chan15_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(16) == Joystick.joystickaxis.None)
                                    rc.chan16_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(17) == Joystick.joystickaxis.None)
                                    rc.chan17_raw = (ushort) 0;
                                if (joystick.getJoystickAxis(18) == Joystick.joystickaxis.None)
                                    rc.chan18_raw = (ushort) 0;

                                if (joystick.getJoystickAxis(1) != Joystick.joystickaxis.None)
                                    rc.chan1_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech1;
                                if (joystick.getJoystickAxis(2) != Joystick.joystickaxis.None)
                                    rc.chan2_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech2;
                                if (joystick.getJoystickAxis(3) != Joystick.joystickaxis.None)
                                    rc.chan3_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech3;
                                if (joystick.getJoystickAxis(4) != Joystick.joystickaxis.None)
                                    rc.chan4_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech4;
                                if (joystick.getJoystickAxis(5) != Joystick.joystickaxis.None)
                                    rc.chan5_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech5;
                                if (joystick.getJoystickAxis(6) != Joystick.joystickaxis.None)
                                    rc.chan6_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech6;
                                if (joystick.getJoystickAxis(7) != Joystick.joystickaxis.None)
                                    rc.chan7_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech7;
                                if (joystick.getJoystickAxis(8) != Joystick.joystickaxis.None)
                                    rc.chan8_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech8;
                                if (joystick.getJoystickAxis(9) != Joystick.joystickaxis.None)
                                    rc.chan9_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech9;
                                if (joystick.getJoystickAxis(10) != Joystick.joystickaxis.None)
                                    rc.chan10_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech10;
                                if (joystick.getJoystickAxis(11) != Joystick.joystickaxis.None)
                                    rc.chan11_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech11;
                                if (joystick.getJoystickAxis(12) != Joystick.joystickaxis.None)
                                    rc.chan12_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech12;
                                if (joystick.getJoystickAxis(13) != Joystick.joystickaxis.None)
                                    rc.chan13_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech13;
                                if (joystick.getJoystickAxis(14) != Joystick.joystickaxis.None)
                                    rc.chan14_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech14;
                                if (joystick.getJoystickAxis(15) != Joystick.joystickaxis.None)
                                    rc.chan15_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech15;
                                if (joystick.getJoystickAxis(16) != Joystick.joystickaxis.None)
                                    rc.chan16_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech16;
                                if (joystick.getJoystickAxis(17) != Joystick.joystickaxis.None)
                                    rc.chan17_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech17;
                                if (joystick.getJoystickAxis(18) != Joystick.joystickaxis.None)
                                    rc.chan18_raw = (ushort) MainV2.comPort.MAV.cs.rcoverridech18;

                                if (lastjoystick.AddMilliseconds(rate) < DateTime.Now)
                                {
                                    /*
                                if (MainV2.comPort.MAV.cs.rssi > 0 && MainV2.comPort.MAV.cs.remrssi > 0)
                                {
                                    if (lastratechange.Second != DateTime.Now.Second)
                                    {
                                        if (MainV2.comPort.MAV.cs.txbuffer > 90)
                                        {
                                            if (rate < 20)
                                                rate = 21;
                                            rate--;

                                            if (MainV2.comPort.MAV.cs.linkqualitygcs < 70)
                                                rate = 50;
                                        }
                                        else
                                        {
                                            if (rate > 100)
                                                rate = 100;
                                            rate++;
                                        }

                                        lastratechange = DateTime.Now;
                                    }

                                }
                                */
                                    //                                Console.WriteLine(DateTime.Now.Millisecond + " {0} {1} {2} {3} {4}", rc.chan1_raw, rc.chan2_raw, rc.chan3_raw, rc.chan4_raw,rate);

                                    //Console.WriteLine("Joystick btw " + comPort.BaseStream.BytesToWrite);

                                    if (!comPort.BaseStream.IsOpen)
                                        continue;

                                    if (comPort.BaseStream.BytesToWrite < 50)
                                    {
                                        if (sitl)
                                        {
                                            MissionPlanner.GCSViews.SITL.rcinput();
                                        }
                                        else
                                        {
                                            comPort.sendPacket(rc, rc.target_system, rc.target_component);
                                        }

                                        count++;
                                        lastjoystick = DateTime.Now;
                                    }
                                }
                            }
                            else
                            {
                                MAVLink.mavlink_manual_control_t rc = new MAVLink.mavlink_manual_control_t();

                                rc.target = comPort.MAV.compid;

                                if (joystick.getJoystickAxis(1) != Joystick.joystickaxis.None)
                                    rc.x = MainV2.comPort.MAV.cs.rcoverridech1;
                                if (joystick.getJoystickAxis(2) != Joystick.joystickaxis.None)
                                    rc.y = MainV2.comPort.MAV.cs.rcoverridech2;
                                if (joystick.getJoystickAxis(3) != Joystick.joystickaxis.None)
                                    rc.z = MainV2.comPort.MAV.cs.rcoverridech3;
                                if (joystick.getJoystickAxis(4) != Joystick.joystickaxis.None)
                                    rc.r = MainV2.comPort.MAV.cs.rcoverridech4;

                                if (lastjoystick.AddMilliseconds(rate) < DateTime.Now)
                                {
                                    if (!comPort.BaseStream.IsOpen)
                                        continue;

                                    if (comPort.BaseStream.BytesToWrite < 50)
                                    {
                                        if (sitl)
                                        {
                                            MissionPlanner.GCSViews.SITL.rcinput();
                                        }
                                        else
                                        {
                                            comPort.sendPacket(rc, comPort.MAV.sysid, comPort.MAV.compid);
                                        }

                                        count++;
                                        lastjoystick = DateTime.Now;
                                    }
                                }
                            }
                        }
                    }

                    await Task.Delay(40).ConfigureAwait(false);
                }
                catch
                {
                } // cant fall out
            }

            joysendThreadExited = true; //so we know this thread exited.
        }

        /// <summary>
        /// Used to fix the icon status for unexpected unplugs etc...
        /// </summary>
        private void UpdateConnectIcon()
        {
            if ((DateTime.UtcNow - connectButtonUpdate).TotalMilliseconds > 500)
            {
                //                        Console.WriteLine(DateTime.Now.Millisecond);
                if (comPort.BaseStream.IsOpen)
                {
                    if (this.MenuConnect.Image == null || (string) this.MenuConnect.Image.Tag != "Disconnect")
                    {
                        this.BeginInvoke((MethodInvoker) delegate
                        {
                            this.MenuConnect.Image = displayicons.disconnect;
                            this.MenuConnect.Image.Tag = "Disconnect";
                            this.MenuConnect.Text = Strings.DISCONNECTc;
                            _connectionControl.IsConnected(true);
                        });
                    }
                }
                else
                {
                    if (this.MenuConnect.Image != null && (string) this.MenuConnect.Image.Tag != "Connect")
                    {
                        this.BeginInvoke((MethodInvoker) delegate
                        {
                            this.MenuConnect.Image = displayicons.connect;
                            this.MenuConnect.Image.Tag = "Connect";
                            this.MenuConnect.Text = Strings.CONNECTc;
                            _connectionControl.IsConnected(false);
                            if (_connectionStats != null)
                            {
                                _connectionStats.StopUpdates();
                            }
                        });
                    }

                    if (comPort.logreadmode)
                    {
                        this.BeginInvoke((MethodInvoker) delegate { _connectionControl.IsConnected(true); });
                    }
                }

                connectButtonUpdate = DateTime.UtcNow;
                UpdateFmtQuickActionButtons();
            }
        }

        private void MenuFmtParameterSettings_Click(object sender, EventArgs e)
        {
            using (var access = new FMT.FmtParameterAccessForm())
            {
                ThemeManager.ApplyThemeTo(access);
                if (access.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            MyView.ShowScreen("FMTParameterSettings");
            SaveConfig();
        }

        ManualResetEvent PluginThreadrunner = new ManualResetEvent(false);

        private void PluginThread()
        {
            Hashtable nextrun = new Hashtable();

            pluginthreadrun = true;

            PluginThreadrunner.Reset();

            while (pluginthreadrun)
            {
                DateTime minnextrun = DateTime.Now.AddMilliseconds(1000);
                try
                {
                    foreach (var plugin in Plugin.PluginLoader.Plugins.ToArray())
                    {
                        if (!nextrun.ContainsKey(plugin))
                            nextrun[plugin] = DateTime.MinValue;

                        if ((DateTime.Now > plugin.NextRun) && (plugin.loopratehz > 0))
                        {
                            // get ms till next run
                            int msnext = (int) (1000 / plugin.loopratehz);

                            // allow the plug to modify this, if needed
                            plugin.NextRun = DateTime.Now.AddMilliseconds(msnext);

                            if (plugin.NextRun < minnextrun)
                                minnextrun = plugin.NextRun;

                            try
                            {
                                bool ans = plugin.Loop();
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                            }
                        }
                    }
                }
                catch
                {
                }

                var sleepms = (int) ((minnextrun - DateTime.Now).TotalMilliseconds);
                // max rate is 100 hz - prevent massive cpu usage
                if (sleepms > 0)
                    System.Threading.Thread.Sleep(sleepms);
            }

            while (Plugin.PluginLoader.Plugins.Count > 0)
            {
                var plugin = Plugin.PluginLoader.Plugins[0];
                try
                {
                    plugin.Exit();
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }

                Plugin.PluginLoader.Plugins.Remove(plugin);
            }

            try
            {
                PluginThreadrunner.Set();
            }
            catch
            {
            }

            return;
        }

        ManualResetEvent SerialThreadrunner = new ManualResetEvent(false);

        /// <summary>
        /// main serial reader thread
        /// controls
        /// serial reading
        /// link quality stats
        /// speech voltage - custom - alt warning - data lost
        /// heartbeat packet sending
        ///
        /// and can't fall out
        /// </summary>
        private async void SerialReader()
        {
            if (serialThread == true)
                return;
            serialThread = true;

            SerialThreadrunner.Reset();

            int minbytes = 10;

            int altwarningmax = 0;

            bool armedstatus = false;

            string lastmessagehigh = "";

            DateTime speechcustomtime = DateTime.Now;

            DateTime speechlowspeedtime = DateTime.Now;

            DateTime linkqualitytime = DateTime.Now;

            while (serialThread)
            {
                try
                {
                    await Task.Delay(1).ConfigureAwait(false); // was 5

                    try
                    {
                        if (ConfigTerminal.comPort is MAVLinkSerialPort)
                        {
                        }
                        else
                        {
                            if (ConfigTerminal.comPort != null && ConfigTerminal.comPort.IsOpen)
                                continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }

                    // update connect/disconnect button and info stats
                    try
                    {
                        UpdateConnectIcon();
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }

                    // 30 seconds interval speech options
                    if (speechEnabled() && (DateTime.UtcNow - speechcustomtime).TotalSeconds > 30 &&
                        (MainV2.comPort.logreadmode || comPort.BaseStream.IsOpen))
                    {
                        if (MainV2.speechEngine.IsReady)
                        {
                            if (Settings.Instance.GetBoolean("speechcustomenabled"))
                            {
                                MainV2.speechEngine.SpeakAsync(ArduPilot.Common.speechConversion(comPort.MAV,
                                    "" + Settings.Instance["speechcustom"]));
                            }

                            speechcustomtime = DateTime.UtcNow;
                        }

                        // speech for battery alerts
                        //speechbatteryvolt
                        float warnvolt = Settings.Instance.GetFloat("speechbatteryvolt");
                        float warnpercent = Settings.Instance.GetFloat("speechbatterypercent");

                        if (Settings.Instance.GetBoolean("speechbatteryenabled") == true &&
                            MainV2.comPort.MAV.cs.battery_voltage <= warnvolt &&
                            MainV2.comPort.MAV.cs.battery_voltage >= 5.0)
                        {
                            if (MainV2.speechEngine.IsReady)
                            {
                                MainV2.speechEngine.SpeakAsync(ArduPilot.Common.speechConversion(comPort.MAV,
                                    "" + Settings.Instance["speechbattery"]));
                            }
                        }
                        else if (Settings.Instance.GetBoolean("speechbatteryenabled") == true &&
                                 (MainV2.comPort.MAV.cs.battery_remaining) < warnpercent &&
                                 MainV2.comPort.MAV.cs.battery_voltage >= 5.0 &&
                                 MainV2.comPort.MAV.cs.battery_remaining != 0.0)
                        {
                            if (MainV2.speechEngine.IsReady)
                            {
                                MainV2.speechEngine.SpeakAsync(
                                    ArduPilot.Common.speechConversion(comPort.MAV,
                                        "" + Settings.Instance["speechbattery"]));
                            }
                        }
                    }

                    // speech for airspeed alerts
                    if (speechEnabled() && (DateTime.UtcNow - speechlowspeedtime).TotalSeconds > 10 &&
                        (MainV2.comPort.logreadmode || comPort.BaseStream.IsOpen))
                    {
                        if (Settings.Instance.GetBoolean("speechlowspeedenabled") == true &&
                            MainV2.comPort.MAV.cs.armed)
                        {
                            float warngroundspeed = Settings.Instance.GetFloat("speechlowgroundspeedtrigger");
                            float warnairspeed = Settings.Instance.GetFloat("speechlowairspeedtrigger");

                            if (MainV2.comPort.MAV.cs.airspeed < warnairspeed)
                            {
                                if (MainV2.speechEngine.IsReady)
                                {
                                    MainV2.speechEngine.SpeakAsync(
                                        ArduPilot.Common.speechConversion(comPort.MAV,
                                            "" + Settings.Instance["speechlowairspeed"]));
                                    speechlowspeedtime = DateTime.UtcNow;
                                }
                            }
                            else if (MainV2.comPort.MAV.cs.groundspeed < warngroundspeed)
                            {
                                if (MainV2.speechEngine.IsReady)
                                {
                                    MainV2.speechEngine.SpeakAsync(
                                        ArduPilot.Common.speechConversion(comPort.MAV,
                                            "" + Settings.Instance["speechlowgroundspeed"]));
                                    speechlowspeedtime = DateTime.UtcNow;
                                }
                            }
                            else
                            {
                                speechlowspeedtime = DateTime.UtcNow;
                            }
                        }
                    }

                    // speech altitude warning - message high warning
                    if (speechEnabled() &&
                        (MainV2.comPort.logreadmode || comPort.BaseStream.IsOpen))
                    {
                        float warnalt = float.MaxValue;
                        if (Settings.Instance.ContainsKey("speechaltheight"))
                        {
                            warnalt = Settings.Instance.GetFloat("speechaltheight");
                        }

                        try
                        {
                            altwarningmax = (int) Math.Max(MainV2.comPort.MAV.cs.alt, altwarningmax);

                            if (Settings.Instance.GetBoolean("speechaltenabled") == true &&
                                MainV2.comPort.MAV.cs.alt != 0.00 &&
                                (MainV2.comPort.MAV.cs.alt <= warnalt) && MainV2.comPort.MAV.cs.armed)
                            {
                                if (altwarningmax > warnalt)
                                {
                                    if (MainV2.speechEngine.IsReady)
                                        MainV2.speechEngine.SpeakAsync(
                                            ArduPilot.Common.speechConversion(comPort.MAV,
                                                "" + Settings.Instance["speechalt"]));
                                }
                            }
                        }
                        catch
                        {
                        } // silent fail


                        try
                        {
                            // say the latest high priority message
                            if (MainV2.speechEngine.IsReady &&
                                lastmessagehigh != MainV2.comPort.MAV.cs.messageHigh &&
                                MainV2.comPort.MAV.cs.messageHigh != null)
                            {
                                if (!MainV2.comPort.MAV.cs.messageHigh.StartsWith("PX4v2 ") &&
                                    !MainV2.comPort.MAV.cs.messageHigh.StartsWith("PreArm:")) // Supress audibly repeating PreArm messages
                                {
                                    MainV2.speechEngine.SpeakAsync(MainV2.comPort.MAV.cs.messageHigh);
                                    lastmessagehigh = MainV2.comPort.MAV.cs.messageHigh;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    // not doing anything
                    if (!MainV2.comPort.logreadmode && !comPort.BaseStream.IsOpen)
                    {
                        altwarningmax = 0;
                    }

                    // attenuate the link qualty over time
                    if ((DateTime.UtcNow - MainV2.comPort.MAV.lastvalidpacket).TotalSeconds >= 1)
                    {
                        if (linkqualitytime.Second != DateTime.UtcNow.Second)
                        {
                            MainV2.comPort.MAV.cs.linkqualitygcs =
                                (ushort) (MainV2.comPort.MAV.cs.linkqualitygcs * 0.8f);
                            linkqualitytime = DateTime.UtcNow;

                            // force redraw if there are no other packets are being read
                            this.BeginInvokeIfRequired(
                                (Action)
                                delegate { GCSViews.FlightData.myhud.Invalidate(); });
                        }
                    }

                    // data loss warning - wait min of 3 seconds, ignore first 30 seconds of connect, repeat at 5 seconds interval
                    if ((DateTime.UtcNow - MainV2.comPort.MAV.lastvalidpacket).TotalSeconds > 3
                        && (DateTime.UtcNow - connecttime).TotalSeconds > 30
                        && (DateTime.UtcNow - nodatawarning).TotalSeconds > 5
                        && (MainV2.comPort.logreadmode || comPort.BaseStream.IsOpen)
                        && MainV2.comPort.MAV.cs.armed)
                    {
                        var msg = "WARNING No Data for " + (int)(DateTime.UtcNow - MainV2.comPort.MAV.lastvalidpacket).TotalSeconds + " Seconds";
                        MainV2.comPort.MAV.cs.messageHigh = msg;
                        if (speechEnabled())
                        {
                            if (MainV2.speechEngine.IsReady)
                            {
                                MainV2.speechEngine.SpeakAsync(msg);
                                nodatawarning = DateTime.UtcNow;
                            }
                        }
                    }

                    // get home point on armed status change.
                    if (armedstatus != MainV2.comPort.MAV.cs.armed && comPort.BaseStream.IsOpen)
                    {
                        armedstatus = MainV2.comPort.MAV.cs.armed;
                        // status just changed to armed
                        if (MainV2.comPort.MAV.cs.armed == true &&
                            MainV2.comPort.MAV.apname != MAVLink.MAV_AUTOPILOT.INVALID &&
                            MainV2.comPort.MAV.aptype != MAVLink.MAV_TYPE.GIMBAL)
                        {
                            System.Threading.ThreadPool.QueueUserWorkItem(state =>
                            {
                                Thread.CurrentThread.Name = "Arm State change";
                                try
                                {
                                    while (comPort.giveComport == true)
                                        Thread.Sleep(100);

                                    MainV2.comPort.MAV.cs.HomeLocation = new PointLatLngAlt(MainV2.comPort.getWP(0));
                                    if (MyView.current != null && MyView.current.Name == "FlightPlanner")
                                    {
                                        // update home if we are on flight data tab
                                        this.BeginInvokeIfRequired((Action) delegate { FlightPlanner.updateHome(); });
                                    }
                                }
                                catch
                                {
                                    // dont hang this loop
                                    this.BeginInvokeIfRequired(
                                        (Action)
                                        delegate
                                        {
                                            CustomMessageBox.Show("Failed to update home location (" +
                                                                  MainV2.comPort.MAV.sysid + ")");
                                        });
                                }
                            });
                        }

                        if (speechEnable && speechEngine != null)
                        {
                            if (Settings.Instance.GetBoolean("speecharmenabled"))
                            {
                                string speech = armedstatus
                                    ? Settings.Instance["speecharm"]
                                    : Settings.Instance["speechdisarm"];
                                if (!string.IsNullOrEmpty(speech))
                                {
                                    MainV2.speechEngine.SpeakAsync(
                                        ArduPilot.Common.speechConversion(comPort.MAV, speech));
                                }
                            }
                        }
                    }

                    if (comPort.MAV.param.TotalReceived < comPort.MAV.param.TotalReported)
                    {
                        if (comPort.MAV.param.TotalReported > 0 && comPort.BaseStream.IsOpen)
                        {
                            this.BeginInvokeIfRequired(() =>
                            {
                                try
                                {
                                    instance.status1.Percent =
                                        (comPort.MAV.param.TotalReceived / (double) comPort.MAV.param.TotalReported) *
                                        100.0;
                                }
                                catch (Exception e)
                                {
                                    log.Error(e);
                                }
                            });
                        }
                    }

                    // send a hb every seconds from gcs to ap
                    if (heatbeatSend.Second != DateTime.UtcNow.Second)
                    {
                        MAVLink.mavlink_heartbeat_t htb = new MAVLink.mavlink_heartbeat_t()
                        {
                            type = (byte) MAVLink.MAV_TYPE.GCS,
                            autopilot = (byte) MAVLink.MAV_AUTOPILOT.INVALID,
                            mavlink_version = 3 // MAVLink.MAVLINK_VERSION
                        };

                        // enumerate each link
                        foreach (var port in Comports.ToArray())
                        {
                            if (!port.BaseStream.IsOpen)
                                continue;

                            // poll for params at heartbeat interval - primary mav on this port only
                            if (!port.giveComport)
                            {
                                try
                                {
                                    // poll only when not armed
                                    if (!port.MAV.cs.armed && DateTime.UtcNow > connecttime.AddSeconds(60))
                                    {
                                        port.getParamPoll();
                                        port.getParamPoll();
                                    }
                                }
                                catch
                                {
                                }
                            }

                            // there are 3 hb types we can send, mavlink1, mavlink2 signed and unsigned
                            bool sentsigned = false;
                            bool sentmavlink1 = false;
                            bool sentmavlink2 = false;

                            // enumerate each mav
                            foreach (var MAV in port.MAVlist)
                            {
                                try
                                {
                                    // poll for version if we dont have it - every mav every port
                                    if (!port.giveComport && MAV.cs.capabilities == 0 &&
                                        (DateTime.Now.Second % 20) == 0 && MAV.cs.version < new Version(0, 1))
                                        port.getVersion(MAV.sysid, MAV.compid, false);

                                    // are we talking to a mavlink2 device
                                    if (MAV.mavlinkv2)
                                    {
                                        // is signing enabled
                                        if (MAV.signing)
                                        {
                                            // check if we have already sent
                                            if (sentsigned)
                                                continue;
                                            sentsigned = true;
                                        }
                                        else
                                        {
                                            // check if we have already sent
                                            if (sentmavlink2)
                                                continue;
                                            sentmavlink2 = true;
                                        }
                                    }
                                    else
                                    {
                                        // check if we have already sent
                                        if (sentmavlink1)
                                            continue;
                                        sentmavlink1 = true;
                                    }

                                    port.sendPacket(htb, MAV.sysid, MAV.compid);
                                }
                                catch (Exception ex)
                                {
                                    log.Error(ex);
                                    // close the bad port
                                    try
                                    {
                                        port.Close();
                                    }
                                    catch
                                    {
                                    }

                                    // refresh the screen if needed
                                    if (port == MainV2.comPort)
                                    {
                                        // refresh config window if needed
                                        if (MyView.current != null)
                                        {
                                            this.BeginInvoke((MethodInvoker) delegate()
                                            {
                                                if (MyView.current.Name == "HWConfig")
                                                    MyView.ShowScreen("HWConfig");
                                                if (MyView.current.Name == "SWConfig")
                                                    MyView.ShowScreen("SWConfig");
                                            });
                                        }
                                    }
                                }
                            }
                        }

                        heatbeatSend = DateTime.UtcNow;
                    }

                    // if not connected or busy, sleep and loop
                    if (!comPort.BaseStream.IsOpen || comPort.giveComport == true)
                    {
                        if (!comPort.BaseStream.IsOpen)
                        {
                            // check if other ports are still open
                            foreach (var port in Comports)
                            {
                                if (port.BaseStream.IsOpen)
                                {
                                    Console.WriteLine("Main comport shut, swapping to other mav");
                                    comPort = port;
                                    break;
                                }
                            }
                        }

                        await Task.Delay(100).ConfigureAwait(false);
                    }

                    // read the interfaces
                    foreach (var port in Comports.ToArray())
                    {
                        if (!port.BaseStream.IsOpen)
                        {
                            // skip primary interface
                            if (port == comPort)
                                continue;

                            // modify array and drop out
                            Comports.Remove(port);
                            port.Dispose();
                            break;
                        }

                        DateTime startread = DateTime.UtcNow;

                        // must be open, we have bytes, we are not yielding the port,
                        // the thread is meant to be running and we only spend 1 seconds max in this read loop
                        while (port.BaseStream.IsOpen && port.BaseStream.BytesToRead > minbytes &&
                               port.giveComport == false && serialThread && startread.AddSeconds(1) > DateTime.UtcNow)
                        {
                            try
                            {
                                await port.readPacketAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                            }
                        }

                        // update currentstate of sysids on the port
                        foreach (var MAV in port.MAVlist)
                        {
                            try
                            {
                                MAV.cs.UpdateCurrentSettings(null, false, port, MAV);
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Tracking.AddException(e);
                    log.Error("Serial Reader fail :" + e.ToString());
                    try
                    {
                        comPort.Close();
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }
            }

            Console.WriteLine("SerialReader Done");
            SerialThreadrunner.Set();
        }

        ManualResetEvent ADSBThreadRunner = new ManualResetEvent(false);

        /// <summary>
        /// adsb periodic send thread
        /// </summary>
        private async void ADSBRunner()
        {
            if (adsbThread)
                return;
            adsbThread = true;
            ADSBThreadRunner.Reset();
            while (adsbThread)
            {
                await Task.Delay(1000).ConfigureAwait(false); // run every 1000 ms
                // Clean up old planes
                HashSet<string> planesToClean = new HashSet<string>();
                lock(adsblock)
                {
                    MainV2.instance.adsbPlanes.Where(a => a.Value.Time < DateTime.Now.AddSeconds(-30)).ForEach(a => planesToClean.Add(a.Key));
                    planesToClean.ForEach(a => MainV2.instance.adsbPlanes.TryRemove(a, out _));

                }
                PointLatLngAlt ourLocation = comPort.MAV.cs.Location;
                // Get only close planes, sorted by distance
                var relevantPlanes = MainV2.instance.adsbPlanes
                    .Select(v => new { v, Distance = v.Value.GetDistance(ourLocation) })
                    .Where(v => v.Distance <= 10000)
                    .Where(v => !(v.v.Value.Source is MAVLinkInterface))
                    .OrderBy(v => v.Distance)
                    .Select(v => v.v.Value)
                    .Take(10)
                    .ToList();
                adsbIndex = (++adsbIndex % Math.Max(1, Math.Min(relevantPlanes.Count, 10)));
                var currentPlane = relevantPlanes.ElementAtOrDefault(adsbIndex);
                if (currentPlane == null)
                {
                    continue;
                }
                MAVLink.mavlink_adsb_vehicle_t packet = new MAVLink.mavlink_adsb_vehicle_t();
                packet.altitude = (int)(currentPlane.Alt * 1000);
                packet.altitude_type = (byte)MAVLink.ADSB_ALTITUDE_TYPE.GEOMETRIC;
                packet.callsign = currentPlane.CallSign.MakeBytes();
                packet.squawk = currentPlane.Squawk;
                packet.emitter_type = (byte)MAVLink.ADSB_EMITTER_TYPE.NO_INFO;
                packet.heading = (ushort)(currentPlane.Heading * 100);
                packet.lat = (int)(currentPlane.Lat * 1e7);
                packet.lon = (int)(currentPlane.Lng * 1e7);
                packet.hor_velocity = (ushort)(currentPlane.Speed);
                packet.ver_velocity = (short)(currentPlane.VerticalSpeed);
                try
                {
                    packet.ICAO_address = uint.Parse(currentPlane.Tag, NumberStyles.HexNumber);
                }
                catch
                {
                    log.WarnFormat("invalid icao address: {0}", currentPlane.Tag);
                    packet.ICAO_address = 0;
                }
                packet.flags = (ushort)(MAVLink.ADSB_FLAGS.VALID_ALTITUDE | MAVLink.ADSB_FLAGS.VALID_COORDS |
                                          MAVLink.ADSB_FLAGS.VALID_VELOCITY | MAVLink.ADSB_FLAGS.VALID_HEADING | MAVLink.ADSB_FLAGS.VALID_CALLSIGN);

                //send to current connected
                MainV2.comPort.sendPacket(packet, MainV2.comPort.MAV.sysid, MainV2.comPort.MAV.compid);

            }

        }


        protected override void OnLoad(EventArgs e)
        {
            // check if its defined, and force to show it if not known about
            if (Settings.Instance["menu_autohide"] == null)
            {
                Settings.Instance["menu_autohide"] = "false";
            }

            try
            {
                AutoHideMenu(Settings.Instance.GetBoolean("menu_autohide"));
            }
            catch
            {
            }

            MyView.AddScreen(new MainSwitcher.Screen("FlightData", FlightData, true));
            MyView.AddScreen(new MainSwitcher.Screen("FlightPlanner", FlightPlanner, true));
            MyView.AddScreen(new MainSwitcher.Screen("HWConfig", typeof(GCSViews.InitialSetup), false));
            MyView.AddScreen(new MainSwitcher.Screen("SWConfig", typeof(GCSViews.SoftwareConfig), false));
            MyView.AddScreen(new MainSwitcher.Screen("FMTParameterSettings", typeof(FMT.FmtParameterSettings), false));

            try
            {
                if (Control.ModifierKeys == Keys.Shift)
                {
                }
                else
                {
                    log.Info("Load Pluggins");
                    Plugin.PluginLoader.DisabledPluginNames.Clear();
                    foreach (var s in Settings.Instance.GetList("DisabledPlugins"))
                        Plugin.PluginLoader.DisabledPluginNames.Add(s);
                    Plugin.PluginLoader.LoadAll();
                    log.Info("Load Pluggins... Done");
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            if (Program.Logo != null && Program.name == "VVVVZ")
            {
                this.PerformLayout();
                MenuFlightPlanner_Click(this, e);
                MainMenu_ItemClicked(this, new ToolStripItemClickedEventArgs(MenuFlightPlanner));
            }
            else
            {
                this.PerformLayout();
                log.Info("show FlightData");
                MenuFlightData_Click(this, e);
                log.Info("show FlightData... Done");
                MainMenu_ItemClicked(this, new ToolStripItemClickedEventArgs(MenuFlightData));
            }

            // for long running tasks using own threads.
            // for short use threadpool

            this.SuspendLayout();

            // setup http server
            try
            {
                log.Info("start http");
                httpthread = new Thread(new httpserver().listernforclients)
                {
                    Name = "motion jpg stream-network kml",
                    IsBackground = true
                };
                httpthread.Start();
            }
            catch (Exception ex)
            {
                log.Error("Error starting TCP listener thread: ", ex);
                CustomMessageBox.Show(ex.ToString());
            }

            log.Info("start joystick");
            try
            {
                // setup joystick packet sender
                joysticksend();
            }
            catch (NotSupportedException ex)
            {
                log.Error(ex);
            }

            log.Info("start serialreader");
            try
            {
                // setup main serial reader
                SerialReader();
            }
            catch (NotSupportedException ex)
            {
                log.Error(ex);
            }

            log.Info("start adsbsender");
            try
            {
                ADSBRunner();
            }
            catch (NotSupportedException ex)
            {
                log.Error(ex);
            }

            log.Info("start plugin thread");
            try
            {
                // Empty or absent plugin directories must not create an idle worker.
                if (Plugin.PluginLoader.RequiresRunner)
                {
                    pluginthread = new Thread(PluginThread)
                    {
                        IsBackground = true,
                        Name = "plugin runner thread",
                        Priority = ThreadPriority.BelowNormal
                    };
                    pluginthread.Start();
                }
                else
                {
                    log.Info("No runnable plugins; plugin runner thread was not started.");
                }
            }
            catch (NotSupportedException ex)
            {
                log.Error(ex);
            }


            ThreadPool.QueueUserWorkItem(LoadGDALImages);

            ThreadPool.QueueUserWorkItem(BGLoadAirports);

            ThreadPool.QueueUserWorkItem(BGCreateMaps);

            //ThreadPool.QueueUserWorkItem(BGGetAlmanac);

            ThreadPool.QueueUserWorkItem(BGLogMessagesMetaData);

            // tfr went dead on 30-9-2020
            //ThreadPool.QueueUserWorkItem(BGgetTFR);

            ThreadPool.QueueUserWorkItem(BGNoFly);

            ThreadPool.QueueUserWorkItem(BGGetKIndex);

            // update firmware version list - only once per day
            ThreadPool.QueueUserWorkItem(BGFirmwareCheck);

            Task.Run(async () =>
            {
                try
                {
                    await UserAlert.GetAlerts().ConfigureAwait(false);
                }
                catch
                {
                }
            });

            log.Info("start AutoConnect");
            AutoConnect.NewMavlinkConnection += (sender, serial) =>
            {
                try
                {
                    log.Info("AutoConnect.NewMavlinkConnection " + serial.PortName);
                    MainV2.instance.BeginInvoke((Action) delegate
                    {
                        if (MainV2.comPort.BaseStream.IsOpen)
                        {
                            var mav = new MAVLinkInterface();
                            mav.BaseStream = serial;
                            MainV2.instance.doConnect(mav, "preset", serial.PortName);

                            MainV2.Comports.Add(mav);

                            try
                            {
                                Comports = Comports.Distinct().ToList();
                            }
                            catch { }
                        }
                        else
                        {
                            MainV2.comPort.BaseStream = serial;
                            MainV2.instance.doConnect(MainV2.comPort, "preset", serial.PortName);
                        }
                    });
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            };
            AutoConnect.NewVideoStream += (sender, gststring) =>
            {
                MainV2.instance.BeginInvoke((Action) delegate
                {
                    try
                    {
                        log.Info("AutoConnect.NewVideoStream " + gststring);
                        GStreamer.GstLaunch = GStreamer.LookForGstreamer();

                        if (!GStreamer.GstLaunchExists)
                        {
                            if (CustomMessageBox.Show(
                                    "A video stream has been detected, but gstreamer has not been configured/installed.\nDo you want to install/config it now?",
                                    "GStreamer", System.Windows.Forms.MessageBoxButtons.YesNo) ==
                                (int) System.Windows.Forms.DialogResult.Yes)
                            {
                                GStreamerUI.DownloadGStreamer();
                                if (!GStreamer.GstLaunchExists)
                                {
                                    return;
                                }
                            }
                            else
                            {
                                return;
                            }
                        }

                        GCSViews.FlightData.hudGStreamer.Start(gststring);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                });
            };
            AutoConnect.Start();

            BinaryLog.onFlightMode += (firmware, modeno) =>
            {
                try
                {
                    if (firmware == "")
                        return null;

                    var modes = ArduPilot.Common.getModesList((Firmwares) Enum.Parse(typeof(Firmwares), firmware));
                    string currentmode = null;

                    foreach (var mode in modes)
                    {
                        if (mode.Key == modeno)
                        {
                            currentmode = mode.Value;
                            break;
                        }
                    }

                    return currentmode;
                }
                catch
                {
                    return null;
                }
            };

            GCSViews.FlightData.hudGStreamer.OnNewImage += (sender, image) =>
            {
                try
                {
                    if (image == null)
                    {
                        GCSViews.FlightData.myhud.bgimage = null;
                        return;
                    }

                    var old = GCSViews.FlightData.myhud.bgimage;
                    GCSViews.FlightData.myhud.bgimage = new Bitmap(image.Width, image.Height, 4 * image.Width,
                        PixelFormat.Format32bppPArgb,
                        image.LockBits(Rectangle.Empty, null, SKColorType.Bgra8888)
                            .Scan0);
                    if (old != null)
                        old.Dispose();
                }
                catch
                {
                }
            };

            vlcrender.onNewImage += (sender, image) =>
            {
                try
                {
                    if (image == null)
                    {
                        GCSViews.FlightData.myhud.bgimage = null;
                        return;
                    }

                    var old = GCSViews.FlightData.myhud.bgimage;
                    GCSViews.FlightData.myhud.bgimage = new Bitmap(image.Width,
                        image.Height,
                        4 * image.Width,
                        PixelFormat.Format32bppPArgb,
                        image.LockBits(Rectangle.Empty, null, SKColorType.Bgra8888).Scan0);
                    if (old != null)
                        old.Dispose();
                }
                catch
                {
                }
            };

            CaptureMJPEG.onNewImage += (sender, image) =>
            {
                try
                {
                    if (image == null)
                    {
                        GCSViews.FlightData.myhud.bgimage = null;
                        return;
                    }

                    var old = GCSViews.FlightData.myhud.bgimage;
                    GCSViews.FlightData.myhud.bgimage = new Bitmap(image.Width, image.Height, 4 * image.Width,
                        PixelFormat.Format32bppPArgb,
                        image.LockBits(Rectangle.Empty, null, SKColorType.Bgra8888).Scan0);
                    if (old != null)
                        old.Dispose();
                }
                catch
                {
                }
            };

            try
            {
                object locker = new object();
                List<string> seen = new List<string>();

                ZeroConf.StartUDPMavlink += (zeroconfHost) =>
                {
                    try
                    {
                        var ip = zeroconfHost.IPAddress;
                        var service = zeroconfHost.Services.Where(a => a.Key == "_mavlink._udp.local.");
                        var port = service.First().Value.Port;

                        lock (locker)
                        {
                            if (Comports.Any((a) =>
                                {
                                    return a.BaseStream.PortName == "UDPCl" + port.ToString() && a.BaseStream.IsOpen;
                                }
                            ))
                                return;

                            if (seen.Contains(zeroconfHost.Id))
                                return;

                            // no duplicates
                            if (!ExtraConnectionList.Any(a => a.Label == "ZeroConf " + zeroconfHost.DisplayName))
                                ExtraConnectionList.Add(new AutoConnect.ConnectionInfo("ZeroConf " + zeroconfHost.DisplayName, false, port, AutoConnect.ProtocolType.Udp, AutoConnect.ConnectionFormat.MAVLink, AutoConnect.Direction.Outbound, ip));

                            if (CustomMessageBox.Show(
                                    "A Mavlink stream has been detected, " + zeroconfHost.DisplayName + "(" +
                                    zeroconfHost.Id + "). Would you like to connect to it?",
                                    "Mavlink", System.Windows.Forms.MessageBoxButtons.YesNo) ==
                                (int) System.Windows.Forms.DialogResult.Yes)
                            {
                                var mav = new MAVLinkInterface();

                                if(!comPort.BaseStream.IsOpen)
                                    mav = comPort;

                                var udc = new UdpSerialConnect();
                                udc.Port = port.ToString();
                                udc.client = new UdpClient(ip, port);
                                udc.IsOpen = true;
                                udc.hostEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                                mav.BaseStream = udc;

                                MainV2.instance.Invoke((Action) delegate
                                {
                                    MainV2.instance.doConnect(mav, "preset", port.ToString());

                                    MainV2.Comports.Add(mav);

                                    try
                                    {
                                        Comports = Comports.Distinct().ToList();
                                    }
                                    catch { }

                                    MainV2._connectionControl.UpdateSysIDS();
                                });

                            }

                            // add to seen list, so we skip on next refresh
                            seen.Add(zeroconfHost.Id);
                        }
                    }
                    catch (Exception)
                    {

                    }
                };

                if (!isHerelink)
                {
                    ZeroConf.ProbeForMavlink();

                    ZeroConf.ProbeForRTSP();
                }
            }
            catch
            {
            }

            CommsSerialScan.doConnect += port =>
            {
                if (MainV2.instance.InvokeRequired)
                {
                    log.Info("CommsSerialScan.doConnect invoke");
                    MainV2.instance.BeginInvoke(
                        (Action) delegate()
                        {
                            MAVLinkInterface mav = new MAVLinkInterface();
                            mav.BaseStream = port;
                            MainV2.instance.doConnect(mav, "preset", "0");
                            MainV2.Comports.Add(mav);

                            try
                            {
                                Comports = Comports.Distinct().ToList();
                            }
                            catch { }
                        });
                }
                else
                {

                    log.Info("CommsSerialScan.doConnect NO invoke");
                    MAVLinkInterface mav = new MAVLinkInterface();
                    mav.BaseStream = port;
                    MainV2.instance.doConnect(mav, "preset", "0");
                    MainV2.Comports.Add(mav);

                    try
                    {
                        Comports = Comports.Distinct().ToList();
                    }
                    catch { }
                }
            };

            try
            {
                // prescan
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    MissionPlanner.Comms.CommsBLE.SerialPort_GetCustomPorts();

#if !LIB
                MissionPlanner.Comms.CommsWinUSB.SerialPort_GetCustomPorts();
#endif
            }
            catch { }

            // add the custom port creator
            CustomPortList.Add(new Regex("BLE_.*"), (s1, s2) => { return new CommsBLE() { PortName = s1, BaudRate = int.Parse(s2) }; });

#if !LIB
            CustomPortList.Add(new Regex("WINUSB_VID_.*"), (s1, s2) => { return new CommsWinUSB() { PortName = s1, BaudRate = int.Parse(s2) }; });
#endif

            this.ResumeLayout();

            Program.Splash?.Close();

            log.Info("appload time");
            MissionPlanner.Utilities.Tracking.AddTiming("AppLoad", "Load Time",
                (DateTime.Now - Program.starttime).TotalMilliseconds, "");

            int p = (int) Environment.OSVersion.Platform;
            bool isWin = (p != 4) && (p != 6) && (p != 128);
            bool winXp = isWin && Environment.OSVersion.Version.Major == 5;
            if (winXp)
            {
                Common.MessageShowAgain("Windows XP",
                    "This is the last version that will support Windows XP, please update your OS");

                // invalidate update url
                System.Configuration.ConfigurationManager.AppSettings["UpdateLocationVersion"] =
                    "https://firmware.ardupilot.org/MissionPlanner/xp/";
                System.Configuration.ConfigurationManager.AppSettings["UpdateLocation"] =
                    "https://firmware.ardupilot.org/MissionPlanner/xp/";
                System.Configuration.ConfigurationManager.AppSettings["UpdateLocationMD5"] =
                    "https://firmware.ardupilot.org/MissionPlanner/xp/checksums.txt";
                System.Configuration.ConfigurationManager.AppSettings["BetaUpdateLocationVersion"] = "";
            }

            try
            {
                // single update check per day - in a seperate thread
                if (Settings.Instance["fmt_update_check"] != DateTime.Now.ToShortDateString())
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(checkFmtUpdate);
                    Settings.Instance["fmt_update_check"] = DateTime.Now.ToShortDateString();
                }
            }
            catch (Exception ex)
            {
                log.Error("Update check failed", ex);
            }

            // play a tlog that was passed to the program/ load a bin log passed
            if (Program.args.Length > 0)
            {
                var cmds = ProcessCommandLine(Program.args);

                if (cmds.ContainsKey("file") && File.Exists(cmds["file"]) && cmds["file"].ToLower().EndsWith(".tlog"))
                {
                    FlightData.LoadLogFile(Program.args[0]);
                    FlightData.BUT_playlog_Click(null, null);
                }
                else if (cmds.ContainsKey("file") && File.Exists(cmds["file"]) &&
                         (cmds["file"].ToLower().EndsWith(".log") || cmds["file"].ToLower().EndsWith(".bin")))
                {
                    LogBrowse logbrowse = new LogBrowse();
                    ThemeManager.ApplyThemeTo(logbrowse);
                    logbrowse.logfilename = Program.args[0];
                    logbrowse.Show(this);
                    logbrowse.BringToFront();
                }

                if (cmds.ContainsKey("script") && File.Exists(cmds["script"]))
                {
                    // invoke for after onload finished
                    this.BeginInvoke((Action) delegate()
                    {
                        try
                        {
                            FlightData.selectedscript = cmds["script"];

                            FlightData.BUT_run_script_Click(null, null);
                        }
                        catch (Exception ex)
                        {
                            CustomMessageBox.Show("Start script failed: " + ex.ToString(), Strings.ERROR);
                        }
                    });
                }

                if (cmds.ContainsKey("joy") && cmds.ContainsKey("type"))
                {
                    if (cmds["type"].ToLower() == "plane")
                    {
                        MainV2.comPort.MAV.cs.firmware = Firmwares.ArduPlane;
                    }
                    else if (cmds["type"].ToLower() == "copter")
                    {
                        MainV2.comPort.MAV.cs.firmware = Firmwares.ArduCopter2;
                    }
                    else if (cmds["type"].ToLower() == "rover")
                    {
                        MainV2.comPort.MAV.cs.firmware = Firmwares.ArduRover;
                    }
                    else if (cmds["type"].ToLower() == "sub")
                    {
                        MainV2.comPort.MAV.cs.firmware = Firmwares.ArduSub;
                    }

                    var joy = JoystickBase.Create(() => MainV2.comPort);

                    if (joy.start(cmds["joy"]))
                    {
                        MainV2.joystick = joy;
                        MainV2.joystick.enabled = true;
                    }
                    else
                    {
                        CustomMessageBox.Show("Failed to start joystick");
                    }
                }

                if (cmds.ContainsKey("rtk"))
                {
                    var inject = new ConfigSerialInjectGPS();
                    if (cmds["rtk"].ToLower().Contains("http"))
                    {
                        inject.CMB_serialport.Text = "NTRIP";
                        var nt = new CommsNTRIP();
                        ConfigSerialInjectGPS.comPort = nt;
                        Task.Run(() =>
                        {
                            try
                            {
                                nt.Open(cmds["rtk"]);
                                nt.lat = MainV2.comPort.MAV.cs.PlannedHomeLocation.Lat;
                                nt.lng = MainV2.comPort.MAV.cs.PlannedHomeLocation.Lng;
                                nt.alt = MainV2.comPort.MAV.cs.PlannedHomeLocation.Alt;
                                this.BeginInvokeIfRequired(() => { inject.DoConnect().RunSynchronously(); });
                            }
                            catch (Exception ex)
                            {
                                this.BeginInvokeIfRequired(() => { CustomMessageBox.Show(ex.ToString()); });
                            }
                        });
                    }
                }

                if (cmds.ContainsKey("cam"))
                {
                    try
                    {
                        MainV2.cam = new WebCamService.Capture(int.Parse(cmds["cam"]), null);

                        MainV2.cam.Start();
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show(ex.ToString());
                    }
                }

                if (cmds.ContainsKey("gstream"))
                {
                    GStreamer.GstLaunch = GStreamer.LookForGstreamer();

                    if (!GStreamer.GstLaunchExists)
                    {
                        if (CustomMessageBox.Show(
                                "A video stream has been detected, but gstreamer has not been configured/installed.\nDo you want to install/config it now?",
                                "GStreamer", System.Windows.Forms.MessageBoxButtons.YesNo) ==
                            (int) System.Windows.Forms.DialogResult.Yes)
                        {
                            GStreamerUI.DownloadGStreamer();
                        }
                    }

                    try
                    {
                        new Thread(delegate()
                            {
                                // 36 retrys
                                for (int i = 0; i < 36; i++)
                                {
                                    try
                                    {
                                        var st = GCSViews.FlightData.hudGStreamer.Start(cmds["gstream"]);
                                        if (st == null)
                                        {
                                            // prevent spam
                                            Thread.Sleep(5000);
                                        }
                                        else
                                        {
                                            while (st.IsAlive)
                                            {
                                                Thread.Sleep(1000);
                                            }
                                        }
                                    }
                                    catch (BadImageFormatException ex)
                                    {
                                        // not running on x64
                                        log.Error(ex);
                                        return;
                                    }
                                    catch (DllNotFoundException ex)
                                    {
                                        // missing or failed download
                                        log.Error(ex);
                                        return;
                                    }
                                }
                            })
                            {IsBackground = true, Name = "Gstreamer cli"}.Start();
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }

                if (cmds.ContainsKey("port") && cmds.ContainsKey("baud"))
                {
                    _connectionControl.CMB_serialport.Text = cmds["port"];
                    _connectionControl.CMB_baudrate.Text = cmds["baud"];

                    doConnect(MainV2.comPort, cmds["port"], cmds["baud"]);
                }
            }

            GMapMarkerBase.length = Settings.Instance.GetInt32("GMapMarkerBase_length", 500);
            GMapMarkerBase.DisplayCOGSetting = Settings.Instance.GetBoolean("GMapMarkerBase_DisplayCOG", true);
            GMapMarkerBase.DisplayHeadingSetting = Settings.Instance.GetBoolean("GMapMarkerBase_DisplayHeading", true);
            GMapMarkerBase.DisplayNavBearingSetting = Settings.Instance.GetBoolean("GMapMarkerBase_DisplayNavBearing", true);
            GMapMarkerBase.DisplayRadiusSetting = Settings.Instance.GetBoolean("GMapMarkerBase_DisplayRadius", true);
            GMapMarkerBase.DisplayTargetSetting = Settings.Instance.GetBoolean("GMapMarkerBase_DisplayTarget", true);
            var inactiveDisplayStyle = GMapMarkerBase.InactiveDisplayStyleEnum.Normal;
            string inactiveDisplayStyleStr = Settings.Instance.GetString("GMapMarkerBase_InactiveDisplayStyle", inactiveDisplayStyle.ToString());
            Enum.TryParse(inactiveDisplayStyleStr, out inactiveDisplayStyle);
            GMapMarkerBase.InactiveDisplayStyle = inactiveDisplayStyle;
            Settings.Instance["GMapMarkerBase_InactiveDisplayStyle"] = inactiveDisplayStyle.ToString();
        }

        private void BGLogMessagesMetaData(object nothing)
        {
            LogMetaData.GetMetaData().ConfigureAwait(false).GetAwaiter().GetResult();
            LogMetaData.ParseMetaData();
        }

        public void LoadGDALImages(object nothing)
        {
            if (Settings.Instance.ContainsKey("GDALImageDir"))
            {
                try
                {
                    Utilities.GDAL.ScanDirectory(Settings.Instance["GDALImageDir"]);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
        }

        private Dictionary<string, string> ProcessCommandLine(string[] args)
        {
            Dictionary<string, string> cmdargs = new Dictionary<string, string>();
            string cmd = "";
            foreach (var s in args)
            {
                if (s.StartsWith("-") || s.StartsWith("/") || s.StartsWith("--"))
                {
                    cmd = s.TrimStart(new char[] {'-', '/', '-'}).TrimStart(new char[] {'-', '/', '-'});
                    continue;
                }

                if (cmd != "")
                {
                    cmdargs[cmd] = s;
                    log.Info("ProcessCommandLine: " + cmd + " = " + s);
                    cmd = "";
                    continue;
                }

                if (File.Exists(s))
                {
                    // we are not a command, and the file exists.
                    cmdargs["file"] = s;
                    log.Info("ProcessCommandLine: " + "file" + " = " + s);
                    continue;
                }

                log.Info("ProcessCommandLine: UnKnown = " + s);
            }

            return cmdargs;
        }

        private void BGFirmwareCheck(object state)
        {
            try
            {
                if (Settings.Instance["fw_check"] != DateTime.Now.ToShortDateString())
                {
                    APFirmware.GetList("https://firmware.oborne.me/manifest.json.gz");

                    Settings.Instance["fw_check"] = DateTime.Now.ToShortDateString();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void BGGetKIndex(object state)
        {
            try
            {
                // check the last kindex date
                if (Settings.Instance["kindexdate"] == DateTime.Now.ToShortDateString())
                {
                    KIndex_KIndex(Settings.Instance.GetInt32("kindex"), null);
                }
                else
                {
                    // get a new kindex
                    KIndex.KIndexEvent += KIndex_KIndex;
                    KIndex.GetKIndex();

                    Settings.Instance["kindexdate"] = DateTime.Now.ToShortDateString();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void BGNoFly(object state)
        {
            try
            {
                NoFly.NoFly.Scan();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }


        void KIndex_KIndex(object sender, EventArgs e)
        {
            CurrentState.KIndexstatic = (int) sender;
            Settings.Instance["kindex"] = CurrentState.KIndexstatic.ToString();
        }

        private void BGCreateMaps(object state)
        {
            // sort logs
            try
            {
                MissionPlanner.Log.LogSort.SortLogs(Directory.GetFiles(Settings.Instance.LogDir, "*.tlog"));

                MissionPlanner.Log.LogSort.SortLogs(Directory.GetFiles(Settings.Instance.LogDir, "*.rlog"));
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            try
            {
                // create maps
                Log.LogMap.MapLogs(Directory.GetFiles(Settings.Instance.LogDir, "*.tlog", SearchOption.AllDirectories));
                Log.LogMap.MapLogs(Directory.GetFiles(Settings.Instance.LogDir, "*.bin", SearchOption.AllDirectories));
                Log.LogMap.MapLogs(Directory.GetFiles(Settings.Instance.LogDir, "*.log", SearchOption.AllDirectories));

                if (File.Exists(tlogThumbnailHandler.tlogThumbnailHandler.queuefile))
                {
                    Log.LogMap.MapLogs(File.ReadAllLines(tlogThumbnailHandler.tlogThumbnailHandler.queuefile));

                    File.Delete(tlogThumbnailHandler.tlogThumbnailHandler.queuefile);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            try
            {
                if (File.Exists(tlogThumbnailHandler.tlogThumbnailHandler.queuefile))
                {
                    Log.LogMap.MapLogs(File.ReadAllLines(tlogThumbnailHandler.tlogThumbnailHandler.queuefile));

                    File.Delete(tlogThumbnailHandler.tlogThumbnailHandler.queuefile);
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private void checkupdate(object stuff)
        {
            if (Program.WindowsStoreApp)
                return;

            try
            {
                MissionPlanner.Utilities.Update.CheckForUpdate();
            }
            catch (Exception ex)
            {
                log.Error("Update check failed", ex);
            }
        }

        private void MainV2_Resize(object sender, EventArgs e)
        {
            // mono - resize is called before the control is created
            if (MyView != null)
                log.Info("myview width " + MyView.Width + " height " + MyView.Height);

            log.Info("this   width " + this.Width + " height " + this.Height);
        }

        private void MenuHelp_Click(object sender, EventArgs e)
        {
            MyView.ShowScreen("Help");
        }


        /// <summary>
        /// keyboard shortcuts override
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (ConfigTerminal.SSHTerminal)
            {
                return false;
            }

            if (keyData == Keys.F12)
            {
                MenuConnect_Click(null, null);
                return true;
            }

            if (keyData == Keys.F2)
            {
                MenuFlightData_Click(null, null);
                return true;
            }

            if (keyData == Keys.F3)
            {
                MenuFlightPlanner_Click(null, null);
                return true;
            }

            if (keyData == Keys.F4)
            {
                MenuTuning_Click(null, null);
                return true;
            }

            if (keyData == Keys.F5)
            {
                comPort.getParamList();
                MyView.ShowScreen(MyView.current.Name);
                return true;
            }

            if (keyData == (Keys.Control | Keys.F)) // temp
            {
                Form frm = new temp();
                ThemeManager.ApplyThemeTo(frm);
                frm.Show();
                return true;
            }

            /*if (keyData == (Keys.Control | Keys.S)) // screenshot
            {
                ScreenShot();
                return true;
            }*/
            if (keyData == (Keys.Control | Keys.P))
            {
                new PluginUI().Show();
                return true;
            }

            if (keyData == (Keys.Control | Keys.G)) // nmea out
            {
                Form frm = new SerialOutputNMEA();
                ThemeManager.ApplyThemeTo(frm);
                frm.Show();
                return true;
            }

            if (keyData == (Keys.Control | Keys.X))
            {
                new GMAPCache().ShowUserControl();
                return true;
            }

            if (keyData == (Keys.Control | Keys.L)) // limits
            {
                //new DigitalSkyUI().ShowUserControl();

                new SpectrogramUI().Show();

                return true;
            }

            if (keyData == (Keys.Control | Keys.W)) // test ac config
            {
                new PropagationSettings().Show();

                return true;
            }

            if (keyData == (Keys.Control | Keys.Z))
            {
                //ScanHW.Scan(comPort);
                new Camera().test(MainV2.comPort);
                return true;
            }

            if (keyData == (Keys.Control | Keys.T)) // for override connect
            {
                try
                {
                    MainV2.comPort.Open(false);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(ex.ToString());
                }

                return true;
            }

            if (keyData == (Keys.Control | Keys.Y)) // for ryan beall and ollyw42
            {
                // write
                try
                {
                    MainV2.comPort.doCommand((byte) MainV2.comPort.sysidcurrent, (byte) MainV2.comPort.compidcurrent,
                        MAVLink.MAV_CMD.PREFLIGHT_STORAGE, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
                }
                catch
                {
                    CustomMessageBox.Show("Invalid command");
                    return true;
                }

                //read
                ///////MainV2.comPort.doCommand(MAVLink09.MAV_CMD.PREFLIGHT_STORAGE, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
                CustomMessageBox.Show("Done MAV_ACTION_STORAGE_WRITE");
                return true;
            }

            if (keyData == (Keys.Control | Keys.J))
            {
                new DevopsUI().ShowUserControl();

                return true;
            }

            if (ProcessCmdKeyCallback != null)
            {
                return ProcessCmdKeyCallback(ref msg, keyData);
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        public delegate bool ProcessCmdKeyHandler(ref Message msg, Keys keyData);

        public event ProcessCmdKeyHandler ProcessCmdKeyCallback;

        private static void ApplyConfiguredLanguageAtStartup(CultureInfo ci)
        {
            if (ci == null)
                return;

            // The UI culture must be selected before InitializeComponent creates
            // any localized controls. Live resource re-application remains
            // intentionally disabled because it can corrupt custom WinForms views.
            Thread.CurrentThread.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            L10N.ConfigLang = ci;
            Strings.Culture = ci;
        }

        public void changelanguage(CultureInfo ci)
        {
            if (ci == null)
                return;

            log.Info("change lang to " + ci + " current " +
                     Thread.CurrentThread.CurrentUICulture);

            // Apply the language on the next startup. Re-applying resources to
            // live customized controls can invoke incompatible or disposed
            // WinForms properties and raise TargetInvocationException.
            Settings.Instance["language"] = ci.Name;
            Settings.Instance.Save();
        }


        public void ChangeUnits()
        {
            try
            {
                // dist
                if (Settings.Instance["distunits"] != null)
                {
                    switch (
                        (distances) Enum.Parse(typeof(distances), Settings.Instance["distunits"].ToString()))
                    {
                        case distances.Meters:
                            CurrentState.multiplierdist = 1;
                            CurrentState.DistanceUnit = "m";
                            break;
                        case distances.Feet:
                            CurrentState.multiplierdist = 3.2808399f;
                            CurrentState.DistanceUnit = "ft";
                            break;
                    }
                }
                else
                {
                    CurrentState.multiplierdist = 1;
                    CurrentState.DistanceUnit = "m";
                }

                // alt
                if (Settings.Instance["altunits"] != null)
                {
                    switch (
                        (distances) Enum.Parse(typeof(altitudes), Settings.Instance["altunits"].ToString()))
                    {
                        case distances.Meters:
                            CurrentState.multiplieralt = 1;
                            CurrentState.AltUnit = "m";
                            break;
                        case distances.Feet:
                            CurrentState.multiplieralt = 3.2808399f;
                            CurrentState.AltUnit = "ft";
                            break;
                    }
                }
                else
                {
                    CurrentState.multiplieralt = 1;
                    CurrentState.AltUnit = "m";
                }

                // speed
                if (Settings.Instance["speedunits"] != null)
                {
                    switch ((speeds) Enum.Parse(typeof(speeds), Settings.Instance["speedunits"].ToString()))
                    {
                        case speeds.meters_per_second:
                            CurrentState.multiplierspeed = 1;
                            CurrentState.SpeedUnit = "m/s";
                            break;
                        case speeds.fps:
                            CurrentState.multiplierspeed = 3.2808399f;
                            CurrentState.SpeedUnit = "fps";
                            break;
                        case speeds.kph:
                            CurrentState.multiplierspeed = 3.6f;
                            CurrentState.SpeedUnit = "kph";
                            break;
                        case speeds.mph:
                            CurrentState.multiplierspeed = 2.23693629f;
                            CurrentState.SpeedUnit = "mph";
                            break;
                        case speeds.knots:
                            CurrentState.multiplierspeed = 1.94384449f;
                            CurrentState.SpeedUnit = "kts";
                            break;
                    }
                }
                else
                {
                    CurrentState.multiplierspeed = 1;
                    CurrentState.SpeedUnit = "m/s";
                }
            }
            catch
            {
            }
        }

        private void CMB_baudrate_TextChanged(object sender, EventArgs e)
        {
            if (!int.TryParse(_connectionControl.CMB_baudrate.Text, out comPortBaud))
            {
                CustomMessageBox.Show(Strings.InvalidBaudRate, Strings.ERROR);
                return;
            }

            var sb = new StringBuilder();
            int baud = 0;
            for (int i = 0; i < _connectionControl.CMB_baudrate.Text.Length; i++)
                if (char.IsDigit(_connectionControl.CMB_baudrate.Text[i]))
                {
                    sb.Append(_connectionControl.CMB_baudrate.Text[i]);
                    baud = baud * 10 + _connectionControl.CMB_baudrate.Text[i] - '0';
                }

            if (_connectionControl.CMB_baudrate.Text != sb.ToString())
            {
                _connectionControl.CMB_baudrate.Text = sb.ToString();
            }

            try
            {
                if (baud > 0 && comPort.BaseStream.BaudRate != baud)
                    comPort.BaseStream.BaudRate = baud;
            }
            catch (Exception)
            {
            }
        }

        private void MainMenu_MouseLeave(object sender, EventArgs e)
        {
            if (_connectionControl.PointToClient(Control.MousePosition).Y < MainMenu.Height)
                return;

            this.SuspendLayout();

            panel1.Visible = false;

            this.ResumeLayout();
        }

        void menu_MouseEnter(object sender, EventArgs e)
        {
            this.SuspendLayout();
            panel1.Location = new Point(0, 0);
            panel1.Width = menu.Width;
            panel1.BringToFront();
            panel1.Visible = true;
            this.ResumeLayout();
        }

        private void autoHideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AutoHideMenu(autoHideToolStripMenuItem.Checked);

            Settings.Instance["menu_autohide"] = autoHideToolStripMenuItem.Checked.ToString();
        }

        void AutoHideMenu(bool hide)
        {
            autoHideToolStripMenuItem.Checked = hide;

            if (!hide)
            {
                this.SuspendLayout();
                panel1.Dock = DockStyle.Top;
                panel1.SendToBack();
                panel1.Visible = true;
                menu.Visible = false;
                MainMenu.MouseLeave -= MainMenu_MouseLeave;
                panel1.MouseLeave -= MainMenu_MouseLeave;
                toolStripConnectionControl.MouseLeave -= MainMenu_MouseLeave;
                this.ResumeLayout();
            }
            else
            {
                this.SuspendLayout();
                panel1.Dock = DockStyle.None;
                panel1.Visible = false;
                MainMenu.MouseLeave += MainMenu_MouseLeave;
                panel1.MouseLeave += MainMenu_MouseLeave;
                toolStripConnectionControl.MouseLeave += MainMenu_MouseLeave;
                menu.Visible = true;
                menu.SendToBack();
                this.ResumeLayout();
            }
        }

        private void MainV2_KeyDown(object sender, KeyEventArgs e)
        {
            Message temp = new Message();
            ProcessCmdKey(ref temp, e.KeyData);
            Console.WriteLine("MainV2_KeyDown " + e.ToString());
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=mich146%40hotmail%2ecom&lc=AU&item_name=Michael%20Oborne&no_note=0&bn=PP%2dDonationsBF%3abtn_donate_SM%2egif%3aNonHostedGuest");
            }
            catch
            {
                CustomMessageBox.Show("Link open failed. check your default webpage association");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public class DEV_BROADCAST_HDR
        {
            public Int32 dbch_size;
            public Int32 dbch_devicetype;
            public Int32 dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class DEV_BROADCAST_PORT
        {
            public int dbcp_size;
            public int dbcp_devicetype;
            public int dbcp_reserved; // MSDN say "do not use"

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string dbcp_name;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class DEV_BROADCAST_DEVICEINTERFACE
        {
            public Int32 dbcc_size;
            public Int32 dbcc_devicetype;
            public Int32 dbcc_reserved;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 16)]
            public Byte[]
                dbcc_classguid;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
            public string dbcc_name;
        }


        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_CREATE:
                    try
                    {
                        DEV_BROADCAST_DEVICEINTERFACE devBroadcastDeviceInterface = new DEV_BROADCAST_DEVICEINTERFACE();
                        IntPtr devBroadcastDeviceInterfaceBuffer;
                        IntPtr deviceNotificationHandle = IntPtr.Zero;
                        Int32 size = 0;

                        // frmMy is the form that will receive device-change messages.


                        size = Marshal.SizeOf(devBroadcastDeviceInterface);
                        devBroadcastDeviceInterface.dbcc_size = size;
                        devBroadcastDeviceInterface.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
                        devBroadcastDeviceInterface.dbcc_reserved = 0;
                        devBroadcastDeviceInterface.dbcc_classguid = GUID_DEVINTERFACE_USB_DEVICE.ToByteArray();
                        devBroadcastDeviceInterfaceBuffer = Marshal.AllocHGlobal(size);
                        Marshal.StructureToPtr(devBroadcastDeviceInterface, devBroadcastDeviceInterfaceBuffer, true);


                        deviceNotificationHandle = NativeMethods.RegisterDeviceNotification(this.Handle,
                            devBroadcastDeviceInterfaceBuffer, DEVICE_NOTIFY_WINDOW_HANDLE);

                        Marshal.FreeHGlobal(devBroadcastDeviceInterfaceBuffer);
                    }
                    catch
                    {
                    }

                    break;

                case WM_DEVICECHANGE:
                    // The WParam value identifies what is occurring.
                    WM_DEVICECHANGE_enum n = (WM_DEVICECHANGE_enum) m.WParam;
                    var l = m.LParam;
                    if (n == WM_DEVICECHANGE_enum.DBT_DEVICEREMOVEPENDING)
                    {
                        Console.WriteLine("DBT_DEVICEREMOVEPENDING");
                    }

                    if (n == WM_DEVICECHANGE_enum.DBT_DEVNODES_CHANGED)
                    {
                        Console.WriteLine("DBT_DEVNODES_CHANGED");
                    }

                    if (n == WM_DEVICECHANGE_enum.DBT_DEVICEARRIVAL ||
                        n == WM_DEVICECHANGE_enum.DBT_DEVICEREMOVECOMPLETE)
                    {
                        Console.WriteLine(((WM_DEVICECHANGE_enum) n).ToString());

                        DEV_BROADCAST_HDR hdr = new DEV_BROADCAST_HDR();
                        Marshal.PtrToStructure(m.LParam, hdr);

                        try
                        {
                            switch (hdr.dbch_devicetype)
                            {
                                case DBT_DEVTYP_DEVICEINTERFACE:
                                    DEV_BROADCAST_DEVICEINTERFACE inter = new DEV_BROADCAST_DEVICEINTERFACE();
                                    Marshal.PtrToStructure(m.LParam, inter);
                                    log.InfoFormat("Interface {0}", inter.dbcc_name);
                                    break;
                                case DBT_DEVTYP_PORT:
                                    DEV_BROADCAST_PORT prt = new DEV_BROADCAST_PORT();
                                    Marshal.PtrToStructure(m.LParam, prt);
                                    log.InfoFormat("port {0}", prt.dbcp_name);
                                    break;
                            }
                        }
                        catch
                        {
                        }

                        //string port = Marshal.PtrToStringAuto((IntPtr)((long)m.LParam + 12));
                        //Console.WriteLine("Added port {0}",port);
                    }

                    log.InfoFormat("Device Change {0} {1} {2}", m.Msg, (WM_DEVICECHANGE_enum) m.WParam, m.LParam);

                    if (DeviceChanged != null)
                    {
                        try
                        {
                            DeviceChanged((WM_DEVICECHANGE_enum) m.WParam);
                        }
                        catch
                        {
                        }
                    }

                    foreach (var item in MissionPlanner.Plugin.PluginLoader.Plugins)
                    {
                        item.Host.ProcessDeviceChanged((WM_DEVICECHANGE_enum) m.WParam);
                    }

                    break;
                case 0x86: // WM_NCACTIVATE
                    //var thing = Control.FromHandle(m.HWnd);

                    var child = Control.FromHandle(m.LParam);

                    var childForm = child as Form;
                    object themeMarker;
                    if (childForm != null && !themedChildForms.TryGetValue(childForm, out themeMarker))
                    {
                        log.Debug("ApplyThemeTo " + childForm.Name);
                        ThemeManager.ApplyThemeTo(childForm);
                        themedChildForms.Add(childForm, ThemeAppliedMarker);
                    }

                    break;
                default:
                    //Console.WriteLine(m.ToString());
                    break;
            }

            base.WndProc(ref m);
        }

        const int DBT_DEVTYP_PORT = 0x00000003;
        const int WM_CREATE = 0x0001;
        const Int32 DBT_DEVTYP_HANDLE = 6;
        const Int32 DBT_DEVTYP_DEVICEINTERFACE = 5;
        const Int32 DEVICE_NOTIFY_WINDOW_HANDLE = 0;
        const Int32 DIGCF_PRESENT = 2;
        const Int32 DIGCF_DEVICEINTERFACE = 0X10;
        const Int32 WM_DEVICECHANGE = 0X219;
        public static Guid GUID_DEVINTERFACE_USB_DEVICE = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");


        public enum WM_DEVICECHANGE_enum
        {
            DBT_CONFIGCHANGECANCELED = 0x19,
            DBT_CONFIGCHANGED = 0x18,
            DBT_CUSTOMEVENT = 0x8006,
            DBT_DEVICEARRIVAL = 0x8000,
            DBT_DEVICEQUERYREMOVE = 0x8001,
            DBT_DEVICEQUERYREMOVEFAILED = 0x8002,
            DBT_DEVICEREMOVECOMPLETE = 0x8004,
            DBT_DEVICEREMOVEPENDING = 0x8003,
            DBT_DEVICETYPESPECIFIC = 0x8005,
            DBT_DEVNODES_CHANGED = 0x7,
            DBT_QUERYCHANGECONFIG = 0x17,
            DBT_USERDEFINED = 0xFFFF,
        }

        private void MainMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            foreach (ToolStripItem item in MainMenu.Items)
            {
                if (item == MenuFmtPreflightCheck || item == MenuFmtArmDisarm ||
                    item == MenuFmtAirspeedZero || item == MenuFmtQnh)
                {
                    ApplyFmtQuickActionButtonStyle(item);
                    continue;
                }

                if (item == MenuFmtGpsStatus || item == MenuFmtFlightTime || item == MenuFmtRotorRpm || item == MenuFmtMqttTraffic)
                {
                    item.BackgroundImage = null;
                    item.BackColor = Color.FromArgb(24, 24, 24);
                    continue;
                }

                if (e.ClickedItem == item)
                {
                    item.BackColor = ThemeManager.ControlBGColor;
                }
                else
                {
                    try
                    {
                        item.BackColor = Color.Transparent;
                        item.BackgroundImage = displayicons.bg; //.BackColor = Color.Black;
                    }
                    catch
                    {
                    }
                }
            }
            //MainMenu.BackColor = Color.Black;
            //MainMenu.BackgroundImage = MissionPlanner.Properties.Resources.bgdark;
        }

        private void fullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // full screen
            if (fullScreenToolStripMenuItem.Checked)
            {
                this.TopMost = true;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                this.WindowState = FormWindowState.Normal;
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                this.TopMost = false;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void readonlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainV2.comPort.ReadOnly = readonlyToolStripMenuItem.Checked;
        }

        private void connectionOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new ConnectionOptions().Show(this);
        }

        private void checkFmtUpdate(object state)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers[HttpRequestHeader.UserAgent] = "FeiMaoTecPlanner/" +
                                                                  FMT.FmtAuthentication.ProductVersion;
                    var json = client.DownloadString(
                        "https://api.github.com/repos/FMT-Wade-hong/MP-GPTT/releases/latest");
                    dynamic release = JsonConvert.DeserializeObject(json);
                    var tag = Convert.ToString(release.tag_name);
                    var latestText = Regex.Match(tag ?? string.Empty, @"\d+\.\d+\.\d+").Value;
                    Version latest = null;
                    Version current = null;
                    if (!Version.TryParse(latestText, out latest) ||
                        !Version.TryParse(FMT.FmtAuthentication.ProductVersion, out current) || latest <= current)
                        return;

                    var page = Convert.ToString(release.html_url);
                    var notes = Convert.ToString(release.body);
                    BeginInvoke((Action)(() =>
                    {
                        using (var updateForm = new FMT.FmtUpdateForm(latest.ToString(), notes))
                        {
                            ThemeManager.ApplyThemeTo(updateForm);
                            if (updateForm.ShowDialog(this) == System.Windows.Forms.DialogResult.Yes &&
                                !string.IsNullOrWhiteSpace(page))
                                Process.Start(page);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                log.Warn("FMT update check failed", ex);
            }
        }

        private void ConfigureFmtMainMenu()
        {
            MainMenu.Items.Remove(MenuSimulation);
            MainMenu.Items.Remove(MenuHelp);
            // FMT owns the product header. Removing the legacy ArduPilot logo also releases
            // the right-side width needed by rotor RPM, flight-time and satellite status.
            MainMenu.Items.Remove(MenuArduPilot);
            MenuFlightPlanner.Text = "任務規劃";

            MenuFmtParameterSettings = new ToolStripButton
            {
                Name = "MenuFmtParameterSettings",
                Text = "參數設定",
                Alignment = ToolStripItemAlignment.Left,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Image = CreateFmtParameterMenuIcon(),
                ImageScaling = ToolStripItemImageScaling.None,
                AutoSize = false,
                Size = new Size(76, 47),
                Margin = Padding.Empty,
                ForeColor = SystemColors.ControlLight,
                TextAlign = ContentAlignment.BottomCenter,
                TextImageRelation = TextImageRelation.ImageAboveText,
                ToolTipText = "輸入參數設定密碼後，開啟完整參數列表"
            };
            MenuFmtParameterSettings.Click += MenuFmtParameterSettings_Click;

            var parameterSettingsIndex = MainMenu.Items.IndexOf(MenuConfigTune) + 1;
            MainMenu.Items.Insert(parameterSettingsIndex, MenuFmtParameterSettings);

            MenuFmtPreflightCheck = new FmtQuickActionToolStripButton
            {
                Name = "MenuFmtPreflightCheck",
                Text = IsFmtTraditionalChineseUi ? "飛行前檢查" : "PREFLIGHT",
                Alignment = ToolStripItemAlignment.Left,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Size = new Size(116, 35),
                Margin = new Padding(8, 0, 4, 0),
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold),
                ToolTipText = IsFmtTraditionalChineseUi
                    ? "依目前飛行器構型顯示飛行前確認清單（目前不影響解鎖）"
                    : "Show the vehicle-specific preflight checklist (advisory only)"
            };
            MenuFmtPreflightCheck.Click += MenuFmtPreflightCheck_Click;

            MenuFmtArmDisarm = new FmtQuickActionToolStripButton
            {
                Name = "MenuFmtArmDisarm",
                Text = IsFmtTraditionalChineseUi ? "解鎖" : "ARM",
                Alignment = ToolStripItemAlignment.Left,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Size = new Size(116, 35),
                Margin = new Padding(0, 0, 4, 0),
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold),
                ToolTipText = IsFmtTraditionalChineseUi
                    ? "連動飛行資料動作頁的解鎖／上鎖功能"
                    : "Arm or disarm using the Flight Data action"
            };
            MenuFmtArmDisarm.Click += MenuFmtArmDisarm_Click;

            MenuFmtAirspeedZero = new FmtQuickActionToolStripButton
            {
                Name = "MenuFmtAirspeedZero",
                Text = IsFmtTraditionalChineseUi ? "空速計歸零" : "ZERO AIRSPEED",
                Alignment = ToolStripItemAlignment.Left,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Size = new Size(116, 35),
                Margin = new Padding(0, 0, 4, 0),
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold),
                ToolTipText = IsFmtTraditionalChineseUi
                    ? "未解鎖時執行空速感測器零點校正"
                    : "Zero the airspeed sensor while disarmed"
            };
            MenuFmtAirspeedZero.Click += MenuFmtAirspeedZero_Click;

            MenuFmtQnh = new FmtQuickActionToolStripButton
            {
                Name = "MenuFmtQnh",
                Text = IsFmtTraditionalChineseUi ? "QNH校正" : "QNH",
                Alignment = ToolStripItemAlignment.Left,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Size = new Size(116, 35),
                Margin = new Padding(0, 0, 4, 0),
                Font = new Font(SystemFonts.MenuFont, FontStyle.Bold),
                ToolTipText = IsFmtTraditionalChineseUi
                    ? "未解鎖時設定海平面氣壓（QNH）"
                    : "Set sea-level pressure (QNH) while disarmed"
            };
            MenuFmtQnh.Click += MenuFmtQnh_Click;

            var quickActionIndex = MainMenu.Items.IndexOf(MenuFmtParameterSettings) + 1;
            MainMenu.Items.Insert(quickActionIndex, MenuFmtPreflightCheck);
            MainMenu.Items.Insert(quickActionIndex + 1, MenuFmtArmDisarm);
            MainMenu.Items.Insert(quickActionIndex + 2, MenuFmtAirspeedZero);
            MainMenu.Items.Insert(quickActionIndex + 3, MenuFmtQnh);
            ApplyFmtQuickActionButtonStyle(MenuFmtPreflightCheck);
            ApplyFmtQuickActionButtonStyle(MenuFmtArmDisarm);
            ApplyFmtQuickActionButtonStyle(MenuFmtAirspeedZero);
            ApplyFmtQuickActionButtonStyle(MenuFmtQnh);
            UpdateFmtQuickActionButtons();

            const string resourceName = "MissionPlanner.FMT.Assets.fmt-app-icon-source.png";
            Image logo = null;
            using (var stream = typeof(MainV2).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var source = Image.FromStream(stream))
                        logo = new Bitmap(source, new Size(72, 31));
                }
            }

            MenuFeiMao = new ToolStripButton
            {
                Name = "MenuFeiMao",
                Text = "FMT",
                Alignment = ToolStripItemAlignment.Right,
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                Image = logo,
                ImageScaling = ToolStripItemImageScaling.None,
                AutoSize = false,
                Size = new Size(78, 35),
                Margin = Padding.Empty,
                ToolTipText = "FMT飛貓科技 - www.feimaotec.com"
            };
            MenuFeiMao.Click += (clickSender, args) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://www.feimaotec.com")
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    log.Warn("Unable to open the FMT website", ex);
                }
            };
            MainMenu.Items.Add(MenuFeiMao);

            const string gpsIconResourceName = "MissionPlanner.FMT.Assets.fmt-satellite-icon.png";
            Image gpsIcon = null;
            using (var stream = typeof(MainV2).Assembly.GetManifestResourceStream(gpsIconResourceName))
            {
                if (stream != null)
                {
                    using (var source = Image.FromStream(stream))
                        gpsIcon = new Bitmap(source, new Size(29, 29));
                }
            }

            var gpsStatusPanel = new Panel
            {
                Name = "FmtGpsStatusPanel",
                BackColor = Color.FromArgb(24, 24, 24),
                Size = new Size(150, 35),
                Margin = Padding.Empty
            };
            var gpsIconBox = new PictureBox
            {
                Name = "FmtGpsIcon",
                Location = new Point(2, 5),
                Size = new Size(25, 25),
                BackColor = Color.Transparent,
                Image = gpsIcon,
                SizeMode = PictureBoxSizeMode.Zoom,
                TabStop = false
            };
            FmtGpsPrimaryLabel = new Label
            {
                Name = "FmtGpsPrimaryLabel",
                AutoSize = false,
                Location = new Point(29, 1),
                Size = new Size(119, 17),
                BackColor = Color.Transparent,
                ForeColor = Color.Gray,
                Font = new Font(SystemFonts.MenuFont.FontFamily, 8.25f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = IsFmtTraditionalChineseUi ? "衛星: --  未連線" : "Sats: --  Disconnected"
            };
            FmtGpsDopLabel = new Label
            {
                Name = "FmtGpsDopLabel",
                AutoSize = false,
                Location = new Point(29, 17),
                Size = new Size(119, 16),
                BackColor = Color.Transparent,
                ForeColor = Color.Gray,
                Font = new Font(SystemFonts.MenuFont.FontFamily, 8.0f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "H: --  |  V: --"
            };
            gpsStatusPanel.Controls.Add(gpsIconBox);
            gpsStatusPanel.Controls.Add(FmtGpsPrimaryLabel);
            gpsStatusPanel.Controls.Add(FmtGpsDopLabel);

            MenuFmtGpsStatus = new ToolStripControlHost(gpsStatusPanel)
            {
                Name = "MenuFmtGpsStatus",
                Alignment = ToolStripItemAlignment.Right,
                AutoSize = false,
                Size = new Size(150, 35),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(24, 24, 24),
                ToolTipText = "GPS satellites, fix status, HDOP and VDOP"
            };
            MainMenu.Items.Add(MenuFmtGpsStatus);

            var flightTimePanel = new Panel
            {
                Name = "FmtFlightTimePanel",
                BackColor = Color.FromArgb(24, 24, 24),
                Size = new Size(164, 35),
                Margin = Padding.Empty
            };
            var flightTimeIconBox = new PictureBox
            {
                Name = "FmtFlightTimeIcon",
                Location = new Point(2, 5),
                Size = new Size(25, 25),
                BackColor = Color.Transparent,
                Image = CreateFmtFlightTimeIcon(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                TabStop = false
            };
            FmtFlightTimeLabel = new Label
            {
                Name = "FmtFlightTimeLabel",
                Location = new Point(29, 1),
                Size = new Size(133, 17),
                ForeColor = Color.LightSkyBlue,
                BackColor = Color.Transparent,
                Font = new Font(SystemFonts.MenuFont.FontFamily, 8.25f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "飛行時間：00:00:00"
            };
            FmtTotalFlightTimeLabel = new Label
            {
                Name = "FmtTotalFlightTimeLabel",
                Location = new Point(29, 17),
                Size = new Size(133, 16),
                ForeColor = Color.Gainsboro,
                BackColor = Color.Transparent,
                Font = new Font(SystemFonts.MenuFont.FontFamily, 8.0f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "飛控總飛時：-- 小時"
            };
            flightTimePanel.Controls.Add(flightTimeIconBox);
            flightTimePanel.Controls.Add(FmtFlightTimeLabel);
            flightTimePanel.Controls.Add(FmtTotalFlightTimeLabel);
            MenuFmtFlightTime = new ToolStripControlHost(flightTimePanel)
            {
                Name = "MenuFmtFlightTime",
                Alignment = ToolStripItemAlignment.Right,
                AutoSize = false,
                Size = new Size(164, 35),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(24, 24, 24),
                ToolTipText = "飛行時間與飛控累計總飛行時間"
            };
            // Right-aligned ToolStrip items are laid out in reverse insertion order.
            // Adding this after GPS places the flight-time panel immediately to its left.
            MainMenu.Items.Add(MenuFmtFlightTime);

            // Right alignment reverses insertion order: RPM -> MQTT -> flight time -> GPS.
            MenuFmtMqttTraffic = new FmtMqttTrafficIndicator(
                () => FlightData?.MqttTrafficSnapshot ?? default(FmtMqttTrafficSnapshot));
            MainMenu.Items.Add(MenuFmtMqttTraffic);

            var rotorRpmPanel = new Panel
            {
                Name = "FmtRotorRpmPanel",
                BackColor = Color.FromArgb(24, 24, 24),
                Size = new Size(112, 35),
                Margin = Padding.Empty
            };
            var rotorRpmIconBox = new PictureBox
            {
                Name = "FmtRotorRpmIcon",
                Location = new Point(2, 5),
                Size = new Size(25, 25),
                BackColor = Color.Transparent,
                Image = CreateFmtRotorRpmIcon(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                TabStop = false
            };
            FmtRotorRpmLabel = new Label
            {
                Name = "FmtRotorRpmLabel",
                Location = new Point(29, 1),
                Size = new Size(81, 32),
                ForeColor = Color.Gray,
                BackColor = Color.Transparent,
                Font = new Font(SystemFonts.MenuFont.FontFamily, 8.25f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "主旋翼\r\nRPM1：--"
            };
            rotorRpmPanel.Controls.Add(rotorRpmIconBox);
            rotorRpmPanel.Controls.Add(FmtRotorRpmLabel);
            MenuFmtRotorRpm = new ToolStripControlHost(rotorRpmPanel)
            {
                Name = "MenuFmtRotorRpm",
                Alignment = ToolStripItemAlignment.Right,
                AutoSize = false,
                Visible = false,
                Size = new Size(112, 35),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(24, 24, 24),
                ToolTipText = "RPM1 主旋翼轉速與上下限警告"
            };
            // MQTT automatically relinquishes its slot when stopped; RPM then adjoins flight time.
            MainMenu.Items.Add(MenuFmtRotorRpm);
            UpdateFmtQuickActionButtons();
        }

        private static Image CreateFmtRotorRpmIcon()
        {
            var image = new Bitmap(29, 29);
            using (var graphics = Graphics.FromImage(image))
            using (var line = new Pen(Color.WhiteSmoke, 2.0F))
            using (var accent = new Pen(Color.FromArgb(65, 194, 235), 2.0F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.DrawEllipse(accent, 3, 3, 23, 23);
                graphics.DrawLine(line, 14.5F, 7F, 14.5F, 22F);
                graphics.DrawLine(line, 7F, 14.5F, 22F, 14.5F);
                graphics.DrawEllipse(accent, 11.5F, 11.5F, 6F, 6F);
            }
            return image;
        }

        private static Image CreateFmtFlightTimeIcon()
        {
            var image = new Bitmap(29, 29);
            using (var graphics = Graphics.FromImage(image))
            using (var arrowPen = new Pen(Color.LightSkyBlue, 2.1F))
            using (var handPen = new Pen(Color.WhiteSmoke, 2.0F))
            using (var aircraftBrush = new SolidBrush(Color.WhiteSmoke))
            using (var aircraftPath = new System.Drawing.Drawing2D.GraphicsPath())
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                arrowPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                arrowPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                handPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                handPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                // Clock and return arrow, matching the supplied aircraft/time reference
                // while remaining readable on the dark FMT toolbar at 29 px.
                graphics.DrawArc(arrowPen, new RectangleF(7.5F, 2.5F, 18.5F, 18.5F), 205F, 248F);
                graphics.DrawLine(arrowPen, 24.0F, 17.5F, 24.2F, 22.2F);
                graphics.DrawLine(arrowPen, 24.0F, 22.0F, 19.8F, 20.4F);
                graphics.DrawLine(handPen, 17.0F, 6.0F, 17.0F, 12.2F);
                graphics.DrawLine(handPen, 17.0F, 12.2F, 21.0F, 15.0F);

                aircraftPath.AddPolygon(new[]
                {
                    new PointF(25.5F, 18.8F), new PointF(18.0F, 16.8F),
                    new PointF(14.3F, 12.2F), new PointF(12.3F, 12.2F),
                    new PointF(13.8F, 17.0F), new PointF(7.0F, 17.8F),
                    new PointF(3.9F, 15.2F), new PointF(2.4F, 15.6F),
                    new PointF(3.9F, 19.5F), new PointF(2.5F, 23.2F),
                    new PointF(4.0F, 23.7F), new PointF(7.1F, 21.0F),
                    new PointF(13.8F, 21.5F), new PointF(12.2F, 26.4F),
                    new PointF(14.3F, 26.4F), new PointF(18.1F, 21.8F),
                    new PointF(25.5F, 20.2F)
                });
                graphics.FillPath(aircraftBrush, aircraftPath);
            }

            return image;
        }

        private static Image CreateFmtParameterMenuIcon()
        {
            var image = new Bitmap(42, 29);
            using (var graphics = Graphics.FromImage(image))
            using (var linePen = new Pen(Color.WhiteSmoke, 2F))
            using (var knobBrush = new SolidBrush(Color.FromArgb(41, 171, 226)))
            using (var outlinePen = new Pen(Color.FromArgb(41, 171, 226), 1.5F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                var yValues = new[] { 6, 14, 22 };
                var knobValues = new[] { 13, 29, 20 };
                for (var index = 0; index < yValues.Length; index++)
                {
                    graphics.DrawLine(linePen, 4, yValues[index], 38, yValues[index]);
                    graphics.FillEllipse(knobBrush, knobValues[index] - 4, yValues[index] - 4, 8, 8);
                    graphics.DrawEllipse(outlinePen, knobValues[index] - 4, yValues[index] - 4, 8, 8);
                }
            }
            return image;
        }

        private static bool IsFmtTraditionalChineseUi =>
            CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        private void ApplyFmtQuickActionButtonStyle(ToolStripItem item)
        {
            if (item == null)
                return;

            Color fill;
            Color border;
            if (item == MenuFmtPreflightCheck)
            {
                fill = Color.FromArgb(18, 122, 72);
                border = Color.FromArgb(89, 224, 145);
            }
            else if (item == MenuFmtArmDisarm)
            {
                fill = Color.FromArgb(196, 32, 32);
                border = Color.FromArgb(255, 102, 102);
            }
            else
            {
                // Calibration buttons use the FMT dark/sky-blue treatment so they
                // cannot be confused with the red safety-critical arm control.
                fill = Color.FromArgb(18, 50, 65);
                border = Color.FromArgb(65, 194, 235);
            }

            // Match the toolbar behind the transparent bitmap corners so the
            // rounded silhouette remains clean on every ToolStrip renderer.
            item.BackColor = Color.FromArgb(24, 24, 24);
            item.ForeColor = Color.White;
            item.BackgroundImage = null;
            var fmtButton = item as FmtQuickActionToolStripButton;
            if (fmtButton != null)
            {
                fmtButton.FillColor = fill;
                fmtButton.BorderColor = border;
                fmtButton.Invalidate();
            }
        }

        private Image GetFmtRoundedButtonBackground(Size size, Color fill, Color border)
        {
            var width = Math.Max(1, size.Width);
            var height = Math.Max(1, size.Height);
            var key = width + "x" + height + ":" + fill.ToArgb() + ":" + border.ToArgb();
            Image existing;
            if (FmtQuickActionBackgrounds.TryGetValue(key, out existing))
                return existing;

            var image = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(image))
            using (var path = CreateFmtRoundedRectanglePath(new RectangleF(1, 1, width - 2, height - 2), 7F))
            using (var fillBrush = new SolidBrush(fill))
            using (var borderPen = new Pen(border, 1.4F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.FillPath(fillBrush, path);
                graphics.DrawPath(borderPen, path);
            }

            FmtQuickActionBackgrounds[key] = image;
            return image;
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateFmtRoundedRectanglePath(
            RectangleF bounds, float radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var diameter = Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height));
            var arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void UpdateFmtQuickActionButtons()
        {
            if (MenuFmtPreflightCheck == null || MenuFmtArmDisarm == null ||
                MenuFmtAirspeedZero == null || MenuFmtQnh == null)
                return;

            var connected = comPort?.BaseStream != null && comPort.BaseStream.IsOpen;
            var armed = connected && comPort.MAV.cs.armed;
            var gpsStatus = connected ? comPort.MAV.cs.gpsstatus : 0;
            var satCount = connected ? comPort.MAV.cs.satcount : 0;
            var hdop = connected ? comPort.MAV.cs.gpshdop : 0;
            var vdop = connected ? comPort.MAV.cs.gpsvdop : 0;
            var rotorRpm = connected ? comPort.MAV.cs.rpm1 : 0;
            var showRotorRpm = connected && IsFmtTraditionalHelicopter();
            var flightSeconds = connected ? Math.Max(0, comPort.MAV.cs.timeInAir) : 0;
            double totalFlightSeconds = 0;
            var hasTotalFlightTime = connected && TryGetFmtTotalFlightSeconds(out totalFlightSeconds);
            this.BeginInvokeIfRequired((Action)(() =>
            {
                MenuFmtPreflightCheck.Text = IsFmtTraditionalChineseUi ? "飛行前檢查" : "PREFLIGHT";
                MenuFmtPreflightCheck.Enabled = connected;
                MenuFmtArmDisarm.Text = IsFmtTraditionalChineseUi
                    ? (armed ? "上鎖" : "解鎖")
                    : (armed ? "DISARM" : "ARM");
                MenuFmtArmDisarm.Enabled = connected && !comPort.ReadOnly;
                MenuFmtAirspeedZero.Enabled = connected && !armed && !comPort.ReadOnly;
                MenuFmtQnh.Enabled = connected && !armed && !comPort.ReadOnly;
                ApplyFmtQuickActionButtonStyle(MenuFmtPreflightCheck);
                ApplyFmtQuickActionButtonStyle(MenuFmtArmDisarm);
                ApplyFmtQuickActionButtonStyle(MenuFmtAirspeedZero);
                ApplyFmtQuickActionButtonStyle(MenuFmtQnh);
                UpdateFmtGpsStatus(connected, gpsStatus, satCount, hdop, vdop);
                UpdateFmtRotorRpm(showRotorRpm, rotorRpm);
                UpdateFmtFlightTime(connected, flightSeconds,
                    hasTotalFlightTime ? (double?)totalFlightSeconds : null);
            }));
        }

        private bool IsFmtTraditionalHelicopter()
        {
            try
            {
                var mav = comPort?.MAV;
                if (mav == null || mav.cs.firmware != Firmwares.ArduCopter2)
                    return false;

                if (mav.aptype == MAVLink.MAV_TYPE.HELICOPTER)
                    return true;

                var parameters = mav.param;
                if (parameters == null)
                    return false;

                if (parameters.ContainsKey("H_RSC_MODE") || parameters.ContainsKey("H_SW_TYPE") ||
                    parameters.ContainsKey("H_SWASH_TYPE") || parameters.ContainsKey("H_COL_MIN"))
                    return true;

                if (!parameters.ContainsKey("FRAME_CLASS"))
                    return false;

                var frameClass = (int)Math.Round(parameters["FRAME_CLASS"].Value);
                return frameClass == 6 || frameClass == 11 || frameClass == 13;
            }
            catch (Exception ex)
            {
                log.Debug("Unable to identify helicopter for the FMT rotor RPM display", ex);
                return false;
            }
        }

        private void UpdateFmtRotorRpm(bool isHelicopter, float rpmValue)
        {
            if (FmtRotorRpmLabel == null || MenuFmtRotorRpm == null)
                return;

            var visibilityChanged = MenuFmtRotorRpm.Visible != isHelicopter;
            MenuFmtRotorRpm.Visible = isHelicopter;
            if (visibilityChanged)
                MainMenu.PerformLayout();

            if (!isHelicopter || float.IsNaN(rpmValue) || float.IsInfinity(rpmValue))
            {
                FmtRotorRpmLabel.Text = "主旋翼\r\nRPM1：--";
                FmtRotorRpmLabel.ForeColor = Color.Gray;
                return;
            }

            var rpm = Math.Max(0, Math.Min(9999, (int)Math.Round(rpmValue)));
            var lower = Settings.Instance.GetInt32("FMT_HeliRpmLower", 1000);
            var upper = Settings.Instance.GetInt32("FMT_HeliRpmUpper", 2500);
            FmtRotorRpmLabel.Text = "主旋翼\r\nRPM1：" + rpm.ToString(CultureInfo.InvariantCulture);
            FmtRotorRpmLabel.ForeColor = rpm > 0 && (rpm < lower || rpm > upper)
                ? Color.OrangeRed
                : rpm > 0 ? Color.LimeGreen : Color.Gold;
        }

        private bool TryGetFmtTotalFlightSeconds(out double seconds)
        {
            seconds = 0;
            try
            {
                if (comPort.MAV.param.ContainsKey("STAT_FLTTIME"))
                {
                    seconds = Math.Max(0, comPort.MAV.param["STAT_FLTTIME"].Value);
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Debug("Unable to read STAT_FLTTIME", ex);
            }
            return false;
        }

        private void UpdateFmtFlightTime(bool connected, double flightSeconds, double? totalFlightSeconds)
        {
            if (FmtFlightTimeLabel == null || FmtTotalFlightTimeLabel == null)
                return;

            var seconds = connected ? Math.Max(0L, (long)Math.Round(flightSeconds)) : 0L;
            FmtFlightTimeLabel.Text = string.Format(CultureInfo.InvariantCulture,
                "飛行時間：{0:00}:{1:00}:{2:00}", seconds / 3600, (seconds / 60) % 60, seconds % 60);
            FmtFlightTimeLabel.ForeColor = connected ? Color.LightSkyBlue : Color.Gray;
            FmtTotalFlightTimeLabel.Text = totalFlightSeconds.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "飛控總飛時：{0:0.0} 小時",
                    totalFlightSeconds.Value / 3600.0)
                : "飛控總飛時：-- 小時";
            FmtTotalFlightTimeLabel.ForeColor = connected ? Color.Gainsboro : Color.Gray;
        }

        private void UpdateFmtGpsStatus(bool connected, float gpsStatus, float satCount, float hdop, float vdop)
        {
            if (FmtGpsPrimaryLabel == null || FmtGpsDopLabel == null)
                return;

            if (!connected)
            {
                FmtGpsPrimaryLabel.Text = IsFmtTraditionalChineseUi
                    ? "衛星: --  未連線"
                    : "Sats: --  Disconnected";
                FmtGpsDopLabel.Text = "H: --  |  V: --";
                FmtGpsPrimaryLabel.ForeColor = Color.Gray;
                FmtGpsDopLabel.ForeColor = Color.Gray;
                return;
            }

            var fixLabel = GetFmtGpsFixLabel(gpsStatus, IsFmtTraditionalChineseUi);
            var fixColor = GetFmtGpsFixColor(gpsStatus);
            FmtGpsPrimaryLabel.Text = string.Format(CultureInfo.InvariantCulture,
                IsFmtTraditionalChineseUi ? "衛星: {0:0}  {1}" : "Sats: {0:0}  {1}",
                Math.Max(0, satCount), fixLabel);
            FmtGpsDopLabel.Text = string.Format(CultureInfo.InvariantCulture,
                "H: {0}  |  V: {1}", FormatFmtDop(hdop), FormatFmtDop(vdop));
            FmtGpsPrimaryLabel.ForeColor = fixColor;
            FmtGpsDopLabel.ForeColor = gpsStatus >= 3 ? Color.LightGreen : fixColor;
        }

        internal static string GetFmtGpsFixLabel(float gpsStatus, bool traditionalChinese)
        {
            if (traditionalChinese)
            {
                switch ((int)Math.Round(gpsStatus))
                {
                    case 0: return "無 GPS";
                    case 1: return "未定位";
                    case 2: return "2D 定位";
                    case 3: return "3D 定位";
                    case 4: return "DGPS";
                    case 5: return "RTK 浮點解";
                    case 6: return "RTK 固定解";
                    case 7: return "靜態定位";
                    case 8: return "PPP";
                    default: return "未知";
                }
            }

            switch ((int)Math.Round(gpsStatus))
            {
                case 0: return "No GPS";
                case 1: return "No Fix";
                case 2: return "2D Fix";
                case 3: return "3D Fix";
                case 4: return "DGPS";
                case 5: return "RTK Float";
                case 6: return "RTK Fixed";
                case 7: return "Static";
                case 8: return "PPP";
                default: return "Unknown";
            }
        }

        private static Color GetFmtGpsFixColor(float gpsStatus)
        {
            if (gpsStatus >= 6)
                return Color.Lime;
            if (gpsStatus >= 3)
                return Color.LimeGreen;
            if (gpsStatus >= 2)
                return Color.Gold;
            return Color.OrangeRed;
        }

        private static string FormatFmtDop(float value)
        {
            return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value)
                ? value.ToString("0.0", CultureInfo.InvariantCulture)
                : "--";
        }

        private void MenuFmtArmDisarm_Click(object sender, EventArgs e)
        {
            FlightData?.ExecuteFmtArmDisarm();
            UpdateFmtQuickActionButtons();
        }

        private void MenuFmtPreflightCheck_Click(object sender, EventArgs e)
        {
            FlightData?.ExecuteFmtPreflightCheck();
        }

        private void MenuFmtAirspeedZero_Click(object sender, EventArgs e)
        {
            FlightData?.ExecuteFmtAirspeedZero();
            UpdateFmtQuickActionButtons();
        }

        private void MenuFmtQnh_Click(object sender, EventArgs e)
        {
            FlightData?.ExecuteFmtQnh();
            UpdateFmtQuickActionButtons();
        }

        private void MenuArduPilot_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://ardupilot.org/?utm_source=Menu&utm_campaign=MP");
            }
            catch
            {
                CustomMessageBox.Show("Failed to open url https://ardupilot.org");
            }
        }

        private void connectionListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.ShowDialog();

            if (File.Exists(openFileDialog.FileName))
            {
                var lines = File.ReadAllLines(openFileDialog.FileName);

                Regex tcp = new Regex("tcp://(.*):([0-9]+)");
                Regex udp = new Regex("udp://(.*):([0-9]+)");
                Regex udpcl = new Regex("udpcl://(.*):([0-9]+)");
                Regex serial = new Regex("serial:(.*):([0-9]+)");

                ConcurrentBag<MAVLinkInterface> mavs = new ConcurrentBag<MAVLinkInterface>();

                Parallel.ForEach(lines, line =>
                        //foreach (var line in lines)
                    {
                        try
                        {
                            Console.WriteLine("Process port " + line);
                            MAVLinkInterface mav = new MAVLinkInterface();

                            if (tcp.IsMatch(line))
                            {
                                var matches = tcp.Match(line);
                                var tc = new TcpSerial();
                                tc.client = new TcpClient(matches.Groups[1].Value, int.Parse(matches.Groups[2].Value));
                                mav.BaseStream = tc;
                            }
                            else if (udp.IsMatch(line))
                            {
                                var matches = udp.Match(line);
                                var uc = new UdpSerial(new UdpClient(int.Parse(matches.Groups[2].Value)));
                                uc.Port = matches.Groups[2].Value;
                                mav.BaseStream = uc;
                            }
                            else if (udpcl.IsMatch(line))
                            {
                                var matches = udpcl.Match(line);
                                var udc = new UdpSerialConnect();
                                udc.Port = matches.Groups[2].Value;
                                udc.client = new UdpClient(matches.Groups[1].Value, int.Parse(matches.Groups[2].Value));
                                mav.BaseStream = udc;
                            }
                            else if (serial.IsMatch(line))
                            {
                                var matches = serial.Match(line);
                                var port = new Comms.SerialPort();
                                port.PortName = matches.Groups[1].Value;
                                port.BaudRate = int.Parse(matches.Groups[2].Value);
                                mav.BaseStream = port;
                                ((SerialPort)mav.BaseStream).espFix = Settings.Instance.GetBoolean("CHK_rtsresetesp32", false);
                                mav.BaseStream.Open();
                            }
                            else
                            {
                                return;
                            }

                            mavs.Add(mav);
                        }
                        catch
                        {
                        }
                    }
                );
                /*
                foreach (var mav in mavs)
                {
                    MainV2.instance.BeginInvoke((Action) delegate
                    {
                        doConnect(mav, "preset", "0", false, false);
                        Comports.Add(mav);
                    });
                }

                */

                Parallel.ForEach(mavs, mav =>
                {
                    Console.WriteLine("Process connect " + mav);
                    doConnect(mav, "preset", "0", false, false);
                    Comports.Add(mav);

                    try
                    {
                        Comports = Comports.Distinct().ToList();
                    }
                    catch { }
                });
            }
        }

        //Handle QV panel coloring from warning engine
        private void WarningEngine_QuickPanelColoring(string name, string color)
        {
            // return if we still initialize
            if (FlightData == null) return;

            //Find panel with
            foreach (var q in FlightData.tabQuick.Controls["tableLayoutPanelQuick"].Controls)
            {
                QuickView qv = (QuickView) q;

                //Get the data field name bind to the control
                var fieldname = qv.DataBindings[0].BindingMemberInfo.BindingField;

                if (fieldname == name)
                {

                    if (color == "NoColor")
                    {
                        qv.BackColor = ThemeManager.BGColor;
                        qv.numberColor = qv.numberColorBackup; //Restore original color from backup :)
                        qv.ForeColor = ThemeManager.TextColor;


                    }
                    else
                    {
                        qv.BackColor = Color.FromName(color);
                        // Ensure color is readable on the background
                        qv.numberColor = (((qv.BackColor.R + qv.BackColor.B + qv.BackColor.G) / 3) > 128)
                            ? Color.Black
                            : Color.White;
                        qv.ForeColor = qv.numberColor; //Same as the number
                    }

                    //We have our panel, color it and exit loop
                    break;
                }
            }
        }

        private void WarningEngine_WarningMessage(object sender, string message)
        {
            MainV2.comPort.MAV.cs.messageHigh = message;
        }
    }
}
