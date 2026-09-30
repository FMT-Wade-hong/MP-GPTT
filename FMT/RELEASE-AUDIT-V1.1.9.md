# V1.1.9 發布前稽核

更新日期：2026-10-01（Asia/Taipei）。結論：**本機 V1.1.9 候選版已建置；仍暫不具備正式發布條件**。
範圍：GitHub 狀態、目前本機修改、現有測試版離線回歸、最近本機日誌抽查。未操作飛控、未推送、未發布。

## GitHub

以下遠端資料是 2026-09-30 的稽核快照，本次版本整理未重新查詢 GitHub，亦未推送或發布。

- 最新正式版：[FMTPlanner-v1.1.8](https://github.com/FMT-Wade-hong/MP-GPTT/releases/tag/FMTPlanner-v1.1.8)，2026-09-18。
- 遠端 HEAD 與本機基準一致：`564fe2093fce97ee1e256517de40e89c4aadbaef`。本輪修改尚未提交。
- 查到的 Issue #2 已關閉，沒有開啟中的 Issue；這不代表軟體沒有缺陷。
- 舊 PR #1「release FMTPlanner v1.0.0」仍開啟，不屬本輪發布，未修改。
- [Windows 工作流程](https://github.com/FMT-Wade-hong/MP-GPTT/actions/runs/35361198654)：Debug、Release 編譯成功；Release 的 SiK 回歸失敗，自動打包與發布步驟跳過。
  - `FMT/Verify-FmtSiK.ps1` 第 17 行仍找舊字串「數傳設定」，而 `InitialSetup.cs` 已使用「SIK 數傳設定」。需修正選單定位測試，保留順序及可見性驗證，不得直接跳過測試。
  - 後續打包仍呼叫固定 V1.1.5 的 `Verify-FMTPlannerV115.ps1`；即使 SiK 通過，也需更新版本驗證邏輯。
- [OSX／iOS 工作流程](https://github.com/FMT-Wade-hong/MP-GPTT/actions/runs/35361198536)：缺少 Xamarin.Mac.CSharp.targets／Xamarin.iOS.CSharp.targets（MSB4226）。與 Windows C# 編譯失敗不同；需決定修復或另行限定支援範圍，不能列為通過。

## 軟體與日誌

- 參數翻譯品質列為發布阻擋項：使用者發現 Roll／Pitch／Yaw 等被錯譯。已停用未核對機譯並移除執行檔中的 draft resource；移除僅按 ID 套用的舊字典，保留原文對照。新增 reviewed JSON 354 條完整原文對照，加上先前 AutoTune 核對；涵蓋共用、Copter、Plane、Rover／Sailboat 多個群組。跨 58 份公開參數快取檢查 3,331 種選項原文及 bit 索引均保留。仍有 3,450 條不同原文待翻譯（跨機型／版本去重），舊手工字典亦未全部重審，**不能宣稱全表已完成**。
- 最新翻譯候選版：`bin/ParameterSemanticReview/net461`，版本仍為 1.1.8.0。原目錄被使用中程式鎖定，因此改用新輸出目錄，未終止使用者程式。建置 0 errors／1,405 warnings；Test-AllParameterTranslation、Test-ReviewedParameterTranslation、Test-ParameterLocalization 通過。完整待辦及來源 SHA-256 見該目錄 `parameter-translation-audit.json`；不把覆蓋率當成語意正確性證明。
- `ConfigSerial.doApplyRules` 仍直接存取 `SERIALn_PROTOCOL.Value`。飛控序列埠編號有空洞時可為 null，與先前使用者 Serial Ports 堆疊相符；尚未修復，列為發布阻擋項。
- `C:/ProgramData/Mission Planner/MissionPlanner.log` 及最近兩個滾動檔抽樣（每檔最多 8,000 行）：SocketException 2,835 筆、ObjectDisposedException 174 筆、ReflectionTypeLoadException 52 筆；其中許多為 DEBUG FirstChanceException，**不能等同未處理當機**。
- 明確 ERROR 記錄包含 PluginLoader 52 筆、CameraProtocol 1 筆。Socket 堆疊涉及 ReceiveFrom／EndReceiveFrom，需檢查斷線／重新連線及關閉後的接收迴圈。
- 日誌路徑由不同 Mission Planner／FMT 測試版共用，不能把全部訊息歸因於目前測試版；需乾淨候選版與獨立診斷時段重新確認。不將原始日誌公開或打包。
- 使用者已確認工作列圖示在重新啟動 Explorer 後恢復；執行中視窗大小圖示已讀回驗證正常。

## 已執行驗證

測試目標：`bin/IconFormatFix/net461`，版本仍是 1.1.8.0。最近一次完整建置 0 errors、1,405 warnings；警告尚未逐項清除。
本輪下列 15 組離線腳本均 exit 0：

1. Test-AirspeedZeroVisibility
2. Test-CopterRefreshBindings
3. Test-CopterVersionWarning
4. Test-ElrsSerialSettings
5. Test-LogSaveLocation
6. Test-MessagesRollingBuffer（2,500 筆，保留 1,000 筆）
7. Test-ParamCompareStaging
8. Test-RelayControlVisibility
9. Test-RoverChinese
10. Test-SafetyReadback
11. Test-SwarmUnifiedUI
12. Test-TaskbarIcon
13. Inspect-ApplicationIcon（256 尺寸在目前 .NET 選擇路徑退回 128，其餘 16–128 尺寸像素相符）
14. Test-PerformanceStability
15. Test-SwarmMenu

`git diff --check` 無空白格式錯誤，只有 CRLF 正規化提示。
離線檢查包括部分原始碼／反射驗證，沒有弱網路飛行、實機參數寫入或 5–10 台群飛驗證。

## V1.1.9 發布門檻

- [ ] 修正 SERIAL 埠缺號空值例外並建立回歸測試。
- [ ] 修正 SiK 選單及版本相依 CI 測試，Windows Actions 重跑通過。
- [ ] 釐清本機 Socket 接收及 PluginLoader 錯誤來源，乾淨候選版測試不出現未處理例外。
- [ ] 確定 macOS／iOS 工作流程與支援範圍。
- [ ] 群飛／ELRS 明確保留實驗性及安全限制；完成必要地面測試。
- [x] 同步 FMT/VERSION、AssemblyInfo、csproj、登入版本、README、CHANGELOG 到 1.1.9；重新編譯並讀回 FileVersion=1.1.9.0、ProductVersion=V1.1.9。
- [x] 在獨立 Release119 目錄產生候選版，重新執行 16 組離線回歸；未壓縮測試目錄，尚未進行正式乾淨打包。
- [ ] 打包排除 `H420-source-full.param`、本機設定／密鑰／日誌／頻率表、測試截圖與暫存檔；目前新增的 `swarm-tab-*.png`、`resource-icon.png`、`source-icon.png` 等不全符合原有 preview 排除規則。
- [ ] 驗證 ZIP 版本、必要 DLL、繁中資源、重複檔案、EXE／DLL 雜湊與 SHA256。
- [ ] 使用者確認正式發布後才提交／推送、建立 tag 與 GitHub Release。

本輪只做檢查與發布資料準備，未將上述阻擋項標為已修正。

## 2026-10-01 候選版整理結果

- 建置：`dotnet build MissionPlanner.csproj --no-restore -c Release -p:OutputPath=bin/Release119/net461/`，0 errors、1,389 warnings。警告仍包括既有套件安全性與資源問題，未宣稱清除。
- 離線測試目標：`bin/Release119/net461/FMTPlanner.exe`，FileVersion `1.1.9.0`；16 組全部 exit 0：AirspeedZeroVisibility、CopterRefreshBindings、CopterVersionWarning、ElrsSerialSettings、LogSaveLocation、MessagesRollingBuffer、ParamCompareStaging、RelayControlVisibility、RoverChinese、SafetyReadback、SwarmUnifiedUI、TaskbarIcon、PerformanceStability、SwarmMenu、ReviewedParameterTranslation、ParameterLocalization。
- ELRS 已移除本機 metadata／MAVRadio 門檻，MAVLink 取消 RC_OPTIONS bit 1 並保留其他位元；使用者回報 SYSID 對應問題已解決。未以程式連線或寫入飛控。
- `git diff --check` exit 0，只有 CRLF 正規化提示。保留其他既有工作，不提交、不推送、不建立正式壓縮包／Release。
- 測試產生的截圖只在本機候選輸出目錄；正式打包前仍須依上述排除規則過濾。私人 `H420-source-full.param` 未納入發布資料。
