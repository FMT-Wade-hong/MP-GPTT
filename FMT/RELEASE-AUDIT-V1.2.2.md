# FeiMaoTecPlanner V1.2.2 發布稽核

2026-10-10 使用者已授權正式發布 V1.2.2。發布建置：`bin/Release122Final/net461`，套件：`FMTPlanner-V1.2.2.zip`。以下保留先前本機開發稽核，各段「未發布」指當時狀態；發布並不代表已完成實機／實飛認證。私人參數檔、操作紀錄及本機設定不得納入公開套件。

## 2026-10-10 參數輸入與整頁排版修正

正式發布建置結果：`bin/Release122Final/net461` 建置成功，0 錯誤、1390 警告；在此輸出上執行 `Test-Local122.ps1`，30 組頂層離線回歸全部通過。套件由此輸出重新製作，不沿用早期 Local.zip。

最新時間同步建置：`bin/TimeSync122/net461/FMTPlanner.exe`，0 錯誤、1284 警告。使用者明確授權連線完成後單次 SYSTEM_TIME；這不是 PARAM_SET。沿用手動系統時間訊息語意（UTC Unix 微秒、time_boot_ms=0），每次 doConnect 成功後排程一次；斷線事件／新連線取消舊世代，唯讀、回放、目標切換不送出，忙碌時背景等待。送出失敗只寫本機程式日誌、不重送；無 ACK，不能宣稱飛控已採用。Test-ConnectionTimeSync 離線驗證 UTC 換算、非 UTC 拒絕、單次排程、舊世代取消與重連重置，並檢查背景／通訊護欄。沒有實機寫入或發布。

最新空速歸零修正建置：`bin/AirspeedFix122/net461/FMTPlanner.exe`，0 錯誤、1284 警告。原 UI 同步 doCommand 等待校正 ACK，25 秒逾時並重試一次，足以造成畫面長時間無回應。現改 Task.Run / doCommandAsync 保留既有校正參數與 transport ACK 語意；捕捉目標、送出前重驗狀態、執行中禁止重按，例外釋放通訊占用。新增 Test-AirspeedZeroAsync 檢查已編譯 async 狀態機與相關程式結構。未以實機或模擬 MAVLink ACK 延遲驗證，不代表飛控校正保證成功。

後續圖示建置：`bin/DefaultIcons122/net461/FMTPlanner.exe`，0 錯誤、1284 警告。新增船預設透明木船藍帆圖與飛貓固定翼／VTOL 預設預覽；`Test-MapIcons.ps1` 驗證透明角落、預覽存在、預設控制項鎖定及自訂控制項恢復。原有自訂偏好不自動清除；選擇船後「恢復此類型預設」再「套用並儲存」可套用新預設。

船資源：`Resources/FMTMapBoat.png`。使用 imagegen 內建編輯模式，依使用者附件製作透明背景版本（非逐像素原圖）；提示重點：移除棋盤背景、保留木船／金色邊緣／兩面藍帆、船頭朝上、全船置中、無背景陰影與新增物件。固定翼繼續引用既有 `FMTMapFixedWing.png`，未重繪。

- 最新建置：`bin/ParamUiFix122/net461/FMTPlanner.exe`，包含下列較早修正。
- 本次編譯 0 錯誤、1284 警告；`Test-Local122.ps1` 共 28 組頂層離線回歸全部通過，並檢視整頁預覽。不是實機／實飛認證。
- 共用 MavlinkNumericUpDown：缺少參數範圍不再建立零寬範圍或將目前值當成上限；有 metadata 時，範圍與步進依顯示比例換算，呼叫端既有顯示單位範圍保留。缺少範圍時不代表所有輸入均安全或飛控均接受。
- ACC P 在舊值 0.3 時鍵入 0.4 的離線測試通過；尚未以實機確認該飛控接受寫入。
- HOVER 下方整排下移，Filter Logs／Harmonic Notch 不重疊；RC6–RC10 及 TUNE 不跨越群組邊框。
- 更新期間只鎖編輯控制項，唯讀說明維持啟用與原色、可捲動；完成時恢復原狀態。
- Copter、Plane、Rover、Ateryx、AntennaTracker 調參入口先提交編輯文字，再檢查 setParam 回傳值；失敗不標示為成功、不清除待寫入修改。
- 未發布、未替換執行中程式，也沒有重新打包舊 ZIP。

## 2026-10-10 完整載入補救修正（取代下方舊的全表限制）

