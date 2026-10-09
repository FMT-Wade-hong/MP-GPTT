# FMTPlanner release history

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
