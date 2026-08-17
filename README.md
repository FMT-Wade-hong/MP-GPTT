# FeiMaoTecPlanner V1.1.2

<p align="center">
  <img src="https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.2/FMT/Assets/fmt-logo.png" alt="FMT 飛貓科技" width="420">
</p>

<p align="center">
  <strong>FMT 飛貓科技客製化 Mission Planner 地面站</strong><br>
  多旋翼、定翼機及 VTOL／QuadPlane 共用操作環境
</p>

<p align="center">
  <a href="https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.1.2/FMTPlanner-V1.1.2.zip"><strong>下載 FMTPlanner V1.1.2</strong></a>
  ·
  <a href="https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.1.2/README-FMT.md">繁體中文圖文操作手冊</a>
  ·
  <a href="https://github.com/FMT-Wade-hong/MP-GPTT/releases/tag/FMTPlanner-v1.1.2">版本發布頁</a>
</p>

![Version](https://img.shields.io/badge/version-V1.1.2-22a9dc)
![Platform](https://img.shields.io/badge/platform-Windows-0078d4)
![Package](https://img.shields.io/badge/package-portable%20ZIP-35a853)
![Language](https://img.shields.io/badge/UI-English%20%7C%20繁體中文-f0ad00)

## 軟體資訊

| 項目 | 內容 |
|---|---|
| 軟體名稱 | **FeiMaoTecPlanner V1.1.2** |
| 執行檔 | **FMTPlanner.exe** |
| 公司／品牌 | **FMT 飛貓科技** |
| 基礎專案 | ArduPilot Mission Planner |
| 支援系統 | Windows |
| 發布形式 | 解壓縮後即可執行的可攜式 ZIP |
| 支援構型 | 多旋翼、定翼機、VTOL／QuadPlane |
| 介面語言 | 預設英文，可使用繁體中文 |

## V1.1.2 穩定版主要更新

- 重製 AUTO 任務狀態列為固定高度、固定欄寬的雙層資訊版面；航點切換與 ETA 更新不再推移欄位，並移除會造成版面壅塞的藍色進度條。
- 目前執行狀態只在 AUTO／RTL 模式啟用，並穩定顯示 Mission Item、下一任務、航段距離、離家距離與 ETA。
- 新增飛行前檢查、主旋翼 RPM1 整數轉速與上下限提示；整理頂端飛行時間及衛星資訊並移除 ArduPilot 圖標，避免遙測資料被遮蔽。
- 整合 3D 地圖與舵機調整面板，兩者只能擇一展開；修正地圖預設位置、自動平移、飛行器圖標中心及多旋翼圖標底色。
- 強化直升機、定翼機、多旋翼及 VTOL 構型辨識與常用設定；直升機新增導航、半徑、航向與主旋翼轉速警告設定。
- 完成 RTK、加速度計、羅盤、遙控器、舵機、馬達及電子圍籬頁面的繁體中文化與 DPI／捲動版面整理。
- 改善外掛載入、畫面更新、未連線參數讀取與空值處理；完整方案以 Visual Studio MSBuild 建置成功，並完成可攜式封裝驗證。

## 主要功能

- FMT Logo、天空藍主題、專屬程式圖示及登入畫面。
- TitanPlanner 風格姿態儀與依嚴重程度著色的飛行訊息。
- 工具列快捷操作：解鎖／上鎖、空速計歸零及 QNH 校正。
- 工具列 GPS 狀態：衛星數量、定位型態、HDOP 與 VDOP。
- 航點間距離標示，拖曳航點時同步更新點位與航線。
- 台灣民航局限禁航區：禁航區紅色、限航區黃色。
- 高度檢查與限禁航區檢查，涵蓋起飛、航點間及返航路徑。
- 多旋翼、定翼機與 VTOL／QuadPlane 對應的常用模式與參數設定。
- Titan 風格遙控器、馬達與舵機設定頁。
- 地圖飛行器使用定翼機、多旋翼與 VTOL 專用發光圖示。
- 遙測欄位、儀表板名稱、單位及主要操作採繁體中文顯示。

## 操作畫面

### 飛行資料與 AUTO 任務控制

![FeiMaoTecPlanner V1.1.2 飛行資料畫面](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.2/FMT/ManualImages/v110-flight-data.png)

### 任務規劃

![任務規劃畫面](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.2/FMT/ManualImages/v110-mission-planning.png)

### 限禁航區檢查

![限禁航區檢查](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.2/FMT/ManualImages/v110-airspace-check.png)

### 飛行高度與地形剖面

![飛行高度與地形剖面](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.1.2/FMT/ManualImages/v110-height-profile.png)

## 快速開始

1. 下載 [FMTPlanner-V1.1.2.zip](https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.1.2/FMTPlanner-V1.1.2.zip)。
2. 將 ZIP **完整解壓縮**到可寫入的資料夾。
3. 執行 **FMTPlanner.exe**，不要直接在壓縮檔內啟動。
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

- [V1.1.2 穩定版發布頁](https://github.com/FMT-Wade-hong/MP-GPTT/releases/tag/FMTPlanner-v1.1.2)
- [下載可攜式程式](https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.1.2/FMTPlanner-V1.1.2.zip)
- [完整繁體中文圖文操作手冊](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.1.2/README-FMT.md)
- [版本變更記錄](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.1.2/CHANGELOG-FMT.md)
- [FMT 飛貓科技官網](https://www.feimaotec.com)

## 安全提醒

FMTPlanner 的高度與限禁航區檢查屬於輔助功能。實際飛行前仍須確認最新法規、公告、任務區域、天候、飛控狀態及現場安全條件。請勿僅依賴軟體提示決定是否可以飛行。

錯誤回報只在本機產生並由使用者手動送出 GitHub Issue；送出前仍須自行檢查並移除任務機密、完整位置、密碼、Token、金鑰與個人資料。

## 授權

本專案基於 Mission Planner，依原專案的 [GPL-3.0 授權](COPYING.txt)散布。FMT 品牌、Logo 與客製素材之權利歸 FMT 飛貓科技所有。
