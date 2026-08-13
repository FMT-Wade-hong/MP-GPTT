using GMap.NET;
using MissionPlanner.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MissionPlanner.FMT
{
    internal sealed class TaiwanCaaZone
    {
        internal string Id { get; set; }
        internal string Name { get; set; }
        internal string Category { get; set; }
        internal string Description { get; set; }
        internal Color Color { get; set; }
        internal List<List<PointLatLng>> Polygons { get; } = new List<List<PointLatLng>>();
    }

    internal static class TaiwanCaaAirspace
    {
        internal const string OfficialMapUrl =
            "https://dronegis.caa.gov.tw/portal/apps/webappviewer/index.html?id=807bd21438ba4208b4a7e28569fe41aa";

        private const string FeatureServer =
            "https://dronegis.caa.gov.tw/server/rest/services/Hosted/UAV_fs/FeatureServer";
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        private static readonly ConcurrentDictionary<string, Task<IReadOnlyList<TaiwanCaaZone>>> Requests =
            new ConcurrentDictionary<string, Task<IReadOnlyList<TaiwanCaaZone>>>();

        internal static async Task<IReadOnlyList<TaiwanCaaZone>> LoadNearbyAsync(PointLatLng center)
        {
            if (center.Lat < 20 || center.Lat > 27 || center.Lng < 117 || center.Lng > 123.5)
                return new List<TaiwanCaaZone>();

            var cellLat = Math.Floor(center.Lat * 2.0) / 2.0;
            var cellLng = Math.Floor(center.Lng * 2.0) / 2.0;
            var key = cellLat.ToString("0.0", CultureInfo.InvariantCulture) + "_" +
                      cellLng.ToString("0.0", CultureInfo.InvariantCulture);
            var request = Requests.GetOrAdd(key, ignored => LoadCellAsync(cellLat, cellLng, key));
            try
            {
                return await request;
            }
            catch
            {
                Task<IReadOnlyList<TaiwanCaaZone>> ignored;
                Requests.TryRemove(key, out ignored);
                throw;
            }
        }

        private static async Task<IReadOnlyList<TaiwanCaaZone>> LoadCellAsync(double cellLat, double cellLng,
            string cacheKey)
        {
            var zones = new List<TaiwanCaaZone>();
            zones.AddRange(await LoadLayerAsync(1, Color.Red, cellLat, cellLng, cacheKey));
            zones.AddRange(await LoadLayerAsync(2, Color.Gold, cellLat, cellLng, cacheKey));
            return zones;
        }

        private static async Task<IEnumerable<TaiwanCaaZone>> LoadLayerAsync(int layer, Color color,
            double cellLat, double cellLng, string cacheKey)
        {
            var cacheDirectory = Path.Combine(Settings.GetDataDirectory(), "TaiwanCAA");
            Directory.CreateDirectory(cacheDirectory);
            var cacheFile = Path.Combine(cacheDirectory, "layer" + layer + "_" + cacheKey + ".geojson");
            string json;

            if (File.Exists(cacheFile) && File.GetLastWriteTimeUtc(cacheFile).AddHours(12) > DateTime.UtcNow)
            {
                json = File.ReadAllText(cacheFile);
            }
            else
            {
                json = await DownloadLayerAsync(layer, cellLat, cellLng);
                File.WriteAllText(cacheFile, json);
            }

            return ParseZones(json, layer, color);
        }

        private static async Task<string> DownloadLayerAsync(int layer, double cellLat, double cellLng)
        {
            var allFeatures = new JArray();
            const int pageSize = 2000;
            for (var offset = 0;; offset += pageSize)
            {
                var envelope = string.Join(",", new[]
                {
                    (cellLng - 0.35).ToString(CultureInfo.InvariantCulture),
                    (cellLat - 0.35).ToString(CultureInfo.InvariantCulture),
                    (cellLng + 0.85).ToString(CultureInfo.InvariantCulture),
                    (cellLat + 0.85).ToString(CultureInfo.InvariantCulture)
                });
                var query = FeatureServer + "/" + layer + "/query?where=1%3D1" +
                            "&geometry=" + Uri.EscapeDataString(envelope) +
                            "&geometryType=esriGeometryEnvelope&inSR=4326&outSR=4326" +
                            "&spatialRel=esriSpatialRelIntersects&returnGeometry=true" +
                            "&outFields=*&orderByFields=objectid" +
                            "&resultOffset=" + offset + "&resultRecordCount=" + pageSize + "&f=geojson";
                var page = JObject.Parse(await Client.GetStringAsync(query));
                var features = page["features"] as JArray ?? new JArray();
                foreach (var feature in features)
                    allFeatures.Add(feature);
                if (features.Count < pageSize)
                    break;
            }

            return new JObject
            {
                ["type"] = "FeatureCollection",
                ["source"] = OfficialMapUrl,
                ["downloadedUtc"] = DateTime.UtcNow.ToString("O"),
                ["features"] = allFeatures
            }.ToString(Formatting.None);
        }

        private static IEnumerable<TaiwanCaaZone> ParseZones(string json, int layer, Color color)
        {
            var root = JObject.Parse(json);
            foreach (var feature in root["features"] as JArray ?? new JArray())
            {
                var properties = feature["properties"] as JObject ?? new JObject();
                var objectId = Property(properties, "objectid", "OBJECTID");
                var zone = new TaiwanCaaZone
                {
                    Id = "TWCAA-" + layer + "-" + objectId,
                    Name = Property(properties, "空域名稱", "name"),
                    Category = Property(properties, "空域類別名稱", "空域類型"),
                    Description = Property(properties, "空域說明", "條件"),
                    Color = color
                };

                var geometry = feature["geometry"] as JObject;
                var coordinates = geometry?["coordinates"] as JArray;
                var type = geometry?.Value<string>("type");
                if (coordinates == null)
                    continue;

                if (string.Equals(type, "Polygon", StringComparison.OrdinalIgnoreCase))
                    AddPolygon(zone, coordinates.First as JArray);
                else if (string.Equals(type, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
                    foreach (var polygon in coordinates.OfType<JArray>())
                        AddPolygon(zone, polygon.First as JArray);

                if (zone.Polygons.Count > 0)
                    yield return zone;
            }
        }

        private static void AddPolygon(TaiwanCaaZone zone, JArray ring)
        {
            if (ring == null)
                return;
            var points = ring.OfType<JArray>()
                .Where(position => position.Count >= 2)
                .Select(position => new PointLatLng(position[1].Value<double>(), position[0].Value<double>()))
                .ToList();
            if (points.Count >= 3)
                zone.Polygons.Add(points);
        }

        private static string Property(JObject properties, params string[] names)
        {
            foreach (var name in names)
            {
                var value = properties.GetValue(name, StringComparison.OrdinalIgnoreCase);
                if (value != null && value.Type != JTokenType.Null && !string.IsNullOrWhiteSpace(value.ToString()))
                    return value.ToString();
            }
            return string.Empty;
        }
    }
}
