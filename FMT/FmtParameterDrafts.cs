using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MissionPlanner.FMT
{
    /// <summary>Source-matched reviewed text with original-English fallback; never changes parameter IDs or values.</summary>
    internal static class FmtParameterDrafts
    {
        private static readonly Lazy<Dictionary<string, string>> Catalog =
            new Lazy<Dictionary<string, string>>(() =>
            {
                using (var stream = typeof(FmtParameterDrafts).Assembly.GetManifestResourceStream("FMT.Parameters.zh-TW.reviewed.json"))
                {
                    if (stream == null) return new Dictionary<string, string>();
                    using (var reader = new StreamReader(stream))
                        return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
                }
            });
        // Source-matched review: ArduPilot AC_AutoTune_Heli/Multi var_info,
        // 2026-09-30. Do not load the unreviewed machine-translation catalog.
        private static readonly Dictionary<string, string> Reviewed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Autotune axis bitmask", "AutoTune 軸向 bitmask" },
            { "RC options", "RC 選項" },
            { "1-byte bitmap of axes to autotune", "以 1 byte bitmask 選擇要執行 AutoTune 的軸向；可選取多個軸。軸向與 bit 定義依目前韌體提供的選項。" },
            { "AutoTune Sequence Bitmask", "AutoTune 調校程序 bitmask" },
            { "AutoTune minimum sweep frequency", "AutoTune 最低 sweep 頻率" },
            { "AutoTune maximum sweep frequency", "AutoTune 最高 sweep 頻率" },
            { "Defines the start frequency for sweeps and dwells", "設定 sweep（掃頻）及 dwell（定頻測試）的起始頻率。" },
            { "Defines the end frequency for sweeps and dwells", "設定 sweep（掃頻）及 dwell（定頻測試）的結束頻率。" },
            { "AutoTune maximum response gain", "AutoTune 最大 response gain" },
            { "Defines the response gain (output/input) to tune", "設定調校時的 response gain 目標（output/input，即輸出響應與輸入激勵的比值）；不是直接設定 Rate P 或 Rate D 的係數。" },
            { "AutoTune velocity xy P gain", "AutoTune 水平速度 P gain" },
            { "Velocity xy P gain used to hold position during Max Gain, Rate P, and Rate D frequency sweeps", "在 Max Gain、Rate P 與 Rate D 的 sweep 測試期間，用來維持位置的 XY 水平速度 P gain。" },
            { "AutoTune maximum allowable angular acceleration", "AutoTune 允許的最大角加速度" },
            { "maximum angular acceleration in deg/s/s allowed during autotune maneuvers", "AutoTune 測試機動期間允許的最大角加速度，單位 deg/s/s。" },
            { "Autotune maximum allowable angular rate", "AutoTune 允許的最大角速度" },
            { "maximum angular rate in deg/s allowed during autotune maneuvers", "AutoTune 測試機動期間允許的最大角速度，單位 deg/s。" },
            { "2-byte bitmask to select what tuning should be performed. Max gain automatically performed if Rate D is selected. Values: 7:All,1:VFF Only,2:Rate D/Rate P Only(incl max gain),4:Angle P Only,8:Max Gain Only,16:Tune Check,3:VFF and Rate D/Rate P(incl max gain),5:VFF and Angle P,6:Rate D/Rate P(incl max gain) and angle P", "以 2 byte bitmask 選擇 AutoTune 調校程序。選取 Rate D 時會先執行 Max Gain 測試。值 1：僅 VFF；2：Rate D／Rate P（含 Max Gain）；4：僅 Angle P；8：僅 Max Gain；16：Tune Check；3：VFF 加 Rate D／Rate P（含 Max Gain）；5：VFF 加 Angle P；6：Rate D／Rate P（含 Max Gain）加 Angle P；7：VFF、Rate D／Rate P 及 Angle P。組合值由 bit 值相加，不是調校次數。" }
        };

        internal static string Translate(string source)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                !CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return source;
            // Numbers, baud rates and unit-only options are not prose.
            if (Regex.IsMatch(source.Trim(), @"^[+-]?\d+(?:\.\d+)?(?:\s*(?:Hz|kHz|MHz|MBaud|kBaud|Baud|ms|s|m|cm|mm|m/s|V|A|%))?$", RegexOptions.IgnoreCase))
                return source;
            string reviewed;
            var key = Regex.Replace(source.Trim(), @"\s+", " ");
            return Reviewed.TryGetValue(key, out reviewed) || Catalog.Value.TryGetValue(key, out reviewed)
                ? reviewed : source;
        }
    }
}
