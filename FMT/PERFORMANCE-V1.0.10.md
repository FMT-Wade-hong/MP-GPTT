# FMTPlanner V1.0.10 效能與穩定性量測

本文件使用同一套 Release 建置、登入與取樣方式比較 V1.0.9 與 V1.0.10。數值不是以操作感覺判斷；自動量測腳本為 `FMT/Measure-FMTPlannerPerformance.ps1`。

## 測試環境

- 量測日期：2026-08-15
- 作業系統：Microsoft Windows 11 家用版 10.0.26200
- 處理器：AMD Ryzen 9 8940HX with Radeon Graphics
- 邏輯處理器：32
- 實體記憶體：33,517,666,304 bytes（約 31.22 GiB）
- 組態：Release / Any CPU / .NET Framework 4.6.1 主程式
- 顯示情境：登入後的主 Flight Data 畫面，HUD 與地圖同時顯示，未連接飛控
- 取樣方式：主畫面就緒後暖機 8 秒，再每秒取樣一次，共 10 秒

## V1.0.9 Baseline

| 項目 | V1.0.9 基準值 | 備註 |
|---|---:|---|
| 登入視窗可見 | 1,200 ms | 從啟動程序到登入視窗被 UI Automation 偵測 |
| 主畫面可用 | 7,061 ms | 自動輸入預設密碼後，主視窗及主要控制項建立完成 |
| Idle RAM（Working Set 平均） | 311,604,429 bytes（297.17 MiB） | HUD 與地圖顯示、未連線 |
| Idle RAM（Working Set 峰值） | 312,340,480 bytes（297.87 MiB） | 10 秒取樣 |
| Private Bytes 平均 | 284,998,042 bytes（271.79 MiB） | 10 秒取樣 |
| Idle CPU 平均 | 0.171% | 依 32 個邏輯處理器正規化 |
| Idle CPU 峰值 | 0.293% | 10 秒取樣 |
| Thread 數量 | 67.8 | 平均 |
| Handle 數量 | 1,480 | 平均 |
| Gen 0 GC | 60 | 程序啟動後累計 |
| Gen 1 GC | 12 | 程序啟動後累計 |
| Gen 2 GC | 4 | 程序啟動後累計 |
| FMTPlanner.exe | 7,927,808 bytes（7.56 MiB） | Release build |
| Release 輸出 | 432,528,436 bytes / 1,603 files | `bin/Release/net461` |
| Plugin 輸出 | 91,502,424 bytes / 293 files | `bin/Release/net461/plugins`，含相依檔 |
| Portable package | 185,939,469 bytes（177.33 MiB） | 1,613 bundled files |
| Package SHA-256 | `9c4f2f778c1ddea5aab481d6eec7f03857867b3c296c3066aec6caae75a7847f` | 本機基準封裝 |

## 連線情境

下列項目必須搭配實機或明確的可重複模擬飛控環境，不能以未連線主畫面數據代替：

| 項目 | V1.0.9 | V1.0.10 |
|---|---:|---:|
| USB Serial 連線後 RAM / CPU | 待實機測試 | 待實機測試 |
| UDP 連線後 RAM / CPU | 待測試端點 | 待測試端點 |
| TCP 連線後 RAM / CPU | 待測試端點 | 待測試端點 |
| ArduCopter HUD / Map | 待實機或 SITL | 待實機或 SITL |
| ArduPlane HUD / Map | 待實機或 SITL | 待實機或 SITL |
| QuadPlane HUD / Map | 待實機或 SITL | 待實機或 SITL |

## V1.0.10 結果

以下數據取自最終 Portable ZIP 解壓後的執行檔。共執行三次冷啟動、自動登入、暖機 8 秒及取樣 10 秒，表格採三次中位數；原始 JSON 保留於 `bin/Performance/v110-run1.json` 至 `v110-run3.json`。

| 項目 | V1.0.9 | V1.0.10 | 差異 |
|---|---:|---:|---:|
| 登入視窗可見 | 1,200 ms | 1,746 ms | +45.5%（受冷啟動與磁碟快取影響） |
| 主畫面可用 | 7,061 ms | 4,958 ms | -29.8% |
| Working Set 平均 | 297.17 MiB | 290.12 MiB | -2.4% |
| Working Set 峰值 | 297.87 MiB | 290.64 MiB | -2.4% |
| Private Bytes 平均 | 271.79 MiB | 266.51 MiB | -1.9% |
| CPU 平均 | 0.171% | 0.190% | +0.019 個百分點（低負載取樣誤差範圍） |
| CPU 峰值 | 0.293% | 0.586% | +0.293 個百分點（瞬間取樣） |
| Threads 平均 | 67.8 | 67.4 | -0.6% |
| Handles 平均 | 1,480 | 1,438.2 | -2.8% |
| Gen 0 / 1 / 2 GC | 60 / 12 / 4 | 48 / 11 / 3 | 均下降 |
| FMTPlanner.exe | 7,927,808 bytes | 7,930,368 bytes | +2,560 bytes |
| Portable package | 185,939,469 bytes（177.33 MiB） | 146,769,092 bytes（139.97 MiB） | -21.1% |
| Package files | 1,613 | 1,467 | -146 |

> 上表的執行檔與封裝大小來自效能量測候選版。正式版其後加入 GitHub 錯誤回報隱私修正與新版圖文手冊，因此最終檔案大小會不同；正式 Release 資產的 SHA-256 以 GitHub 發布頁記錄為準。

三次主畫面可用時間分別為 8,600、4,650、4,958 ms。第一次受到解壓後首次執行、磁碟快取與更新檢查等因素影響，因此依預先定義的方法採中位數，不只挑選最快的一次。

## 量測限制

- 啟動時間會受磁碟快取、網路地圖與 GitHub 更新檢查回應時間影響，因此最終比較至少執行三次並使用中位數。
- CPU 數值是整部電腦 32 個邏輯處理器正規化後的程序使用率。
- GC 計數是程序啟動後的累計值，不代表每秒 GC 次數。
- 未連接飛控的數據不能用來宣稱 MAVLink、Serial、UDP、TCP 或各構型已完成實機驗證。
