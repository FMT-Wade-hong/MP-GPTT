# FMTPlanner V1.2.4 本機驗證紀錄

日期：2026-10-27。狀態：本機候選版，未發布、未推送、未建立 GitHub 標籤或 Release。程式版本及組件版本為 1.2.4／1.2.4.0。

## 飛行模式列自適應縮放（2026-10-28）

最新輸出 `bin/ModeFit124/net461` 保留先前 ICE／HUD 修正，Release 建置 0 errors、1,272 warnings。本機 V1.2.4 ZIP 隨之更新，未發布。

- 移除模式區的 AutoScroll、捲動範圍與 85% 最小縮放限制。各分組維持單列，按鈕平均使用可用寬度，按實際高度保留下面操作頁空間；按鈕、模式文字與標題一同放大／縮小。
- 依實際文字尺寸調整字型，避免 STABILIZE 等長模式名稱被截斷或換行；重複尺寸及等值字型重用，避免控制項仍持有已釋放字型。未更改模式定義、連線保護或發送模式命令的流程。
- `Test-ModeScaleSafety`：六種機型、寬度 280／420／620／900／1200 再縮回 280、高度 180／300／600，共 108 組配置；所有按鈕在可見範圍內、沒有捲軸／重疊／換行、模式文字完整，斷線按鈕保持停用。實際 GDI 字型測量與窄／中／寬畫面渲染檢查通過。
- `Test-ScreenSizeMatrix` 的 12 組邏輯工作區模擬均無模式捲軸或狀態列截字；`Test-ReleaseVersion` 通過。這是邏輯尺寸與離線 WinForms 驗證，未變更作業系統 DPI 或連接實機。

## 不定時閃退與 ICE 參數檢查（2026-10-28）

最新本機輸出為 `bin/CrashGuard124/net461`，最終 Release 建置 0 errors、1,272 warnings。未發布。

