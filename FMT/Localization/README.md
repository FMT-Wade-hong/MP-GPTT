# 全部參數表繁體中文對照

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
