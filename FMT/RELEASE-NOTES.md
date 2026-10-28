# FeiMaoTecPlanner V1.2.4

這次集中修正長時間操作的繪圖資源、ICE 參數寫入，以及 ArduPilot 4.6／4.7 參數相容性。

主要更新：

- HUD Bitmap／Graphics／字型資源釋放與有容量上限的本機診斷紀錄。
- ICE／起飛參數作業互斥、離頁及斷線取消後續重試、寫入後讀回確認；修正失敗後通訊卡住。
- 模式列按鈕與文字隨視窗縮放，取消水平／垂直捲軸。
- 31 組官方 4.6／4.7 參數文件，修正 PID、起飛、直升機、OSD 與數值精度。
- 雙 GPS 狀態與警示顏色、馬達測試防連點、adsb.fi 兩秒更新與台灣／100 海里範圍限制。
- 繁體中文資源及參數說明檢查；參數代碼、原文與原始數值保留。

驗證：Release 編譯 0 errors（1,439 warnings）、62 項離線回歸全部通過；10 分鐘連續測試中 GDI／OpenGL 各 8,659 幀，87 次 ICE 頁生命週期，資源留存未超過門檻。未實機／實飛驗證，不宣稱已排除所有不定時閃退。

下載 `FMTPlanner-V1.2.4.zip`，完整解壓後執行 `FMTPlanner-V1.2.4.exe`。旁附 SHA-256 校驗檔。

[操作手冊](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.2.4/README-FMT.md) · [完整更新紀錄](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.2.4/CHANGELOG-FMT.md) · [測試與資源紀錄](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.2.4/FMT/RELEASE-AUDIT-V1.2.4.md)
