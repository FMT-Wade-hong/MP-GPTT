# 全部參數表繁體中文對照

人工核對字典優先；其餘來源文字使用 `Parameters.zh-TW.draft.json` 的離線機譯稿。表格與選項顯示 `〔機譯待核對〕`，原始英文說明保留在滑鼠提示中。不得以翻譯稿取代飛控版本對應的官方技術文件。

覆蓋率與正確性是兩件事：零遺漏表示每筆來源有對照，不表示機器譯文已人工核對。`Parameters.review.json` 記錄機譯可能未保留的參數引用；警告、數字、否定語句與限制條件仍需人工覆核。參數 ID、實際值、選項鍵值、位元索引及單位不改動。沒有對照的新韌體文字保留英文，絕不由參數名稱猜測說明。

來源範圍及 SHA-256 記錄在 `Parameters.zh-TW.draft.manifest.json`：本機公開 ArduPilot 參數定義快取及專案內建備援 XML。不含飛行紀錄、使用者參數值或密碼。

離線生成使用 [Argos Translate 英中模型 1.9](https://github.com/argosopentech/argospm-index)、CTranslate2 及 OpenCC s2twp；模型與 Python 僅放在忽略的 `bin/TranslationRuntime`，不隨軟體發佈，也不需要在使用者電腦執行。應用程式只載入內嵌 JSON，不連接翻譯服務。

模型出處：Jörg Tiedemann、Santhosh Thottingal，*OPUS-MT — Building open translation services for the World*，EAMT 2020。Argos 套件說明列原始 OPUS 模型為 CC-BY 4.0；模型來源與套件資訊見上述官方索引。譯稿經 OpenCC 繁體轉換、參數引用保留修復；不代表原作者核可本軟體或翻譯內容。

重建：使用 `FMT/Build-ParameterTranslations.py`，指定模型、XML 來源與輸出 JSON；`FMT/Test-ParameterCatalog.py` 檢查完整來源覆蓋，`FMT/Test-ParameterLocalization.ps1` 檢查程式顯示、人工譯文優先及值保留。
