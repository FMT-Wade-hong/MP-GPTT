using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using log4net;

namespace MissionPlanner.Utilities
{
    public static class ParameterMetaDataRepositoryAPMpdef
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ParameterMetaDataRepositoryAPMpdef));
        private static readonly ConcurrentDictionary<string, Dictionary<string, Dictionary<string, string>>> Documents =
            new ConcurrentDictionary<string, Dictionary<string, Dictionary<string, string>>>();
        private static readonly ConcurrentDictionary<string, Lazy<bool>> CheckedFiles = new ConcurrentDictionary<string, Lazy<bool>>();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new ConcurrentDictionary<string, SemaphoreSlim>();
        private static readonly ConcurrentDictionary<string, DateTime> RetryAfter = new ConcurrentDictionary<string, DateTime>();
        private static readonly string[] vehicles = { "SITL", "AP_Periph", "ArduSub", "Rover", "ArduCopter", "ArduPlane", "AntennaTracker", "Blimp", "Heli" };
        private static readonly string[] vehicles_versioned = { "Copter", "Plane", "Rover", "Sub", "Tracker" };
        // Scope TLS to metadata downloads; legacy process defaults must not break HTTPS.
        private static HttpClient client = new HttpClient(new HttpClientHandler
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12
        });
        private static TimeSpan DownloadTimeout = TimeSpan.FromSeconds(12);
        private const int MaxDocumentBytes = 32 * 1024 * 1024;
        private static int revision;
        public static int Revision => Volatile.Read(ref revision);
        public static event Action<string> MetadataUpdated;
        internal static IEnumerable<string> AvailableVersions
        {
            get
            {
                var keys = new HashSet<string>(Documents.Keys.Where(IsVersionKey), StringComparer.Ordinal);
                foreach (var directory in new[] { CacheDirectory, Settings.GetDataDirectory() }.Distinct())
                {
                    try
                    {
                        if (!Directory.Exists(directory)) continue;
                        foreach (var file in Directory.GetFiles(directory, "*.apm.pdef.xml"))
                        {
                            var key = Path.GetFileName(file).Replace(".apm.pdef.xml", "");
                            if (IsVersionKey(key)) keys.Add(key);
                        }
                    }
                    catch (Exception ex) { log.Warn("Could not enumerate cached parameter versions", ex); }
                }
                return keys;
            }
        }

        // A user-writable cache avoids requiring elevation for ProgramData.
        private static string CacheDirectory => Path.Combine(Settings.GetUserDataDirectory(), "ParameterMetadata");
        private static string url = "https://autotest.ardupilot.org/Parameters/{0}/apm.pdef.xml.gz";
        private static string urlversioned = "https://autotest.ardupilot.org/Parameters/versioned/{0}/stable-{1}/apm.pdef.xml";

        internal static bool IsVersionKey(string key) => Regex.IsMatch(key ?? "", @"^(Copter|Heli|Plane|Rover|Sub|Tracker)\d+\.\d+\.\d+$");
        private static string PhysicalKey(string key) => key.StartsWith("Heli", StringComparison.Ordinal) && IsVersionKey(key)
            ? "Copter" + key.Substring(4) : key;
        private static string Normalize(string key)
        {
            switch (key)
            {
                case "ArduCopter2": return "ArduCopter";
                case "ArduRover": return "Rover";
                case "ArduTracker": return "AntennaTracker";
                default: return key;
            }
        }

        public static void CheckLoad(string vehicle = "")
        {
            vehicle = Normalize(vehicle);
            if (string.IsNullOrEmpty(vehicle)) return;
            var physical = PhysicalKey(vehicle);
            _ = CheckedFiles.GetOrAdd(physical, name => new Lazy<bool>(() => { Reload(name); return true; },
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        public static bool ContainsVersion(string key)
        {
            CheckLoad(key);
            return key != null && Documents.ContainsKey(Normalize(key));
        }

        // Existing callers can request all families; each succeeds/fails independently.
        public static Task GetMetaDataVersioned(Version version)
        {
            if (version == null || version.Build < 0) return Task.CompletedTask;
            return Task.WhenAll(vehicles_versioned.Select(family => GetMetaDataVersioned(family + version.ToString(3))));
        }

        public static Task<bool> GetMetaDataVersioned(string key, bool force = false)
        {
            if (!IsVersionKey(key)) return Task.FromResult(false);
            if (VersionedParameterMetadata.ContainsVersion(key)) return Task.FromResult(true);
            var physical = PhysicalKey(key);
            var match = Regex.Match(physical, @"^([A-Za-z]+)(\d+\.\d+\.\d+)$");
            return Update(physical, String.Format(urlversioned, match.Groups[1].Value, match.Groups[2].Value), force);
        }

        public static Task GetMetaData(bool force = false) => Task.WhenAll(vehicles.Select(
            vehicle => Update(vehicle, String.Format(url, vehicle), force)));

        private static async Task<bool> Update(string key, string address, bool force)
        {
            var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                CheckLoad(key);
                var file = Path.Combine(CacheDirectory, key + ".apm.pdef.xml");
                if (!force && RetryAfter.TryGetValue(key, out var retry) && retry > DateTime.UtcNow)
                    return Documents.ContainsKey(key);
                if (!force && Documents.ContainsKey(key) && File.Exists(file) &&
                    File.GetLastWriteTimeUtc(file).AddDays(7) > DateTime.UtcNow) return true;

                Directory.CreateDirectory(CacheDirectory);
                foreach (var candidate in new[] { address, address.EndsWith(".gz", StringComparison.Ordinal) ? address.Substring(0, address.Length - 3) : address + ".gz" })
                {
                    string temporary = null;
                    try
                    {
                        using (var timeout = new CancellationTokenSource(DownloadTimeout))
                        using (var response = await client.GetAsync(candidate, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            if (response.Content.Headers.ContentLength > MaxDocumentBytes) throw new InvalidDataException("Metadata response too large");
                            using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var bytes = new MemoryStream())
                            {
                                var buffer = new byte[16384];
                                int count;
                                while ((count = await source.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                                {
                                    if (bytes.Length + count > MaxDocumentBytes) throw new InvalidDataException("Metadata response too large");
                                    bytes.Write(buffer, 0, count);
                                }
                                bytes.Position = 0;
                                XDocument document;
                                if (candidate.EndsWith(".gz", StringComparison.Ordinal))
                                {
                                    using (var gzip = new GZipStream(bytes, CompressionMode.Decompress, true)) document = ReadDocument(gzip, key);
                                }
                                else document = ReadDocument(bytes, key);
                                // Failed downloads must retain the last good file and index.
                                temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                                document.Save(temporary);
                                if (File.Exists(file)) File.Replace(temporary, file, null);
                                else File.Move(temporary, file);
                                temporary = null;
                                Publish(key, document);
                                RetryAfter.TryRemove(key, out _);
                                return true;
                            }
                        }
                    }
                    catch (Exception ex) { log.Warn("Parameter metadata download failed: " + candidate, ex); }
                    finally { if (temporary != null) { try { File.Delete(temporary); } catch { } } }
                }
                RetryAfter[key] = DateTime.UtcNow.AddMinutes(2);
                return Documents.ContainsKey(key);
            }
            catch (Exception ex)
            {
                RetryAfter[key] = DateTime.UtcNow.AddMinutes(2);
                log.Warn("Parameter metadata cache update failed: " + key, ex);
                return Documents.ContainsKey(key);
            }
            finally { gate.Release(); }
        }

        private static XDocument ReadDocument(Stream stream, string key)
        {
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxDocumentBytes
            }))
            {
                var document = XDocument.Load(reader);
                if (document.Root?.Name.LocalName != "paramfile" || !document.Descendants("param").Any())
                    throw new InvalidDataException("Empty or invalid parameter metadata");
                var family = Regex.Match(PhysicalKey(key), @"^([A-Za-z]+)\d").Groups[1].Value;
                var expected = family == "Copter" ? "ArduCopter" : family == "Plane" ? "ArduPlane" :
                    family == "Sub" ? "ArduSub" : family == "Tracker" ? "AntennaTracker" : family;
                if (expected.Length > 0 && !document.Descendants("parameters").Any(node => (string)node.Attribute("name") == expected))
                    throw new InvalidDataException("Parameter metadata vehicle family mismatch");
                return document;
            }
        }

        private static void Publish(string key, XDocument document)
        {
            Documents[key] = VersionedParameterMetadata.BuildIndex(key, document);
            if (key.StartsWith("Copter", StringComparison.Ordinal) && IsVersionKey(key))
                Documents["Heli" + key.Substring(6)] = VersionedParameterMetadata.BuildIndex("Heli" + key.Substring(6), document);
            Interlocked.Increment(ref revision);
            var handlers = MetadataUpdated;
            if (handlers != null)
                foreach (Action<string> handler in handlers.GetInvocationList())
                    try { handler(key); } catch (Exception ex) { log.Warn("Metadata refresh subscriber failed", ex); }
        }

        public static void Reset()
        {
            Documents.Clear(); CheckedFiles.Clear(); RetryAfter.Clear();
            Interlocked.Increment(ref revision);
        }

        public static void Reload(string vehicle = "")
        {
            vehicle = Normalize(vehicle);
            if (String.IsNullOrEmpty(vehicle) || (!vehicles.Contains(vehicle) && !IsVersionKey(vehicle))) return;
            var key = PhysicalKey(vehicle);
            foreach (var directory in new[] { CacheDirectory, Settings.GetDataDirectory() }.Distinct())
            {
                var file = Path.Combine(directory, key + ".apm.pdef.xml");
                try
                {
                    if (!File.Exists(file)) continue;
                    using (var input = File.OpenRead(file)) Publish(key, ReadDocument(input, key));
                    return;
                }
                catch (Exception ex) { log.Warn("Could not load parameter metadata: " + file, ex); }
            }
        }

        public static string GetParameterMetaData(string nodeKey, string metaKey, string vechileType)
        {
            if (string.IsNullOrWhiteSpace(nodeKey) || string.IsNullOrWhiteSpace(metaKey) || string.IsNullOrEmpty(vechileType)) return string.Empty;
            vechileType = Normalize(vechileType);
            CheckLoad(vechileType);
            return Documents.TryGetValue(vechileType, out var index) && index.TryGetValue(nodeKey, out var parameter) &&
                parameter.TryGetValue(metaKey, out var value) ? value : string.Empty;
        }
    }
}
