using System.Linq;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public sealed class ConfigTakeoff : ConfigIceEngine
    {
        internal static readonly Definition[] TakeoffDefinitions =
        {
            new Definition("TKOFF_THR_SLEW", "起飛油門漸增速率", "%/s", "-1–127；0 沿用 THR_SLEWRATE", -1, 127, true,
                "每秒容許的油門百分點變化；-1 不限速，0 沿用一般油門速率。", "滑跑可評估 20 起始值；官方不建議正值低於 20，避免爬升推力不足。10% 到 100% 在 20 %/s 下理論約 4.5 s，不代表 RPM 加速時間。"),
            new Definition("THR_SLEWRATE", "一般油門變化速率", "%/s", "0–127", 0, 127, true,
                "一般油門輸出的變化速率；TKOFF_THR_SLEW=0 時起飛也沿用。", "會影響起飛以外的油門反應。若只想讓起飛加油變緩，優先調整 TKOFF_THR_SLEW。實際最低速率受迴圈頻率與 PWM 解析度限制。"),
            new Definition("TKOFF_THR_MAX", "起飛油門上限", "%", "0–100；0 沿用 THR_MAX", 0, 100, true,
                "自動起飛允許的最大油門。", "不是加速時間；避免用降低上限代替漸增設定，須保留足夠起飛及爬升推力。"),
            new Definition("THR_MAX", "一般油門上限", "%", "0–100", 0, 100, true,
                "一般自動油門上限；TKOFF_THR_MAX=0 時作為起飛上限。", "依動力系統限制設定，會影響其他模式，不能只看起飛需求。"),
            new Definition("TKOFF_THR_MIN", "起飛油門下限", "%", "0–100", 0, 100, true,
                "TKOFF_OPTIONS bit 0 啟用時的起飛油門下限，與 THR_MIN 取較高者；亦影響 QuadPlane 前向轉換。", "需先驗證 TECS 與空速，無所有機型共用的安全值。"),
            new Definition("TKOFF_THR_MAX_T", "起飛最大油門持續時間", "s", "0–10", 0, 10, false,
                "起飛階段強制使用最大油門的時間，並非油門從怠速漸增的時間。", "依起飛油門策略與爬升需求調整；不要拿此值替代 TKOFF_THR_SLEW。"),
            new Definition("TKOFF_OPTIONS", "起飛油門策略", "bitmask", "bit 0：0 最大油門；1 TECS", 0, 1, true,
                "bit 0 未設：使用起飛最大油門；設為 1：允許 TECS 在起飛油門上下限間控制，需使用空速感測器。", "未完成空速／TECS 校準時不要任意啟用。若韌體新增其他 bit，請使用對應版本的完整參數頁設定，避免清掉未知 bit。"),
            new Definition("TKOFF_THR_MINACC", "啟動檢查加速度", "m/s²", "0–30", 0, 30, false,
                "通過前向加速度條件後才啟動後續起飛地速檢查；0 停用此檢查。", "手拋／彈射用於判斷發射動作；滑跑不應直接套用手拋門檻。依實際發射加速度驗證。"),
            new Definition("TKOFF_THR_MINSPD", "油門釋放最低地速", "m/s", "0–30", 0, 30, false,
                "自動起飛解除油門抑制的 GPS 地速條件；不是抬頭空速。", "手拋／彈射需搭配加速度與延遲檢查，GPS 有延遲與誤差，不能當作人員防護裝置。"),
            new Definition("TKOFF_THR_DELAY", "起飛油門延遲", "ds（0.1 s）", "0–127", 0, 127, true,
                "加速度檢查通過後，延後地速檢查的時間。輸入 2 代表 0.2 s；30 代表 3 s。", "手拋／彈射需確保離手／脫離發射裝置後才啟動動力。此值延後啟動，不會使加油過程變緩。"),
            new Definition("TKOFF_ROTATE_SPD", "滑跑抬頭空速", "m/s", "0–30", 0, 30, false,
                "達到此空速後進入起飛爬升俯仰；0 直接採用起飛俯仰。", "滑跑需高於失速速度並保留裕度；手拋／彈射按官方指南設 0。先確認空速可信及跑道足夠。"),
            new Definition("TKOFF_GND_PITCH", "滑跑俯仰目標", "deg", "-5–10", -5, 10, false,
                "低於抬頭速度時的地面滑跑俯仰目標。", "從機體實際地面姿態評估，小量調整並確認不會過早離地；不直接套用他機角度。"),
            new Definition("TKOFF_TDRAG_ELEV", "初段滑跑升降舵", "%", "-100–100；0 略過", -100, 100, true,
                "滑跑初段的升降舵輸出，用於維持尾輪或前輪接地。", "依起落架型式驗證；手拋／彈射設 0。過大的壓頭輸出可能造成不穩定，不自動套用。"),
            new Definition("TKOFF_TDRAG_SPD1", "結束壓尾階段空速", "m/s", "0–30", 0, 30, false,
                "達此空速後結束初段壓尾控制，接著等待抬頭空速。", "與 TDRAG_ELEV 配合；手拋／彈射設 0，尾輪機依失速速度與地面操控調整。"),
            new Definition("TKOFF_ALT", "TAKEOFF 目標高度", "m", "0–200", 0, 200, false,
                "TAKEOFF 模式的起飛目標高度；AUTO 任務的 NAV_TAKEOFF 高度由任務指定。", "依地形、障礙物及空域設定，不代表海拔高度；勿把 0 當作安全的預設起飛高度。"),
            new Definition("TKOFF_LVL_ALT", "初段機翼水平高度", "m", "0–50", 0, 50, false,
                "低於此高度時，以 LEVEL_ROLL_LIMIT 限制起飛滾轉，之後逐步恢復正常限制。", "保留足夠離地裕度，並檢查後續轉彎空間。"),
            new Definition("TKOFF_LVL_PITCH", "TAKEOFF 起飛俯仰", "deg", "0–30", 0, 30, false,
                "TAKEOFF 模式使用的起飛俯仰目標。", "依已驗證的爬升能力設定；過大可能導致空速不足，不能用抬高機頭補償推力不足。"),
            new Definition("TKOFF_DIST", "TAKEOFF 盤旋點距離", "m", "0–500", 0, 500, false,
                "沿起飛方向設定盤旋點與起飛位置的距離；不是保證滑跑距離。", "確認盤旋範圍與地形／障礙物淨空，不能只依跑道長度設定。"),
            new Definition("TKOFF_TIMEOUT", "起飛逾時保護", "s", "0–120；0 停用", 0, 120, true,
                "指定時間內未達至少 4 m/s 地速，會中止起飛並上鎖。", "油門漸增變慢後要一併評估此時間；避免正常加速期間誤觸發，也不要為掩蓋問題而任意停用。")
        };
        private const string Guide = "本頁為固定翼 TAKEOFF／AUTO 起飛設定，不是 QuadPlane 的 QTAKEOFF。\r\n\r\n" +
            "滑跑：先驗證輪舵、跑道距離及抬頭空速，再評估 TKOFF_THR_SLEW。\r\n手拋／彈射：檢查加速度、地速、延遲及發射器脫離條件，不能照抄滑跑的慢速加油設定。\r\n\r\n" +
            "想讓怠速到全油門變緩：調整 TKOFF_THR_SLEW，不是 TKOFF_THR_DELAY 或 ICE_IDLE_SLEW。\r\n本頁不提供一鍵套用，不會更改模式、解鎖或執行起飛。未回傳參數僅供說明查閱。\r\n\r\n" +
            "官方：https://ardupilot.org/plane/docs/automatic-takeoff.html\r\nhttps://ardupilot.org/plane/docs/takeoff-mode.html\r\n" +
            "定義及範圍核對：Plane-4.6.3 / ArduPlane/Parameters.cpp、mode_takeoff.cpp。其他版本請核對對應韌體，未知 bit 不自動覆寫。";

        internal static bool IsRelevant(string name, int method)
        {
            if (method >= 6) return true;
            if (method <= 0) return true;
            bool ground = name == "TKOFF_ROTATE_SPD" || name == "TKOFF_GND_PITCH" ||
                name == "TKOFF_TDRAG_ELEV" || name == "TKOFF_TDRAG_SPD1";
            bool launch = name == "TKOFF_THR_MINACC" || name == "TKOFF_THR_MINSPD" || name == "TKOFF_THR_DELAY";
            // Nose-wheel aircraft can also use TDRAG settings to control wheel loading.
            return method <= 3 ? !ground : !launch;
        }

        internal static string MethodGuide(int method)
        {
            switch (method)
            {
                case 1: return "手拋：核對加速度、地速、離手延遲及爬升姿態；勿照抄輪跑的慢速加油。ROTATE_SPD、TDRAG_ELEV、TDRAG_SPD1 應核對為 0。";
                case 2: return "彈射：依發射加速度及脫離架體的時間設定，確保螺旋槳不碰架體；勿把手拋延遲直接套用。輪跑抬頭／壓尾設定應核對為 0。";
                case 3: return "彈力繩：依實際脫鉤／離繩時間核對動力延遲，避免螺旋槳捲繩；過長延遲也可能失速。輪跑抬頭／壓尾設定應核對為 0。";
                case 4: return "輪跑（前三點）：優先核對油門漸增、抬頭空速、地面俯仰及跑道長度；TDRAG 設定僅於需要調整前輪負荷時使用。";
                case 5: return "輪跑（尾輪）：核對初段壓尾、解除壓尾空速、抬頭空速、油門漸增及地面方向控制；避免過早抬尾或抬頭。";
                case 6: return "多旋翼（ArduCopter）：搖桿觸發起飛與 AUTO 任務高度不同；先確認馬達方向、螺旋槳及高度估測。勿使用固定翼油門漸增參數。";
                case 7: return "傳統直升機（ArduCopter Heli）：先完成旋翼／總距與 motor interlock 設定，待旋翼完成加速才起飛；油門漸增時間不等於旋翼實際就緒時間。";
                case 8: return "VTOL（ArduPlane QuadPlane）：此處為垂直起飛，不是固定翼滑跑。任務高度由 NAV_VTOL_TAKEOFF 指定；先驗證懸停、轉換及 Q_OPTIONS 的起飛行為。";
                default: return "請先選擇起飛方式才能編輯。選項只提供指引，不代表飛控已設定成該起飛方式。";
            }
        }

        internal static readonly Definition[] CopterDefinitions =
        {
            new Definition("PILOT_TKOFF_ALT", "搖桿觸發起飛高度", "cm", "0–1000", 0, 1000, false,
                "高度控制模式以油門搖桿觸發起飛時的爬升高度，不是 AUTO 任務起飛高度。", "依淨空及定位狀態設定；100 cm = 1 m。較新韌體若改名／改單位，勿直接複製數值；未回傳時請使用對應版本完整參數。"),
            new Definition("PILOT_SPEED_UP", "搖桿最大爬升速度", "cm/s", "50–500", 50, 500, false,
                "飛手控制高度時可要求的最大向上速度。", "依推力裕度及飛行測試設定；不是馬達油門漸增速率，也不是任務高度。"),
            new Definition("PILOT_ACCEL_Z", "搖桿垂直加速度", "cm/s²", "50–500", 50, 500, false,
                "飛手控制高度時的垂直加速度。", "與爬升速度一併評估；不要用極低加速度掩蓋動力或控制問題。")
        };
        internal static readonly Definition[] HeliDefinitions = CopterDefinitions.Concat(new[]
        {
            new Definition("H_RSC_RAMP_TIME", "旋翼油門漸增時間", "s", "0–60", 0, 60, false,
                "motor interlock 啟用後，HeliRSC 油門由地面怠速增加至飛行怠速設定的時間。", "依引擎／ESC 與傳動限制設定；不是旋翼轉速已到位的證明。"),
            new Definition("H_RSC_RUNUP_TIME", "旋翼加速就緒時間", "s", "0–60", 0, 60, false,
                "motor interlock 啟用後，預留旋翼達到飛行轉速的實際時間。", "至少比 H_RSC_RAMP_TIME 長 1 秒，且須涵蓋實測暖機與加速時間；不足可能在動力未就緒前自動起飛。")
        }).ToArray();
        internal static readonly Definition[] VtolDefinitions =
        {
            new Definition("Q_VELZ_MAX", "VTOL 搖桿最大爬升速度", "cm/s", "50–500", 50, 500, false,
                "VTOL 高度控制模式中，飛手可要求的最大向上速度。", "依懸停推力裕度設定；不等同固定翼爬升或轉換速度。"),
            new Definition("Q_ACCEL_Z", "VTOL 搖桿垂直加速度", "cm/s²", "50–500", 50, 500, false,
                "VTOL 飛手控制高度時的垂直加速度。", "先驗證懸停再調整，不自動套用固定翼設定。"),
            new Definition("Q_WP_SPEED_UP", "VTOL 任務爬升速度", "cm/s", "10–1000", 10, 1000, false,
                "VTOL 航點任務爬升的目標速度。", "依推力、負載及淨空設定，與搖桿爬升限制分開核對。"),
            new Definition("Q_NAVALT_MIN", "VTOL 起飛導航起始高度", "m", "0–5；0 停用", 0, 5, false,
                "自動起飛低於此高度時，目標 Roll／Pitch 為零；超過後開始導航。", "依地面淨空及偏移風險評估；不是最終起飛高度，也不保證低空定點。")
        };
        internal static Definition[] DefinitionsForProfile(int method) => method == 6 ? CopterDefinitions
            : method == 7 ? HeliDefinitions : method == 8 ? VtolDefinitions : TakeoffDefinitions;

        public ConfigTakeoff() : base("起飛設定 TAKEOFF", TakeoffDefinitions,
            Guide + "\r\n\r\n多旋翼／傳統直升機：https://ardupilot.org/copter/docs/parameters.html\r\nVTOL：https://ardupilot.org/plane/docs/quadplane-auto-mode.html\r\n垂直起飛選項獨立顯示參數，不會變更機型、韌體或任務。", false,
            "選擇固定翼、多旋翼、傳統直升機或 VTOL 起飛方式後設定參數。\r\n本頁不切換模式、解鎖或啟動動力；只有已回傳參數可寫入，不自動套用。\r\n各機型與韌體參數不可互用；顯示全部只顯示所選機型的參數。")
        {
            AddProfileSelector(new[] { "請選擇起飛方式", "手拋", "彈射", "彈力繩", "輪跑（前三點起落架）", "輪跑（尾輪式起落架）", "多旋翼（ArduCopter）", "傳統直升機（Heli）", "VTOL（QuadPlane 垂直起飛）" },
                IsRelevant, MethodGuide, DefinitionsForProfile);
        }
    }
}
