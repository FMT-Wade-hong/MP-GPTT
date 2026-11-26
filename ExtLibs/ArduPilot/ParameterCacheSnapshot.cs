using System;
using System.Linq;
using Newtonsoft.Json;

namespace MissionPlanner.ArduPilot
{
    public sealed class ParameterCacheSnapshot
    {
        public int FormatVersion { get; set; }
        public int TotalReported { get; set; }
        public MAVLink.MAVLinkParam[] Parameters { get; set; }

        public static MAVLink.MAVLinkParamList Read(string json)
        {
            try
            {
                var snapshot = JsonConvert.DeserializeObject<ParameterCacheSnapshot>(json);
                if (snapshot == null || snapshot.FormatVersion != 1 || snapshot.TotalReported <= 0 ||
                    snapshot.Parameters == null || snapshot.Parameters.Length != snapshot.TotalReported ||
                    snapshot.Parameters.Any(p => p == null || string.IsNullOrWhiteSpace(p.Name)) ||
                    snapshot.Parameters.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != snapshot.TotalReported)
                    return null;
                var result = new MAVLink.MAVLinkParamList { TotalReported = snapshot.TotalReported };
                result.AddRange(snapshot.Parameters);
                return result;
            }
            catch (JsonException) { return null; } // Legacy arrays cannot prove completeness; download again once.
        }

        public static string Write(MAVLink.MAVLinkParamList parameters)
        {
            var snapshot = new ParameterCacheSnapshot
            { FormatVersion = 1, TotalReported = parameters.TotalReported, Parameters = parameters.ToArray() };
            var json = JsonConvert.SerializeObject(snapshot);
            if (Read(json) == null) throw new System.IO.InvalidDataException("Cannot cache an incomplete parameter table.");
            return json;
        }
    }
}
