# 全部參數表繁體中文對照

最新電源校正驗證：`bin/TranslationSemantic119N/net461`；編譯 0 errors、1,390 warnings。156 個原文語意案例、ReviewedParameterTranslation、58 份 metadata／3,335 個選項與 Copter 4.5.7／4.6.3／4.7.1 精確版本測試通過；`git diff --check` 通過。未連線實機、未推送或發布；全表逐條語意複核仍持續進行。

2026-10-02 電源校正複核：補強 9 條原文。類比電流係數使用 A/V，零電流 offset 先以 V 扣除再換算；電壓 offset 同樣在乘倍率前扣除。Synthetic Current 的共用參數意義另行說明：係數與零油門 offset 以 A 解讀，電流來自油門與電壓估算而非直接量測，MAX_VOLT 是縮放基準而非過電壓門檻。保留原始 metadata 單位欄位與鍵，不以模式解釋覆寫共用單位。依 [4.5.7 Analog](https://github.com/ArduPilot/ardupilot/blob/Copter-4.5.7/libraries/AP_BattMonitor/AP_BattMonitor_Analog.cpp)、[4.6.3 Analog](https://github.com/ArduPilot/ardupilot/blob/Copter-4.6.3/libraries/AP_BattMonitor/AP_BattMonitor_Analog.cpp)、[4.6.3 Synthetic Current](https://github.com/ArduPilot/ardupilot/blob/Copter-4.6.3/libraries/AP_BattMonitor/AP_BattMonitor_Synthetic_Current.cpp) 及 master Synthetic Current 核對；新增 9 個案例，累計 156 個原文語意案例。

最新解鎖檢查驗證：`bin/TranslationSemantic119M/net461`；編譯 0 errors、1,390 warnings。147 個原文語意案例、ReviewedParameterTranslation、58 份 metadata／3,335 個選項，以及 Copter 4.5.7／4.6.3／4.7.1 精確版本測試通過。未連線實機、未推送或發布；全表逐條語意複核仍持續進行。

2026-10-02 電池解鎖前檢查複核：32 條 ARM_MAH 原文變體補充剩餘容量 mAh、容量估算基準、重啟不代表充滿，以及 0 僅停用該項門檻；ARM_VOLT 說明區分監測電壓與壓降補償估計值，0 不保證可以解鎖。依 [Copter 4.5.7 backend](https://github.com/ArduPilot/ardupilot/blob/Copter-4.5.7/libraries/AP_BattMonitor/AP_BattMonitor_Backend.cpp)、[4.6.3 backend](https://github.com/ArduPilot/ardupilot/blob/Copter-4.6.3/libraries/AP_BattMonitor/AP_BattMonitor_Backend.cpp)、[4.6.3 參數定義](https://github.com/ArduPilot/ardupilot/blob/Copter-4.6.3/libraries/AP_BattMonitor/AP_BattMonitor_Params.cpp) 及 master 核對。保留各版原文鍵與參數引用，未修改飛控值；新增 33 個案例，累計 147 個原文語意案例（含編號電池變體）。

最新電池保護驗證：`bin/TranslationSemantic119L/net461`；編譯 0 errors、1,390 warnings。114 個原文語意案例、ReviewedParameterTranslation、58 份 metadata／3,335 個選項及 Copter 4.5.7／4.6.3／4.7.1 精確版本測試通過。測試改為逐條讀取容量原文，避免 Windows PowerShell 全表 JSON 大小寫鍵名衝突；未修改來源鍵。未連線實機、未推送或發布；自動覆蓋率不代表全表逐條語意複核完成。

2026-10-02 電池 failsafe 複核：48 條剩餘容量原文變體明示 mAh、總容量減去已消耗容量、電流監測條件，以及容量門檻不等待低電壓計時。LOW_TIMER 說明補強連續低電壓、恢復後重置、低／嚴重電壓各自計時；0 不表示立即觸發，也不取消容量或解鎖前電壓檢查。保留所有原文參數引用與版本鍵。依 [Copter 4.5.7 backend](https://github.com/ArduPilot/ardupilot/blob/Copter-4.5.7/libraries/AP_BattMonitor/AP_BattMonitor_Backend.cpp)、[4.6.3 backend](https://github.com/ArduPilot/ardupilot/blob/Copter-4.6.3/libraries/AP_BattMonitor/AP_BattMonitor_Backend.cpp)、[4.6.3 參數定義](https://github.com/ArduPilot/ardupilot/blob/Copter-4.6.3/libraries/AP_BattMonitor/AP_BattMonitor_Params.cpp) 及 master 核對；master 不作為舊版 metadata 的替代。新增 49 個原文語意測試案例（其中 48 個為容量變體），累計 114 個。

最新降落後輸出驗證：`bin/TranslationSemantic119K/net461`。65 項語意、ReviewedParameterTranslation、58 份 metadata／3,335 種選項及 Copter 4.5.7／4.6.3／4.7.1 精確版本測試通過；首次編譯 0 errors、1,390 warnings，繁體字修正後增量編譯 0 errors、1,284 warnings。未連線實機、未推送或發布；全表語意複核持續進行。

2026-10-02 降落後輸出／上鎖複核：LAND_THEN_NEUTRL 修正「所有 servo」為控制舵面輸出，保留 AUTO、LAND_DISARMDELAY 及已上鎖條件；區分中立控制量與 0 PWM，後者不是切斷電源。LAND_DISARMDELAY 補充不再飛行判定與 0 的停用範圍；LAND_ABORT_THR 明示是 RC 油門輸入而非馬達輸出，仍須降落流程接受請求。修改 3 條說明及 2 個標籤，語意測試累計 65 項。依 [Plane 4.5.7 servos](https://github.com/ArduPilot/ardupilot/blob/Plane-4.5.7/ArduPlane/servos.cpp)、[4.6.3 servos](https://github.com/ArduPilot/ardupilot/blob/Plane-4.6.3/ArduPlane/servos.cpp)、master servos 及 [4.6.3 ArduPlane](https://github.com/ArduPilot/ardupilot/blob/Plane-4.6.3/ArduPlane/ArduPlane.cpp) 核對，未更改參數值或 bit 索引。

最新降落驗證：`bin/TranslationSemantic119J/net461`，編譯 0 errors、1,390 warnings。62 項語意、ReviewedParameterTranslation、58 份 metadata／3,335 種選項及 Copter 4.5.7／4.6.3／4.7.1 精確版本測試通過。未連線實機、未推送或發布；全表語意複核仍持續進行。

2026-10-02 固定翼降落複核：5 條原文修正。LAND_WIND_COMP 新舊說明改為百分比（值 ÷ 100），50 不是 50 倍，保留各版 AIRSPEED_MAX／ARSPD_FBW_MAX 引用；LAND_PF_ARSPD=0 只停用 pre-flare，不是要求零空速或停用降落。LAND_FLARE_ALT 的兩種 Pitch 參數原文明示 Pitch 是下限，且高度條件不必同時滿足時間條件，仍保留進場階段限制。核對 [Plane 4.5.7 Slope](https://github.com/ArduPilot/ardupilot/blob/Plane-4.5.7/libraries/AP_Landing/AP_Landing_Slope.cpp)、[4.6.3 Slope](https://github.com/ArduPilot/ardupilot/blob/Plane-4.6.3/libraries/AP_Landing/AP_Landing_Slope.cpp) 與 [4.6.3 參數定義](https://github.com/ArduPilot/ardupilot/blob/Plane-4.6.3/libraries/AP_Landing/AP_Landing.cpp)。新增 5 項語意測試，累計 62 項；不更改飛控值或 metadata 單位。

最新固定翼起飛驗證：`bin/TranslationSemantic119I/net461`；編譯 0 errors、1,390 warnings。57 項語意測試、ReviewedParameterTranslation、58 份 metadata 全表稽核及 Copter 4.5.7／4.6.3／4.7.1 精確版本測試均通過。未連線實機、未推送或發布；覆蓋率不代表全表逐條語意複核完成。

2026-10-02 固定翼起飛複核：補強 4 條完整原文。TKOFF_THR_SLEW 明示每秒油門百分點而非相對比例；TKOFF_THR_DELAY 明示 30 為 3 秒、到期仍需其他條件，不承諾拋投安全；TKOFF_ACCEL_CNT 明示前後交替事件、0.5 秒間隔重置及不是飛控解鎖；TKOFF_TIMEOUT 明示此回合達到 4 m/s 後清除計時，不是全程低速保護，0 只停用該逾時。依 [Plane 4.5.7 takeoff](https://github.com/ArduPilot/ardupilot/blob/Plane-4.5.7/ArduPlane/takeoff.cpp)、[4.6.3 takeoff](https://github.com/ArduPilot/ardupilot/blob/Plane-4.6.3/ArduPlane/takeoff.cpp) 與 master 核對；逾時函式以 4.6.3／master 為據，不據此宣稱 4.5 有相同參數。保留各版原文鍵與數值，新增 4 項語意測試，累計 57 項。

最新精準降落驗證：`bin/TranslationSemantic119H/net461`，編譯 0 errors、1,390 warnings；53 項語意、ReviewedParameterTranslation、全表 58 份 metadata／3,335 種選項及 Copter 4.5.7／4.6.3／4.7.1 精確版本測試通過。未連線實機、未推送或發布。

2026-10-02 精準降落複核：PLND_ALT_MIN／MAX 的 0 分別只取消低高度例外／高度上限，不停用精準降落；PLND_TIMEOUT 兩種原文補上最後有效目標輸出的計時基準，0 不等於停用重試；PLND_RET_MAX=0 仍可進入 failsafe。PLND_RET_BEHAVE 兩種選項區分「最後偵測時的機體位置」與「估計目標位置」，完整英文標籤及數值保留。依 [AC_PrecLand 4.5.7](https://github.com/ArduPilot/ardupilot/blob/Copter-4.5.7/libraries/AC_PrecLand/AC_PrecLand.cpp)、[StateMachine 4.6.3](https://github.com/ArduPilot/ardupilot/blob/Copter-4.6.3/libraries/AC_PrecLand/AC_PrecLand_StateMachine.cpp) 與 master 對照；沒有把 master 視為每個 4.7 patch 的驗證。新增 5 項語意測試（累計 53）及 2 項選項語意測試。

本輪驗證輸出：`bin/TranslationSemantic119G/net461`；編譯 0 errors、1,450 warnings。48 項語意測試、ReviewedParameterTranslation、58 份 metadata 全表稽核及 Copter 4.5.7／4.6.3／4.7.1 版本測試通過。3,335 種選項的原文與 bit 索引保留；已掃描說明的英文回退仍為 0。此輪僅本機修正，未連線實機、未推送或發布；全表逐項語意核對仍不可僅憑覆蓋率判定完成。

2026-10-02 起飛／返航／降落補充核對：TAKEOFF 目標高度明示地面完整起飛時相對起始位置，不是海拔或每次切入再爬升同一高度；依 [Plane 4.5.7](https://github.com/ArduPilot/ardupilot/blob/Plane-4.5.7/ArduPlane/mode_takeoff.cpp)、[4.6.3](https://github.com/ArduPilot/ardupilot/blob/Plane-4.6.3/ArduPlane/mode_takeoff.cpp) 與 master 核對。Q_RTL_PAUSE_TIME 的 0 明示只略過等待，不是停用降落，依 [QuadPlane](https://github.com/ArduPilot/ardupilot/blob/master/ArduPlane/quadplane.cpp)。RTL 最終高度兩種原文同步補上 Home 基準、0 降落、正值停留與 RC failsafe 例外，不把任務結束一概等同 RTL；依 [Copter 4.5.7](https://github.com/ArduPilot/ardupilot/blob/Copter-4.5.7/ArduCopter/mode_rtl.cpp)、4.6.3 與 master 的 build_path／run。保留各版 metadata 單位，不將新版 m 套用到舊版 cm；master 比對不等同已驗證全部 4.7 patch。新增 3 項語意測試（累計 48），另測試 RTL 新舊原文顯示一致。

2026-10-01 全表說明覆蓋：相對本輪起點新增 2,354 條完整原文對照，reviewed JSON 累計 3,836 條。掃描本機 58 份公開 metadata，共 3,934 種不同說明（含既有人工對照），英文回退為 0。涵蓋 4.5／4.6／4.7 的本機快取及其他已存在版本，不以連線飛控目前啟用或可見的參數篩選。這是明確來源集合的覆蓋結果，不代表所有未下載的 patch 或未來韌體。

選項另以完整原文匹配加上中文解釋，保留完整英文、模式名、協定、縮寫、軸向及數值。最新 audit 另列 LocalizedOptionLabels 與 OriginalOptionLabels；原文選項包含刻意保留的硬體型號、模式、軸向、單位、編號等，不能把其數量直接當成漏譯數。覆蓋率與完整語意審核分開記錄，CompleteSemanticReview 不由零英文回退自動設為 true。

本輪核對官方實作並註明原文問題：jerk 單位為 m/s/s/s、STAT_RESET 為時間戳而非經過時間、ARM 腳本數值為 MAV_SEVERITY、D_FF 取目標微分、Blimp 位置輸入為目標變化速率、GPS_COM_PORT2 更名為 GPS2_COM_PORT、THRCRV_100 為最大 collective、TOFSENSE_INST3 的 3 選第三個 backend、轉向 I 限制的 cdeg 換算。來源：[AC_WPNav](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AC_WPNav/AC_WPNav.cpp)、[AP_Stats](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Stats/AP_Stats.cpp)、[arming-checks](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Scripting/applets/arming-checks.lua)、[AC_PID](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AC_PID/AC_PID.cpp)、[Blimp](https://github.com/ArduPilot/ardupilot/blob/master/Blimp/mode_loiter.cpp)、[AP_GPS](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_GPS/AP_GPS.cpp)、[Heli RSC](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Motors/AP_MotorsHeli_RSC.cpp)、[TOFSense CAN](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Scripting/drivers/TOFSense-M/TOFSense-M_CAN.lua)、[SteerController](https://github.com/ArduPilot/ardupilot/blob/master/libraries/APM_Control/AP_SteerController.cpp)。後續已完成這兩項原文矛盾核對：TMODE_LOAD_FILT 依 Copter 4.5.7／4.6.3 與 master 的 `ArduCopter/toy_mode.cpp`，N 表示總共輸出 N 次（0 與 1 同效，ConstantThrust 不套用），不是額外重複 N 次。FBWB_ELEV_REV 依 Plane 4.5.7／4.6.3 的 `navigation.cpp` 及官方修正提交 [eedcecd](https://github.com/ArduPilot/ardupilot/commit/eedcecd71c7325743a72888c24d32e02027e48c3)，確認舊說明方向顛倒；0 拉桿升高、1 降低，前提為 RC 方向已正確校正。EK3_OPTIONS 截斷句依官方 AP_NavEKF3.cpp 補齊為避免不良位置估計，不將新版其他 bits 移植到舊版。語意測試增為 29 項。

候選目錄 `bin/Translation119FullAudit2/net461`。驗證通過 58 份 metadata 的原文來源／版本匹配，以及 3,335 種選項原文／bit 索引保留；語意回歸增至 26 項，Copter 4.5.7／4.6.3／4.7.1 精確版本測試通過。AutoTune 八項、未知新版原文回退及英文介面測試通過。編譯 0 errors、57 warnings；未啟動完整程式或連線實機。

最新選項稽核：1,571 種雙語標籤、1,764 種保留原文標籤（包含技術名稱，不等同漏譯）。補齊 GPS_NAVFILTER 的 Portable／Pedestrian／Automotive／Sea、OSD_UNITS 的 Aviation 及 SIM_PLD_TYPE 的 cylinder／cone／sphere，並新增八項原文與中文含義回歸檢查。GPS 選項另核對官方 `libraries/AP_GPS/AP_GPS.cpp`；其他項目依本機公開 pdef 的所屬參數語境核對，未改動數值。

本輪另修正全表測試在 Windows PowerShell 5 對僅大小寫不同 JSON 鍵的限制：以 Newtonsoft JObject 逐項讀取，保留執行期的大小寫敏感原文鍵；完全相同鍵的重複檢查仍保留。未發布、未推送，未連接或修改飛控。

後續安全與複製筆誤檢查：修正 LAND_DS_SLOPE_A／B 共用原文，明確區分 a 乘數與 b 常數項；SIM_ARSPD_FAIL／SIM_ARSPD2_FAIL 區分第一／第二個感測器；SERIAL8_BAUD／SERIAL9_BAUD 區分實際串列埠；servo 逾時說明改為 actuator 指令與 0 PWM，並非中立位置。這些共用原文會列出適用參數，避免僅按文字翻譯而錯套編號。來源：[Deepstall](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Landing/AP_Landing_Deepstall.cpp)（另核對 Plane-4.5.7／Plane-4.6.3）、[SITL 群組綁定](https://github.com/ArduPilot/ardupilot/blob/master/libraries/SITL/SITL.cpp)、[SITL Airspeed](https://github.com/ArduPilot/ardupilot/blob/master/libraries/SITL/SITL_Airspeed.cpp)、[SerialManager](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_SerialManager/AP_SerialManager.cpp)、[Periph rc_out](https://github.com/ArduPilot/ardupilot/blob/master/Tools/AP_Periph/rc_out.cpp)。語意回歸增為 33 項。

後續 failsafe／電池檢查：依 [Plane-4.5.7 Parameters](https://github.com/ArduPilot/ardupilot/blob/Plane-4.5.7/ArduPlane/Parameters.cpp) 與 [events](https://github.com/ArduPilot/ardupilot/blob/Plane-4.5.7/ArduPlane/events.cpp)，將舊說明 FS_LONG_ACTION 明示為 FS_LONG_ACTN 的拼字錯誤。依 [AP_BattMonitor_Backend](https://github.com/ArduPilot/ardupilot/blob/Plane-4.5.7/libraries/AP_BattMonitor/AP_BattMonitor_Backend.cpp)，補充 resting voltage 是依電流與內阻補償壓降後的估計值，而非直接量到的無負載／充滿電壓；平均單節值不是逐節量測，保留 OSD_CELL_COUNT 自動估算警告。語意回歸增為 36 項。

後續選項標籤核對：Raw Voltage 明示未補償負載壓降，Sag Compensated Voltage 明示估計值；SUM 監測器明示電流加總、電壓預設取平均（最低電壓選項另行控制）；Terminate 在 Rover 與航空載具共用，故明示緊急終止而非一般返航或降落。新增五項標籤回歸檢查，原始選項仍完整保留。核對 [AP_BattMonitor_Sum](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_BattMonitor/AP_BattMonitor_Sum.cpp) 與 [Copter-4.5.7 events](https://github.com/ArduPilot/ardupilot/blob/Copter-4.5.7/ArduCopter/events.cpp)。同時確認 Brake or Land 的既有翻譯與程式相符：成功切入 Brake 即返回，無法切入才改 Land；沒有把正常 Brake 模式誤寫成自動降落。

後續單位／翼型用語檢查：7 條 RC／PWM 脈寬與 dead zone 說明補上微秒（µs），保留 microseconds 原文，不更改 metadata 的 PWM 單位或數值。Heli 零推力總距說明將翼型 camber 明確寫為「彎度」，避免與 rad 弧度混淆；TECS_PTCH_FF_K 依公式明確標示 rad/(m/s)，不是角速度。來源：[RC_Channel 4.5.7](https://github.com/ArduPilot/ardupilot/blob/Copter-4.5.7/libraries/RC_Channel/RC_Channel.cpp)、[AP_MotorsHeli](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Motors/AP_MotorsHeli.cpp)、[AP_TECS 4.5.7](https://github.com/ArduPilot/ardupilot/blob/Plane-4.5.7/libraries/AP_TECS/AP_TECS.cpp)。新增 9 項語意／單位測試，累計 45 項；另確認兩種 H_SV_MAN 原文各自保留飛行須設 0／勿手動設定的警告，沒有合併跨版本限制。

2026-10-02 備援模式選項核對：RTL or Hold、RTL or Land、Loiter or Hold、AUTOLAND or RTL 明確翻成「先切入……；無法切入時改用……」，不是任選或成功後依序執行兩種模式。新增四項備援順序回歸，保留原始選項字串及數值。核對來源：[Rover 4.5.7 failsafe](https://github.com/ArduPilot/ardupilot/blob/Rover-4.5.7/Rover/failsafe.cpp)、[Rover fence](https://github.com/ArduPilot/ardupilot/blob/master/Rover/fence.cpp)、[Copter fence](https://github.com/ArduPilot/ardupilot/blob/master/ArduCopter/fence.cpp)、[Plane events](https://github.com/ArduPilot/ardupilot/blob/master/ArduPlane/events.cpp)。此翻譯不保證所有韌體或機型均提供上述模式，選項仍依目前 metadata。

以下為歷史批次紀錄，其中待核對／未翻譯計數不代表目前狀態；目前狀態以上方摘要與最新全表稽核為準。

2026-10-01 全表集中續校：新增 281 條完整原文對照，reviewed JSON 累計 1,482 條。跨 SIM／OSD／網路／GPIO／MAVLink streams／輪速與姿態濾波／Blimp／Soaring／精準降落等群組，並補 Plane FS_LONG_ACTN／FS_SHORT_ACTN 與 FENCE_AUTOENABLE 的版本差異。依公開 pdef 原文保留警告、數值、參數引用；參考 [Plane Failsafe](https://ardupilot.org/plane/docs/apms-failsafe-function.html)。舊說明中 FS_LONG_ACTION 拼法及 LAND_DS_SLOPE_B 共用的 a 係數原文均保留，未僅依 ID 猜測改寫。LOIT_MAX_POS 的原文位置／m/s 單位歧義、FINS high offset 等仍待查證；技術短語不以硬翻方式消除英文。此紀錄不是全表完成聲明。

候選 `bin/Translation119FullAudit/net461`，沒有連接或修改飛控，沒有發布。全表翻譯與全表語意審核尚有剩餘項目，不能以自動測試通過代替人工語意查核。

2026-10-01 全表跨群組續批：新增 59 條完整原文對照，reviewed JSON 累計 1,201 條。涵蓋 SIM、LOIT、輪速／Sail Heel 濾波、VISO、PLND、Soaring、Tiltrotor、網路／影像 IP、ADSB、日誌、絞盤及機動功能。原文寫 sensor 1 的 SIM_ARSPD2_FAIL 仍依原文保留 1，不僅依 ID 擅自改寫；FINS_TURBO_MODE 的 high offset 語意尚待核對，不套用猜測譯文。此批來源為公開 pdef 全表，不使用飛控參數值。

候選 `bin/Translation119FullNext/net461`：編譯 0 errors、1,389 warnings，TranslationSemantics 與 4.5／4.6／4.7 確切版本 metadata 測試通過。仍未完成全表語意審核，未發布或寫入飛控。

2026-10-01 五組參數第二批：BARO／BRD／CAM／CAN／COMPASS 新增 58 條完整原文对照，reviewed JSON 累計 1,142 條。涵蓋 compass 裝置 ID 禁止手動修改警告、offset 軸向、氣壓計來源與補償、相機 PWM／間隔、CAN bitmask 與板載控制。相機舊版 CAM_MIN_INTERVAL 的 ms 與新版 CAMx_INTRVAL_MIN 的秒分開保留。参考 [官方相機觸發說明](https://ardupilot.org/copter/docs/common-camera-shutter-with-servo.html)，不把觸發維持時間當作相機內部曝光設定。舊版「node should be set implicitly」語意不夠明確，仍保留英文待核對，不由新版定義推定舊版操作。

候選 `bin/Translation119BoardSensors2/net461`：編譯 0 errors、1,389 warnings；TranslationSemantics 與 VersionedParameterMetadata 通過。尚未完成五組全部參數或全表審核，未修改飛控、未推送／發布。

2026-10-01 BARO／BRD／CAM／CAN／COMPASS 第一批：新增 43 條原文對照（reviewed JSON 1,084 條），包含氣壓、加熱器、相機視角、CAN 串列及馬達磁場補償；不是五組全部完成。另修正完整參數頁的 4.5.x／4.6.x／4.7.x 確切版本查找，避免 WPNAV／PSC 舊名稱因通用 metadata 更新而空白。候選 `bin/Metadata119Versioned/net461` 編譯 0 errors、1,389 warnings；版本回歸對 Copter 4.5.7、4.6.3、4.7.1 分別比對 46／46／44 條導航控制參數說明與單位，並確認缺失版本不跨版回退。尚未驗證所有 patch／機型的實機連線；需要對應本機 pdef，沒有內建所有版本資料。未發布。

2026-10-01 語意回校：校正既有 14 條譯文，未增加 catalog 條數。PRX 忽略區域 1–4 區分中心方向與總角度寬度；FLOW_HGT_OVR 補明高度覆寫用途；FLOW_OPTIONS 補明 Roll／Pitch 穩定及 bit 0 設為 1；FENCE_OPTIONS 補明勾選條件；GRIP_AUTOCLOSE／REGRAB 的 0 值只停用對應自動行為。核對 [AP_Proximity_Backend](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Proximity/AP_Proximity_Backend.cpp)、[AP_OpticalFlow](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_OpticalFlow/AP_OpticalFlow.cpp) 及 [AP_Gripper](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Gripper/AP_Gripper.cpp)。FLOW 的原始碼 Description 與 Bitmask 都明定 bit 0，支持 pdef；先前 Wiki 位元差異已完成此範圍的核對，不更動飛控選項。新增 Test-TranslationSemantics.ps1 防止上述 14 條及 5 條方向／停用範圍說明退步。

候選 `bin/Translation119Corrections/net461`：首次平行建置遇到 GMap IGraphics 泛型錯誤；改用 `-m:1` 重試成功（0 errors、1,283 warnings），未改動繪圖程式碼。未發布、未寫入飛控；全表審核仍未完成。

2026-10-01 周邊第二批：新增 34 條完整原文對照，reviewed JSON 累計 1,041 條。涵蓋 rangefinder 濾波與公分單位下限、CHUTE PWM、GRIP／EPM 時序、OA／AVOID 退避與加速度限制、FLOW 機體座標正方向及 TERRAIN 快取。依完整原文保留機型不適用與設為 0 的不同含義，不改動功能或參數值。參考 [Simple Object Avoidance](https://ardupilot.org/copter/docs/common-simple-object-avoidance.html) 與 [Optical Flow Setup](https://ardupilot.org/copter/docs/common-optical-flow-sensor-setup.html)。注意後者 Wiki 寫 bit 1，但此批 FLOW_OPTIONS 的 pdef 原文寫 bit 0；翻譯保留原文 bit 0，未以 Wiki 改寫 metadata 或選項，版本適用性差異待進一步確認。候選目錄 `bin/Translation119Optional2/net461`，未推送或發布。

2026-10-01 未啟用功能／周邊批：新增 64 條完整原文對照，reviewed JSON 累計 1,007 條。包含 RNGFND 多實例、PRX 忽略區域、OA、CHUTE、GRIP、ARSPD、FENCE、TERRAIN、電池 SOC 與 Q_ENABLE。對照範圍不依連線飛控的 enable 值或目前 UI 可見性篩選，並非宣稱這些參數在每個韌體都會隱藏或受到支援。保留原文的停用條件、機型不適用說明、bit 定義與單位版本（公尺／公分），不改動功能啟用狀態或參數值。

本批參考 [官方 RangeFinders Setup](https://ardupilot.org/copter/docs/common-rangefinder-setup.html) 及 [官方 Parachute](https://ardupilot.org/copter/docs/common-parachute.html)，實際逐條依公開 pdef 完整原文匹配。全表 audit 新增 VisibilityIndependent 與範圍註記；僅證明 metadata 的離線涵蓋，不證明實機動態顯示。候選目錄：`bin/Translation119Optional/net461`。未推送或發布。

最新驗證（周邊第二批）：編譯 0 errors、1,389 warnings；全表來源／版本匹配及 AutoTune 專有名詞測試通過。掃描 58 份 metadata，3,335 種選項原文與 bit 索引保留。尚有 2,795 條不同說明保留英文（含部分應保留的技術短語），全表語意審核尚未完成。詳細清單見 `bin/Translation119Optional2/net461/parameter-translation-audit.json`。

2026-10-01 起降／安全第二批：新增 41 條完整原文對照，reviewed JSON 累計 943 條。涵蓋 Copter LAND_REPOSITION、Plane RTL_RADIUS／pre-flare／GCS failsafe、QuadPlane QRTL 高度與轉換，以及 Heli collective／RSC 模式／governor／autorotation／油門曲線，另補 Sub failsafe 動作說明。參數名稱、版本差異、原文警告及數值條件保留；例如新版 GCS failsafe 的首次主要 GCS heartbeat 條件，不套入未載明該條件的舊版描述。

以公開 pdef 的完整原文為匹配鍵，並參考 [官方 Internal RSC Governor](https://ardupilot.org/copter/docs/traditional-helicopter-internal-rsc-governor.html) 核對 governor、droop 與 Feedforward 概念。不同版本的建議不可互換。LAND_DS_ARSP_MIN 說明語意仍待進一步確認，暫保留英文；P／I／D gain 等技術用語不強制翻譯。本批候選輸出為 `bin/Translation119FlightSafety2/net461`，未推送、發布或寫入飛控。

2026-10-01 起降／安全優先批：新增 38 條完整原文對照（共 902 條），並補 14 條一般安全選項中文解釋，保留完整英文選項與原始 bit／數值。涵蓋 Copter／Heli 降落減速高度與搖桿起飛高度、Plane THR_FAILSAFE／RTL_AUTOLAND／flare／油門中止降落與發射觸發、Heli RSC runup／autorotation／collective、QuadPlane 起飛 RPM 與空速限制。同名參數的不同版本文字分開匹配。`FS_OPTIONS=0` 是停用額外選項，不是停用所有 failsafe；起飛加速度說明中的 arming 是啟用地速檢查，不是解鎖飛控。

本批查詢來源：[Copter LAND](https://ardupilot.org/copter/docs/land-mode.html)、[Plane TAKEOFF](https://ardupilot.org/plane/docs/takeoff-mode.html)、[Heli RSC](https://ardupilot.org/copter/docs/traditional-helicopter-rsc-setup.html)、[Radio failsafe](https://ardupilot.org/copter/docs/radio-failsafe.html)、[GCS failsafe](https://ardupilot.org/copter/docs/gcs-failsafe.html)。實際譯文依本機公開 pdef 原文完整對照；舊版 RSC_RAMP_TIME 等引用照原文保留，不擅自改成新版名稱。此批未涵蓋上述機型全部常用參數，仍持續以起降及安全群組優先。

2026-10-01 全表持續核對：本輪在上一批 379 條基礎上新增 485 條完整原文對照，累計 864 條。涵蓋 GPS／moving base、SERIAL／flow control、RC override、OSD、CAN、EFI、SITL、IMU bias、Mount 及周邊設備說明。技術名詞保留；同一完整原文可供多個參數與版本共用，不代表 864 個參數全部完成。來源疑似筆誤不由 ID 猜測改写，例如 SERIAL9 的舊說明使用 Serial8；GPS COM_PORT2 的舊更名資訊待核對。

驗證版本：`bin/Translation119FullReview/net461`，1.1.9.0；編譯 0 errors、1,389 warnings。AllParameterTranslation、ReviewedParameterTranslation、ParameterLocalization 通過；58 份公開 metadata、3,335 種選項原文與 bit 索引保留。仍有 2,972 條不同原文保留英文，不能宣稱全部完成。最新 `parameter-translation-audit.json` 的 PendingItems 提供每條未完成原文、ParameterIds、SourceFiles 與待核對狀態；不含機體的參數值。測試另檢查 reviewed JSON 重複鍵，防止同一原文遭無聲覆蓋。

後續全表範圍包含上述 PendingItems、舊人工字典複核，以及尚未加註中文的一般選項；專有名詞選項保留原文是預期行為。新增來源依 [串列埠官方說明](https://ardupilot.org/copter/docs/common-serial-options.html) 與 [GPS for Yaw 官方說明](https://ardupilot.org/copter/docs/common-gps-for-yaw.html) 核對相關概念，最終套用仍以各版本完整 documentation 為鍵。本輪未推送或發布。

2026-10-01 EKF3 第二批：新增 25 條完整原文對照（reviewed JSON 共 379 條），涵蓋 ENABLE、GPS 速度／位置、GLITCH_RAD、高度、磁力計雜訊／gate、空速、測距、optical flow、gyro／accelerometer／wind process noise。依本機公開 ArduCopter.apm.pdef.xml 的完整 documentation 匹配，並參考 [ArduPilot EKF 說明](https://ardupilot.org/copter/docs/common-apm-navigation-extended-kalman-filter-overview.html)。保留 innovation、RMS、bias 等技術詞；區分量測權重、拒絕與限幅。這是部分參數的新增對照，並非整個 EKF3 群組或全表完成；MAG_CAL 等較長且跨版本不同的定義仍待核對。

2026-09-30：停用 `Parameters.zh-TW.draft.json` 的執行期顯示。未核對內容保留官方英文，不再顯示機譯稿。既有人工字典仍須持續審核，不宣稱全表已完成語意核對。原文保留在滑鼠提示；翻譯不可取代飛控版本對應的官方技術文件。

專有名詞、軸向及縮寫（Roll、Pitch、Yaw、YawD、AutoTune、PID、VFF、Rate P、Rate D、Angle P、EKF、GPS 等）保留英文；參數 ID、單位、數值、bit 索引不可變更。未知選項不可從名稱猜測功能。

已核對截圖的 AutoTune 軸向、SEQ、FRQ_MIN/MAX、GN_MAX、VELXY_P、ACC_MAX、RAT_MAX；新增 `Parameters.zh-TW.reviewed.json` 共用字典，包含 RC／SERVO、電池、AHRS、Acro、RTL、failsafe、fence、馬達、PID、Harmonic Notch、TECS／空速、Rover／Sailboat、MAVLink streams、relay 及溫度監測的已核對原文。這是上述群組中的已列入條目，不代表整個群組都已審核完成。依完整原文匹配，避免同名參數在 Heli／Multi 或不同韌體的說明被錯誤套用。FRQ 說明不擅自把 rad/s 改成 Hz；範圍與選項仍由韌體中繼資料提供。YawD 是否存在也由該版本的 bitmask 提供，不自行增加到 Heli。

已移除只依參數 ID 翻譯的字典及未使用的 ID 拆詞字典。執行檔只內嵌 reviewed JSON，不再包含 draft JSON。選項的完整官方原文一律保留；一般選項可加中文解釋，飛行模式、軸向及縮寫保留原名。描述與選項使用不同查找路徑，避免同名短句被誤套用。

全表驗證：`FMT/Test-AllParameterTranslation.ps1` 掃描本機公開 `*.apm.pdef.xml`，核對新增字典原文出處、參數引用、所有選項原文與 bit 索引，並驗證同 ID 的未知新版說明不被舊翻譯覆蓋。輸出 `parameter-translation-audit.json` 至指定測試目錄，記錄每份來源 SHA-256、已核對／舊字典／英文回退數量與未翻譯原文清單。`CompleteSemanticReview` 仍為 false；自動測試通過不代表全部語意已核對。

核對依據：[Heli AutoTune 原始定義](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AC_AutoTune/AC_AutoTune_Heli.cpp)、[Multi AutoTune 原始定義](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AC_AutoTune/AC_AutoTune_Multi.cpp)、[官方 Heli AutoTune 說明](https://ardupilot.org/copter/docs/traditional-helicopter-autotune.html)。尚未完成所有參數群的逐項審核。以下機譯流程只保留作歷史資料，不是目前顯示策略。

覆蓋率與正確性是兩件事：零遺漏表示每筆來源有對照，不表示機器譯文已人工核對。`Parameters.review.json` 記錄機譯可能未保留的參數引用；警告、數字、否定語句與限制條件仍需人工覆核。參數 ID、實際值、選項鍵值、位元索引及單位不改動。沒有對照的新韌體文字保留英文，絕不由參數名稱猜測說明。

來源範圍及 SHA-256 記錄在 `Parameters.zh-TW.draft.manifest.json`：本機公開 ArduPilot 參數定義快取及專案內建備援 XML。不含飛行紀錄、使用者參數值或密碼。

離線生成使用 [Argos Translate 英中模型 1.9](https://github.com/argosopentech/argospm-index)、CTranslate2 及 OpenCC s2twp；模型與 Python 僅放在忽略的 `bin/TranslationRuntime`，不隨軟體發佈，也不需要在使用者電腦執行。應用程式只載入內嵌 JSON，不連接翻譯服務。

模型出處：Jörg Tiedemann、Santhosh Thottingal，*OPUS-MT — Building open translation services for the World*，EAMT 2020。Argos 套件說明列原始 OPUS 模型為 CC-BY 4.0；模型來源與套件資訊見上述官方索引。譯稿經 OpenCC 繁體轉換、參數引用保留修復；不代表原作者核可本軟體或翻譯內容。

重建：使用 `FMT/Build-ParameterTranslations.py`，指定模型、XML 來源與輸出 JSON；`FMT/Test-ParameterCatalog.py` 檢查完整來源覆蓋，`FMT/Test-ParameterLocalization.ps1` 檢查程式顯示、人工譯文優先及值保留。
