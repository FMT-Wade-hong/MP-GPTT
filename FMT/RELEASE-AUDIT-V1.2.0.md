# FeiMaoTecPlanner V1.2.0 發布稽核

發布名稱：FeiMaoTecPlanner V1.2.0。正式版，不使用 RC 後綴。

## 範圍

承接 V1.1.9，納入電源模組選單復原、頁籤標題中文化、地圖繁體中文與參數語意修正。發布包只使用 `bin/Release120/net461` 建置結果，排除私人參數、設定、憑證、日誌與測試產物。

## 已知限制

- 全表 58 份公開 metadata 的 3,934 種說明均有中文，但不宣稱全部逐條語意複核完成。
- 3,335 種選項中 1,571 種為雙語，1,764 種保留原文，其中包含型號、縮寫與專有名稱。
- 精確版本測試涵蓋 Copter 4.5.7／4.6.3／4.7.1 的指定導航／控制參數；不代表內建所有機型及 patch 版本的 metadata。
- 離線測試不等於實機、飛行或 5–10 台群飛驗證。升級前請備份設定，先完成地面驗證。
- 編譯警告仍存在，並非無警告版本。

## 發布驗證

- Release 建置成功：0 errors、1,390 warnings；FileVersion `1.2.0.0`，ProductVersion `V1.2.0`。
- 13 組離線測試通過：TranslationSemantics（156 個原文案例）、ReviewedParameterTranslation、AllParameterTranslation、VersionedParameterMetadata、PowerModuleMenu、TraditionalMapLabels、TabMenuLabels、ElrsSerialSettings、ParamCompareStaging、RelayControlVisibility、ShutdownCleanup、TaskbarIcon、LogSaveLocation。
- `git diff --check` 通過。
- 正式 ZIP 於上傳前檢查版本標籤、必要 runtime、重複項目、私人檔案排除與 EXE／DLL 雜湊一致性；GitHub 資產提供 SHA-256 校驗檔。
