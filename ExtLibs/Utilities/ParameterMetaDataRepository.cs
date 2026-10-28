using System;
using System.Configuration;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;

namespace MissionPlanner.Utilities
{
    public static class ParameterMetaDataRepository
    {
        // Installed by the host; utilities remain independent of the UI assembly.
        public static Func<string, string> VehicleTypeResolver { get; set; }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> FirmwareKeys =
            new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        public static string GetFirmwareKey(string firmware, string versionText)
        {
            if (string.IsNullOrEmpty(firmware) || string.IsNullOrWhiteSpace(versionText)) return firmware;
            return FirmwareKeys.GetOrAdd(firmware + "|" + versionText, _ => ResolveFirmwareKey(firmware, versionText));
        }

        private static string ResolveFirmwareKey(string firmware, string versionText)
        {
            string family;
            switch (firmware)
            {
                case "ArduCopter2": case "ArduCopter": case "Heli": family = "Copter"; break;
                case "ArduPlane": family = "Plane"; break;
                case "ArduRover": case "Rover": family = "Rover"; break;
                case "ArduSub": family = "Sub"; break;
                case "ArduTracker": case "AntennaTracker": family = "Tracker"; break;
                default: return firmware;
            }
            if (VersionDetection.TryGetVersion(versionText, out var version) &&
                version.Major == 4 && version.Minor >= 5 && version.Minor <= 7 && version.Build >= 0)
            {
                var key = family + version.ToString(3);
                return firmware == "Heli" && VersionedParameterMetadata.ContainsVersion(key)
                    ? "Heli" + version.ToString(3) : key;
            }
            return firmware;
        }

        private static readonly Lazy<MemoryCache> _cache = new Lazy<MemoryCache>(() =>
            new MemoryCache(new MemoryCacheOptions()
            {
                /*SizeLimit = 1024 * 1024 * 500*/
            }));

        /// <summary>
        /// Gets the parameter meta data.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <param name="metaKey">The meta key.</param>
        /// <returns></returns>
        public static string GetParameterMetaData(string nodeKey, string metaKey, string vechileType)
        {
            vechileType = VehicleTypeResolver?.Invoke(vechileType) ?? vechileType;
            // An explicit firmware snapshot must never fall back to another release's units/ranges.
            if (System.Text.RegularExpressions.Regex.IsMatch(vechileType ?? "", @"^(Copter|Heli|Plane|Rover|Sub|Tracker)\d+\.\d+\.\d+$"))
            {
                if (VersionedParameterMetadata.TryGet(vechileType, nodeKey, metaKey, out var snapshotValue))
                    return snapshotValue;
                return ParameterMetaDataRepositoryAPMpdef.GetParameterMetaData(nodeKey, metaKey, vechileType);
            }
            lock (_cache)
            {
                var ans = _cache.Value.Get(nodeKey + metaKey + vechileType) as string;
                if (ans != null)
                    return ans;
            }

            if (vechileType == "PX4")
            {
                return ParameterMetaDataRepositoryPX4.GetParameterMetaData(nodeKey, metaKey, vechileType);
            }
            else
            {
                var answer = ParameterMetaDataRepositoryAPMpdef.GetParameterMetaData(nodeKey, metaKey, vechileType);
                if (answer == string.Empty)
                    answer = ParameterMetaDataRepositoryAPMpdef.GetParameterMetaData(nodeKey, metaKey, "SITL");
                if (answer == string.Empty)
                    answer = ParameterMetaDataRepositoryAPMpdef.GetParameterMetaData(nodeKey, metaKey, "AP_Periph");
                // add fallback
                if (answer == string.Empty)
                    answer = ParameterMetaDataRepositoryAPM.GetParameterMetaData(nodeKey, metaKey, vechileType);

                if (answer == string.Empty)
                    return String.Empty;

                lock (_cache)
                {
                    try
                    {
                        var ci = _cache.Value.CreateEntry(nodeKey + metaKey + vechileType);
                        ci.Value = answer;
                        ci.Size = ((string)ci.Value).Length;
                        // evict after no access
                        ci.SlidingExpiration = TimeSpan.FromMinutes(5);
                        ci.Dispose();
                    } catch { }
                }

                return answer;
            }
        }

