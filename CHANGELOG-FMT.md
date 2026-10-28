# FMTPlanner release history

## FeiMaoTecPlanner V1.2.4 — 本機候選版，未發布（2026-10-27）

- 2026-10-28：飛行模式快捷列改為依可用寬高縮放按鈕、字型與分組標題，移除 85% 縮小下限與水平／垂直捲軸。各分組維持單列，按鈕平均使用整列寬度，較長模式名稱依實際文字尺寸縮放；模式切換、連線限制及目前模式標示不變。六種機型、108 組尺寸與 12 組邏輯螢幕情境檢查通過，實際渲染確認無截字／重疊；仍為本機候選版。
- 2026-10-28：修正 HUD 縮放／關閉時未釋放的 Bitmap、Graphics、字形快取與每幀筆刷；圖像擷取與清理使用同一把鎖，JPEG 編碼完成後才交給讀取端。ICE／起飛參數寫入與當頁更新共用作業保護，保留原飛控、連線及安全狀態，離頁／取消確認／斷線後停止後續讀回與寫入重試；修正參數封包準備失敗造成連線一直忙碌。預設停用大量網路／first-chance 追蹤，新增容量受限的本機錯誤紀錄。這些是已驗證的缺陷修正與防護，尚未確認使用者不定時閃退的唯一原因；詳見本機驗證紀錄。
- 2026-10-28：檢查繁體資源與參數翻譯，補齊 185 項共用繁體訊息及 18 段原文相符的參數說明。修正圍籬越界、預估抵達時間、空速變化率、電池節數、校準提示與失效保護文字；安全選項不再使用片段替換。失效保護頁修正 2D 誤標 3D、RTK 空白及上鎖／解鎖文字。89 份參數資料的 3,951 種說明在此次掃描中均有中文譯文，選項原文、數值及位元索引不變；保留技術名稱與未審核選項，不宣稱所有介面逐句語意已完成。詳見 `FMT/TranslationAudit/translation-audit-v1.2.4.html`。
- 2026-10-28：馬達測試改為非同步命令，單顆／全部／依序測試共用跨頁實例的忙碌保護。等待回覆時停用測試與設定按鈕，重複點擊直接忽略、不累積佇列；保留全部停止入口，合併重複停止請求，取消尚未送出的啟動與後續重試，待目前讀取結束後接續停止命令。失敗／逾時釋放忙碌與通訊狀態，離頁、斷線或更換目標時不繼續啟動。1,000 次重複啟動、1,000 次停止及 200 次實際頁面強制呼叫等離線驗證通過，未啟動實體馬達。
- 2026-10-28：修復雙 GPS 識別色覆蓋定位警示的問題。兩組各自以無 GPS／未定位顯示紅色、2D 定位顯示黃色；3D 及以上才恢復第一組綠色、第二組黃色。沿用原本定位狀態判斷，不新增衛星顆數門檻；斷線維持灰色。
- 右上角衛星資訊支援雙 GPS：第一組綠色、第二組黃色，各自顯示衛星數、定位狀態、HDOP／VDOP。沒有第二組時自動收合；第二組尚未定位仍顯示，斷線清除舊數值。補上 GPS2_RAW 的 VDOP 解析，未知值顯示 `--`。離線控制項渲染與實際封包解析檢查通過。
- 統一程式、組件、標題及套件版本為 V1.2.4；未建立 GitHub 標籤、Release 或推送。
- 由官方實際存在的 31 組 4.6.x／4.7.x 發布標籤，以各版本自己的解析器產生並內嵌完整參數文件。涵蓋 Copter、Plane、Rover、Tracker 的 4.6.0–4.6.3、4.7.0–4.7.2，以及 Sub 的 4.7.0–4.7.2。核對 144,653 組版本內不重複參數定義與 30 組完整版本差異；所有重複定義保留在核對報告。
- 共用參數資料庫依目前機型／版本選擇，不以其他版本補入缺少欄位，也不讓背景更新覆蓋其他機型資料。按 MAVLink 機型區分多旋翼／直升機的姿態控制與自動調參定義。
- 一般參數、ADS-B 與簡易 PID 控制項依實際數值型別保留浮點精度；修正整數步進造成取整，以及滑桿同步造成精細輸入被截斷。切換飛控／版本／機型會清除舊控制項和未送出的編輯值。
- 直升機頁補上 4.7 的爬升／下降速度及返航高度別名與倍率；OSD 串流設定對應 `SRx_*`／`MAVx_*`，只操作飛控實際回傳的既有埠與參數，斷線／唯讀／日誌播放時不寫入。延續下方 PID 與起飛頁修正。
- 官方文件涵蓋不同硬體的可選功能，不代表任何一塊飛控都應回傳全部參數。Sub 未找到 4.6 發布標籤，Blimp／AP_Periph 未找到對應標籤；其餘硬體相依同名定義保留上游順序，未宣稱全部硬體變體可自動辨識。詳細來源與限制見 `FMT/ParameterAudit` 及 `FMT/RELEASE-AUDIT-V1.2.4.md`。

## 未發布參數相容性修正 — 2026-10-27

