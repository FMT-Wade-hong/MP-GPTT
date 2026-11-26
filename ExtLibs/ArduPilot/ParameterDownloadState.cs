using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MissionPlanner.ArduPilot
{
    // A download is complete only when one stable table has every unique index and name.
    internal sealed class ParameterDownloadState
    {
        private readonly object sync = new object();
        private readonly Dictionary<int, MAVLink.MAVLinkParam> values = new Dictionary<int, MAVLink.MAVLinkParam>();
        private readonly Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.Ordinal);
        private int total;
        private bool changed;
        private bool sawLast;

        internal void Accept(MAVLink.mavlink_param_value_t packet, bool floatEncoding)
        {
            if (packet.param_count == 0 || packet.param_count == ushort.MaxValue ||
                packet.param_index >= packet.param_count || packet.param_id == null) return;
            var name = Encoding.ASCII.GetString(packet.param_id).TrimEnd('\0');
            if (string.IsNullOrWhiteSpace(name) || name.Length > 16 || name.Any(c => c < 33 || c > 126)) return;
            lock (sync)
            {
                if (changed) return;
                if (total != 0 && total != packet.param_count) { changed = true; return; }
                total = packet.param_count;
                if (values.TryGetValue(packet.param_index, out var previous))
                {
                    if (previous.Name != name) changed = true;
                    return;
                }
                if (names.TryGetValue(name, out var index) && index != packet.param_index) { changed = true; return; }
                var type = (MAVLink.MAV_PARAM_TYPE)packet.param_type;
                values[packet.param_index] = new MAVLink.MAVLinkParam(name, BitConverter.GetBytes(packet.param_value),
                    floatEncoding ? MAVLink.MAV_PARAM_TYPE.REAL32 : type, type);
                names[name] = packet.param_index;
                sawLast |= packet.param_index == total - 1;
            }
        }

        internal (int Total, int Received, bool Changed, bool SawLast) Status
        {
            get { lock (sync) return (total, values.Count, changed, sawLast); }
        }

        internal int[] Missing(int start, int maximum)
        {
            lock (sync)
            {
                var result = new List<int>();
                for (int offset = 0; offset < total && result.Count < maximum; offset++)
                {
                    var index = (start + offset) % total;
                    if (!values.ContainsKey(index)) result.Add(index);
                }
                return result.ToArray();
            }
        }

        internal MAVLink.MAVLinkParamList CompleteSnapshot()
        {
            lock (sync)
            {
                if (changed || total == 0 || values.Count != total || names.Count != total)
                    throw new System.IO.InvalidDataException("參數清單尚未完整或下載期間已變更。");
                var result = new MAVLink.MAVLinkParamList { TotalReported = total };
                result.AddRange(values.OrderBy(pair => pair.Key).Select(pair => pair.Value));
                return result;
            }
        }
    }
}
