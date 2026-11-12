# FeiMaoTecPlanner V1.2.5

這次修正參數修改與寫入、數值精度、VTOL 兩排按鈕縮放，以及分離視窗與 COM 查詢的穩定性問題。

主要更新：

- 修正選配參數缺失造成進階調校整頁修改不變色、不進入待寫入清單；寫入後另行讀回，確認一致才清除修改標示，失敗保留修改並列出差異。
- 參數數值及預設值改為一般小數顯示，例如 `1E-05` 顯示為 `0.00001`；編輯與顯示保留原值精度。
- 修正 32 位遮罩運算及整數參數讀回捨入；無法精確透過 REAL32 傳輸的整數會停止送出並顯示原因。
- VTOL 上下兩排模式共用六欄，對應按鈕同寬、同高、同字型並對齊，維持無捲軸縮放。
- 修正分離儀表欄位綁定與殘留視窗的關閉例外，防止重複分離。
- COM 名稱查詢改為背景執行，修正埠名截斷、無效項目及慢速列舉卡住；保留上次有效結果。

驗證：Release 編譯 0 errors（1,379 warnings）、66 項離線回歸全部通過；10 分鐘連續測試完成 GDI／OpenGL 各 8,477 幀及 85 次 ICE 頁面生命週期，資源留存未超過門檻。詳細紀錄見發布驗證。

尚未以實體飛控、實飛或特定藍牙轉接器驗證；不宣稱已排除所有不定時閃退。本版未新增 MAVFTP 參數上傳功能。

下載 `FMTPlanner-V1.2.5.zip`，完整解壓後執行 `FMTPlanner-V1.2.5.exe`。旁附 SHA-256 校驗檔。

[操作手冊](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.2.5/README-FMT.md) · [完整更新紀錄](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.2.5/CHANGELOG-FMT.md) · [測試與資源紀錄](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.2.5/FMT/RELEASE-AUDIT-V1.2.5.md)
