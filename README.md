# FeiMaoTecPlanner V1.1.6

<p align="center">
  <img src="https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.6/FMT/Assets/fmt-logo.png" alt="FMT 飛貓科技" width="420">
</p>

<p align="center">
  <strong>FMT 飛貓科技客製化 Mission Planner 地面站</strong><br>
  多旋翼、定翼機及 VTOL／QuadPlane 共用操作環境
</p>

<p align="center">
  <a href="https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.1.6/FMTPlanner-V1.1.6.zip"><strong>下載 FMTPlanner V1.1.6</strong></a>
  ·
  <a href="README-FMT.md">繁體中文圖文操作手冊</a>
  ·
  <a href="https://github.com/FMT-Wade-hong/MP-GPTT/releases/tag/FMTPlanner-v1.1.6">版本發布頁</a>
</p>

![Version](https://img.shields.io/badge/version-V1.1.6-22a9dc)
![Platform](https://img.shields.io/badge/platform-Windows-0078d4)
![Package](https://img.shields.io/badge/package-portable%20ZIP-35a853)
![Language](https://img.shields.io/badge/UI-English%20%7C%20繁體中文-f0ad00)

## 軟體資訊

| 項目 | 內容 |
|---|---|
| 軟體名稱 | **FeiMaoTecPlanner V1.1.6** |
| 執行檔 | **FMTPlanner-V1.1.6.exe** |
| 公司／品牌 | **FMT 飛貓科技** |
| 基礎專案 | ArduPilot Mission Planner |
| 支援系統 | Windows |
| 發布形式 | 解壓縮後即可執行的可攜式 ZIP |
| 支援構型 | 多旋翼、定翼機、VTOL／QuadPlane |
| 介面語言 | 預設英文，可使用繁體中文 |

## V1.1.6 主要更新

- 新增 P400 DATA／AT 設定與 PicoConfig 備用視窗，提供參數功能、選單及手冊預設值。
- S107 支援遮蔽密碼輸入；S102／S110 可設定鮑率與資料格式，須使用 CONFIG 強制 9600／8N1 AT 模式。
- 主／副頻率表各支援 50 筆讀寫，提供 CSV 匯入匯出、貼上及 10 個可命名保存的本機組合；寫入前備份，寫入後逐筆比對。
- 修正 SIK 頻率讀回及發射功率選單，設定頁改名「SIK 數傳設定」。
- 新增 H420 基礎參數，調整 Throttle Accel 數值輸入及導控控制切換介面。
- 更新五個工具列圖示、圖示大小及電池顯示，修正工具列遮蔽，移除 QNH 按鈕。

已完成 Release 編譯、離線／模擬測試與 ZIP 校驗；尚未完成實機端到端驗證。數傳設定、搖桿及接力控制須先在拆槳或無動力地面狀態驗證。

### P400 使用重點

1. 先中斷飛控遙測及其他占用序列埠的程式，再從「初始配置 → P400 數傳設定」進入。
2. 修改 S102／S110 時，用 CONFIG 強制進入 AT，選 9600／8N1 並勾選對應選項；返回 DATA 後才採用新值，連接端須同步設定。
3. 第三分頁選主表 ATP0 或副表 ATP1；本機保存組合不會寫入模組。模組寫入需明確確認，僅支援 S128=2、S238=1 且完整 50 筆。
4. 頻率寫入失敗可能已部分保存，不會自動回復；配對端不會同步變更。S107 遮蔽回覆只能確認指令接受，不能驗證密碼原文。

詳見 [P400 操作說明](FMT/P400-AT-README.md)、[H420 基礎參數說明](FMT/FrameParams/README.md)及[更新紀錄](CHANGELOG-FMT.md)。

## 主要功能

- FMT Logo、天空藍主題、專屬程式圖示及登入畫面。
- TitanPlanner 風格姿態儀與依嚴重程度著色的飛行訊息。
- 工具列快捷操作：解鎖／上鎖、空速計歸零及控制來源切換。
- 工具列 GPS 狀態：衛星數量、定位型態、HDOP 與 VDOP。
- 航點間距離標示，拖曳航點時同步更新點位與航線。
- 台灣民航局限禁航區：禁航區紅色、限航區黃色。
- 高度檢查與限禁航區檢查，涵蓋起飛、航點間及返航路徑。
- 多旋翼、定翼機與 VTOL／QuadPlane 對應的常用模式與參數設定。
- Titan 風格遙控器、馬達與舵機設定頁。
- 地圖飛行器使用定翼機、多旋翼與 VTOL 專用發光圖示。
- 遙測欄位、儀表板名稱、單位及主要操作採繁體中文顯示。

## 操作畫面

以下沿用前版示意圖，圖示及按鈕可能與 V1.1.6 不同；QNH 入口已移除。

### 飛行資料與 AUTO 任務控制

![FeiMaoTecPlanner V1.1.6 飛行資料畫面](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.6/FMT/ManualImages/v110-flight-data.png)

### 任務規劃

![任務規劃畫面](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.6/FMT/ManualImages/v110-mission-planning.png)

### 限禁航區檢查

![限禁航區檢查](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.6/FMT/ManualImages/v110-airspace-check.png)

### 飛行高度與地形剖面

![飛行高度與地形剖面](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.6/FMT/ManualImages/v110-height-profile.png)

## 快速開始

1. 下載 [FMTPlanner-V1.1.6.zip](https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.1.6/FMTPlanner-V1.1.6.zip)。
2. 將 ZIP **完整解壓縮**到可寫入的資料夾。
3. 執行 **FMTPlanner-V1.1.6.exe**，不要直接在壓縮檔內啟動。
4. 登入後選擇正確 COM 埠與傳輸速率，再按 **CONNECT／連線**。
5. 正式使用前請變更預設密碼，並先完成飛控校正與任務安全檢查。

## 三種飛行器常用設定

| 構型 | 常用設定 |
|---|---|
| 多旋翼 | 導航速度、GPS 定位速度、WP 半徑、WP 航向、RTL 航向及 RTL 速度 |
| 定翼機 | 巡航／RTL 空速、最低 GPS 地速及 WP 半徑 |
| VTOL／QuadPlane | 定翼巡航空速、VTOL 導航／返航速度、GPS 速度、WP 半徑及 QRTL 返航模式 |

速度統一以 **m/s** 顯示，WP 半徑以 **m** 顯示；程式會處理 ArduPilot 新舊版參數及單位換算。

## 文件與下載

- [V1.1.6 穩定版發布頁](https://github.com/FMT-Wade-hong/MP-GPTT/releases/tag/FMTPlanner-v1.1.6)
- [下載可攜式程式](https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.1.6/FMTPlanner-V1.1.6.zip)
- [完整繁體中文圖文操作手冊](README-FMT.md)
- [版本變更記錄](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.1.6/CHANGELOG-FMT.md)
- [FMT 飛貓科技官網](https://www.feimaotec.com)

## 安全提醒

FMTPlanner 的高度與限禁航區檢查屬於輔助功能。實際飛行前仍須確認最新法規、公告、任務區域、天候、飛控狀態及現場安全條件。請勿僅依賴軟體提示決定是否可以飛行。

錯誤回報只在本機產生並由使用者手動送出 GitHub Issue；送出前仍須自行檢查並移除任務機密、完整位置、密碼、Token、金鑰與個人資料。

## 授權

本專案基於 Mission Planner，依原專案的 [GPL-3.0 授權](COPYING.txt)散布。FMT 品牌、Logo 與客製素材之權利歸 FMT 飛貓科技所有。
