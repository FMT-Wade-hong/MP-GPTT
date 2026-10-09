namespace MissionPlanner.GCSViews.ConfigurationView
{
    public sealed class ConfigLanding : ConfigIceEngine
    {
        internal static readonly Definition[] LandingDefinitions =
        {
            new Definition("TECS_LAND_ARSPD", "進場目標空速", "m/s", "-1–127；-1 自動選取", -1, 127, false,
                "自動降落進場的目標空速；負值使用韌體依最低與巡航空速選取的目標。", "需可靠空速感測器；依失速速度、襟翼及陣風保留裕度。無空速感測器時參考 TECS_LAND_THR，不可照抄他機速度。"),
            new Definition("TECS_LAND_THR", "進場巡航油門參考", "%", "-1–100；-1 停用", -1, 100, false,
                "無空速感測器時使用的降落進場巡航油門參考；不是全程固定油門指令。", "須依實測進場速度與下滑能力設定；不能用降油門掩蓋過陡下滑航線。"),
            new Definition("TECS_APPR_SMAX", "進場最大下降率", "m/s", "0–20；0 沿用 TECS_SINK_MAX", 0, 20, false,
                "降落進場階段允許的最大下降率。", "依滑降能力與進場坡度調整；過大可能造成超速，無通用建議值。"),
            new Definition("LAND_FLARE_SEC", "拉平觸發垂直時間", "s", "0–10；0 僅使用高度", 0, 10, false,
                "以離地高度與下降率估計垂直到地面的時間，達門檻即觸發拉平；不是到跑道的水平飛行時間。", "配合實際下降率與拉平反應調整，不能以此取代正確的降落高度基準。"),
            new Definition("LAND_FLARE_ALT", "拉平觸發高度", "m", "0–30", 0, 30, false,
                "自動降落進入拉平的高度門檻，配合 LAND_FLARE_SEC 使用。", "太高可能飄浮或失速，太低可能來不及減緩下降；先驗證高度量測及地面基準。"),
            new Definition("LAND_PITCH_DEG", "拉平後最低俯仰", "deg", "-20–20", -20, 20, false,
                "最後降落階段的俯仰下限，不是固定俯仰目標。", "依起落架與接地姿態設定，控制器仍可能要求更高俯仰；避免尾擦地或失速。"),
            new Definition("TECS_LAND_PMAX", "最後階段最大俯仰", "deg", "-5–40；0 沿用正常限制", -5, 40, false,
                "自動降落最後階段的俯仰上限。", "與 LAND_PITCH_DEG 一併核對，不要讓上下限互相矛盾；保留失速裕度。"),
            new Definition("TECS_LAND_SINK", "最後階段下降率", "m/s", "0–2", 0, 2, false,
                "拉平後最後降落階段的目標下降率。", "依起落架承受能力與飄浮距離調整；過小不保證更安全，可能延長接地距離。"),
            new Definition("LAND_PF_ARSPD", "預拉平目標空速", "m/s", "0–30；0 停用預拉平", 0, 30, false,
                "預拉平階段的空速目標，用於拉平前減速。", "不得低於該重量與襟翼狀態所需的失速裕度；未驗證前不任意啟用。"),
            new Definition("LAND_PF_ALT", "預拉平觸發高度", "m", "0–30", 0, 30, false,
                "進入預拉平階段的高度門檻；LAND_PF_ARSPD=0 時不生效。", "應留出在正式拉平前減速的空間，搭配 PF_SEC 及 FLARE_ALT 核對。"),
            new Definition("LAND_PF_SEC", "預拉平垂直時間", "s", "0–10", 0, 10, false,
                "以高度與下降率估計的預拉平觸發時間；LAND_PF_ARSPD=0 時不生效。", "應在正式拉平之前觸發；非計時器延遲，下降率改變會影響觸發位置。"),
            new Definition("LAND_THR_SLEW", "降落油門變化速率", "%/s", "0–127；0 沿用 THR_SLEWRATE", 0, 127, true,
                "自動降落油門變化的每秒百分點限制。", "官方不建議正值低於 50，避免低空速時無法及時增加油門；勿照抄起飛的慢速加油值。"),
            new Definition("LAND_FLAP_PERCNT", "降落襟翼", "%", "0–100", 0, 100, true,
                "自動降落進場及拉平使用的襟翼比例。", "先驗證襟翼方向、機械行程及俯仰影響；百分比不是實際偏轉角度。"),
            new Definition("LAND_FLARE_AIM", "拉平瞄準點補償", "%", "0–100", 0, 100, true,
                "補償拉平所需飛行距離的瞄準點調整比例。", "持續落在目標前方可減少，持續落過頭可增加；先排除風、高度及空速誤差再調整。"),
            new Definition("LAND_WIND_COMP", "進場逆風補償", "%", "0–100；0 停用", 0, 100, true,
                "將逆風分量按比例加入降落目標空速，仍受最大空速限制。", "依風況與跑道長度評估；增加空速也可能增加飄浮及接地距離。"),
            new Definition("LAND_OPTIONS", "降落選項", "bitmask", "0–3（已核對 bit 0–1）", 0, 3, true,
                "bit 0（1）：拉平期間遵守最低油門。bit 1（2）：將降落空速目標上限由巡航空速放寬到 AIRSPEED_MAX。", "設定 bit 0 可能保留動力，不保證引擎熄火。較新韌體若有其他 bit，請用對應版本完整參數頁，勿清除未知 bit。"),
            new Definition("LAND_ABORT_THR", "高油門中止降落", "", "0 停用 / 1 啟用", 0, 1, true,
                "啟用後，RC 油門輸入達 90% 以上可要求中止降落。", "先確認 RC 油門映射、控制權及復飛程序；本頁不會發送復飛指令。"),
            new Definition("LAND_SLOPE_RCALC", "測距下滑線重算門檻", "m", "0–5；0 保持原下滑線", 0, 5, false,
                "降落測距高度修正顯示飛機比預期低，超過門檻時重算較緩下滑線；需 RNGFND_LANDING。", "先驗證測距有效範圍與地形，避免樹木／障礙物被當成跑道。"),
            new Definition("LAND_ABORT_DEG", "下滑線自動復飛門檻", "deg", "0–90；0 停用", 0, 90, false,
                "測距修正使新下滑線比原計畫陡超過此角度時，可自動中止降落；需 LAND_SLOPE_RCALC>0。", "不是絕對進場角度；此自動復飛只嘗試一次，必須先規劃可安全執行的復飛航線。"),
            new Definition("LAND_DISARMDELAY", "降落完成後上鎖延遲", "s", "0–127；0 不自動上鎖", 0, 127, true,
                "LAND 航點降落完成後，等待此秒數才自動上鎖。", "須涵蓋接地滑跑；上鎖不一定會切斷 ICE 點火，需同時確認引擎安全設定。"),
            new Definition("LAND_THEN_NEUTRL", "自動上鎖後舵機動作", "", "0 不變 / 1 中立 / 2 零 PWM", 0, 2, true,
                "自動降落並經 LAND_DISARMDELAY 上鎖後的舵機輸出處理。", "零 PWM 是停止脈波，不是舵機回中；依舵機及外接模組的失訊行為驗證。")
        };

        private const string Guide = "本頁為固定翼標準下滑線自動降落，通常配合 AUTO 任務中的 NAV_LAND；不是 QuadPlane QLAND／垂直降落設定，也不是切換模式按鈕。\r\n\r\n" +
            "建議順序：驗證空速與 TECS → 設定進場航線及高度 → 襟翼與進場速度 → 預拉平／拉平 → 接地與上鎖 → 規劃復飛。\r\n" +
            "必須核對降落點高度基準、坡度、風、地形與障礙物。預拉平和拉平時間都是垂直到地面的估計，不是水平抵達時間。\r\n\r\n" +
            "LAND_TYPE=1 為 Deepstall，並非本頁標準降落流程；相關 LAND_DS_* 需專門設定。本頁不切換 LAND_TYPE，也不套用通用降落值。\r\n" +
            "復飛必須預先規劃安全航線及足夠動力。ICE 上鎖與停止點火不是同一件事，請另外驗證引擎停止機制。\r\n\r\n" +
            "官方：https://ardupilot.org/plane/docs/automatic-landing.html\r\n" +
            "定義依據：Plane-4.6.3 / libraries/AP_Landing/AP_Landing.cpp 與 libraries/AP_TECS/AP_TECS.cpp。不同版本以實際回傳參數及對應官方文件為準。";

        public ConfigLanding() : base("降落設定 LAND", LandingDefinitions, Guide, false,
            "固定翼標準自動降落參數；不包含 QuadPlane 垂直降落或 Deepstall。\r\n本頁不切換模式、不執行降落／復飛；只讀寫選定參數。\r\n選取參數查看定義、單位、官方範圍及建議；未回傳項目不可寫入，不自動套用設定。") { }
    }
}
