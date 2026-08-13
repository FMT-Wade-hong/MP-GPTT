# FeiMaoTecPlanner V1.0.4

<p align="center">
  <img src="https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.0.4/FMT/Assets/fmt-logo.png" alt="FMT 飛貓科技" width="420">
</p>

<p align="center">
  <strong>FMT 飛貓科技客製化 Mission Planner 地面站</strong><br>
  多旋翼、定翼機及 VTOL／QuadPlane 共用操作環境
</p>

<p align="center">
  <a href="https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.0.4/FMTPlanner-V1.0.4.zip"><strong>下載 FMTPlanner V1.0.4</strong></a>
  ·
  <a href="https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.0.4/README-FMT.md">繁體中文圖文操作手冊</a>
  ·
  <a href="https://github.com/FMT-Wade-hong/MP-GPTT/releases/tag/FMTPlanner-v1.0.4">版本發布頁</a>
</p>

![Version](https://img.shields.io/badge/version-V1.0.4-22a9dc)
![Platform](https://img.shields.io/badge/platform-Windows-0078d4)
![Package](https://img.shields.io/badge/package-portable%20ZIP-35a853)
![Language](https://img.shields.io/badge/UI-English%20%7C%20繁體中文-f0ad00)

## 軟體資訊

| 項目 | 內容 |
|---|---|
| 軟體名稱 | **FeiMaoTecPlanner V1.0.4** |
| 執行檔 | **FMTPlanner.exe** |
| 公司／品牌 | **FMT 飛貓科技** |
| 基礎專案 | ArduPilot Mission Planner |
| 支援系統 | Windows |
| 發布形式 | 解壓縮後即可執行的可攜式 ZIP |
| 支援構型 | 多旋翼、定翼機、VTOL／QuadPlane |
| 介面語言 | 預設英文，可使用繁體中文 |

## V1.0.4 主要功能

- FMT Logo、天空藍主題、專屬程式圖示及登入畫面。
- TitanPlanner 風格姿態儀與依嚴重程度著色的飛行訊息。
- 工具列快捷操作：解鎖／上鎖、空速計歸零及 QNH 校正。
- 工具列 GPS 狀態：衛星數量、定位型態、HDOP 與 VDOP。
- 航點間距離標示，拖曳航點時同步更新點位與航線。
- 台灣民航局限禁航區：禁航區紅色、限航區黃色。
- 高度檢查與限禁航區檢查，涵蓋起飛、航點間及返航路徑。
- 參數頁面密碼保護及深色主題文字可讀性修正。
- 多旋翼、定翼機與 VTOL／QuadPlane 各自獨立的常用參數方框。
- 啟動時不顯示額外命令提示字元視窗。

## 操作畫面

![FeiMaoTecPlanner V1.0.4 飛行資料畫面](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.0.4/FMT/ManualImages/flight-data-overview.png)

### 任務限禁航區檢查

![限禁航區檢查](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.0.4/FMT/ManualImages/mission-airspace-check.png)

### 飛行訊息顏色

![飛行訊息顏色](https://raw.githubusercontent.com/FMT-Wade-hong/MP-GPTT/FMTPlanner-v1.0.4/FMT/ManualImages/message-severity.png)

## 快速開始

1. 下載 [FMTPlanner-V1.0.4.zip](https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.0.4/FMTPlanner-V1.0.4.zip)。
2. 將 ZIP **完整解壓縮**到可寫入的資料夾。
3. 執行 **FMTPlanner.exe**，不要直接在壓縮檔內啟動。
4. 使用預設登入資料：

   - 帳號：**FMT**
   - 密碼：**1234**

5. 進入程式後選擇正確 COM 埠與傳輸速率，再按 **CONNECT／連線**。
6. 正式使用前請變更預設密碼，並先完成飛控校正與任務安全檢查。

## 三種飛行器常用設定

「初始配置 → 飛行模式設定」下方提供三個獨立設定方框。程式會依目前連線飛控啟用正確構型，其他方框保持停用，避免跨構型誤寫參數。

| 構型 | 常用設定 |
|---|---|
| 多旋翼 | 導航速度、GPS 定位速度、WP 半徑、WP 航向、RTL 航向及 RTL 速度 |
| 定翼機 | 巡航／RTL 空速、最低 GPS 地速及 WP 半徑 |
| VTOL／QuadPlane | 定翼巡航空速、VTOL 導航／返航速度、GPS 速度、WP 半徑及 QRTL 返航模式 |

速度統一以 **m/s** 顯示，WP 半徑以 **m** 顯示；程式會處理 ArduPilot 新舊版參數及單位換算。

## 文件與下載

- [V1.0.4 正式發布頁](https://github.com/FMT-Wade-hong/MP-GPTT/releases/tag/FMTPlanner-v1.0.4)
- [下載可攜式程式](https://github.com/FMT-Wade-hong/MP-GPTT/releases/download/FMTPlanner-v1.0.4/FMTPlanner-V1.0.4.zip)
- [完整繁體中文圖文操作手冊](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.0.4/README-FMT.md)
- [版本變更記錄](https://github.com/FMT-Wade-hong/MP-GPTT/blob/FMTPlanner-v1.0.4/CHANGELOG-FMT.md)
- [FMT 飛貓科技官網](https://www.feimaotec.com)

## 安全提醒

FMTPlanner 的高度與限禁航區檢查屬於輔助功能。實際飛行前仍須確認最新法規、公告、任務區域、天候、飛控狀態及現場安全條件。請勿僅依賴軟體提示決定是否可以飛行。

## 授權

本專案基於 Mission Planner，依原專案的 [GPL-3.0 授權](COPYING.txt)散布。FMT 品牌、Logo 與客製素材之權利歸 FMT 飛貓科技所有。