- 修復 ArduPilot 4.7 系列（含 4.7.2）擴充調整頁：水平位置 P 支援 `PSC_NE_POS_P`／`Q_P_NE_POS_P`；新版 `NE_VEL_IMAX` 與 `D_ACC_IMAX` 使用飛控原始值，移除舊版額外除以 10 的顯示／寫入倍率。保留小數輸入，避免步進值為 1 時把 8.5 取整為 9；舊韌體倍率與 LAT／LON 聯動保留。
- 起飛設定頁依實際回傳名稱支援 Copter／Heli 的 `PILOT_TKO_ALT_M`、`PILOT_SPD_UP`、`PILOT_ACC_Z`，以及 QuadPlane 的 `Q_PILOT_SPD_UP`、`Q_PILOT_ACCEL_Z`、`Q_WP_SPD_UP`。同時切換單位、範圍、讀取及寫入名稱；不轉換或覆寫既有飛控值。
- 更正 4.7 提示：垂直加速度 IMAX 的韌體遷移係數為 0.001；姿態角加速度單位由 cdeg/s² 改為 deg/s²。依據官方 [Plane-4.7.2 AC_PosControl](https://github.com/ArduPilot/ardupilot/blob/Plane-4.7.2/libraries/AC_AttitudeControl/AC_PosControl.cpp)、[Copter 參數](https://github.com/ArduPilot/ardupilot/blob/Plane-4.7.2/ArduCopter/Parameters.cpp)、[QuadPlane 參數](https://github.com/ArduPilot/ardupilot/blob/Plane-4.7.2/ArduPlane/quadplane.cpp)與 AC_WPNav／AC_AttitudeControl 的定義及遷移程式。
- 驗證：Release 建置 0 errors；`FMT/Test-ArduPilot47Parameters.ps1` 的 27 組參數案例通過，涵蓋實際控制項輸入、待寫入值、快取不變、當頁更新名稱、起飛頁單位／範圍與斷線寫入保護。TakeoffSetup、WpNavMetric、ParameterUiFix、CopterVersionWarning、HoverControl、CopterRefreshBindings、PageParameterRefresh 共 7 個相關回歸腳本亦通過。
- 本機輸出 `bin/ArduPilot472Fix/net461/FMTPlanner.exe`，基於 V1.2.3 並保留既有 ADS-B 修改；尚未連接實際飛控驗證，未發布 GitHub。

## 未發布修正 — 2026-10-18

- ADS-B 預設來源改為 adsb.fi（`https://opendata.adsb.fi/api`），使用 v3 半徑查詢，正常每 2 秒更新。首次啟用時自動遷移原 adsb.lol 預設設定；保留自訂接收器／其他 HTTP 來源，並尊重之後手動更換的來源。設定視窗及網路航機提示標示 adsb.fi 資料來源。
- 保留民航局 2026-10-01 生效 eAIP ENR 5.2.3 台灣 ADIZ 邊界（含東南斜邊）。網路查詢固定在目前飛機／地圖中心 100 海里，取消資料少時自動擴大至 250 海里；中心在區外時暫停查詢。區外網路航機在建立更新事件前排除；地圖上的接收器／MAVLink 航機使用相同邊界。
- ADS-B HTTP 請求加入版本與專案聯絡網址，修正原先錯掛下載事件導致的缺少 User-Agent／403 問題；HTTP 錯誤或限流時保留退避重試，成功後恢復 2 秒。
- 驗證：47 項離線檢查通過，涵蓋設定遷移、v3 網址、識別區邊界、100 海里半徑、2 秒頻率、403 重試及 2,000 筆區外航機排除。新版實際連線 adsb.fi 收到 34 架航機，首批約 513 ms、下次更新間隔約 1,868 ms，來源與範圍檢查通過。Release 建置成功，輸出 `bin/AdsbFi123/net461/FMTPlanner.exe`；尚未在使用者畫面確認地圖標記與流暢度。

## FeiMaoTecPlanner V1.2.3 — 2026-10-14

- 依使用者要求，正式版撤回姿態插值、獨立快速更新通道及診斷選單，維持原本姿態資料綁定與 30 ms 繪圖節流；不主動調高飛控姿態回報率。保留透明紅色漸層警示外框。
- 納入 CAA 全台離線底稿／手動更新、500 公里顯示半徑、HOME 圖示與飛行中重讀保留、連線取消改善、介面調整及其他下列修正。
- 下方為開發歷程；其中「未發布」及平滑實驗描述是當時紀錄，不代表正式版功能。正式版不含姿態平滑實驗。

### V1.2.3 開發歷程（歷史紀錄）

- 2026-10-14：姿態插值改依變動資料間隔調整（50～150 ms），修正短插值後等待資料的停頓；更新計時器改 15 ms、動畫繪圖限速改 8 ms，降低計時器取整與雙重節流影響。HUD 右鍵新增「姿態更新診斷」顯示實際 ATTITUDE 接收 Hz／近一秒繪圖次數，未改飛控回報率、未發布。
- 2026-10-14：隱藏多旋翼圖示周圍的黃／紅防撞距離圈，保留藍色選取圈與方向線；不改 AVD 參數或飛控防撞功能。未發布。
- 2026-10-14：修正姿態平滑仍受整頁 100 ms 綁定節流限制：roll／pitch／heading 改由獨立 16 ms UI 計時器读取已收到的 CurrentState，移除三軸的慢速重複綁定；保留顯示插值及其他欄位更新節奏，不增加飛控請求。未發布。
- 2026-10-14：姿態儀加入顯示專用 80 ms 插值，動態更新目標約 60 FPS；roll／heading 走最短角度，斷線與切換來源重設，停止資料後不外推。原始遙測、警示與飛控回報頻率不變；實際幀率與延遲仍需實機確認，未發布。
- 2026-10-14：姿態儀異常外框改為透明紅色向內淡出的漸層，保留兩秒明暗呼吸週期；取代不透明紅色實線框，警示條件不變，未發布。
- 2026-10-14：任務規劃 HOME 空域顯示半徑上限提高為 500 公里，預設仍 5 公里；改從完整離線快照在背景篩選，不受舊小區塊查詢限制，距離改用 HOME 中心等距投影。縮放顯示條件與飛行頁原 50 公里範圍不變，未發布。
- CAA 離線版驗證：`bin/CaaOffline123/net461/FMTPlanner.exe`；Release 0 errors／1,284 warnings，40 個離線回歸脚本及空域顯示測試通過。隔離目錄完成真實全台更新、落盤及快照重新載入測試；未更動使用者現有快取、未連線飛控、未發布。
- 2026-10-14：隨 EXE 內嵌全台 CAA 圖層 1／2 共 5,082 筆壓縮底稿（約 7.53 MB），空域顯示與航線檢查改讀本機完整快照；任務規劃新增「CAA 資料／更新」，飛行地圖右鍵新增更新入口。更新顯示下载日期與進度、可取消；驗證筆數／邊界後原子替換，錯誤保留舊資料，快照損壞退回內嵌底稿。衛星底圖不在此次離線範圍，仍需地圖快取。未發布。
- 2026-10-14：隱藏地圖上的 Disable Joystick 按鈕，移除搖桿啟用時自動顯示的路徑；搖桿控制功能及風向／風速顯示不變，未發布。
- 2026-10-14：任務 HOME 換成透明底、黃色粗線的圓環 H／四向箭頭圖示，中心對準 HOME 座標；一般航點不變，未發布。
- 2026-10-14：姿態儀加入紅色呼吸外框（約 2 秒週期）：連線時 Warning 以上回報保留 10 秒，並涵蓋既有 failsafe、GPS、EKF、震動、CPU 滿載及未通過解鎖檢查；正常或離線時取消，僅本機顯示、不發送飛控請求。未發布。
- 2026-10-14：更正任務重讀的 HOME 保留條件：在空中／起飛／降落中保留，已落地恢复原流程；落地狀態未知時以解鎖狀態保守判斷。一般／FTP／快取路徑一致，實際 HOME_POSITION 回報仍正常更新。未發布。
- 2026-10-13：RTK 定位、SIK 數傳、P400 數傳移至「硬體設備（選擇性）」子頁；保留離線可用與 RTK 原顯示選項，不更改設定或通訊行為，未發布。
- 2026-10-13：油門／WP 數值改為靠右，與固定右側單位相隔半個字高；左側標題不動，長數值仍自動縮小避免重疊。取代先前置中數值的配置，未發布。
- 2026-10-13：HOME 黃線改用斜切接合，避免返航／出發線夾角很小時，尖角突出 H 點。涵蓋共用任務疊圖與飛行頁航線；不修改 HOME 座標或任務資料。離線實線／虛線像素測試：原版突出 8 px、修正版止於 H 點；Release 0 errors／1,391 warnings。輸出 `bin/HomeLine123/net461/FMTPlanner.exe`，未發布。
- 2026-10-13：連線取消補強 UDP 開啟輪詢（500→20 ms）、串列埠前後穩定等待與版本回覆等待的取消檢查；取消後停止倒數更新、解除 UDP 事件並保留「正在中止」提示，不再提早回報取消完成。串列等待／無資料／半包離線取消測試約 15–17 ms；此數字不代表實體驅動 Open/Close 或完整對話框關閉時間。最新 `bin/CancelImmediate123/net461/FMTPlanner.exe`，未發布。
- 2026-10-13：油門／WP 方格改為固定三欄，標題靠左、數值置中、單位靠右；長數字只在數值欄內縮放，不移動標題或單位。最新 `bin/HudColumns123/net461/FMTPlanner.exe`；Release 0 errors／1,390 warnings，離線渲染含 0／100% 及 12.3 KM，未發布。
- 2026-10-13：HUD 油門移至 WP 上方獨立同寬格子，單排「油門 0 %」；移除原彩色條下方油門文字。由上而下為油門／WP／GPS2 預留位／GPS1，縮小格高避免 480×360 預覽遮住高度尺。最新 `bin/HudThrottle123/net461/FMTPlanner.exe`；Release 0 errors／1,284 warnings，單／雙 GPS GDI 渲染檢查，未發布。
- 2026-10-13：依使用者確認，HUD 改為右側垂直堆疊 WP／GPS2／GPS1，WP 上移一列；GPS2 未出現時保留中間空位，GPS1、VIBE、EKF 維持原本底列水平位置。取代前次 GPS2 水平預留設計。最新本機 `bin/HudStack123/net461/FMTPlanner.exe`；Release 0 errors／1,390 warnings，單／雙 GPS 離線渲染與目視檢查通過，未發布。
- 2026-10-13：HUD 航點距離縮成單排 `WP 0 M`，移除中文標題與航點編號，保留原距離單位換算（長距離可顯示 KM）。固定保留右側 GPS2 欄位，GPS1／VIBE／EKF 不因 GPS2 出現而移位。最新本機 `bin/HudCompact123/net461/FMTPlanner.exe`，Release 0 errors／1,284 warnings；離線 GDI 單／雙 GPS 渲染並檢视雙 GPS 畫面，未連線飛控、未發布。
- 2026-10-13：一般／MAVFTP 航線重讀及任務快取切回畫面時，不再以任務第 0 項提示覆寫規劃 HOME，保留既有起飛點座標與高度；快取按序號排序。檔案匯入／手動設定與飛控 HOME_POSITION 回報不凍結，沒有新增跨飛控 HOME 快取。最新本機 `bin/HomePreserve123/net461/FMTPlanner.exe`；Release 0 errors／1,390 warnings，HOME 路徑結構、任務估算及版本測試通過，未做飛控下載實測、未發布。
- 2026-10-13：飛行畫面左側模式區改為各群組固定一列、按鈕與字體等比例縮放，不隨寬度重新換行；最低縮放 85%，更窄時可捲動，並保留操作頁籤高度。安全設定移除解鎖硬性阻擋，改於寫入確認顯示風險；保留預設否、唯讀／回放禁止寫入及目標變更檢查。最新輸出 `bin/ModeScale123/net461/FMTPlanner.exe`，含圖示修正。Release 0 errors／1,390 warnings，35 個離線回歸腳本通過；未連線飛控、未發布。
- 2026-10-13：主視窗直接回應 WM_GETICON 的小／大／備用小圖示查詢，並於 Shown／Activated 重設原生圖示，降低完整啟動後被覆寫為通用圖示的風險。新增完整顯示、重建、模擬原生圖示清除的回歸；版本一致性與圖示測試通過，Release 0 errors／1,390 warnings。最新輸出 `bin/IconFix123/net461/FMTPlanner.exe`；尚待使用者實際工作列確認，未更動釘選捷徑或清除 Windows 快取。
- 統一專案、EXE、組件、啟動畫面與打包來源版本為 1.2.3／1.2.3.0；不建立 GitHub Release、不推送或建立標籤。
- 納入下列 2026-10-12 本機修改：分站轉送延遲、連線取消、快捷按鈕縮窄、任務估算與錯誤複查修正。
- 保留已知限制：ELRS MAVLink／導控來源互斥尚未完成；完整加減速、RTL 與起降時間模型尚未完成；離線驗證不等於實飛驗證。
- 本機程式輸出：`bin/Local123/net461/FMTPlanner.exe`。下方舊版路徑與版本號保留為歷次驗證紀錄。
- 驗證：Release 建置 0 errors／1,390 warnings；34 個離線回歸腳本通過，含 V1.2.3 啟動／登入／參數視窗及 EXE 版本一致性檢查。未做實機／實飛驗證。

## 未發布 — 2026-10-12 錯誤複查

- 修正任務估算 DO_CHANGE_SPEED 錯套前一航段；依序處理變速後的下一航段，新增連續變速回歸案例。
- 估算器拒絕無效 HOME、負等待時間、不適用機型／非法 VTOL 轉換目標；UI 顯示輸入錯誤而非未處理例外。仍未建模完整加減速、RTL 與起降，不代表實飛總時間。
- 初始調參提示分開列出 4.7 PSC_D_ACC_P/I 的 0.1/0.2 倍率，以及舊版 PSC_ACCZ_P/I 的 1/2 倍率；提示本身不寫入參數。修正 RoundTo 精度範圍檢查的 AND/OR 錯誤。
- 傳統接收機控制切換新增預設「否」的確認，明確警告 ELRS MAVLink 不適用。這不是 MAVLink 來源仲裁修復；同一輸入鏈路上的 ELRS／導控互斥仍待設計與實機驗證。
- GitHub 查核：本倉庫 Issue 搜尋僅回傳已完成的 #2，無開啟中的 Issue，#2 無留言；未更動 GitHub。搜尋結果不代表軟體不存在其他 BUG。
- 本機輸出 `bin/BugAudit122/net461/FMTPlanner.exe`，仍為 1.2.2.0；未發布、未連線飛控。
- 驗證：Release 建置 0 errors／1,284 warnings；34 個離線回歸腳本全部通過。UDP 本機轉送 16–21 ms、讀取取消 15–18 ms，非實際鏈路端到端測量。未完成實機／實飛驗證。

## 未發布 — 2026-10-12 連線取消等待修正

- 連線心跳偵測將取消旗標傳入封包讀取，於等待資料、標頭、內容與讀取鎖時提早退出；保留既有公開無參數 API 與原連線清理流程，不使用遺留背景讀取的競速超時。
- 新增 `FMT/Test-ConnectCancel.ps1`：本機 UDP 無資料／半包取消約 15–17 ms，並驗證讀取鎖釋放與鎖競爭取消。這不是完整連線視窗關閉延遲測量；驅動 Open/Close、串口啟動延遲及其他交握階段仍可能等待。
- 本機輸出 `bin/ConnectCancel/net461/FMTPlanner.exe`，含前次快捷按鈕縮窄；未發布，未連線飛控。

## 未發布 — 2026-10-12 任務估算修正（LOG 比對後）

- 單一有限 DO_JUMP 依任務序號展開，保留重複期間的位置與速度；無限、無效、多重跳轉及超過 20,000 步的任務停止估算，避免 UI 無限迴圈。未將個別實飛的中途返航時間當作整份任務校正倍率。
- 固定翼預設參數的 LOITER_TO_ALT 加入「水平進場後盤旋升降」近似模型，明示不含入圈／離圈轉彎與對準下一航點的等待；多旋翼定時懸停不再增加虛構水平航程。
- 不完整任務不再顯示局部合計，只顯示停止指令與原因。可計算結果改稱「任務指令段估算」，明示不含任務結束後返航／待命。VTOL 起降、RTL、複雜條件仍未建模，不宣稱支援完整起飛至落地預估。
- 本機輸出：`bin/MissionEstimateFix/net461/FMTPlanner.exe`；不修改飛控或原始 LOG，未發布 GitHub。指令語意參照 ArduPilot 官方 Mission Commands 文件（連結見下一節）。

## 未發布 — 2026-10-12 任務時間估算

- 任務規劃右側新增「預估飛行時間」，讀取目前任務快照與 HOME；可輸入巡航／爬升／下降速度及 VTOL 轉換秒數，計算各段距離、時間與總計。所有初值均為示例，需使用者確認，不修改飛控參數或任務數值。
- 無風、直線航段與瞬時變速假設；支援基本航點、爬升下降、定時／定圈盤旋、相對 NAV_DELAY、簡單 DO_CHANGE_SPEED、手動 VTOL 轉換時間。不確定命令（如 RTL、跳轉、條件等待、VTOL 起降、地形高度等）停止估算並只呈現已算部分；降落只估到目標點，總時間標示不確定。不是續航／實飛保證。
- 依 ArduPilot 任務指令文件處理 NAV 與 DO 的關係，不將 CONDITION_DELAY 當成固定停留秒數。參考：https://ardupilot.org/plane/docs/common-mavlink-mission-command-messages-mav_cmd.html 。本機建置：`bin/MissionEstimate/net461/FMTPlanner.exe`，未發布。

## 未發布 — 2026-10-12 分站控制延遲修正

- 接力轉送新增獨立背景上行接收，不再等待飛控下行遙測才讀取分站控制資料。僅接力模式啟用；保持逐來源授權並在送出前再檢查，唯讀／回放／斷線時不送出；停止轉送與釋放介面時取消背景工作。
- 本機建置 `bin/RelayLatencyFix/net461/FMTPlanner.exe`，未替換 GitHub V1.2.2 套件。UDP 回環驗證共享與獨立來源兩條路徑在零遙測下行時可轉送，並測試撤權、唯讀與停止。此結果不是 VPN／實際無線鏈路的端到端延遲測量。
- ELRS MAVLink 與導控的來源互斥接管問題仍未解決；本次不調換按鈕、不改飛控參數，也不提高 RC 傳送頻率。

## FeiMaoTecPlanner V1.2.2 — 2026-10-10（正式發布版）

以下保留開發期間各次本機檢查紀錄；其中「未發布」指當次檢查狀態。正式發布整合全部 V1.2.2 修正，建置來源為 `bin/Release122Final/net461`。實機／實飛驗證限制仍適用。

- 2026-10-10：成功連線後背景送出一次 SYSTEM_TIME（電腦 UTC，微秒），不改參數、不等待 ACK、不自動重送；忙碌延後、斷線取消舊工作，重新連線重新計次。唯讀／回放不送出。建置：`bin/TimeSync122/net461/FMTPlanner.exe`，未發布；不能回溯修正既有 LOG，飛控是否採用時間需實機確認。

- 2026-10-10：空速計歸零不再於 UI 執行緒同步等待校正回覆，改背景執行；加入執行中／日誌下載／參數載入防重入、送出前重新檢查目標與未解鎖／非唯讀狀態、例外後釋放通訊占用。逾時提醒命令可能已執行。建置：`bin/AirspeedFix122/net461/FMTPlanner.exe`，未實機驗證、未發布。

- 2026-10-10：船的預設改為使用者指定的木船藍帆透明圖；固定翼保留原有飛貓資源。預設預覽與地圖共用資源，停用對預設不生效的自訂控制項，保留既有自訂偏好。建置：`bin/DefaultIcons122/net461/FMTPlanner.exe`，未發布。

- 2026-10-10：修正共用數值控制項在範圍缺失時將目前值當成上限，ACC P 0.4 可正常輸入及暫存；同步換算 metadata 範圍／步進。修正 HOVER 擴高造成的濾波器重疊及 RC 選項貼框；當頁更新僅鎖編輯欄位，說明區不再整頁停用變白。調參寫入檢查回傳值，失敗保留修改。最新本機程式：`bin/ParamUiFix122/net461/FMTPlanner.exe`。

- 2026-10-10：恢復 Loading 頁與「參數設定」完整參數表的明確全量重新載入，使用可取消的背景進度視窗；與一般調參頁的當頁讀取分流。空快取不再誤判完成；取消／失敗不清除待寫入修改。建置：`bin/FullReload122/net461/FMTPlanner.exe`，未發布。

- Throttle Accel 的 IMAX 下新增 HOVER；依機型綁定 MOT_THST_HOVER／Q_M_THST_HOVER，保留原始推力比例，參數缺少時灰化；沿用明確寫入及當頁更新，不改變 HOVER_LEARN。建置：`bin/Hover122/net461/FMTPlanner.exe`。

- WPNav 調參區速度統一顯示／輸入 m/s，半徑使用 m；依 Copter／QuadPlane 實際參數名稱處理舊版 cm 單位與新版 m 單位，包含範圍、步進及寫入換算。21 個別名離線雙向換算測試通過；新建置 `bin/WpNavMetric122/net461/FMTPlanner.exe`，未發布。

- 設定頁更新與 F5 不再下載完整參數表；改用當頁參數、非同步單筆讀取、300 ms 間隔、單一更新工作、離頁取消與逾時停止。完整參數表／常用參數僅讀取畫面可見項目，OSD 僅讀取所選分頁。
- 移除電池／圍籬啟用及參數數量變更時的隱含整表下載；初始載入狀態頁不會再啟動第二份下載。初始連線載入仍保留，沒有改寫飛控控制頻率或參數。

- 軟體設定新增本機地圖載具圖示介面：固定翼、多旋翼、直升機、VTOL、船、車，支援 PNG、大小、顏色、不透明度、方向修正與恢復預設；保留既有導航線與警戒圈。

- 取消連線／重連自動改寫 RC_OPTIONS；GCS ID 不符只提示，不自動保存新值；新連線清除舊的延遲參數儲存命令。
- 修正設定頁語音 UTC 計時、線長讀寫、更新入口與繁體中文說明。
- 調參、初始設定、完整參數共用密碼與保護開關；驗證目前密碼後可關閉。
- 新增 ICE、TAKEOFF、降落設定與說明；起飛加入固定翼發射方式、多旋翼、傳統直升機及 QuadPlane 分類，寫入區移至表格上方。
- 其他混控新增 PTCH2SRV_RLL，沿用明確按下寫入才送出的流程。
- 包含先前本機地圖首次縮放、空速歸零顯示、引擎工具列與接力控制回讀保護修正。
- 不代表已通過實飛；ELRS MAVLink 不可使用傳統接收機隔離切換。Pixhawk 6C / 4.7.0 的下載紀錄 main_loop_stk 尚待 BIN 診斷，未宣稱修復。

## FeiMaoTecPlanner V1.2.1 — 2026-10-02

- 修正 V1.2.0 介面、錯誤回報與更新判斷仍使用寫死的 V1.1.9；共用版本改由應用程式 assembly 取得。
- 新增正式建置版本一致性測試，檢查實際登入／啟動畫面與參數視窗、EXE、專案及打包版本。
- 重新執行離線回歸與正式 ZIP 稽核；實機與全表語意複核限制保留，詳見 `FMT/RELEASE-AUDIT-V1.2.1.md`。

## FeiMaoTecPlanner V1.2.0 — 2026-10-02

- 納入下列 V1.1.9 發布後的本機翻譯複核與介面修正；下方「尚未發布」為各輪當時狀態，這些變更合併於 V1.2.0。
- 復原電源模組 1／2 的選單入口，保留連線、參數讀取與顯示設定條件。
- 頁籤選單改用中文標題並保留設定鍵；地圖相關文字採繁體中文。
- 加強起降、RTL、電池 failsafe、解鎖前檢查與類比／Synthetic Current 校正的翻譯語意，保留原始參數、數值與專有名詞。
- 中文覆蓋不代表逐條語意複核全部完成；本版未完成實機與群飛驗證。詳見 `FMT/RELEASE-AUDIT-V1.2.0.md`。

## V1.1.9 參數語意複核 — 2026-10-02（本機更新，尚未發布）

- 電源校正補強 9 條說明：類比 A/V 與 offset 換算順序、Synthetic Current 共用參數的 A 單位用途與估算限制、MAX_VOLT 縮放基準；不修改飛控值或共用 metadata 單位。
- 電池解鎖前檢查複核 33 條說明：ARM_MAH 剩餘容量與重啟估計限制、ARM_VOLT 電壓基準及各自的 0 值停用範圍；不更動飛控參數。
- 電池 failsafe 複核 49 條說明：剩餘容量 mAh／電流監測條件、低電壓連續計時與 0 的停用範圍；保留原始參數引用，新增 49 個語意案例（含 48 個容量變體）。
- 降落後輸出／上鎖修正 3 條說明及 2 個選項：控制舵面而非所有 servo、中立輸出不等於 0 PWM、上鎖與 RC 油門中止降落的適用條件。65 項語意、全表及版本測試通過；輸出 `bin/TranslationSemantic119K/net461`，最終增量編譯 0 errors、1,284 warnings。
- 固定翼降落修正 5 條說明：迎風補償百分比、pre-flare 停用範圍、flare 高度／時間觸發關係及 Pitch 下限。62 項語意、全表及精確版本測試通過；編譯 0 errors、1,390 warnings，輸出 `bin/TranslationSemantic119J/net461`。
- 固定翼起飛另補強 4 條翻譯：油門 slew 百分點、0.1 秒延遲單位、交替加速度事件及起飛逾時解除條件。語意測試累計 57 項，全表及精確版本測試通過；編譯 0 errors、1,390 warnings，輸出 `bin/TranslationSemantic119I/net461`。
- 精準降落另修正 5 條說明與 2 個重試位置選項：區分機體位置／降落目標位置、取消高度條件／停用功能、逾時／停用重試。語意測試累計 53 項，全表及版本測試通過；本輪編譯 0 errors、1,390 warnings，輸出 `bin/TranslationSemantic119H/net461`。
- 補強 TAKEOFF 高度基準、VTOL 降落等待時間 0 的意義，以及 RTL 最終高度的新舊原文；保留 Home 基準、RC failsafe 例外與各版原始單位，不修改飛控參數值。
- 48 項語意檢查、全表 58 份 metadata／3,335 種選項及 Copter 4.5.7／4.6.3／4.7.1 精確版本檢查通過。編譯 0 errors、1,450 warnings；未做實機驗證。詳見 `FMT/Localization/README.md` 的逐輪核對記錄。

## V1.1.9 電源模組修正 — 2026-10-01（本機更新，尚未發布）

- 恢復「初始設定 → 硬體設備（選擇性）」中的電源模組與電源模組 2；保留連線／完整參數條件，不開啟其他隱藏頁面。
- 保留原有電池容量、電壓及電流校正功能，不重設或寫入飛控參數。編譯與離線選單／控制項測試通過，未實機驗證。
- FileVersion 保留 1.1.9.0；本機更新包 `FMTPlanner-V1.1.9-PowerModuleFix.zip`。GitHub 原 V1.1.9 發布已鎖定，附件無法替換。

## FMTPlanner v1.1.9 — 2026-10-01（正式發布）

- 完整參數頁針對 4.5.x／4.6.x／4.7.x 使用確切韌體版本說明檔，避免新版通用 metadata 缺少舊 WPNAV／PSC 名稱；同版本套用單位、範圍及安全屬性。缺少對應版本／參數時顯示提示，不以其他版本資料補值。尚依賴本機對應 pdef（既有連線流程負責下載），未內建所有 patch 版本。
- 納入全表參數說明對照：reviewed 字典 3,836 條，跨本機 58 份公開 metadata 的 3,934 種說明，英文回退為 0；包含未啟用功能的參數。增加大量選項中文解釋並保留完整原文、專有名詞、版本差異及 bit 索引；原文已知筆誤另加註記，不改飛控值。來源集合覆蓋不等於所有韌體實機驗證，詳見 FMT/Localization/README.md。
- 關閉流程加入接力服務終止保護、UDP／計時器釋放及背景迴圈停止，縮短無限等待風險；離線清理測試不等同完整實機關閉驗證。
- 修正訊息列表長時間更新、參數比對待寫入清單、多旋翼更新畫面阻塞及 Bad Version 例外。
- LOG 下載可選資料夾；安全設定同步目前值並加入 RTL 高度／速度；車船調參頁中文化。
- 停用未核對參數機譯，採原文精確對照，保留技術術語；未完成的譯文回退英文。
- 新增 ELRS CRSF／MAVLink 串列設定；依官方 UART／RSSI／資料流設定，不依本機參數說明或 MAVRadio 位元阻擋；MAVLink 清除 RC_OPTIONS bit 1，其餘位元保留。
- 使用者確認 ELRS 無 RC 輸入最後由 SYSID 對應修正解決；程式不擅自改寫機體／接收機 ID。
- 配置頁整合實驗性群飛管理、相對位置及航點編隊；接力控制頂部按鈕可選擇顯示、預設隱藏。
- 空速計未啟用時隱藏歸零按鈕，修正工作列圖示相容性。
- 最新翻譯更新整合到 `bin/Release119TranslationUpdate/net461`，版本 1.1.9.0；正式更新包為 `FMTPlanner-V1.1.9.zip`。尚有發布阻擋項與未完成實機驗證，詳見 FMT/RELEASE-AUDIT-V1.1.9.md；依使用者要求發布正式 V1.1.9；發布標記不代表已知問題已修復。

## FMTPlanner v1.1.8 — 2026-09-18

- 航點拖曳不再丟棄 33 毫秒內的滑鼠事件；合併重繪、移除重複位置更新，測繪多邊形拖曳期間不重建整個圖層。
- 航段距離改為 `<-10M->` 樣式，移至中點上方，避開「＋」插入航點按鈕。
- 起始位置下新增限禁航區顯示開關與 HOME 半徑（整數公里），新設定預設 5 KM、範圍 1～30 KM；既有值換算並四捨五入。
- 限禁航區僅於縮放 10～18 級（含）顯示，只保留與 HOME 半徑相交的完整區域；取消圖形滑鼠命中測試，減少拖曳負擔。
- 修正接力控制按鈕在隱藏分頁、切換主／分站後重疊；窄視窗採下一排顯示。
- 顯示開關與半徑不會停用航線安全檢查。本版仍使用既有線上／快取資料來源，未加入完整離線限禁航區資料包。編譯與模擬測試不等於實機認證。

## FMTPlanner v1.1.7 — 2026-09-18

- 飛行畫面資料綁定與通訊處理分離，避免 COM 逾時時介面等待收訊鎖；取消過期後重複排隊的畫面更新。
- 限制參數接收進度更新的頻率及待處理數量；滑鼠地形查詢改為單一工作、只顯示最新位置結果。
- 航點圖示與航線同步節流重繪，放開滑鼠時採用精確位置。
- 內建 H420 預設參數選項立即顯示，不再等待 GitHub；線上清單更新保留原選取項目。
- 擴充繁體中文參數說明與選項；未人工審核內容標示「機譯待核對」，保留英文原文供參考，不宣稱全部完成專業校訂。
- 保留接力控制、UDP 主站及搖桿恢復修正。編譯與離線／模擬測試不代表實機認證，使用前須完成地面未解鎖測試。

## FMTPlanner v1.1.6 — 2026-09-10

- 新增 P400 DATA/AT 設定及 PicoConfig 備用視窗，依手冊提供參數說明、選項與預設值；S107 支援遮蔽密碼輸入，S102/S110 可於 CONFIG 強制 AT 模式設定。
- 新增主／副頻率表讀寫、50 筆編輯、匯入匯出及 10 個自訂保存組合；写入前備份、寫入後讀回比對，失敗不宣稱自動回復。
- 修正 SiK 頻率欄位及功率選單；新增 H420 基礎參數、調整 Throttle Accel 輸入及控制切換介面。
- 更新五個工具列圖示、調整圖示與電池顯示尺寸，修正工具列高度遮蔽問題，移除 QNH 入口。
- Release 編譯與離線／模擬測試不等於實機認證；使用數傳寫入、接力或搖桿控制前，須完成拆槳地面驗證。

## FMTPlanner v1.1.5 — 2026-09-05

### 接力控制與 MAVLink 轉發

- 移除舊舵機調整地圖面板入口，改為與地圖整合的「接力控制」介面，集中顯示站台與控制權、MAVLink 轉發及本機搖桿設定。
- MAVLink 轉發列新增灰／綠／紅狀態燈號、執行狀態及 IP／Port 衝突驗證；重複端點、無效位址或本機監聽埠占用時拒絕啟動並顯示原因。
- 新增 Windows 本機位置與接力站位置暫存，可在地圖以站號標示導控電腦；沒有有效定位時不顯示，且不改寫飛機 HOME 點。
- 接力控制權採保守鎖定：介面可配置站號與租約狀態，未建立完整驗證的遠端授權封包前不宣稱自動遠端交接。

### 實體遙控器與導控搖桿切換

- QNH 右側新增控制來源選擇，以 `RC_OPTIONS` 隔離實體接收機與 MAVLink RC override；程式啟動及重新連線預設回到實體遙控器。
- 只有本機搖桿物件已建立且啟用時才開放「導控控制」，切換前要求主要軸值對齊並維持穩定時間。
- 掃描 `RC1_OPTION`～`RC18_OPTION` 的解鎖／上鎖、緊急停槳、馬達互鎖等關鍵功能；由導控按鈕負責的功能不強迫重複綁定搖桿。
- 飛行器已解鎖時不執行自動控制來源切換；由導控切回實體遙控器時顯示必要人工核對清單，避免使用未同步的開關狀態。

### 安全與發布

- 電子圍籬未啟用時停用 `FENCE_ACTION` 卡片及寫入，並顯示「目前：未啟用」。
- 版本統一為 V1.1.5／1.1.5.0，Windows ZIP 入口為 `FMTPlanner-V1.1.5.exe`，附發布說明及 SHA-256 校驗檔。
- 完成 Release 編譯、既有 MQTT／SiK／飛行動作／安全設定／油門與校正排版回歸，以及 V1.1.5 專屬封裝驗證。
- VPN 接力、RC_OPTIONS 切換、實體搖桿、關鍵 RC 開關及飛控端到端功能仍須在拆槳或無動力的地面安全狀態驗證。

## FMTPlanner v1.1.4 — 2026-09-04

### 安全設定與遙控器

- 地圖上方新增與 MQTT、3D 地圖、調校曲線互斥的「安全設定」面板，各項失效保護參數可獨立套用。
- 失效保護列舉及位元選項改為繁體中文選單，縮小卡片尺寸並保留對應 ArduPilot 參數名稱。
- 遙控器校正頁新增中立回中油門與手動油門設定，檢查 `PILOT_THR_BHV`、RC 油門校準範圍及 `THR_DZ`，不額外修改其他油門行為位元。

### 感測器校正與介面

- 加速度計採六面飛機姿態圖卡引導：綠色為已完成、黃色為目前需擺放、灰色為尚未校正；狀態由 MAVLink 校正訊息驅動。
- 加速度計與羅盤校正頁改為緊湊固定尺寸、靠左上排列，避免在寬螢幕上拉伸；修正校正訊息覆蓋姿態圖卡。
- MQTT 設定改為使用者明確勾選後才保存，未選擇時不載入舊版殘留 Broker、Topic 或連線資料。

### 發布與驗證

- 版本統一為 V1.1.4／1.1.4.0，Windows ZIP 入口為 `FMTPlanner-V1.1.4.exe`，附操作說明與 SHA-256 校驗檔。
- 新增安全設定、油門模式與感測器排版回歸檢查；完整 Release 編譯與封裝驗證通過後建立正式標籤。
- 實體感測器校正、油門寫入、Failsafe 行為與正式 MQTT／TLS 仍須在無槳、地面安全狀態下驗證。

## FMTPlanner v1.1.3 — 2026-09-01

### MQTT 橋接與連線保護

- 新增地圖內嵌 MQTT 3.1.1／TLS 雙向二進位橋接，與 3D 地圖及調校曲線共用區域；收合不斷線，停止時釋放連接埠。
- 支援不含密碼的 `.fmt` 匯入／匯出，選擇記住密碼時使用 Windows DPAPI；保留 TLS 憑證與主機名稱驗證。
- 工具列新增透明 MQTT 圖示與上下兩列收發速度，與直升機轉速計、飛行時間緊鄰排列；停止橋接時隱藏。
- 主視窗關閉前檢查所有 MP 遙測連線及 MQTT 執行狀態，預設取消關閉，避免誤觸中斷。

### 數傳設定與繁體中文排版

- 在初始設置 RTK 項目下新增 SiK 本機／遠端數傳設定，使用獨立序列埠與繁體中文操作介面。
- 保留寫入、恢復預設、韌體及 PPM 操作確認；匯入與加密設定變更不會立即寫入設備。
- 修正 SiK 標籤、欄位及底部按鈕重疊；停用狀態保持可讀性，不取消安全停用保護。
- 動作分頁按鈕及常用選單中文化，保留 MAVLink／自訂命令鍵值；修正 Loiter 按鈕原本誤標為手動的文字。
- 速度、高度及盤旋半徑數值與按鈕固定同列；地圖底部選項固定單列，窄視窗使用捲動。
- 保留既有馬達配置圖與 H 型機架示意調整。

### 發布與驗證

- 版本統一為 V1.1.3／1.1.3.0，Windows ZIP 入口為 `FMTPlanner-V1.1.3.exe`，附操作說明與 SHA-256 校驗檔。
- 本機回歸涵蓋 73 項 MQTT、44 項 SiK、287 項動作分頁檢查，包含 100%／150%／200% 字型及幾何縮放。
- 測試不會連線正式 Broker、寫入數傳或操作飛控。實體 TLS／DTU、數傳讀寫與飛行行為仍須地面安全驗證；不包含手機 App 或 VM 網頁管理工具。

## FMTPlanner v1.1.2 — 2026-08-17

### AUTO mission panel and flight display

- Rebuilt the AUTO mission panel with fixed-height, fixed-width two-line fields so Mission updates, waypoint jumps and ETA refreshes no longer shift or truncate the layout.
- Kept the current-execution status active only in AUTO and RTL modes, and stabilized Mission Item, next-task, leg-distance, home-distance and ETA presentation.
- Removed the blue progress bar while retaining the Mission Item count and percentage as stable text values.
- Added a mutually exclusive embedded 3D-map/tuning panel so expanding either tool cannot overlap or push the primary map out of view.

### Vehicle controls and configuration

- Added the green preflight-check action, red arm/disarm action and matching rounded dark-blue airspeed-zero and QNH actions.
- Added integer RPM1 main-rotor telemetry and configurable helicopter RPM warning limits; compacted the flight-time and satellite telemetry blocks and removed the ArduPilot toolbar logo.
- Corrected helicopter, multirotor, fixed-wing and VTOL identification, replaced Position Hold with Brake for helicopter/multirotor quick modes, and expanded per-frame common navigation settings.
- Added the RTK setup entry under mandatory hardware and localized/reflowed accelerometer, compass, radio, servo-output, motor-test, fence and traditional-helicopter pages.

### Map, parameters and localization

- Set the disconnected/default map anchor to `23.8456499, 120.9759521`, corrected aircraft marker anchoring and reduced the multirotor icon's cyan fill.
- Fixed the electronic-fence restriction choices, widened its dropdown, and ensured parameter bitmask controls are visible on first display.
- Kept ArduPilot parameter keys in English while providing Traditional Chinese field tooltips and descriptions.
- Improved Traditional Chinese labels and DPI-aware layout in advanced tools, waypoint control and calibration pages.

### Stability and validation

- Cached plugin assembly discovery and reduced redundant Flight Data, mission-panel and theme refresh work to avoid UI stalls and white flashes.
- Added disconnected, timeout, missing-resource and null-control guards for configuration and advanced-tool paths.
- Built the complete Release solution with Visual Studio 2022 MSBuild with zero compiler errors, then validated the versioned portable package, required files and exclusion policy.
- The repository test project currently exposes no discoverable automated test cases; physical motor/servo output, GPS/RTK, pitot/QNH and real-controller behavior remain hardware validation items.

## FMTPlanner v1.1.1 — 2026-08-16

### FMT startup and login experience

- Added a new embedded FMT aviation-map splash screen and matching dark login screen with V1.1.1 branding.
- Added a functional account field, password visibility toggle, optional remembered account and gradient sign-in action while preserving the existing FMT authentication rules.
- The splash and login artwork is embedded in `FMTPlanner.exe`, so the portable package does not depend on loose external background files.

### Terrain and airspace safety presentation

- Reworked the altitude/terrain profile header so planned-route and terrain ranges no longer overlap.
- Added route-sampled terrain clearance states: red for terrain collision, yellow for clearance below 30 m and green when no terrain risk is detected. Home is excluded from the minimum-clearance threshold to avoid a false zero-clearance warning at takeoff.
- Added high-contrast, scrollable Taiwan airspace results: prohibited-area hits use red with white text and restricted-area hits use yellow with dark text. Takeoff, mission and return segments are listed separately and duplicate entries are removed.
- Localized fence-circle mission commands without changing their MAVLink command values.

### Traditional Chinese UI and layout

- Localized Flight Data and Flight Planner context menus, the advanced-tools window, sensor-status panel and multi-aircraft waypoint-control interface.
- Reorganized the advanced-tools sensor status area and added DPI-aware scrolling so controls and descriptions remain accessible.
- Changed QNH entry to hPa (百帕), aligned the airspeed-zero and QNH rounded buttons, added a flight-time icon and changed the AUTO action to the Traditional Chinese `跳轉航點` control.
- Improved telemetry labels and language switching so English and Traditional Chinese can be selected without corrupting custom labels.

### Stability and mission planning

- Fixed localized mission-command combo initialization causing `NullReferenceException` during Flight Planner startup.
- Fixed map clicks failing to write latitude and longitude after localized column headers were applied.
- Added guards for disconnected parameter reads, missing optional resources and null child controls so unsupported advanced actions fail with a clear local message instead of opening the crash reporter.
- Preserved the privacy-safe GitHub issue workflow: reports remain local and are copied/opened only after explicit operator confirmation.

## FMTPlanner v1.1.0 — 2026-08-15

### AUTO Mission Item synchronization

- Renamed the AUTO progress display from waypoint-style `WP X/X` wording to the protocol-accurate `Mission Item X / X` concept.
- Mission count, item and acknowledgement packets now trigger a coalesced UI refresh so uploading, downloading or replacing a Mission updates the AUTO panel immediately.
- A received Mission count is displayed before the individual Mission Items finish downloading; Fence and Rally transfers are ignored by the AUTO panel.
- Progress uses the Mission Item's actual ordered list index instead of dividing its MAVLink sequence directly by the total. Sparse sequences such as `1, 3, 7` now correctly show item `2 / 3` and `67%`.

### Map and mission-planning layout

- Corrected fixed-wing, VTOL and multirotor marker anchors so the reported aircraft coordinate aligns with the visual center of the map icon.
- Reorganized waypoint radius, loiter radius, default altitude and altitude-mode controls with reserved value widths to prevent missing or overlapping values.

### Stable validation

- Ran a repeatable MAVLink/SITL integration harness and validated ArduCopter, ArduPlane and QuadPlane with the same 25-check matrix (75 checks total).
- Verified heartbeat, vehicle type, simulated GPS position, Mission upload/download, live count changes, AUTO mode switching and List Index progress behavior.
- Release build completed with zero compiler errors. Physical motor/servo output, real GPS/RTK reception, pitot/QNH sensors and USB hardware remain real-controller validation items.

## FMTPlanner v1.0.10 — 2026-08-15

### Flight operations and interface

- Added the FMT AUTO mission control panel above the Flight Data map with the current mission item, selectable next item, progress and ETA.
- AUTO mission jumps require an active connection, a valid mission selection and an explicit confirmation; higher-risk takeoff, landing and RTL commands receive an additional warning.
- Improved flight-mode button contrast and aligned the map options into a consistent horizontal row.
- Updated the GitHub home page and Traditional Chinese manual with V1.0.10 screenshots for Flight Data, mission planning, Taiwan airspace, terrain profile, route warnings and parameter access.

### Performance

- Plugin assembly resolution is registered once per process and assembly directory indexes are cached safely.
- Missing or empty plugin directories no longer create a C# compilation task or permanent plugin runner thread.
- The common flight-mode bar no longer reapplies labels, colors and button state when vehicle state has not changed.
- Child forms no longer receive a complete repeated Theme pass on every non-client activation.

### Stability

- Fixed active MAVLink interface replacement leaving callbacks attached to the previous interface.
- MainV2 now detaches static layout, warning-engine and Windows power events during shutdown.
- Flight Data detaches parameter, POI, no-fly and camera events, and prevents duplicate camera callbacks after parameter refresh.
- Replaced the release-update message box with a typed, DPI-aware, scrollable Traditional Chinese dialog using explicit UTF-8; fixed the `RuntimeBinderException` caused by comparing an integer return value with `DialogResult`.

### Security and privacy

- Removed the legacy unencrypted crash-report upload to the third-party `vps.oborne.me` endpoint.
- Crash reports are now generated locally, de-identified and editable. Nothing is uploaded automatically: the operator must explicitly copy the report, open the FMTPlanner GitHub Issue page, review the public content and submit it.
- GitHub credentials or access tokens are never requested or embedded in FMTPlanner.

### Cleanup

- The public Windows ZIP excludes PDB debug symbols, macOS/Linux native libraries and developer `plugins/example*.cs` samples without deleting their source or build outputs.
- Added a package manifest, repeatable performance measurement script, V1.0.9 baseline and stable-audit report.
- Retained DLL plugins, drivers, maps, language fallback resources, parameter/firmware metadata, Python tools and all V1.0.9 FMT features.

### Validation boundary

- Automated Release build, startup/login smoke, disconnected HUD/map resource measurement, static regression checks and package-content checks are documented with results.
- USB Serial, UDP, TCP and ArduCopter/ArduPlane/QuadPlane connected-state validation still requires representative hardware or an approved repeatable endpoint and is not claimed by this release audit.

## FMTPlanner v1.0.9 — 2026-08-14

- 修正飛行資料主畫面未建立飛行軌跡時不顯示飛機位置，未解鎖狀態仍會顯示即時飛行器圖示。
- 修正「自動平移地圖」不追蹤飛機位置，改為直接依目前飛機座標更新地圖中心。
- 修正完整參數頁首次載入的空值例外、頂部版面遮蔽及固定翼參數名稱消失。
- 修正 Servo Output 最下方 CH16／CH32 被裁切。
- 將 GCS 地面站失控保護選項繁體中文化並改善配置，避免文字重疊。
- 停止 Altitude Angel 啟動時自動登入，移除主畫面與任務規劃地圖的登入提示列。

## FMTPlanner v1.0.8 — 2026-08-14

- 在主工具列新增獨立「參數設定」頁面，完整參數列表及密碼設定集中管理；移除完整參數表內的巢狀密碼提示，解決參數無法編輯問題。
- 固定翼基本調校頁全面繁體中文化，新增 Servo Roll／Pitch／Yaw、L1、TECS、導航角度、油門及空速控制的中文用途與調整方向說明。
- HUD 航點距離移至右側高度計與 GPS 定位狀態之間，以天空藍高對比資訊框顯示；AUTO、RTL、Guided 與 Loiter 顯示對應距離名稱。
- HUD 油門名稱置於百分比上方，改善狹窄高度帶的閱讀性。
- 強制解鎖／上鎖警告視窗的標題、風險說明及按鈕繁體中文化。
- 強化固定翼與 VTOL／QuadPlane 地圖圖示的判斷、建立與重建流程，並縮小啟用中的天空藍背光外框。
- 移除 Altitude Angel 地圖登入提示，避免遮蔽地圖及飛行資訊。

## FMTPlanner v1.0.7 — 2026-08-14

- 修正參數頁首次解鎖後仍無法修改數值，Enter 可直接解鎖並進入編輯。
- 限禁航區依飛機位置僅顯示 50 公里範圍，新增「顯示限禁航區」開關並降低地圖負載。
- HUD 彩色柱下新增即時油門百分比。
- VTOL／QuadPlane 依 Q_ENABLE 正確顯示 VTOL 地圖圖示，縮小啟用外框。
- 未解鎖飛機不再繪製飛行軌跡，上鎖後清除上一段軌跡。
- 高度與限禁航區檢查改為可調大小、可捲動且靠左的結果視窗。
- 高度地形曲線標題、警語、圖例及座標軸繁體中文化。
- 任務高度模式繁體中文化，補回航點半徑、盤旋半徑及預設高度初始值。

## FMTPlanner v1.0.6 — 2026-08-14

- Repositioned the flight-mode bar and fixed ALT HOLD plus VTOL/fixed-wing group indicators.
- Added session and controller total flight time to the top toolbar.
- Moved Motor Test into Mandatory Hardware.
- Added Enter-to-unlock and reliable first-entry parameter editing.
- Reduced waypoint drag redraw frequency and renamed Flight Planner to 任務規劃.
- Localized and centered mission types, and restored radius/default-altitude value visibility.

## FMTPlanner v1.0.5 — 2026-08-14

### Flight interface

- Added a vehicle-aware common flight-mode panel between the HUD and telemetry information. Multirotor, Fixed Wing, VTOL/QuadPlane, Rover, and Sub use their own safe mode sets.
- Added a TitanPlanner-inspired visual refresh for Radio Calibration, Motor Setup, and Servo Output while retaining the original MAVLink parameter and calibration paths.
- Added dedicated fixed-wing, multirotor, and VTOL map markers based on the supplied FMT aircraft images. The active vehicle uses a cyan glow for visibility over satellite maps.

### Traditional Chinese telemetry

- Changed all three telemetry field-selection windows to the Traditional Chinese title `選擇顯示項目`.
- Localized visible CurrentState field names, QuickView dashboard labels, units, and Flight Data tab names. Internal MAVLink and CurrentState keys remain unchanged for compatibility.
- Added readable terminology for acceleration, pressure, battery cells, ESC telemetry, GPS accuracy, range finders, channels, flight time, airspeed, wind, heading, and distance-to-home values.

### Branding and compatibility

- Added a stable Windows AppUserModelID so the running taskbar icon groups under FMTPlanner.
- Added ArduSub flight-mode lookup support and kept all quick mode changes behind connected/armed-state safety checks.

## FMTPlanner v1.0.4 — 2026-08-13

### Parameter interface

- Fixed the protected Full Parameter List being created after theme initialization, which could leave the parameter tree and table white with unreadable text.
- The parameter tree, rows, alternating rows, headers, selection colors, and grid lines now explicitly use the FMT theme whenever the protected page is opened.
- The cached parameter control is hidden while the password prompt and parameter data are loading, preventing the previous white page from flashing behind the dialog.

### Flight quick actions

- Added a QNH sea-level pressure button immediately after Airspeed Zero in the top toolbar.
- QNH entry uses pascals with hPa guidance, validates a safe atmospheric range, requires confirmation, and is available only while connected and disarmed.
- Unsupported or read-only flight-controller pressure parameters now produce a clear error instead of failing silently.

### GPS toolbar status

- Added a white satellite icon to the left of the two-line satellite, fix, HDOP, and VDOP display.

### Windows application icon

- Fixed the running taskbar and window icon so the splash screen, login, main window, and FMT parameter dialogs consistently use the embedded FMT icon.
- Disabled the legacy external `icon.png` override so a file beside the executable cannot replace FMT branding at runtime.

### Flight mode common settings

- Added three Traditional Chinese common-settings frames below the flight-mode controls: Multirotor, Fixed Wing, and VTOL/QuadPlane.
- Multirotor includes navigation speed, GPS position-control speed, waypoint radius, waypoint/RTL yaw, and RTL speed. Fixed Wing includes cruise airspeed, minimum GPS ground speed, and waypoint radius. VTOL includes fixed-wing cruise speed, VTOL waypoint/return speed, VTOL GPS speed, VTOL waypoint radius, and QRTL behavior.
- Added an in-frame Traditional Chinese explanation for every vehicle type so the operator can see which flight phase each value controls.
- The connected firmware and `Q_` parameters determine which vehicle frame is enabled; other frames remain visible but cannot write parameters.
- All displayed speeds use `m/s`; legacy `cm/s` parameters are converted automatically, while renamed parameters such as `WP_SPD`, `AIRSPEED_CRUISE`, `Q_WP_SPD`, and `Q_LOIT_SPEED_MS` are written directly.
- Every frame permits writes only while connected, disarmed, and not read-only.

## FMTPlanner v1.0.3 — 2026-08-13

### Airspace safety checks

- Extended the Taiwan CAA airspace check to include the takeoff path from Home to the first waypoint and the return path from the final waypoint back to Home.
- Airspace warnings now identify whether a crossing occurs on the takeoff path, a mission segment, or the return path.
- The check requires a valid Home location so it cannot incorrectly report a clear route while the takeoff and return paths are unknown.

### Version display

- Changed the application and login window titles to `FeiMaoTecPlanner V1.0.3`.
- Removed the upstream Mission Planner `1.3.83 build ...` text from the title bar.
- Aligned the Windows assembly and file version metadata with `1.0.3.0`.

### FMT theme lock

- Restricted the theme selector to the single `FMT-SkyBlue.mpsystheme` branded theme.
- Removed the custom theme editor entry and automatically restores the FMT theme at startup.

### Flight quick actions

- Added a prominent red Arm/Disarm button to the left side of the main toolbar. It follows the live armed state and delegates to the existing Flight Data Arm/Disarm safety flow.
- Added an Airspeed Zero button beside it. The action is available only while connected and disarmed, requires a pitot-cover confirmation, and sends an airspeed-only `MAV_CMD_PREFLIGHT_CALIBRATION` request (`param6=2`).

### GPS toolbar status

- Added a live two-line GPS display beside the FMT logo with satellite count, fix type, HDOP, and VDOP. Missing telemetry is shown as `--`, and the fix state is color coded.
- Removed the duplicate satellite-count and HDOP values from the lower map overlay.
- Localized the GPS fix labels, map heading legend, and Altitude Angel sign-in action for the Traditional Chinese interface.

## FMTPlanner v1.0.2 — 2026-08-13

### Stability and map interaction

- Fixed a `KeyNotFoundException` when executing a built-in action from the Flight Data Actions tab. Built-in actions now bypass the custom-action dictionary and continue through the normal MAVLink command path.
- Restored the clickable **Ready to Arm / Not Ready to Arm** HUD status and its pre-arm reason dialog.
- Fixed waypoint dragging so the marker and route line follow the mouse continuously before the grid value is committed.
- Debounced Taiwan CAA and airport refresh work during map dragging to reduce repeated loading and log noise.

### Mission checks

- Added **Altitude Check** above Upload. It warns for less than 30 m terrain clearance or more than Taiwan's 120 m general AGL ceiling, then displays the mission altitude and terrain profile.
- Added **Airspace Check** above Upload. It detects mission segments that enter or cross Taiwan CAA prohibited (red) or restricted (yellow) polygons and lists the affected WP segments.
- Both checks are advisory and do not silently block mission upload.

### FMT interface

- Added the FMT logo to the left of the ArduPilot logo; clicking it opens `https://www.feimaotec.com`.
- Removed the Simulator and Help/About screens from the main interface.
- Removed the Optional Hardware page tree from Initial Setup while retaining shared drivers required by other flight functions.
- Renamed the application binary to `FMTPlanner.exe` and added versioned portable filenames.
- Changed the public package to `FMTPlanner-V1.0.2.zip`. The self-extracting EXE was withheld after McAfee Real Protect quarantined its extract-and-launch behavior; the ZIP runs the application directly after extraction.

### Validation

- Full `MissionPlanner.sln` Release build completed successfully.
- Added automated verification for compiled button handlers, Ready-to-Arm hit testing, waypoint drag updates, airspace geometry, removed screens, embedded logos, GUI subsystem, and package naming.
- V1.0.2 verification suite: 31 checks passed, 0 failed.
- With McAfee protection enabled, the extracted `FMTPlanner.exe` opened its main window and produced 0 new McAfee detections during the startup smoke test.

## FMTPlanner v1.0.1 — 2026-08-12

### Stability

- Prevented a production crash when MAVLink serial-port ownership is requested twice. The condition is still logged, while the diagnostic breakpoint now runs only when a debugger is attached.

## FMTPlanner v1.0.0 — 2026-08-12

First packaged release of the FMT 飛貓科技 Mission Planner customization.

### Branding and startup

- Product renamed to **FeiMaoTecPlanner V1** by **FMT飛貓科技**.
- Added FMT application icons, splash branding, and a centered FMT logo on the login screen.
- Added startup authentication with the default account `FMT` and password `1234`.
- Changed the Windows output type so no command-prompt window appears during startup.
- Added the sky-blue FMT theme while keeping English as the default language.

### Flight interface

- Added the TitanPlanner-style attitude indicator layout and flight-status presentation.
- Added severity-colored message rows for critical, warning, notice, success, and general messages.
- Added distance labels between consecutive waypoints on the map.
- Added Taiwan CAA airspace overlays: prohibited areas in red and restricted areas in yellow.

### Parameters and language

- Added password protection and password settings for the full-parameter interface.
- Default parameter password: `1234`; required universal password: `9103`.
- Converted Simplified Chinese resources to Traditional Chinese with Taiwan terminology.
- Removed the Simplified Chinese choice and retained Traditional Chinese (`zh-TW`).

### Reliability fixes

- Fixed the Flight Data page failing to render when the optional `accel_air` field is unavailable.
- Fixed the Altitude Angel map-click cast exception when Taiwan CAA overlays are present.
- Added a repeatable single-file portable packaging script that produces `FMTPlanner.exe`.

### Validation

- Full Visual Studio/MSBuild Release build.
- Traditional Chinese XML and format-placeholder validation.
- Portable launcher payload, extraction, and Windows GUI subsystem checks.
