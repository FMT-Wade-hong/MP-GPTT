using System;
using System.Globalization;

namespace MissionPlanner.Utilities
{
    public static class AdsbApi
    {
        public const string DefaultServer = "https://opendata.adsb.fi/api";
        public const string AdsbFiAttribution = "adsb.fi (https://adsb.fi)";
        private const string MigrationKey = "adsbFiDefaultMigrated";

        public static string GetConfiguredServer(Settings settings)
        {
            var configured = settings["adsbserver"];
            if (string.IsNullOrWhiteSpace(configured) ||
                (!settings.GetBoolean(MigrationKey) && IsOldDefault(configured)))
                configured = DefaultServer;

            configured = NormalizeServer(configured);
            settings["adsbserver"] = configured;
            // Migrate once so a later explicit choice of a different server is respected.
            settings[MigrationKey] = bool.TrueString;
            return configured;
        }

        private static bool IsOldDefault(string server)
        {
            return Uri.TryCreate(server.Trim(), UriKind.Absolute, out var uri) &&
                   uri.Host.Equals("api.adsb.lol", StringComparison.OrdinalIgnoreCase) &&
                   uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
        }

        public static bool IsAdsbFi(string server)
        {
            return Uri.TryCreate(server?.Trim(), UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
                   (uri.Host.Equals("opendata.adsb.fi", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("api.adsb.fi", StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeServer(string server)
        {
            // Accept the service host as well as the documented /api base URL.
            return IsAdsbFi(server) ? DefaultServer : server?.Trim().TrimEnd('/');
        }

        public static string CreatePointQuery(string server, double latitude, double longitude, int radiusNm)
        {
            // https://github.com/adsbfi/opendata: use v3 for radius queries.
            var path = IsAdsbFi(server)
                ? "{0}/v3/lat/{1}/lon/{2}/dist/{3}"
                : "{0}/v2/point/{1}/{2}/{3}";
            return string.Format(CultureInfo.InvariantCulture, path,
                NormalizeServer(server), latitude, longitude, radiusNm);
        }
    }
}