- 證據：最新應用程式日誌的結尾為正常關閉，未取得可直接對應這次不定時退出的堆疊。Windows 舊 WER 檔案存在，但作業系統 ACL 不允許讀取內容；未更動權限。檢查到網路追蹤約 1.49 GB，以及大量已處理的 first-chance 例外，不能把這些等同致命崩潰。
- 已離線重現 HUD 舊 Bitmap 在縮放及 Dispose 後仍可存取；修補後相同物件確實失效，Graphics、字形 Bitmap／Path 及實例繪圖物件同步釋放。快照不再與釋放競爭，借用的背景圖片不會由 HUD 釋放；JPEG 完成編碼才發布，仍保留讀取端持有的舊記憶體串流。
- ICE／起飛寫入與當頁／全部參數更新共用互斥保護；確認後釘住連線、串流、飛控與 system/component ID。失敗不宣稱寫入成功；離頁、隱藏、取消安全確認、斷線或安全狀態變更，會中止後續讀回與尚未送出的重試。已送出的寫入不能撤回，介面提示重新讀取。參數型別缺失等封包準備例外不再留下 `giveComport=true`。
- 保留既有 `setParamAsync` 方法簽章供外掛相容；新增帶檢查委派的入口，既有其他呼叫端行為不變。ICE 原有讀回值比對、安全確認及啟動器參數鎖定保留，未新增任何引擎啟動命令。
- 預設關閉不受限的 System.Net 追蹤，first-chance 僅在明確設定 `FMT_TRACE_FIRST_CHANCE=1` 時記錄並防止記錄器遞迴。一般應用程式日誌改以大小輪替（每檔 1,000 KB、30 份備份），保留既有歷史檔案。新增三份各不超過 256 KiB 的本機 `Diagnostics/crash.log`，記錄啟動、正常關閉、UI／背景致命例外與 ICE 寫入錯誤；致命背景例外不再於即將結束的執行緒開啟模態視窗，不自動上傳。
- GitHub 查閱：上游 [#2223](https://github.com/ArduPilot/MissionPlanner/issues/2223)（2019）與 [#2354](https://github.com/ArduPilot/MissionPlanner/issues/2354)（2020）回報寫入失敗卻呈現成功；[#3264](https://github.com/ArduPilot/MissionPlanner/issues/3264)（2024）涉及 double／decimal 例外；[#3696](https://github.com/ArduPilot/MissionPlanner/issues/3696)（2026）涉及高位 bitmask。這些是不同缺陷的參考，不能直接歸因為本機 ICE 閃退。目前搜尋未找到 MP-GPTT 或上游中可直接對應此 ICE 頁症狀的回報；未建立或留言任何 Issue。
- 測試：`Test-CrashGuard124` 使用實際組件、記憶體 MAVLink 連線及 WinForms，驗證 1,000 次重複寫入拒絕、共用讀寫互斥、ACK 等待、拒絕／逾時、切換目標、取消後不再重試、封包準備失敗復原、既有 API 簽章及診斷輪替；另執行 1,000 次 HUD 縮放／繪圖／JPEG、25 次資源釋放及 30 次 ICE 建立／切頁／關閉。最終組件追加 100 次實際 OpenGL 縮放／擷取與 context 清理均通過，沒有退回 GDI。
- 12 組回歸通過：IceSetup、TakeoffSetup、PageParameterRefresh、HudWarning、HudDefault、HudCompact、SafetyReadback、ParameterCompatibility124、MotorTestGuard、DualGpsToolbar、TranslationAudit124、ReleaseVersion。未連接或寫入實體飛控，亦未啟動引擎／馬達。仍需使用者實際連線及長時間運行核對，不宣稱已完全排除所有閃退。

## 參數涵蓋與來源

以官方 ArduPilot 發布標籤解析 31 組文件化參數快照：Copter、Plane、Rover、Tracker 各含 4.6.0–4.6.3、4.7.0–4.7.2；Sub 含 4.7.0–4.7.2。每個版本執行該 commit 自己的解析器，31 次均正常結束。封裝器只加入來源路徑註記，不變更參數文件內容；各檔 SHA-256 與官方 commit 記錄於原始碼的 `ExtLibs/Utilities/ParameterMetadata/manifest.json`。

- 累計 144,653 組「機型版本＋參數名稱」定義；此數字包含不同版本重複出現的名稱。
- 逐欄比對 1,497,329 個中繼資料欄位，包括缺少欄位、選項與位元遮罩；另檢查 343 組直升機定義。
- 30 組完整版本差異，包含非預設的硬體相依同名定義；清點設定頁 1,001 處參數名稱引用。
- [完整差異與來源報告](ParameterAudit/ArduPilot-4.6-4.7-audit.html)；[機器可讀報告](ParameterAudit/ArduPilot-4.6-4.7-audit.json)。

完整參數頁顯示飛控實際回傳的全部名稱；專用設定頁提供該機型的常用操作，不為每一項參數新增專用控制項。未回傳的參數不會從文件清單假造出來，也不自動遷移或寫入既有飛控值。

## 本次修正

- 內嵌完整版本資料，依目前機型、韌體與直升機類別選擇；缺少欄位不混用另一版本，背景更新不覆蓋其他機型資料。
- 補齊 4.7 的位置 PID、IMAX、起飛、直升機爬升／下降／返航高度名稱與單位；OSD 使用正確的 SRx／MAVx 串流實例對應。
- 一般參數、ADS-B 及簡易 PID 數值控制項保留浮點精度，修正步進為整數時誤取整及滑桿同步截斷輸入；切換目標時重建控制項並清除舊編輯。
- 保留既有 ADS-B 與其他本機修改。測試用程式、私人設定及原始飛行紀錄不放入套件。

## 驗證

`dotnet build MissionPlanner.csproj -c Release -p:NuGetAudit=false -p:OutputPath=bin/Local124/net461/ -v:minimal`：0 errors、1,334 warnings；警告包含既有專案／相依套件警告，非零警告建置。

`FMT/Test-Local124.ps1 -Directory bin/Local124/net461`：50 項離線回歸腳本全部通過，包含既有 43 項回歸及 7 項參數測試。

完整參數表使用實際 WinForms 控制項離線載入以下資料，檢查名稱、單位、原始值及無待寫入項目，共 30,579 列：

| 資料 | 顯示列數 |
|---|---:|
| Copter 4.6.3 | 4,776 |
| Copter 4.7.2 | 5,688 |
| Plane 4.7.2 | 5,722 |
| Rover 4.7.2 | 5,442 |
| Sub 4.7.2 | 5,369 |
| Tracker 4.7.2 | 3,582 |

另涵蓋版本切換、直升機／多旋翼切換、27 組 4.7 PID／起飛案例、浮點與整數輸入、不同小數點文化、48 個新舊 OSD 參數名稱，以及斷線／唯讀保護。所有輸入測試停留在離線控制項與待寫入值，未連線實際飛控。

## 適用範圍

官方文件包含可選硬體、機架與腳本功能，不代表每一塊飛控都具有所有參數。Sub 未找到 4.6 發布標籤；Blimp／AP_Periph 未找到對應標籤，未冒用其他版本。姿態控制／自動調參已選擇直升機或多旋翼定義；其餘硬體相依同名定義仍保留上游順序，報告完整列出替代定義，未宣稱全部硬體變體均能自動辨識。

本次驗證證明文件快照的涵蓋、選擇與受測頁面的離線行為，並不等於所有硬體、客製韌體、參數實際寫入或飛行驗證。

## 同版本追加：雙 GPS 工具列（2026-10-27）

右上角衛星資訊改由 `FmtGpsStatusControl` 顯示：GPS1 綠色、GPS2 黃色，衛星數、定位狀態、H／V 各自獨立。只有實際回報 GPS2 狀態（含未定位）才展開第二組，沒有第二組或斷線時收合。補上 `GPS2_RAW.epv` 到 `gpsvdop2` 的解析；未知衛星數或 DOP 顯示 `--`。

追加建置位於 `bin/GpsDual124/net461`，0 errors。`FMT/Test-DualGpsToolbar.ps1` 以實際 WinForms 控制項和 MAVLink GPS2_RAW 封包驗證單組／雙組／未定位／斷線、顏色、兩組數值隔離、未知值和中英文文字寬度，並產生離線畫面預覽。未使用實體 GPS 或連線寫入飛控。

## 警示色回歸修正（2026-10-28）

修復雙 GPS 固定配色覆蓋原有定位警示：各接收器無 GPS／未定位時使用原本的 OrangeRed 紅色，2D 定位為 Gold 黃色；3D 及以上才使用 GPS1 綠色、GPS2 黃色，斷線仍為灰色。沿用原本依定位狀態變色的規則，未增加固定衛星顆數門檻。

最新測試版位於 `bin/GpsWarning124/net461`，Release 建置 0 errors（1,379 warnings）。`Test-DualGpsToolbar`、`Test-HudWarning`、`Test-ReleaseVersion` 全部通過；涵蓋兩組獨立警示、正常配色恢復、無 GPS／未定位／2D／3D／RTK、單組收合、斷線、H／V 隔離，以及既有 HUD 的 10 種警示來源。未連線或向飛控發送命令。完整本機 ZIP 已改以此輸出封裝，仍為未發布的 V1.2.4。

## 馬達測試防連點（2026-10-28）

最新輸出改為 `bin/MotorGuard124/net461`，Release 0 errors（1,272 warnings），包含前述 GPS 警示修正。馬達測試使用背景非同步傳輸，單顆、全部及依序測試共用一個作業保護，取得保護後才顯示既有批次確認。忙碌時停用測試／油門／秒數／旋轉值設定，重複啟動直接拒絕，沒有啟動佇列；完成、拒絕或逾時後才解除保護，不使用外層逾時提前放行仍在執行的傳輸。已核對官方 ArduCopter 4.7.2 的 `motor_test.cpp` 與 `GCS_MAVLink_Copter.cpp`：測試會設定馬達 armed 並回報心跳，因此保留飛控原有安全檢查，不以測試期間的 armed 回報誤擋後續馬達命令。

「全部停止」保留可用並合併成一次請求；停止時取消尚未送出的啟動及 ACK 等候中的啟動重試。停止封包會等待目前讀取結束，避免同一 DO_MOTOR_TEST 的 ACK 被兩個讀取者搶走。保留原有百分比油門、時間、馬達次序及停止參數；失敗仍嘗試其餘馬達的停止並回報未能確認全部停止。綁定連線、MAV 物件、系統／組件 ID 與串流，防止切換目標後向新目標送出剩餘命令；離頁只取消後續啟動，不假稱已停止實體馬達。

`Test-MotorTestGuard.ps1` 測試實際編譯組件：1,000 次啟動重入均拒絕、1,000 次停止合併為一批，驗證 ACK 串行、停止優先、拒絕／逾時恢復、舊作業不解除新作業、離頁取消、目標變更與停止失敗後續處理。兩個實際設定頁共用保護，200 次強制點擊呼叫不排隊；停止仍可操作，重新進頁不能繞過，完成後按鈕恢復。所有命令以模擬委派驗證，未開啟實體通訊、未啟動馬達；UI 重試取消與通訊狀態清理另做來源結構核對。

## 繁體中文稽核（2026-10-28）

最新輸出為 `bin/TranslationAudit124/net461`，Release 建置 0 errors、1,272 warnings。版本仍為 V1.2.4，本機 ZIP 隨之更新，未發布。

- 靜態掃描 144 份中文資源、4,308 個字串，並檢查 179 份中性資源的繁體回退。格式參數、重複鍵、替代字元與參數名稱引用檢查通過；補齊 185 項共用繁體訊息，zh-TW／zh-Hant／zh-HK 均實際載入成功。
- 核對 58 份本機公共快取及 31 份內嵌版本資料，共 89 份、3,951 種不同參數說明、3,384 個選項名稱。新增 18 段原文完全相符的譯文後，此次掃描的說明英文回退為 0；目錄共 3,854 筆，另有既有精確對照詞典。1 段歷史來源已不在目前快照，另行列出，不冒稱已核對其目前來源。
- 修正圍籬越界、預估抵達時間、空速變化率、分.秒格式、電池節數、欄位曲線、校準提示與預期／實際值的誤譯。安全選項僅用完整對照；未知選項保留全文，不再局部取代。
- 失效保護頁移除固定 1／20 秒的錯誤暗示，標明 0 不等於全面停用、長時逾時依 FS_LONG_TIMEOUT；已依官方 Plane 4.7.2 Parameters.cpp 核對。修正該頁 2D 被寫成 3D、RTK 顯示空白及上鎖／解鎖用詞，使用與工具列一致的 GPS 定位文字。
- `Test-TranslationAudit124`、`Test-AllParameterTranslation`、`Test-TranslationSemantics`（156 組）、`Test-ReviewedParameterTranslation`、`Test-ParameterLocalization`、`Test-TraditionalMapLabels`、`Test-SafetyReadback`、`Test-ReleaseVersion`、`Test-DualGpsToolbar`、`Test-MotorTestGuard` 通過。最終顯示修正後重跑新增翻譯測試、版本及雙 GPS；離線渲染固定翼、目前直升機與失效保護頁，未連線或寫入實體飛控。

[完整翻譯稽核與修正明細](TranslationAudit/translation-audit-v1.2.4.html) 列出 1,286 個靜態英文回退候選，以及 1,813 個保留原文的參數選項。它們包含技術名稱、隱藏／舊版控制項與執行時才套用的翻譯，不等同於已確認漏翻；仍需逐頁判斷。此次不宣稱所有介面逐句語意審核已完成，其他語系與獨立 FLOG／MQTT 工具不在範圍。
