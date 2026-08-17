# FMTPlanner Stable / Performance 稽核報告

日期：2026-08-17
範圍：FMTPlanner V1.1.2 發布候選版本

## 結論

本輪只處理低風險的穩定性與重繪效率問題，未新增功能、未改 MAVLink 架構、未重構 MainV2，也未刪除 Mission Planner 核心功能。

已完成的核心改善：

- Flight Data 的資料繫結只由既有主更新迴圈驅動；控制項重繪事件不再額外觸發整批資料更新。
- Flight Data 非同步 UI 更新加入可靠的閘門釋放，避免關閉頁面或單次例外後永久停止更新。
- Flight Data 在 Dispose 時先停止背景工作，再解除長生命週期事件，避免已關閉頁面仍被靜態事件引用。
- AUTO 任務面板一般更新限制為 500 ms，跨執行緒更新會合併，避免堆積 BeginInvoke。
- AUTO 任務面板在父頁關閉期間若 BeginInvoke 失敗，會清除合併旗標，不會永久拒絕後續更新。
- AUTO 任務列資料變更後只排定一次版面重繪，不再同步 Update 每個子控制項。
- AUTO 任務快照使用任務內容簽章，不只比較數量；簽章包含 Sequence、Command、Lat、Lon、Alt、Param1～4 與 Frame。
- 任務進度以任務清單中的索引計算，不直接拿 MAVLink sequence 除以總數。
- Plugin Loader 保留既有一次性初始化與掃描快取；載入例外改為寫入 log，不再完全吞掉。

## Bugs Found

### P0

- 未發現可由目前無飛控環境重現的 P0 問題。

### P1

1. Flight Data UI 更新閘門可能永久卡住
   - 原因：BeginInvoke 失敗、頁面 Dispose 或資料繫結工作丟出例外時，排程計數不一定歸零。
   - 影響：長時間運行後 Flight Data 可能不再更新，必須切頁或重開。

2. Flight Data 關閉生命週期不完整
   - 原因：Dispose 先呼叫 base，且部分 POI、NoFly、Camera、ParamListChanged 事件只在 FormClosing 路徑解除。
   - 影響：非標準關閉路徑可能留下事件引用或背景工作。

### P2

1. 控制項 Invalidated 事件繞過 100 ms 節流，直接執行整批資料繫結。
2. AUTO 任務面板從 100 ms Flight Data 更新路徑被呼叫，超過規格要求的 500 ms。
3. AUTO 任務內容變更時逐一 Invalidate/Update 子控制項，增加 UI thread 尖峰與白屏閃爍風險。
4. Plugin 載入與 AssemblyResolve 例外被吞掉，不利於診斷背景載入問題。

### P3

1. 既有專案包含大量重複 resx 項目、舊框架警告與未使用成員警告。
2. Release staging 目錄累積多個歷史 ZIP／資料夾，佔用約 2.21 GiB，但不影響單次執行。

## Bugs Fixed

| 問題 | 修改檔案 | 修改方式 | 驗證 |
|---|---|---|---|
| Invalidated 繞過主節流 | `GCSViews/FlightData.cs` | 移除 OnInvalidated 對整批 binding work 的直接呼叫 | Debug/Release build 通過 |
| UI 更新閘門可能卡死 | `GCSViews/FlightData.cs` | BeginInvoke、Dispose 與工作例外均以 finally 釋放更新槽位 | 編譯與啟動煙霧測試通過 |
| Flight Data 事件殘留 | `GCSViews/FlightData.cs` | Dispose 先停止執行緒並解除 Param/POI/NoFly/Camera 事件，再釋放 Control | 編譯通過；硬體長測待驗證 |
| AUTO 更新過密 | `FMT/FmtAutoMissionPanel.cs` | NORMAL 更新限制 500 ms；跨執行緒請求合併 | 靜態路徑稽核、編譯通過 |
| AUTO 重繪過量 | `FMT/FmtAutoMissionPanel.cs` | 移除逐一同步 Update，只保留一次 layout invalidate | 靜態路徑稽核、編譯通過 |
| Mission 數量相同但內容改變未更新 | `FMT/FmtAutoMissionPanel.cs` | 使用完整內容簽章判斷 | 程式碼稽核、編譯通過 |
| Mission sequence 缺號造成進度錯誤 | `FMT/FmtAutoMissionPanel.cs` | 以排序後 missionItems 的 current index 計算 | 程式碼稽核、編譯通過 |
| Plugin 例外無紀錄 | `Plugin/PluginLoader.cs` | Assembly resolve 以 Debug、plugin load 以 Warn 紀錄 | 編譯通過 |

