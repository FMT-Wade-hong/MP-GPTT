using System.Collections.Generic;
using System.Linq;

namespace MissionPlanner.FMT
{
    internal sealed class FmtSafetyParameterDefinition
    {
        internal FmtSafetyParameterDefinition(string title, string description, params string[] names)
        {
            Title = title;
            Description = description;
            Names = names;
        }

        internal string Title { get; }
        internal string Description { get; }
        internal string[] Names { get; }

        internal string Resolve(IEnumerable<string> availableNames)
        {
            var available = new HashSet<string>(availableNames ?? Enumerable.Empty<string>());
            return Names.FirstOrDefault(available.Contains);
        }
    }

    internal static class FmtSafetyParameterCatalog
    {
        internal static IReadOnlyList<FmtSafetyParameterDefinition> Definitions { get; } =
            new[]
            {
                new FmtSafetyParameterDefinition("遙控器失聯動作", "遙控器訊號遺失或失效時要執行的動作。",
                    "FS_THR_ENABLE", "THR_FAILSAFE"),
                new FmtSafetyParameterDefinition("遙控器失聯逾時", "遙控器資料中斷多久後進入失效保護。",
                    "RC_FS_TIMEOUT"),
                new FmtSafetyParameterDefinition("地面站失聯動作", "MAVLink 心跳中斷時要執行的動作。",
                    "FS_GCS_ENABLE", "FS_GCS_ENABL"),
                new FmtSafetyParameterDefinition("地面站失聯逾時", "地面站心跳中斷多久後進入失效保護。",
                    "FS_GCS_TIMEOUT", "FS_LONG_TIMEOUT"),
                new FmtSafetyParameterDefinition("低電量動作", "電池到達低電量門檻時要執行的動作。",
                    "BATT_FS_LOW_ACT", "FS_BATT_ENABLE"),
                new FmtSafetyParameterDefinition("低電壓門檻", "低電量失效保護的電壓門檻；零值通常代表停用此門檻。",
                    "BATT_LOW_VOLT", "FS_BATT_VOLTAGE", "LOW_VOLT"),
                new FmtSafetyParameterDefinition("低電量延遲", "低電量狀況持續達此秒數後才觸發。",
                    "BATT_LOW_TIMER"),
                new FmtSafetyParameterDefinition("嚴重電量動作", "電池到達嚴重電量門檻時要執行的動作。",
                    "BATT_FS_CRT_ACT"),
                new FmtSafetyParameterDefinition("嚴重電壓門檻", "嚴重電量失效保護的電壓門檻。",
                    "BATT_CRT_VOLT"),
                new FmtSafetyParameterDefinition("EKF 異常動作", "姿態或位置估測異常時要執行的動作。",
                    "FS_EKF_ACTION"),
                new FmtSafetyParameterDefinition("撞擊檢查動作", "偵測到撞擊事件時要執行的動作。",
                    "FS_CRASH_CHECK"),
                new FmtSafetyParameterDefinition("電子圍籬動作", "突破電子圍籬限制時要執行的動作。",
                    "FENCE_ACTION"),
                new FmtSafetyParameterDefinition("失效保護例外選項", "位元遮罩：控制 AUTO、GUIDED、降落中等狀態是否繼續。請依飛控參數說明設定。",
                    "FS_OPTIONS")
            };

        internal static string TranslateOption(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "未命名選項";

            var translations = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Disabled", "停用" },
                { "NoAction", "不執行動作" },
                { "Enabled", "啟用" },
                { "Continue with Mission in Auto Mode", "AUTO 模式繼續任務" },
                { "ContinueMission", "繼續任務" },
                { "Always RTL", "一律返航" },
                { "Always Land", "一律降落" },
                { "SmartRTL or RTL", "SmartRTL，否則返航" },
                { "SmartRTL or Land", "SmartRTL，否則降落" },
                { "Auto DO_LAND_START or RTL", "執行降落序列，否則返航" },
                { "Brake or Land", "煞車，否則降落" },
                { "Heartbeat", "心跳逾時" },
                { "HeartbeatAndREMRSSI", "心跳或遙測接收失效" },
                { "HeartbeatAndAUTO", "僅 AUTO 模式心跳逾時" },
                { "Continue if in auto mode on RC failsafe", "遙控失聯時繼續 AUTO 任務" },
                { "Continue if in Auto on RC failsafe", "遙控失聯時繼續 AUTO 任務" },
                { "Continue if in auto mode on GCS failsafe", "地面站失聯時繼續 AUTO 任務" },
                { "Continue if in Auto on GCS failsafe", "地面站失聯時繼續 AUTO 任務" },
                { "Continue if in guided mode on RC failsafe", "遙控失聯時繼續 GUIDED 模式" },
                { "Continue if in Guided on RC failsafe", "遙控失聯時繼續 GUIDED 模式" },
                { "Continue if landing on any failsafe", "失效保護期間繼續降落" },
                { "Continue in pilot controlled modes on GCS failsafe", "地面站失聯時繼續人工控制" },
                { "Release gripper", "失效保護時釋放夾爪" },
                { "Terminate", "終止飛行" },
                { "Hold", "保持" },
                { "HoldAndDisarm", "保持並鎖定" },
                { "Land", "降落" },
                { "RTL", "返航" }
            };

            string translated;
            if (translations.TryGetValue(text.Trim(), out translated))
                return translated;

            translated = text;
            foreach (var pair in translations.OrderByDescending(pair => pair.Key.Length))
                translated = translated.Replace(pair.Key, pair.Value);
            return translated;
        }
    }
}
