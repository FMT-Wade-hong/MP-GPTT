using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MissionPlanner;
using MissionPlanner.GCSViews.ConfigurationView;
using MissionPlanner.Utilities;

internal static class MetadataDownloadHarness
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Type Downloader = typeof(ParameterMetaDataRepositoryAPMpdef);
    static int Checks, Requests;
    static readonly List<string> Addresses = new List<string>();
    static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Reply;
    static void Check(bool ok, string text) { Checks++; if (!ok) throw new Exception(text); }
    static object Field(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    static string Get(string name, string field, string key) => ParameterMetaDataRepository.GetParameterMetaData(name, field, key);
    static string Xml(string family = "ArduCopter", string description = "Downloaded exact description", string parameter = "ATC_RAT_RLL_P") =>
        "<paramfile><vehicles><parameters name='" + family + "'><param name='" + family + ":" + parameter +
        "' humanName='Roll rate P' documentation='" + description + "' user='Advanced'><field name='Units'>m/s</field><field name='Range'>0 10</field>" +
        "<field name='Increment'>0.01</field></param></parameters></vehicles></paramfile>";
    static HttpResponseMessage Response(string xml, bool gzip = false)
    {
        byte[] data = Encoding.UTF8.GetBytes(xml);
        if (gzip)
            using (var bytes = new MemoryStream())
            {
                using (var stream = new GZipStream(bytes, CompressionMode.Compress, true)) stream.Write(data, 0, data.Length);
                data = bytes.ToArray();
            }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
    }
    sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Requests);
            lock (Addresses) Addresses.Add(request.RequestUri.AbsoluteUri);
            return Reply(request, token);
        }
    }
    static Task<bool> Download(string key, bool force = false) => ParameterMetaDataRepositoryAPMpdef.GetMetaDataVersioned(key, force);
    static void NewCache(string stage)
    {
        Settings.CustomUserDataDirectory = Path.GetFullPath("tmp/metadata473-tests/" + stage + "-" + Guid.NewGuid().ToString("N"));
        Settings.AppConfigName = "MetadataTest"; Settings.FileName = "metadata-test.config.xml";
        Directory.CreateDirectory(Settings.GetUserDataDirectory());
        ParameterMetaDataRepositoryAPMpdef.Reset();
    }
    static string CacheFile(string key) => Path.Combine(Settings.GetUserDataDirectory(), "ParameterMetadata", key + ".apm.pdef.xml");

    static void Compatibility()
    {
        foreach (var version in new[] { "4.7.4", "4.8.0", "5.0.0" })
        {
            var key = ParameterMetaDataRepository.GetFirmwareKey("ArduCopter2", "ArduCopter V" + version);
            Check(key == "Copter" + version, "Future version was mapped to a generic vehicle");
            Check(Get("ATC_RAT_RLL_P", "Description", key) == Get("ATC_RAT_RLL_P", "Description", "Copter4.7.3"), "Same-name reference text missing");
            Check(ParameterMetaDataRepository.GetCompatibilitySource("ATC_RAT_RLL_P", key) == "Copter4.7.3", "Reference source not identified");
            foreach (var field in new[] { "Units", "Range", "Increment", "Values", "Bitmask", "ReadOnly", "RebootRequired" })
                Check(Get("ATC_RAT_RLL_P", field, key) == "", "Reference inferred write semantics: " + field);
            Check(Get("WPNAV_SPEED", "Description", key) == "", "Renamed parameter resurrected from an older release");
            Check(Get("FMT_UNKNOWN", "Description", key) == "", "Invented parameter metadata");
        }
        Check(ParameterMetaDataRepository.GetFirmwareKey("Heli", "ArduCopter V4.8.0") == "Heli4.8.0", "Future helicopter lost frame identity");
        Check(ParameterMetaDataRepository.GetCompatibilitySource("ATC_RAT_RLL_P", "Heli4.8.0") == "Heli4.7.3", "Helicopter reference mixed with multirotor");
        Check(Get("ATC_RAT_RLL_P", "Range", "Copter4.7.3") == "0.01 0.5", "Exact metadata lost range");
        Check(Get("ATC_RAT_RLL_P", "Range", "Heli4.7.3") == "0.0 0.35", "Helicopter metadata wrong");
        Check(Get("ARMING_CHECK", "Bitmask", "Copter4.7.3") == "", "Missing exact field inherited old bitmask");
        Console.WriteLine("PASS: 4.7.3 exact data; future patch/minor/major names; helicopter; missing/renamed names; reference-only semantics.");
    }

    static async Task Network()
    {
        Reply = (request, token) =>
        {
            var path = request.RequestUri.AbsolutePath;
            if (path.Contains("/Copter/")) return Task.FromResult(Response(Xml()));
            if (path.Contains("/Rover/") && path.EndsWith(".gz")) return Task.FromResult(Response(Xml("Rover"), true));
            if (path.Contains("/Sub/")) return Task.FromResult(Response("<paramfile>broken"));
            if (path.Contains("/Tracker/")) return Task.FromResult(Response("<paramfile/>"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };
        await ParameterMetaDataRepositoryAPMpdef.GetMetaDataVersioned(new Version(4, 8, 77));
        Check(Get("ATC_RAT_RLL_P", "Description", "Copter4.8.77") == "Downloaded exact description", "One family failure blocked a successful family");
        Check(Get("ATC_RAT_RLL_P", "Units", "Rover4.8.77") == "m/s", "Gzip fallback failed");
        Check(!ParameterMetaDataRepositoryAPMpdef.ContainsVersion("Sub4.8.77"), "Malformed response accepted");
        Check(!ParameterMetaDataRepositoryAPMpdef.ContainsVersion("Tracker4.8.77"), "Empty response accepted");
        Check(Addresses.Any(x => x.EndsWith("/Copter/stable-4.8.77/apm.pdef.xml")), "Versioned URL wrong");

        var good = File.ReadAllBytes(CacheFile("Copter4.8.77"));
        foreach (var bad in new[] { "<html>Error</html>", "<paramfile>truncated", Xml("ArduPlane"), "<!DOCTYPE paramfile [<!ENTITY x SYSTEM 'file:///missing'>]><paramfile>&x;</paramfile>" })
        {
            Reply = (request, token) => Task.FromResult(Response(bad));
            Check(await Download("Copter4.8.77", true), "Failed replacement discarded a valid cache");
            Check(good.SequenceEqual(File.ReadAllBytes(CacheFile("Copter4.8.77"))), "Bad XML replaced valid disk cache");
            Check(Get("ATC_RAT_RLL_P", "Units", "Copter4.8.77") == "m/s", "Bad XML replaced live index");
        }
        Reply = (request, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        int before = Requests;
        Check(!await Download("Copter4.9.20"), "Failed new release reported available");
        Check(Requests == before + 2, "Did not try XML and gzip independently");
        Check(!await Download("Copter4.9.20") && Requests == before + 2, "Failed version retried on every lookup");

        Reply = async (request, token) => { await Task.Delay(30, token); return Response(Xml()); };
        before = Requests;
        var result = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Download("Copter4.9.21")));
        Check(result.All(x => x) && Requests == before + 1, "Concurrent requests not coalesced");
        Check(await Download("Heli4.9.21") && Requests == before + 1, "Helicopter did not share physical download");
        Check(Get("ATC_RAT_RLL_P", "Units", "Heli4.9.21") == "m/s", "Downloaded helicopter key not indexed");

        Downloader.GetField("DownloadTimeout", Flags).SetValue(null, TimeSpan.FromMilliseconds(60));
        Reply = async (request, token) => { await Task.Delay(Timeout.Infinite, token); return Response(Xml()); };
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Check(!await Download("Copter4.9.22") && watch.ElapsedMilliseconds < 2000, "Timeout not bounded");
        Downloader.GetField("DownloadTimeout", Flags).SetValue(null, TimeSpan.FromSeconds(12));

        var broken = CacheFile("Copter4.9.23");
        Directory.CreateDirectory(Path.GetDirectoryName(broken)); File.WriteAllText(broken, "<html>bad cached response</html>");
        Reply = (request, token) => Task.FromResult(Response(Xml()));
        Check(await Download("Copter4.9.23"), "Fresh corrupt cache blocked retry");
        Check(File.ReadAllText(broken).Contains("Downloaded exact description"), "Corrupt cache not repaired");
        Check(Directory.GetFiles(Path.GetDirectoryName(broken), "*.tmp").Length == 0, "Temporary cache files leaked");
        Parallel.For(0, 2000, i => CheckRead("Copter4.9.23"));
        ParameterMetaDataRepositoryAPMpdef.Reset();
        Check(ParameterMetaDataRepository.GetCompatibilitySource("ATC_RAT_RLL_P", "Copter4.9.24") == "Copter4.9.23", "Restart ignored the newest valid disk cache");
        Check(Get("ATC_RAT_RLL_P", "Description", "Copter4.9.24") == "Downloaded exact description", "Disk-cache reference not loaded");
        Console.WriteLine("PASS: XML/gzip, per-family failure, corrupt/empty/wrong-family/XML-entity responses, atomic cache, timeout, retry cooldown, 20 concurrent downloads, 2000 reads.");
    }
    static void CheckRead(string key) { if (Get("ATC_RAT_RLL_P", "Units", key) != "m/s") throw new Exception("Concurrent metadata read failed"); }

    static void Vehicle(string version, params string[] names)
    {
        var mav = MainV2.comPort.MAV;
        mav.cs.firmware = (MissionPlanner.ArduPilot.Firmwares)Enum.Parse(mav.cs.firmware.GetType(), "ArduCopter2");
        mav.aptype = MAVLink.MAV_TYPE.QUADROTOR; mav.VersionString = "ArduCopter V" + version;
        mav.param.Clear();
        foreach (var name in names) mav.param[name] = new MAVLink.MAVLinkParam(name, 0.1234567, MAVLink.MAV_PARAM_TYPE.REAL32);
    }
    static ConfigRawParams Raw(string version)
    {
        Vehicle(version, "ATC_RAT_RLL_P", "WP_SPD", "ARMING_SKIPCHK");
        var page = new ConfigRawParams();
        Set(page, "fmtMetadataVehicle", "Copter" + version);
        typeof(ConfigRawParams).GetField("startup", Flags).SetValue(null, true);
        Call(page, "processToScreen");
        return page;
    }
    static void UI()
    {
        NewCache("ui");
        Reply = (request, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        using (var page = Raw("4.7.3"))
        {
            var grid = (DataGridView)Field(page, "Params");
            Check(grid.Rows.Cast<DataGridViewRow>().All(row => !Convert.ToString(row.Cells["Desc"].Value).Contains("缺少對應版本")), "4.7.3 grid descriptions missing");
            Check(((IDictionary)Field(page, "_changes")).Count == 0, "Rendering staged writes");
        }
        using (var page = Raw("4.8.88"))
        {
            var grid = (DataGridView)Field(page, "Params");
            var row = grid.Rows.Cast<DataGridViewRow>().Single(r => Convert.ToString(r.Cells["Command"].Value) == "ATC_RAT_RLL_P");
            Check(Convert.ToString(row.Cells["Desc"].Value).Contains("同名參數參考說明：Copter4.7.3"), "UI did not label reference metadata");
            row.Cells["Value"].Value = "0.2345678";
            var changes = (IDictionary)Field(page, "_changes"); changes["ATC_RAT_RLL_P"] = 0.2345678f;
            Reply = (request, token) => Task.FromResult(Response(Xml()));
            Check(Download("Copter4.8.88", true).GetAwaiter().GetResult(), "UI fixture download failed");
            Call(page, "RefreshFmtMetadata");
            Check(Convert.ToString(row.Cells["Units"].Value) == "m/s" && Convert.ToString(row.Cells["Desc"].Value).Contains("Downloaded exact description"), "Active grid did not refresh after download");
            Check(Convert.ToString(row.Cells["Value"].Value) == "0.2345678" && changes.Count == 1, "Metadata refresh discarded pending edit");
            Check(Math.Abs(MainV2.comPort.MAV.param["ATC_RAT_RLL_P"].Value - 0.1234567) < 1e-7, "Metadata refresh changed aircraft value cache");
        }
        NewCache("friendly"); Vehicle("4.8.89", "ATC_RAT_RLL_P");
        Reply = (request, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        using (var page = new ConfigFriendlyParams { ParameterMode = Get("ATC_RAT_RLL_P", "User", "Copter4.7.3") })
        {
            page.Activate();
            var panel = (FlowLayoutPanel)Field(page, "flowLayoutPanel1");
            var control = panel.Controls.Cast<Control>().Single(c => c.Name == "ATC_RAT_RLL_P");
            Check(control.GetType().Name == "FmtReferenceParameterControl", "Future same-name parameter disappeared from friendly page");
            var editor = (TextBox)Field(control, "editor"); editor.Text = "0.02345678";
            var validating = typeof(Control).GetMethod("OnValidating", Flags);
            var args = new CancelEventArgs(); validating.Invoke(editor, new object[] { args });
            Check(!args.Cancel && ((IDictionary)Field(page, "_params_changed")).Count == 1, "Reference numeric edit was not staged");
            editor.Text = "NaN"; args = new CancelEventArgs(); validating.Invoke(editor, new object[] { args });
            Check(args.Cancel, "Reference editor accepted non-finite value");
            page.Deactivate();
        }
        Console.WriteLine("PASS: actual 4.7.3/raw/friendly pages, annotated future reference, download-to-visible-grid refresh, staged edits preserved, no aircraft values written.");
    }

    static void Demo()
    {
        NewCache("demo");
        var page = Raw("4.7.3");
        var form = new Form { Text = "FMTPlanner 4.7.3 參數資料 — 離線測試", Width = 1250, Height = 720, StartPosition = FormStartPosition.CenterScreen };
        var label = new Label { Text = "離線範例：Copter 4.7.3 對應說明、單位與選項。未連接飛控，不會寫入參數。", Dock = DockStyle.Top, Height = 42, ForeColor = Color.White, BackColor = Color.FromArgb(23, 39, 48) };
        page.Dock = DockStyle.Fill; form.Controls.Add(page); form.Controls.Add(label);
        foreach (Control control in page.Controls.Find("BUT_writePIDS", true)) control.Enabled = false;
        var grid = (DataGridView)Field(page, "Params"); grid.ReadOnly = true;
        form.FormClosed += (sender, args) => page.Dispose();
        Application.Run(form);
    }
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-TW"); CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            Settings.config.Clear(); NewCache("network");
            if (args.Contains("--live"))
            {
                log4net.Config.BasicConfigurator.Configure();
                Check(Download("Copter4.5.7").GetAwaiter().GetResult(), "Official live metadata download failed");
                Check(Get("WPNAV_SPEED", "Units", "Copter4.5.7") == "cm/s", "Live exact metadata units incorrect");
                Check(File.Exists(CacheFile("Copter4.5.7")), "Live metadata not persisted in per-user cache");
                Console.WriteLine("PASS: real official Copter 4.5.7 endpoint; validated exact XML and per-user cache; no vehicle connection.");
                return 0;
            }
            Downloader.GetField("client", Flags).SetValue(null, new HttpClient(new Handler()));
            Reply = (request, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            if (args.Contains("--demo")) { Demo(); return 0; }
            Compatibility(); Network().GetAwaiter().GetResult(); UI();
            Console.WriteLine("PASS: " + Checks + " metadata compatibility/download/UI checks.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
