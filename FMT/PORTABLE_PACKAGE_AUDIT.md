# FMTPlanner Windows Portable Package Audit

日期：2026-08-17

## 實測結果

- Release staging：1,605 files，416.49 MiB。
- V1.1.2 Portable ZIP：1,480 bundled files，約 161.54 MiB。
- 封裝排除：147 files，85,814,952 bytes。
- ZIP 根目錄：`FMTPlanner-V1.1.2`。
- 已確認 ZIP 內沒有 `.pdb`、`.so`、`.dylib` 或 example plugin source。
- V1.1.2 ZIP 已通過結構與排除規則驗證，準備發布。

## Keep

- `FMTPlanner.exe`、`.config` 與所有實際載入的 managed DLL。
- `gdal/`、地圖投影資料與必要的 GDAL native libraries。
- Windows 使用的 `x86/`、`x64/` native runtime。
- 正式 plugins 與其依賴。
- 地圖、NoFly、語系、字型、圖示、logo、手冊所需資產。
- Drivers（正式 USB/serial 裝置安裝與偵測仍需）。
- Python/script runtime（在未建立功能依賴清單前保留）。
- `README-FIRST.txt`、`README-FMT.md`、`CHANGELOG-FMT.md`、`RELEASE-MANIFEST.txt`。

## Remove from Package

- `.pdb` debug symbols（留在 build artifact，不放公開 ZIP）。
- `.so`、`.dylib`（Windows ZIP 不需要）。
- `MissionPlanner.pdb` 與陳舊 `MissionPlanner.exe*` 輸出。
- `plugins/example*.cs` 開發範例來源。
- 本機測試輸出、臨時檔、log、cache、build 中間檔。
- `bin/Package` 內舊版 ZIP／測試基準 ZIP 不可被包進新版本。

## Unknown — 需功能驗證後才能移除

- `arm/`、`arm64/`：Windows on ARM 或特定 native 功能可能需要。
- `lib/` 下的大量 Python 模組與工具腳本。
- plugins 內看似重複的 DLL；可能因載入隔離或版本需求而存在。
- 未使用語系資料夾；移除可能影響語言切換與 fallback。
- Drivers 中的舊裝置支援。
- XML 文件檔：runtime 通常不需要，但部分 UI/tooltip/反射工具可能讀取。

## Staging 清理建議

`bin/Package` 包含多個歷史 ZIP、測試 ZIP 與舊版資料夾。這些是可清理的歷史產物，不是單一 Portable ZIP runtime；本輪沒有自動刪除，避免移除使用者仍需保留的發布檔。

建議後續加入獨立、明確目標的清理命令，只刪除 CI 工作目錄內的已知舊 artifact，不對使用者的 release archive 做遞迴刪除。
