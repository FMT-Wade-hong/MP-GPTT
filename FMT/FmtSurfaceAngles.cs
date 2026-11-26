using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MissionPlanner.Utilities;
using Newtonsoft.Json;

namespace MissionPlanner.FMT
{
    internal sealed class FmtSurfaceChannel
    {
        internal int Channel, Kind, Function, Min, Trim, Max, Reversed;
        internal string Signature => string.Join("_", Channel, Function, Min, Trim, Max, Reversed);
        internal bool Valid => Min > 0 && Min < Max && Trim >= Min && Trim <= Max;
        public override string ToString() => "CH" + Channel;
    }

    internal sealed class FmtSurfaceCalibration
    {
        public double AtMin { get; set; }
        public double AtMax { get; set; }
        internal bool ValidFor(FmtSurfaceChannel c) => c.Valid && Finite(AtMin) && Finite(AtMax) &&
            Math.Abs(AtMin) <= 180 && Math.Abs(AtMax) <= 180 && (AtMin != 0 || AtMax != 0) &&
            (c.Min != c.Trim || AtMin == 0) && (c.Max != c.Trim || AtMax == 0);
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    internal static class FmtSurfaceAngles
    {
        internal static readonly string[] Names = { "副翼", "FLAP", "升降舵", "方向舵" };

        internal static int Kind(int function)
        {
            switch (function)
            {
                case 4: return 0;
                case 2: case 3: return 1;
                case 19: return 2;
                case 21: return 3;
                default: return -1;
            }
        }

        internal static List<FmtSurfaceChannel> ReadChannels(MAVState target)
        {
            var result = new List<FmtSurfaceChannel>();
            if (target == null) return result;
            for (int ch = 1; ch <= 32; ch++)
            {
                string prefix = "SERVO" + ch + "_";
                int function = Parameter(target, prefix + "FUNCTION", -1);
                int kind = Kind(function);
                if (kind < 0) continue;
                result.Add(new FmtSurfaceChannel { Channel = ch, Function = function, Kind = kind,
                    Min = Parameter(target, prefix + "MIN", 0), Trim = Parameter(target, prefix + "TRIM", 0),
                    Max = Parameter(target, prefix + "MAX", 0), Reversed = Parameter(target, prefix + "REVERSED", 0) });
            }
            return result;
        }

        private static int Parameter(MAVState target, string name, int fallback)
        {
            var p = target.param[name];
            double value = p == null ? double.NaN : p.Value;
            return FmtSurfaceCalibration.Finite(value) && value == Math.Truncate(value) &&
                value >= int.MinValue && value <= int.MaxValue ? (int)value : fallback;
        }

        // PWM already includes SERVO_REVERSED. The user's signed physical endpoint
        // angles establish direction; applying REVERSED a second time would be wrong.
        internal static double? Fraction(FmtSurfaceChannel c, int pwm)
        {
            if (c == null || !c.Valid || pwm < c.Min || pwm > c.Max) return null;
            if (pwm == c.Trim) return 0;
            return pwm < c.Trim ? (pwm - c.Trim) / (double)(c.Trim - c.Min) :
                (pwm - c.Trim) / (double)(c.Max - c.Trim);
        }

        internal static double? Estimate(FmtSurfaceChannel c, int pwm, FmtSurfaceCalibration calibration)
        {
            var fraction = Fraction(c, pwm);
            if (!fraction.HasValue || calibration == null || !calibration.ValidFor(c)) return null;
            return fraction < 0 ? -fraction * calibration.AtMin : fraction * calibration.AtMax;
        }

        internal static string VehicleKey(MAVState target)
        {
            if (target == null) return null;
            string uid = target.cs.uid2;
            if (!string.IsNullOrEmpty(uid) && uid.Length >= 16 && uid.Any(c => c != '0') &&
                uid.All(Uri.IsHexDigit)) return "UID2_" + uid.ToUpperInvariant() + "_" + target.compid;
            return target.cs.uid == 0 ? null : "UID_" + target.cs.uid.ToString("X16", CultureInfo.InvariantCulture) + "_" + target.compid;
        }
    }

    internal sealed class FmtSurfaceAngleStore
    {
        internal const string SettingsKey = "FMT_SurfaceAngles_V1";
        private Dictionary<string, FmtSurfaceCalibration> saved;
        private readonly Dictionary<string, FmtSurfaceCalibration> session = new Dictionary<string, FmtSurfaceCalibration>();
        internal FmtSurfaceAngleStore()
        {
            try { saved = JsonConvert.DeserializeObject<Dictionary<string, FmtSurfaceCalibration>>(Settings.Instance[SettingsKey] ?? "{}"); }
            catch (JsonException) { }
            saved = saved ?? new Dictionary<string, FmtSurfaceCalibration>();
        }
        internal void ClearSession() => session.Clear();
        internal FmtSurfaceCalibration Get(string vehicle, FmtSurfaceChannel channel)
        {
            FmtSurfaceCalibration value;
            var values = vehicle == null ? session : saved;
            string key = vehicle == null ? channel.Signature : vehicle + "|" + channel.Signature;
            return values.TryGetValue(key, out value) && value != null && value.ValidFor(channel) ? value : null;
        }
        internal void Save(string vehicle, IList<FmtSurfaceChannel> channels, IList<FmtSurfaceCalibration> values)
        {
            if (channels.Count != values.Count) throw new ArgumentException("角度設定數量不符。");
            var next = new Dictionary<string, FmtSurfaceCalibration>(vehicle == null ? session : saved);
            for (int i = 0; i < channels.Count; i++)
            {
                if (values[i] != null && !values[i].ValidFor(channels[i])) throw new ArgumentException("角度設定無效。");
                string key = vehicle == null ? channels[i].Signature : vehicle + "|" + channels[i].Signature;
                if (values[i] == null) next.Remove(key); else next[key] = values[i];
            }
            if (vehicle == null)
            {
                session.Clear();
                foreach (var item in next) session.Add(item.Key, item.Value);
                return;
            }
            string previous = Settings.Instance[SettingsKey];
            try
            {
                Settings.Instance[SettingsKey] = JsonConvert.SerializeObject(next);
                Settings.Instance.Save();
                saved = next;
            }
            catch { Settings.Instance[SettingsKey] = previous; throw; }
        }
    }
}
