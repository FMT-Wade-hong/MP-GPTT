using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MissionPlanner.FMT
{
    // Translates visible labels only. CurrentState/MAVLink property keys remain unchanged.
    internal static class FmtTelemetryLocalization
    {
        private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Accel Strength", "加速度強度" }, { "accel_air", "空氣加速度" },
            { "accelx", "X 軸加速度" }, { "accely", "Y 軸加速度" }, { "accelz", "Z 軸加速度" },
            { "accelx2", "第二組 X 軸加速度" }, { "accely2", "第二組 Y 軸加速度" }, { "accelz2", "第二組 Z 軸加速度" },
            { "accelx3", "第三組 X 軸加速度" }, { "accely3", "第三組 Y 軸加速度" }, { "accelz3", "第三組 Z 軸加速度" },
            { "press_abs", "絕對氣壓" }, { "press_abs2", "第二組絕對氣壓" },
            { "Time over Home (sec)", "返航點上空時間（秒）" }, { "Wind Velocity (m/s)", "風速（公尺/秒）" },
            { "Yaw (deg)", "航向角（度）" }, { "vibez", "Z 軸震動" },
            { "Time in Air (min.sec)", "飛行時間（分:秒）" }, { "Time in Air (sec)", "飛行時間（秒）" },
            { "AirSpeed (m/s)", "空速（公尺/秒）" }, { "Mag Field", "磁場強度" },
            { "Sat Count", "衛星數" }, { "satcount", "衛星數" },
            { "Dist to Home (m)", "距返航點（公尺）" }, { "DistToHome", "距返航點" },
            { "flow_comp_m_x", "光流 X 軸補償" }, { "flow_comp_m_y", "光流 Y 軸補償" },
            { "armed", "解鎖狀態" }, { "connected", "連線狀態" }, { "failsafe", "故障保護狀態" },
            { "freemem", "可用記憶體" }, { "boardvoltage", "飛控板電壓" }, { "brklevel", "煞車強度" },
            { "climbrate", "爬升率" }, { "groundspeed", "地速" }, { "airspeed", "空速" },
            { "alt", "高度" }, { "altasl", "海拔高度" }, { "alt_error", "高度誤差" }, { "alt_target", "目標高度" },
            { "roll", "橫滾角" }, { "pitch", "俯仰角" }, { "yaw", "航向角" },
            { "roll2", "第二組橫滾角" }, { "pitch2", "第二組俯仰角" }, { "yaw2", "第二組航向角" },
            { "gpshdop", "GPS 水平精度因子" }, { "gpshdop2", "第二組 GPS 水平精度因子" },
            { "gpsvdop", "GPS 垂直精度因子" }, { "gpsvdop2", "第二組 GPS 垂直精度因子" },
            { "gpsstatus", "GPS 定位狀態" }, { "gpsstatus2", "第二組 GPS 定位狀態" },
            { "battery_remaining", "電池剩餘電量" }, { "battery_voltage", "電池電壓" },
            { "battery_current", "電池電流" }, { "battery_usedmah", "電池已用容量" },
            { "battery_kmleft", "預估剩餘航程" }, { "battery_wattperkm", "每公里耗電量" },
            { "AOA", "攻角" }, { "crit_AOA", "臨界攻角" }, { "asratio", "空速比" }, { "aspd_error", "空速誤差" },
            { "GeoFenceDist", "地理圍籬距離" }, { "fenceb_count", "圍籬 B 點數" },
            { "fenceb_status", "圍籬 B 狀態" }, { "fenceb_type", "圍籬 B 類型" },
            { "DistTraveled", "已飛行距離" }, { "DistFromMovingBase", "距移動基站" },
            { "DistRSSIRemain", "依訊號估算剩餘距離" }, { "HomeAlt", "返航點高度" },
            { "HomeLocation", "返航點位置" }, { "RangeFinder1 (cm)", "測距儀 1（公分）" }
        };

        private static readonly Dictionary<string, string> EnglishExact =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "press_abs", "Absolute Pressure" }, { "press_abs2", "Secondary Absolute Pressure" },
                { "DistTraveled", "Distance Traveled" }, { "DistToHome", "Distance to Home" },
                { "Dist to Home (m)", "Distance to Home (m)" },
                { "Time over Home (sec)", "Time over Home (sec)" },
                { "Wind Velocity (m/s)", "Wind Velocity (m/s)" },
                { "Yaw (deg)", "Heading (deg)" }, { "vibez", "Z-axis Vibration" },
                { "Time in Air (min.sec)", "Flight Time (min:sec)" },
                { "Time in Air (sec)", "Flight Time (sec)" },
                { "AirSpeed (m/s)", "Airspeed (m/s)" }, { "airspeed", "Airspeed" },
                { "groundspeed", "Ground Speed" }, { "Mag Field", "Magnetic Field" },
                { "Sat Count", "Satellite Count" }, { "satcount", "Satellite Count" },
                { "RangeFinder1 (cm)", "Rangefinder 1 (cm)" },
                { "Accel Strength", "Acceleration Strength" }, { "load", "Load" }
            };

        private static readonly Dictionary<string, string> Tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "battery", "電池" }, { "cell", "單體" }, { "voltage", "電壓" }, { "current", "電流" },
            { "remaining", "剩餘電量" }, { "usedmah", "已用容量" }, { "temp", "溫度" }, { "rpm", "轉速" },
            { "volt", "電壓" }, { "curr", "電流" }, { "accel", "加速度" }, { "gyro", "陀螺儀" },
            { "mag", "磁力計" }, { "press", "氣壓" }, { "abs", "絕對值" }, { "airspeed", "空速" },
            { "alt", "高度" }, { "lat", "緯度" }, { "lng", "經度" }, { "roll", "橫滾角" },
            { "pitch", "俯仰角" }, { "yaw", "航向角" }, { "target", "目標" }, { "error", "誤差" },
            { "status", "狀態" }, { "count", "數量" }, { "speed", "速度" }, { "heading", "航向" },
            { "home", "返航點" }, { "dist", "距離" }, { "rangefinder", "測距儀" }, { "flow", "光流" },
            { "comp", "補償" }, { "field", "磁場" }, { "time", "時間" }, { "air", "空中" },
            { "wind", "風" }, { "velocity", "速度" }, { "in", "輸入" }, { "out", "輸出" },
            { "percent", "百分比" }, { "rssi", "訊號強度" }, { "noise", "雜訊" }, { "remnoise", "遠端雜訊" },
            { "fence", "圍籬" }, { "fenceb", "圍籬 B" }, { "type", "類型" }, { "health", "健康狀態" },
            { "load", "負載" }, { "fuel", "燃油" }, { "pressure", "壓力" }, { "intake", "進氣" },
            { "head", "汽缸頭" }, { "runtime", "運轉時間" }, { "maint", "保養" }, { "gen", "發電機" },
            { "mode", "模式" }, { "armed", "解鎖" }, { "capabilities", "功能能力" }, { "errors", "錯誤" },
            { "fixedp", "定位類型" }, { "gimbal", "雲台" }, { "landing", "降落" }, { "landed", "已降落" },
            { "terrain", "地形" }, { "sonarrange", "聲納距離" }, { "wp", "航點" }
        };

        internal static string Field(string propertyName, string displayText = null)
        {
            if (!UseTraditionalChinese)
                return EnglishField(propertyName, displayText);

            string translated;
            if (!string.IsNullOrWhiteSpace(displayText) && Exact.TryGetValue(displayText.Trim(), out translated)) return translated;
            if (!string.IsNullOrWhiteSpace(propertyName) && Exact.TryGetValue(propertyName.Trim(), out translated)) return translated;
            var raw = string.IsNullOrWhiteSpace(propertyName) ? displayText : propertyName;
            if (string.IsNullOrWhiteSpace(raw)) return "未命名遙測資料";

            var range = Regex.Match(raw, @"^RangeFinder(\d+)$", RegexOptions.IgnoreCase);
            if (range.Success) return "測距儀 " + range.Groups[1].Value + "（公分）";
            var channel = Regex.Match(raw, @"^ch(\d+)(in|out|percent)$", RegexOptions.IgnoreCase);
            if (channel.Success) return "通道 " + channel.Groups[1].Value + " " + Tokens[channel.Groups[2].Value];
            var esc = Regex.Match(raw, @"^esc(\d+)_(curr|rpm|temp|volt)$", RegexOptions.IgnoreCase);
            if (esc.Success) return "ESC " + esc.Groups[1].Value + " " + Tokens[esc.Groups[2].Value];
            var battery = Regex.Match(raw, @"^battery_(cell|voltage|remaining|usedmah|temp)(\d+)$", RegexOptions.IgnoreCase);
            if (battery.Success) return "電池 " + battery.Groups[2].Value + " " + Tokens[battery.Groups[1].Value];
            var numbered = Regex.Match(raw, @"^(current|gpsstatus|gpshdop|gpsvdop)(\d+)$", RegexOptions.IgnoreCase);
            if (numbered.Success) return "第 " + numbered.Groups[2].Value + " 組 " + Field(numbered.Groups[1].Value);

            var words = Regex.Replace(raw, "([a-z])([A-Z])", "$1_$2").Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var output = new List<string>();
            foreach (var originalWord in words)
            {
                var match = Regex.Match(originalWord, @"^([A-Za-z]+)(\d*)$");
                var word = match.Success ? match.Groups[1].Value : originalWord;
                var number = match.Success ? match.Groups[2].Value : string.Empty;
                output.Add((Tokens.TryGetValue(word, out translated) ? translated : word.ToUpperInvariant()) +
                           (number.Length > 0 ? " " + number : string.Empty));
            }
            return LocalizeUnits(string.Join(" ", output));
        }

        internal static string Display(string propertyName, string displayText) { return Field(propertyName, displayText); }

        private static bool UseTraditionalChinese
        {
            get
            {
                return CultureInfo.CurrentUICulture.Name.StartsWith(
                    "zh", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string EnglishField(string propertyName, string displayText)
        {
            string english;
            if (!string.IsNullOrWhiteSpace(propertyName) &&
                EnglishExact.TryGetValue(propertyName.Trim(), out english))
                return english;
            if (!string.IsNullOrWhiteSpace(displayText) &&
                EnglishExact.TryGetValue(displayText.Trim(), out english))
                return english;

            // Older FMT builds may have persisted a Chinese display label. Map it
            // back to its stable telemetry key before producing the English text.
            var raw = string.IsNullOrWhiteSpace(propertyName) ? displayText : propertyName;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var pair in Exact)
                {
                    if (string.Equals(raw.Trim(), pair.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        raw = pair.Key;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(raw))
                return "Unnamed Telemetry Field";
            if (EnglishExact.TryGetValue(raw.Trim(), out english))
                return english;

            // CurrentState descriptions that are already readable English should
            // be preserved, including their units.
            if (!string.IsNullOrWhiteSpace(displayText) &&
                Regex.IsMatch(displayText, @"^[\x00-\x7F]+$") &&
                (displayText.IndexOf(' ') >= 0 || displayText.IndexOf('(') >= 0))
                return displayText.Trim();

            var range = Regex.Match(raw, @"^RangeFinder(\d+)$", RegexOptions.IgnoreCase);
            if (range.Success)
                return "Rangefinder " + range.Groups[1].Value + " (cm)";
            var channel = Regex.Match(raw, @"^ch(\d+)(in|out|percent)$", RegexOptions.IgnoreCase);
            if (channel.Success)
                return "Channel " + channel.Groups[1].Value + " " +
                       CultureInfo.InvariantCulture.TextInfo.ToTitleCase(channel.Groups[2].Value.ToLowerInvariant());
            var esc = Regex.Match(raw, @"^esc(\d+)_(curr|rpm|temp|volt)$", RegexOptions.IgnoreCase);
            if (esc.Success)
                return "ESC " + esc.Groups[1].Value + " " + esc.Groups[2].Value.ToUpperInvariant();

            raw = Regex.Replace(raw, "([a-z0-9])([A-Z])", "$1 $2").Replace('_', ' ');
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant());
        }

        private static string LocalizeUnits(string value)
        {
            return value.Replace("(m/s)", "（公尺/秒）").Replace("(cm)", "（公分）")
                .Replace("(m)", "（公尺）").Replace("(deg)", "（度）")
                .Replace("(sec)", "（秒）").Replace("(min.sec)", "（分:秒）");
        }
    }
}