- 最新建置為 `bin/FullReload122/net461/FMTPlanner.exe`，保留 HOVER 與 WPNav m/s 修改。
- Loading 頁及「參數設定」的完整參數表，按鈕明確重新載入全部參數，沿用背景進度／取消介面；不再僅檢查狀態或只讀可見列。
- PID、ICE、TAKEOFF、LAND 等頁仍只讀取當頁；全量與當頁更新共用工作閘門，飛控解鎖、下載 LOG 或初始參數載入進行中不重複啟動。
- 全量載入取消／失敗時不清除待寫入修改；初始快取為零時不判定為完成。沒有修改飛控參數或 CPU 設定；實機驗證仍待執行。

## 核對範圍

- 參數更新修正建置：`bin/PageRefresh122/net461/FMTPlanner.exe`。設定頁不再呼叫 getParamList；全參數表按鈕標示「更新畫面顯示列」，F5 使用當頁按鈕。ICE／TAKEOFF／LAND 讀取當頁篩選結果，OSD 僅目前分頁。
- 共用更新以 300 ms 間隔順序讀取、背景執行及單一工作閘門；離頁／斷線／切換機體停止後續請求，逾時停止並保留原畫面，解鎖或通訊忙碌時不開始。已送出的單筆仍須等底層回覆或有限次重試結束。
- 啟用電池／圍籬、寫入後參數數量變更不再自動重新下載整表；初始載入狀態頁按鈕只檢查進度。新增的隱藏參數需安全停機後重新連線完成初始載入。
- 此修正不更改正常連線時的初始參數載入，亦不保證降低韌體自身 CPU 負載；仍需實機封包及負載驗證。舊 `bin/Package` ZIP 未重新打包，不含此修正。

- 版本來源：AssemblyInfo、專案 Version、FMT/VERSION、執行時標題、更新判斷及錯誤回報。
- 連線控制來源只讀 RC_OPTIONS；移除啟動預設寫入。背景取得參數只使用讀取請求。
- GCS ID 改為驗證而非自動改寫；不符時封鎖地面站搖桿，使用者須斷線後自行確認。
- 新連線清除前次待處理的延遲參數儲存狀態。
- 設定頁、密碼連動、PTCH2SRV_RLL、ICE／起飛／降落、地圖、ELRS、接力及相關離線回歸測試。

## 限制與實機驗證項目

- 本版是未發布的本機候選建置；自動測試不代表實機或實飛驗證。
- 傳統「遙控器控制」按鈕仍以 RC_OPTIONS 隔離 MAVLink RC Override，僅適用傳統 SBUS／CRSF；ELRS MAVLink 請保留原設定，不使用此隔離方式。已增加明確警告。
- 開啟 USB 連線重置仍會操作 DTR／RTS；資料串流請求可能改變通訊速率，並非參數寫入。使用者手動啟用的功能不等同純連線唯讀模式。
- 無法保證所有第三方插件、每個設定頁載入事件及所有硬體皆無副作用；需封包擷取確認重連未送 PARAM_SET／PARAM_EXT_SET 與非預期儲存命令。
- Pixhawk 6C / 4.7.0 在下載 LOG 時回報 main_loop_stk 的原因尚未確定，需要當次 BIN 與完整韌體版本，不視為已修復。
- 部分新版改名參數未涵蓋於精簡設定頁，請使用與韌體相符的完整參數；未回傳的參數不寫入。
- 建置警告仍需後續分類，不宣稱零警告。沒有修改飛控保護、ARMING_CHECK 或 SCHED_LOOP_RATE。

## 本次參數更新修正驗證

- `Test-Local122.ps1` 的 24 組頂層回歸全部通過，另執行 `Test-MapIcons.ps1` 通過；沒有連接飛控。
- `Test-PageParameterRefresh.ps1` 驗證名稱綁定／去重、可見列與捲動、OSD 不連續頁碼、非同步節流、取消、切換目標及首次失敗後不再讀取；靜態檢查設定頁零整表下載、F5 範圍、只讀與 LOG 忙碌保護。
- 使用 `bin/PageRefresh122/net461/FMTPlanner.exe`。沒有替換目前執行中的程式，也沒有上傳 GitHub 或重新打包舊 ZIP。

## 前次本機交付（不含本次修正）

- 建置結果：0 錯誤、1390 警告。
- Test-Local122.ps1 共 23 組頂層回歸腳本通過，含版本一致性、ELRS、ICE、起飛／降落、RC 來源只讀、密碼、設定頁、訊息、圖示與關閉清理。
- 最新測試可用 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File FMT/Test-Local122.ps1 -Directory bin/PageRefresh122/net461` 重跑。

- 建置目錄：bin/Release122/net461。
- 本機套件：bin/Package/FMTPlanner-V1.2.2-Local.zip（完成套件核對後使用）。
- 私人參數、登入設定、LOG、憑證、測試預覽不得納入套件。
