using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner.Utilities;

internal static class AdsbHttpHarness
{
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            var path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try
        {
            if (args.Length > 1 && args[1] == "--live") RunLive();
            else Run();
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        CheckTaiwanCoverage();
        CheckProviderConfiguration();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = new ManualResetEventSlim();
        adsb.PointLatLngAltHdg plane = null;
        int acceptedUpdates = 0;
        int unexpectedUpdates = 0;
        int requestsReceived = 0;
        EventHandler<adsb.PointLatLngAltHdg> handler = (sender, value) =>
        {
            if (value.Tag != "ABC123")
            {
                Interlocked.Increment(ref unexpectedUpdates);
                return;
            }
            plane = value;
            if (Interlocked.Increment(ref acceptedUpdates) == 2) received.Set();
        };
        var fileHook = typeof(Download).GetField("RequestModification", BindingFlags.Static | BindingFlags.NonPublic);
        var originalHook = fileHook.GetValue(null);
        adsb.UpdatePlanePosition += handler;
        try
        {
            // Exercise the real polling thread: a rejected request must retry, then publish traffic.
            var server = Task.Run(() =>
            {
                var first = Reply(listener, "403 Forbidden", "User-Agent required");
                Interlocked.Increment(ref requestsReceived);
                var second = Reply(listener, "200 OK", AircraftResponse(false));
                var interval = Stopwatch.StartNew();
                var third = Reply(listener, "200 OK", AircraftResponse(true));
                Check(interval.ElapsedMilliseconds >= 1800 && interval.ElapsedMilliseconds < 4500,
                    "successful polling uses the two-second interval");
                foreach (var request in new[] { first, second, third })
                {
                    Check(request.Contains("GET /v2/point/25/121/100 HTTP/1.1"), "query stays at 100 nm even after a sparse response");
                    Check(request.Contains("User-Agent: FMTPlanner/1.2.3-test (+https://github.com/FMT-Wade-hong/MP-GPTT)"),
                        "ADS-B request carries version and project contact");
                }
            });
            adsb.server = endpoint;
            adsb.CurrentPosition = new PointLatLngAlt(35, 139);
            adsb.ApplicationVersion = "1.2.3-test";
            new adsb();
            Thread.Sleep(1000);
            Check(Volatile.Read(ref requestsReceived) == 0 && !listener.Pending() && !server.IsCompleted,
                "no API request while map center is outside Taiwan ADIZ");
            adsb.CurrentPosition = new PointLatLngAlt(25, 121);
            Check(received.Wait(TimeSpan.FromSeconds(30)), "traffic resumes inside ADIZ and retries HTTP 403");
            adsb.Stop();
            server.GetAwaiter().GetResult();
            Check(unexpectedUpdates == 0 && acceptedUpdates == 2,
                "2000 outside aircraft are discarded before aircraft/map events");
            Check(plane.Tag == "ABC123" && plane.CallSign == "TEST123", "aircraft identity");
            Check(plane.Lat == 25.1 && plane.Lng == 121.2 && Math.Abs(plane.Alt - 1000) < 0.01,
                "aircraft map coordinates and altitude");
            Check(plane.Heading == 90 && Math.Abs(plane.Speed - 5144.4) < 0.01 && plane.Squawk == 0x1200,
                "aircraft heading, speed and squawk");
            Check(Equals(fileHook.GetValue(null), originalHook), "ADS-B leaves file-download handlers unchanged");

            // Keep the existing one-argument API compatible; ADS-B identification must not leak to other downloads.
            var otherServer = Task.Run(() => Reply(listener, "403 Forbidden", "test rejection"));
            var result = Download.GetAsyncWithStatus(endpoint + "/other").GetAwaiter().GetResult();
            Check(result.status == HttpStatusCode.Forbidden && result.content == "test rejection",
                "HTTP status and body remain available on failure");
            Check(!otherServer.GetAwaiter().GetResult().Contains("FMTPlanner/"), "identification is scoped to ADS-B");
            Console.WriteLine("PASS: Taiwan ADIZ boundaries, bounded polling, retry, identification and aircraft updates");
        }
        finally
        {
            adsb.Stop();
            adsb.UpdatePlanePosition -= handler;
            listener.Stop();
            received.Dispose();
        }
    }