        /// <summary>
        /// Return a key, value list off all options selectable
        /// </summary>
        /// <param name="nodeKey"></param>
        /// <returns></returns>
        public static List<KeyValuePair<int, string>> GetParameterOptionsInt(string nodeKey, string vechileType)
        {
            string availableValuesRaw = GetParameterMetaData(nodeKey, ParameterMetaDataConstants.Values, vechileType);
            string[] availableValues = availableValuesRaw.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            if (availableValues.Any())
            {
                var splitValues = new List<KeyValuePair<int, string>>();
                // Add the values to the ddl
                foreach (string val in availableValues)
                {
                    try
                    {
                        string[] valParts = val.Split(new[] {':'});
                        splitValues.Add(new KeyValuePair<int, string>(int.Parse(valParts[0].Trim()),
                            (valParts.Length > 1) ? valParts[1].Trim() : valParts[0].Trim()));
                    }
                    catch
                    {
                        Console.WriteLine("Bad entry in param meta data: " + nodeKey);
                    }
                }
                ;

                return splitValues;
            }

            return new List<KeyValuePair<int, string>>();
        }

        public static List<KeyValuePair<int, string>> GetParameterBitMaskInt(string nodeKey, string vechileType)
        {
            string availableValuesRaw;

            availableValuesRaw = GetParameterMetaData(nodeKey, ParameterMetaDataConstants.Bitmask, vechileType);

            string[] availableValues = availableValuesRaw.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            if (availableValues.Any())
            {
                var splitValues = new List<KeyValuePair<int, string>>();
                // Add the values to the ddl
                foreach (string val in availableValues)
                {
                    try
                    {
                        string[] valParts = val.Split(new[] {':'});
                        splitValues.Add(new KeyValuePair<int, string>(int.Parse(valParts[0].Trim()),
                            (valParts.Length > 1) ? valParts[1].Trim() : valParts[0].Trim()));
                    }
                    catch
                    {
                        Console.WriteLine("Bad entry in param meta data: " + nodeKey);
                    }
                }
                ;

                return splitValues;
            }

            return new List<KeyValuePair<int, string>>();
        }

        public static bool GetParameterRange(string nodeKey, ref double min, ref double max, string vechileType)
        {
            string rangeRaw = ParameterMetaDataRepository.GetParameterMetaData(nodeKey, ParameterMetaDataConstants.Range,
                vechileType);

            string[] rangeParts = rangeRaw.Split(new[] {' '});
            if (rangeParts.Count() == 2)
            {
                double lowerRange;
                if (double.TryParse(rangeParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lowerRange))
                {
                    double upperRange;
                    if (double.TryParse(rangeParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out upperRange))
                    {
                        min = lowerRange;
                        max = upperRange;

                        return true;
                    }
                }
            }

            return false;
        }

        public static bool GetParameterRebootRequired(string nodeKey, string vechileType)
        {
            // set the default answer
            bool answer = false;

            string rebootrequired = ParameterMetaDataRepository.GetParameterMetaData(nodeKey,
                ParameterMetaDataConstants.RebootRequired, vechileType);

            if (!string.IsNullOrEmpty(rebootrequired))
            {
                bool.TryParse(rebootrequired, out answer);
            }

            return answer;
        }

        public static bool GetParameterIncrement(string nodeKey, ref double inc, string vechileType)
        {
            string incrementAmt = ParameterMetaDataRepository.GetParameterMetaData(nodeKey,
                ParameterMetaDataConstants.Increment, vechileType);
            if (incrementAmt.Length == 0) return false;
            float Amt = 0;
            float.TryParse(incrementAmt, NumberStyles.Float, CultureInfo.InvariantCulture, out Amt);
            inc = Amt;
            return true;
        }
    }
}
