# FMTPlanner Dependency Upgrade Backlog

日期：2026-08-17

本清單只記錄，不在 Stable / Performance 小改版直接升級。

| 優先級 | 套件 / 項目 | 目前觀察 | 主要風險 | 建議獨立驗證 |
|---|---|---|---|---|
| High | SSH.NET 2020.0.2 | NU1903，高嚴重度弱點 | SCP/SSH、飛行記錄下載、遠端工具 | SCP 下載、認證、斷線重連、舊設備相容性 |
| High | SkiaSharp 2.80.2 | NU1903，高嚴重度弱點 | HUD、SVG、繪圖、地圖與原生 runtime | HUD/Map/圖示/列印、多架構 native assets |
| Medium | log4net 2.0.13 | NU1902，中嚴重度弱點 | 全域記錄、plugin 與 ExtLibs | 啟動、滾動檔案、錯誤回報、長時間記錄 |
| Medium | SharpCompress 0.29.0 | NU1902，中嚴重度弱點 | 韌體、log、封裝解壓縮 | 各壓縮格式、損毀檔、韌體下載/解壓 |
| Medium | .NET Framework / target graph | net461/net472/netstandard/netcoreapp3.1 混合 | 大量相容性面、部署環境 | 建置矩陣、所有硬體驅動、Win10/11 |
| Medium | System.Runtime.CompilerServices.Unsafe 6.1.2 | netcoreapp3.1 不受支援警告 | px4uploader runtime | PX4 韌體上傳、USB reconnect |
| Low | Microsoft.Windows.CsWin32 | 要求 0.3.268，實際解析 0.3.269 | WinUSB 產生碼差異 | WinUSB 裝置枚舉與連線 |
| Low/Platform | Mono.Posix | Windows build 無法解析參考 | Mono/Linux 特定路徑 | 若仍支援 Mono，於獨立 CI 驗證 |

## 建議升級順序

1. 先建立硬體與封裝回歸測試矩陣。
2. 每次只升級一個高風險套件並產生獨立 branch/PR。
3. 優先 SSH.NET，再處理 log4net/SharpCompress；SkiaSharp 因 native assets 與 UI 影響最大，獨立進行。
4. Framework migration 最後進行，不與功能版混合。

## 驗收門檻

- Debug / Release / Portable ZIP 均可建立。
- ArduCopter、ArduPlane、QuadPlane 連線與重連。
- Parameter/Mission/TLOG/DataFlash/Map/HUD/Video/Plugin/韌體流程回歸。
- 2～4 小時 soak test 無 RAM、Handle、GDI 單向成長。