    private static void CheckTaiwanCoverage()
    {
        foreach (var p in new[] { new[] { 21.0, 117.5 }, new[] { 21.0, 121.5 }, new[] { 22.5, 123.0 },
            new[] { 29.0, 123.0 }, new[] { 29.0, 117.5 }, new[] { 25.03, 121.56 },
            new[] { 22.62, 120.3 }, new[] { 23.57, 119.58 }, new[] { 24.43, 118.32 }, new[] { 26.16, 119.94 } })
            Check(AdsbTaiwanCoverage.Contains(p[0], p[1]), "ADIZ vertex / Taiwan and offshore islands included");
        foreach (var p in new[] { new[] { 20.999, 120.0 }, new[] { 29.001, 120.0 },
            new[] { 25.0, 117.499 }, new[] { 25.0, 123.001 }, new[] { 21.5, 122.001 },
            new[] { 0.0, 0.0 }, new[] { double.NaN, 121.0 }, new[] { 25.0, double.PositiveInfinity } })
            Check(!AdsbTaiwanCoverage.Contains(p[0], p[1]), "outside or invalid coordinates rejected");
        Check(AdsbTaiwanCoverage.Contains(21.5, 122) && !AdsbTaiwanCoverage.Contains(21.49, 122),
            "southeast diagonal is enforced rather than a rectangular approximation");
        Check(AdsbTaiwanCoverage.CreateQueryUrl("https://example.test", null) == null &&
            AdsbTaiwanCoverage.CreateQueryUrl("https://example.test", PointLatLngAlt.Zero) == null &&
            AdsbTaiwanCoverage.CreateQueryUrl("https://example.test", new PointLatLngAlt(35, 139)) == null,
            "missing and foreign query centers do not generate requests");
        var culture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Check(AdsbTaiwanCoverage.CreateQueryUrl(" https://example.test/ ", new PointLatLngAlt(25.1, 121.2)) ==
                "https://example.test/v2/point/25.1/121.2/100", "query formatting is invariant and radius is bounded");
            foreach (var server in new[] { AdsbApi.DefaultServer, "https://opendata.adsb.fi/", " https://api.adsb.fi/ " })
                Check(AdsbTaiwanCoverage.CreateQueryUrl(server, new PointLatLngAlt(25.1, 121.2)) ==
                    "https://opendata.adsb.fi/api/v3/lat/25.1/lon/121.2/dist/100",
                    "adsb.fi uses its v3 radius endpoint with an invariant 100 nm limit");
            Check(AdsbTaiwanCoverage.CreateQueryUrl(AdsbApi.DefaultServer, new PointLatLngAlt(35, 139)) == null,
                "adsb.fi does not query a center outside Taiwan ADIZ");
        }
        finally { Thread.CurrentThread.CurrentCulture = culture; }
    }

    private static void CheckProviderConfiguration()
    {
        // Exercise the real settings migration without loading or saving the user's config file.
        var originalConfig = Settings.config;
        Settings.config = new Dictionary<string, string>();
        try
        {
            var settings = new Settings();
            settings["adsbserver"] = " https://api.adsb.lol/ ";
            Check(AdsbApi.GetConfiguredServer(settings) == AdsbApi.DefaultServer &&
                settings["adsbserver"] == AdsbApi.DefaultServer, "saved adsb.lol selection migrates to adsb.fi");
            settings["adsbserver"] = "https://api.adsb.lol";
            Check(AdsbApi.GetConfiguredServer(settings) == "https://api.adsb.lol",
                "migration runs once and respects a later explicit provider choice");
            Settings.config.Clear();
            Check(AdsbApi.GetConfiguredServer(settings) == AdsbApi.DefaultServer,
                "new settings default to adsb.fi");
            Settings.config.Clear();
            settings["adsbserver"] = "192.168.1.10";
            settings["adsbport"] = "30003";
            Check(AdsbApi.GetConfiguredServer(settings) == "192.168.1.10" && settings["adsbport"] == "30003",
                "custom TCP receivers retain their address and port");
            Settings.config.Clear();
            settings["adsbserver"] = "https://example.test/api";
            Check(AdsbApi.GetConfiguredServer(settings) == "https://example.test/api",
                "custom HTTP providers remain selected");
            Check(AdsbApi.IsAdsbFi("https://opendata.adsb.fi/api") &&
                !AdsbApi.IsAdsbFi("https://opendata.adsb.fi.example.test/api"), "provider matching checks the exact host");
        }
        finally { Settings.config = originalConfig; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunLive()
    {
        // Match Program.cs: this standalone harness otherwise inherits legacy .NET TLS defaults.
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        var center = new PointLatLngAlt(25.08, 121.23);
        var completed = new ManualResetEventSlim();
        var counts = new Dictionary<string, int>();
        var timer = Stopwatch.StartNew();
        string firstId = null;
        long firstMilliseconds = 0;
        long secondMilliseconds = 0;
        bool valid = true;
        EventHandler<adsb.PointLatLngAltHdg> handler = (sender, plane) =>
        {
            valid &= AdsbTaiwanCoverage.Contains(plane.Lat, plane.Lng) &&
                plane.DataSource == AdsbApi.AdsbFiAttribution && center.GetDistance(plane) <= 100 * 1852 + 500;
            counts[plane.Tag] = counts.ContainsKey(plane.Tag) ? counts[plane.Tag] + 1 : 1;
            if (firstId == null)
            {
                firstId = plane.Tag;
                firstMilliseconds = timer.ElapsedMilliseconds;
            }
            else if (plane.Tag == firstId && counts[plane.Tag] == 2)
            {
                secondMilliseconds = timer.ElapsedMilliseconds;
                completed.Set();
            }
        };
        adsb.UpdatePlanePosition += handler;
        try
        {
            adsb.server = AdsbApi.DefaultServer;
            adsb.CurrentPosition = center;
            adsb.ApplicationVersion = "1.2.3";
            new adsb();
            var receivedTwice = completed.Wait(TimeSpan.FromSeconds(20));
            adsb.Stop();
            Check(receivedTwice, "live adsb.fi polling publishes two aircraft updates (received " + counts.Count + " aircraft)");
            Check(valid && counts.Count > 0, "live traffic has adsb.fi attribution and respects ADIZ / 100 nm limits");
            Console.WriteLine("LIVE: " + counts.Count + " aircraft; first update " + firstMilliseconds +
                " ms; next update after " + (secondMilliseconds - firstMilliseconds) + " ms");
        }
        finally
        {
            adsb.Stop();
            adsb.UpdatePlanePosition -= handler;
            completed.Dispose();
        }
    }

    private static string AircraftResponse(bool addOutsideTraffic)
    {
        var result = new StringBuilder("{\"ac\":[");
        if (addOutsideTraffic)
        {
            // All of these lie inside the rectangle but outside the southeast ADIZ edge.
            // Invalid squawk text also proves filtering happens before aircraft conversion.
            for (int i = 0; i < 2000; i++)
                result.Append("{\"hex\":\"outside\",\"lat\":21.2,\"lon\":122,\"squawk\":\"invalid\"},");
            result.Append("null,");
        }
        result.Append("{\"hex\":\"abc123\",\"flight\":\"TEST123 \",\"lat\":25.1,\"lon\":121.2," +
            "\"alt_baro\":3280,\"track\":90,\"gs\":100,\"baro_rate\":0,\"squawk\":\"1200\"}]}");
        return result.ToString();
    }

    private static string Reply(TcpListener listener, string status, string body)
    {
        var pending = listener.AcceptTcpClientAsync();
        if (!pending.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("No HTTP request received");
        using (var client = pending.Result)
        using (var stream = client.GetStream())
        {
            stream.ReadTimeout = 5000;
            var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            var request = new StringBuilder();
            string line;
            while (!string.IsNullOrEmpty(line = reader.ReadLine())) request.AppendLine(line);
            var content = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: application/json\r\nContent-Length: " +
                content.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length);
            stream.Write(content, 0, content.Length);
            return request.ToString();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}
