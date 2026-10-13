using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using MissionPlanner.Utilities;
using Newtonsoft.Json.Linq;

namespace MissionPlanner.FMT
{
    internal static partial class TaiwanCaaAirspace
    {
        private sealed class Snapshot
        {
            internal DateTime Date;
            internal List<TaiwanCaaZone> Zones;
            internal bool Bundled;
            internal string Warning;
            internal Dictionary<TaiwanCaaZone, double[]> Bounds;
        }
        private static readonly SemaphoreSlim SnapshotGate = new SemaphoreSlim(1, 1);
        private static Snapshot currentSnapshot;
        private static int updating;
        private static string SnapshotPath => Path.Combine(Settings.GetUserDataDirectory(), "TaiwanCAA", "taiwan-full-v1.json.gz");

        private static Snapshot ParseSnapshot(Stream input, bool bundled)
        {
            JObject root;
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip))
                root = JObject.Parse(reader.ReadToEnd());
            if ((int?)root["schema"] != 1) throw new InvalidDataException("不支援的空域資料格式");
            var result = new Snapshot { Date = root.Value<DateTime>("downloadedUtc").ToUniversalTime(),
                Zones = new List<TaiwanCaaZone>(), Bundled = bundled };
            foreach (int layer in new[] { 1, 2 })
            {
                var data = root["layers"]?[layer.ToString()] as JObject;
                var features = data?["features"] as JArray;
                if (features == null || features.Count == 0 || features.Count != (int?)data["expectedCount"])
                    throw new InvalidDataException("空域資料不完整");
                var zones = ParseZones(data.ToString(), layer, layer == 1 ? Color.Red : Color.Gold).ToList();
                if (zones.Count != features.Count || zones.Select(z => z.Id).Distinct().Count() != zones.Count)
                    throw new InvalidDataException("空域邊界或識別碼無效");
                result.Zones.AddRange(zones);
            }
            result.Bounds = result.Zones.ToDictionary(z => z, z => new[] {
                z.Polygons.Min(p => p.Min(v => v.Lat)), z.Polygons.Max(p => p.Max(v => v.Lat)),
                z.Polygons.Min(p => p.Min(v => v.Lng)), z.Polygons.Max(p => p.Max(v => v.Lng)) });
            return result;
        }

        private static async Task<Snapshot> GetSnapshotAsync()
        {
            await SnapshotGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (currentSnapshot != null) return currentSnapshot;
                currentSnapshot = await Task.Run(() =>
                {
                    string warning = null;
                    if (File.Exists(SnapshotPath))
                    {
                        try { using (var stream = File.OpenRead(SnapshotPath)) return ParseSnapshot(stream, false); }
                        catch (Exception) { warning = "本機更新資料無法讀取，已改用隨附底稿。"; }
                    }
                    using (var stream = typeof(TaiwanCaaAirspace).Assembly.GetManifestResourceStream("MissionPlanner.FMT.TaiwanCAA.gz"))
                    {
                        if (stream == null) throw new FileNotFoundException("缺少隨附全台空域資料");
                        var bundled = ParseSnapshot(stream, true);
                        bundled.Warning = warning;
                        return bundled;
                    }
                }).ConfigureAwait(false);
                return currentSnapshot;
            }
            finally { SnapshotGate.Release(); }
        }

        private static async Task<IReadOnlyList<TaiwanCaaZone>> LoadOfflineNearbyAsync(PointLatLng center)
        {
            if (center.Lat < 20 || center.Lat > 27 || center.Lng < 117 || center.Lng > 123.5)
                return new List<TaiwanCaaZone>();
            var snapshot = await GetSnapshotAsync().ConfigureAwait(false);
            double lat = Math.Floor(center.Lat * 2) / 2, lng = Math.Floor(center.Lng * 2) / 2;
            return snapshot.Zones.Where(zone => {
                var bounds = snapshot.Bounds[zone];
                return bounds[0] <= lat + .85 && bounds[1] >= lat - .35 &&
                    bounds[2] <= lng + .85 && bounds[3] >= lng - .35;
            }).ToList();
        }

        internal static async Task<IReadOnlyList<TaiwanCaaZone>> LoadDisplayRadiusAsync(PointLatLng home, double radius)
        {
            var snapshot = await GetSnapshotAsync().ConfigureAwait(false);
            return await Task.Run<IReadOnlyList<TaiwanCaaZone>>(() =>
            {
                var result = new List<TaiwanCaaZone>();
                foreach (var zone in snapshot.Zones)
                {
                    var selected = new TaiwanCaaZone { Id = zone.Id, Name = zone.Name,
                        Category = zone.Category, Description = zone.Description, Color = zone.Color };
                    selected.Polygons.AddRange(zone.Polygons.Where(p => FmtAirspaceRadius.Intersects(p, home, radius)));
                    if (selected.Polygons.Count > 0) result.Add(selected);
                }
                return result;
            }).ConfigureAwait(false);
        }

        private static async Task<JObject> QueryAsync(string url, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 FMTPlanner/1.2.3");
                using (var response = await Client.SendAsync(request, token).ConfigureAwait(false))
                {
                response.EnsureSuccessStatusCode();
                var json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                token.ThrowIfCancellationRequested();
                if (json["error"] != null) throw new InvalidDataException("CAA 服務回報錯誤：" + json["error"]);
                return json;
                }
            }
        }

        internal static async Task UpdateOfflineAsync(IProgress<string> progress, CancellationToken token)
        {
            if (Interlocked.Exchange(ref updating, 1) != 0) throw new InvalidOperationException("已有空域更新正在執行");
            string staging = null;
            try
            {
                var layers = new JObject();
                foreach (int layer in new[] { 1, 2 })
                {
                    string url = FeatureServer + "/" + layer + "/query?where=1%3D1";
                    int expected = (await QueryAsync(url + "&returnCountOnly=true&f=json", token).ConfigureAwait(false)).Value<int>("count");
                    if (expected <= 0) throw new InvalidDataException("CAA 回傳空圖層，保留原資料");
                    var features = new JArray();
                    for (int offset = 0; offset < expected;)
                    {
                        progress?.Report("下載圖層 " + layer + "：" + offset + " / " + expected);
                        var page = await QueryAsync(url + "&outSR=4326&returnGeometry=true&outFields=*&orderByFields=objectid&resultRecordCount=2000&resultOffset=" + offset + "&f=geojson", token).ConfigureAwait(false);
                        var items = page["features"] as JArray;
                        if (items == null || items.Count == 0) throw new InvalidDataException("CAA 分頁不完整");
                        foreach (var item in items) features.Add(item);
                        offset += items.Count;
                    }
                    int after = (await QueryAsync(url + "&returnCountOnly=true&f=json", token).ConfigureAwait(false)).Value<int>("count");
                    if (features.Count != expected || after != expected) throw new InvalidDataException("下載期間筆數變動，請重新更新");
                    layers[layer.ToString()] = new JObject { ["expectedCount"] = expected, ["features"] = features };
                }
                var root = new JObject { ["schema"] = 1, ["downloadedUtc"] = DateTime.UtcNow,
                    ["source"] = OfficialMapUrl, ["layers"] = layers };
                progress?.Report("驗證並儲存全台資料…");
                Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath));
                staging = SnapshotPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var file = File.Create(staging))
                using (var zip = new GZipStream(file, CompressionMode.Compress))
                using (var writer = new StreamWriter(zip))
                    await writer.WriteAsync(root.ToString(Newtonsoft.Json.Formatting.None)).ConfigureAwait(false);
                Snapshot validated;
                using (var file = File.OpenRead(staging)) validated = ParseSnapshot(file, false);
                token.ThrowIfCancellationRequested();
                await SnapshotGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (File.Exists(SnapshotPath)) File.Replace(staging, SnapshotPath, null);
                    else File.Move(staging, SnapshotPath);
                    staging = null;
                    currentSnapshot = validated;
                }
                finally { SnapshotGate.Release(); }
                progress?.Report("更新完成，共 " + validated.Zones.Count + " 筆");
            }
            finally
            {
                if (staging != null && File.Exists(staging)) { try { File.Delete(staging); } catch { } }
                Interlocked.Exchange(ref updating, 0);
            }
        }

        internal static void ShowOfflineUpdate(IWin32Window owner, Action refreshed)
        {
            var form = new Form { Text = "台灣 CAA 離線空域資料", Width = 560, Height = 240,
                StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
            var label = new Label { Dock = DockStyle.Fill, Padding = new Padding(12), Text = "讀取資料日期…" };
            var update = new Button { Text = "更新全台 CAA 資料", Dock = DockStyle.Bottom, Height = 36, Enabled = false };
            var cancel = new Button { Text = "關閉", Dock = DockStyle.Bottom, Height = 32 };
            form.Controls.Add(label); form.Controls.Add(update); form.Controls.Add(cancel);
            CancellationTokenSource cancellation = null;
            Func<Task> showStatus = async () =>
            {
                var snapshot = await GetSnapshotAsync();
                if (form.IsDisposed) return;
                label.Text = (snapshot.Bundled ? "隨附離線底稿" : "本機更新資料") + "：" + snapshot.Zones.Count + " 筆\r\n下載日期（UTC）：" +
                    snapshot.Date.ToString("yyyy-MM-dd HH:mm") + "\r\n" + snapshot.Warning +
                    "\r\n可離線顯示；此日期不是公告生效日，飛行前仍須核對官方公告。\r\n衛星底圖需另有地圖快取。";
            };
            form.Shown += async (sender, args) => { try { await showStatus(); } catch (Exception ex) { label.Text = ex.Message; } update.Enabled = true; };
            update.Click += async (sender, args) =>
            {
                update.Enabled = false; cancel.Text = "取消更新";
                cancellation = new CancellationTokenSource();
                try
                {
                    await UpdateOfflineAsync(new Progress<string>(text => { if (!form.IsDisposed) label.Text = text; }), cancellation.Token);
                    await showStatus();
                    refreshed?.Invoke();
                }
                catch (OperationCanceledException) { label.Text = cancellation.IsCancellationRequested
                    ? "已取消更新，原資料保留。" : "連線逾時，原資料保留。"; }
                catch (Exception ex) { label.Text = "更新失敗，原資料保留。\r\n" + ex.Message; }
                finally { cancellation.Dispose(); cancellation = null; update.Enabled = true; cancel.Text = "關閉"; }
            };
            cancel.Click += (sender, args) => { if (cancellation != null) cancellation.Cancel(); else form.Close(); };
            form.FormClosing += (sender, args) => { if (cancellation != null) { args.Cancel = true; cancellation.Cancel(); } };
            using (form) form.ShowDialog(owner);
        }
    }
}
