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

完成低風險清理、完整建置與相同自動量測後填入，並保留原始 JSON 輸出供比對。

| 項目 | V1.0.9 | V1.0.10 | 差異 |
|---|---:|---:|---:|
| 主畫面可用 | 7,061 ms | 待測 | 待測 |
| Working Set 平均 | 297.17 MiB | 待測 | 待測 |
| Private Bytes 平均 | 271.79 MiB | 待測 | 待測 |
| CPU 平均 | 0.171% | 待測 | 待測 |
| Threads 平均 | 67.8 | 待測 | 待測 |
| Handles 平均 | 1,480 | 待測 | 待測 |
| FMTPlanner.exe | 7.56 MiB | 待測 | 待測 |
| Portable package | 177.33 MiB | 待測 | 待測 |

## 量測限制

- 啟動時間會受磁碟快取、網路地圖與 GitHub 更新檢查回應時間影響，因此最終比較至少執行三次並使用中位數。
- CPU 數值是整部電腦 32 個邏輯處理器正規化後的程序使用率。
- GC 計數是程序啟動後的累計值，不代表每秒 GC 次數。
- 未連接飛控的數據不能用來宣稱 MAVLink、Serial、UDP、TCP 或各構型已完成實機驗證。