## 更新節奏稽核

| 區域 | 目前節奏 | 判定 | 本輪處理 |
|---|---:|---|---|
| Flight Data binding / HUD 必要資訊 | 100 ms / 10 Hz | 必要 | 保留既有主迴圈，移除額外重入路徑 |
| Tuning graph（開啟時） | 約 75 ms | 特定功能才啟用 | 本輪不改，避免影響即時調校 |
| Map connected | 約 300 ms | 已節流 | 保留 |
| Map disconnected | 約 2000 ms | 合理 | 保留 |
| AUTO mission panel | 500 ms / 2 Hz | 符合 NORMAL | 本輪加入限制與合併排程 |
| Route rebuild | 約 5000 ms | 合理 | 保留 |
| Airspace refresh | 約 5000 ms | 合理 | 保留 |
| Transponder | 約 5000 ms | 合理 | 保留 |

沒有新增永久 Timer。FAST/NORMAL 都沿用 Flight Data 既有更新路徑。

## 全專案靜態搜尋摘要

以下為排除 `ExtLibs/bin/obj` 後的核心 C# 原始碼命中數。命中不等於錯誤；Designer、一次性對話框、背景傳輸與測試工具也會包含在內，必須依呼叫路徑判斷，不能批次刪除。

| 類型 | 命中數 | 本輪判定 |
|---|---:|---|
| `System.Windows.Forms.Timer` | 76 | 多為既有頁面／工具；Flight Data 主路徑未新增 timer |
| `System.Threading.Timer` | 2 | 既有背景用途，未發現本輪新增重複實例 |
| `Task.Delay` | 15 | 多為非 UI 長流程；保留 |
| `Thread.Sleep` | 180 | legacy 通訊／工具路徑很多；不可直接改寫 |
| `Invalidate()` | 135 | Flight Data/AUTO 高頻路徑已收斂 |
| `Refresh()` | 65 | 多為操作完成後的顯式更新；未批次修改 |
| `.Update()` | 9 | AUTO 子控制項同步 Update 已移除 |
| `BeginInvoke()` | 89 | AUTO 一般更新已合併；Flight Data 閘門補強 |
| `Invoke()` | 158 | 大量 legacy UI marshal；需逐頁 profiler 才能安全降載 |
| `Directory.GetFiles` / `EnumerateFiles` | 43 | Plugin 掃描已有快取；未發現 Flight Data telemetry loop 掃目錄 |
| `SearchOption.AllDirectories` | 18 | 工具／掃描功能為主；未放入高頻 Flight Data 路徑 |
| `File.ReadAllText` / `ReadAllBytes` | 16 | 未發現新增於 telemetry loop |
| `Image.FromFile` | 28 | 多為一次性功能；vehicle/logo 高頻路徑未新增磁碟讀取 |
| `new Bitmap` / `Graphics.FromImage` | 67 | 繪圖功能廣泛使用；本輪不做高風險全域替換 |
| `new Thread` / `Task.Run` | 47 | legacy 背景工作；Flight Data 關閉路徑已補強 |
| `while (true)` | 11 | 通訊／工具迴圈需個別驗證 exit condition，列為後續稽核 |

優先人工檢查位置為 `GCSViews/FlightData.cs`、`FMT/FmtAutoMissionPanel.cs`、`FMT/FmtFlightModeBar.cs`、`Plugin/PluginLoader.cs` 與 `MainV2.cs`。未發現 Flight Data 高頻迴圈重新讀取圖檔或遞迴掃描目錄。

## Plugin Loader 稽核

- AssemblyResolve：使用一次性旗標，每個 Process 只註冊一次。
- LoadAll：使用一次性旗標，不重複執行。
- 掃描：assembly 檔案清單有鎖與快取。
- `.cs` 編譯：沒有原始碼 plugin 時不啟動背景編譯。
- Runner：MainV2 會先檢查 `RequiresRunner`；沒有 plugin 時不建立永久 runner。
- 例外：本輪補上 log。

## 效能與資源量測

目前工作樹在本輪開始前已包含大量尚未提交的 UI／功能修改，因此沒有可重現、同一程式版本的修改前二進位基準。為避免製造不實數據，下表只列可比資料與本輪實測；Connected 欄位必須等實機。

| 指標 | Before | After / 本輪實測 |
|---|---:|---:|
| Startup | 無同版基準 | 登入視窗 0.657 s；主視窗 3.925 s 完成載入 |
| RAM Idle（Working Set） | 無同版基準 | 平均 289.46 MiB；峰值 290.57 MiB |
| Private RAM Idle | 無同版基準 | 平均 267.72 MiB |
| CPU Idle | 無同版基準 | 平均 0.244%；峰值 0.488%（32 logical processors） |
| Threads Idle | 無同版基準 | 平均 66.4 |
| Handles Idle | 無同版基準 | 平均 1,439 |
| .NET GC | 無同版基準 | Gen0 52／Gen1 14／Gen2 5 次 |
| RAM/CPU/Threads/Handles/GDI Connected | 未量測 | 無飛控，待實機 |
| Release staging | 416.49 MiB / 1,605 files | 不直接作為發行 ZIP |
| Portable ZIP | 既有 V1.1.1 約 131.82 MiB | V1.1.2 候選 ZIP 161.54 MiB / 1,480 bundled files |

可確定的更新量改善：

- AUTO 一般資料評估由最多 10 Hz 降至 2 Hz，理論上減少 80% 一般更新呼叫。
- 每次 AUTO 顯示變更由「多個子控制項同步 Update」改為「一次排程重繪」，移除超過 90% 的顯式同步重畫呼叫。
- 控制項重繪事件不再觸發額外整批 Flight Data binding pass。

## Build / Test

- Release：Visual Studio 2022 MSBuild 全方案成功，0 errors。
- .NET Framework 4.7.2 專案建置：成功，0 errors、18 warnings（既有相依套件警告）。
- 測試專案命令可正常結束，但目前沒有可被測試執行器探索的自動測試案例，因此不宣稱單元測試通過。
- 啟動煙霧測試：成功；自動登入後主視窗標題為 `FeiMaoTecPlanner V1.1.2`，10 秒取樣期間持續回應。
- Portable ZIP：成功；1,480 bundled files、161.54 MiB；排除 147 files／85,814,952 bytes。
- Portable 結構：單一 `FMTPlanner-V1.1.2` 根目錄；確認無 `.pdb`、`.so`、`.dylib` 與 example plugin source。

## Build Warning 分類

### A — FMT 新增程式造成

- 本輪新增修改未產生 build error。
- 部分既有 FMT async/unused 警告需逐檔確認語意後再處理，不宜本輪批次改寫。

### B — 可能影響 runtime 穩定性

- `Mono.Posix` 參考無法解析：Windows 主流程通常不使用，但 Mono/特定工具路徑需另測。
- 多個 resx 存在重複名稱：可能造成部分本地化資源被忽略，需獨立進行資源檔去重與 UI 回歸測試。
- `BaseIntermediateOutputPath` 在 import 後修改：可能導致非預期增量建置結果，應移至 `Directory.Build.props` 的獨立建置維護版本處理。

### C — Mission Planner legacy

- 大量未使用欄位、重複 using、同步執行的 async 方法、舊 API 與 resx 警告。
- 本輪不為了清 warning 大規模改核心。

### D — 第三方 / ExtLibs

- NuGet 弱點與相依版本解析警告，詳見 `DEPENDENCY_UPGRADE_BACKLOG.md`。

## Remaining Risk

### Legacy

- MainV2、FlightData、Map、HUD 仍是大型 WinForms 生命週期架構；全面 CancellationToken 或中央排程重構不適合本輪。
- Tuning 75 ms 更新保留，需在真實使用情境以 CPU profiler 評估。

### Dependency

- log4net、SSH.NET、SharpCompress、SkiaSharp 有已知弱點；不可在穩定性小改版中直接批次升級。

### Hardware-only

- ArduCopter、ArduPlane、QuadPlane USB connect/disconnect/reconnect。
- Parameter/Mission download/upload、Set Current Mission、AUTO、RTL、Q mode/transition/VTOL land。
- TLOG、DataFlash、GPS、Battery、vehicle marker。
- USB 拔除、UDP 中斷、telemetry timeout、下載中斷。

### Long-duration

- 尚未完成 30 分鐘、1/2/4 小時連線 soak test。
- 正式標記 Stable 前應每 30 分鐘記錄 RAM、CPU、Threads、Handles、GDI，確認沒有單向成長。

## Git 狀態

本輪依使用者指示整合既有已驗證修改，準備建立 V1.1.2 發布 commit 與 `FMTPlanner-v1.1.2` 標籤。
