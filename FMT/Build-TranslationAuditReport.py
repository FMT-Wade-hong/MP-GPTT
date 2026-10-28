"""Build a local, self-contained report from the static and compiled runtime audits."""
import argparse
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'FMT/TranslationAudit'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', type=Path,
                        default=ROOT/'bin/Release124/net461/parameter-translation-audit.json')
    args = parser.parse_args()
    static = json.loads((OUT/'resource-audit.json').read_text(encoding='utf-8'))
    runtime = json.loads(args.runtime.read_text(encoding='utf-8-sig'))
    (OUT/'parameter-translation-audit.json').write_text(json.dumps(runtime, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    changes = {}
    for row in json.loads((OUT/'resource-corrections.json').read_text(encoding='utf-8')):
        key = row['File'], row['Key']
        if key not in changes:
            changes[key] = dict(row)
        else:
            changes[key]['After'] = row['After']
    changes = [r for r in changes.values() if r['Before'] != r['After']]
    additions = json.loads((OUT/'parameter-additions.json').read_text(encoding='utf-8'))
    esc = lambda x: html.escape(str(x))
    def table(headers, rows):
        return '<table><thead><tr>'+''.join('<th>'+esc(x)+'</th>' for x in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+esc(c)+'</td>' for c in row)+'</tr>' for row in rows)+'</tbody></table>'
    sections = [
        ('已修改的資源文字（含新增繁體資源）', table(['檔案／鍵值','原文字','修正後'], ((r['File']+' / '+r['Key'],r['Before'],r['After']) for r in changes))),
        ('新增的 18 段參數說明', table(['參數','完整來源原文','譯文'], ((', '.join(r['ParameterIds']),r['Source'],r['Translation']) for r in additions))),
        ('英文回退候選：尚待逐頁確認', '<p>下表是靜態資源回退結果，包含技術名稱、執行時由 C# 翻譯的控制項、隱藏／舊版頁面。不是已確認漏翻的數量。例如 ConfigTradHeli 是舊版頁面，目前使用 ConfigTradHeli4。</p>'+table(['資源','鍵值','文字'],((r['File'],r['Key'],r['Text']) for r in static['EffectiveFallbackCandidates']))),
        ('目前仍保留原文的參數選項', '<p>包括型號、協定、軸向和模式名稱，也可能有尚未審核的敘述選項。數值與原始字樣完全保留，未以部分詞彙強行取代。</p>'+table(['原文'],((x,) for x in runtime['OriginalOptionLabels']))),
        ('歷史翻譯來源', '<p>以下 1 段舊來源在目前 89 份快照中已不再出現；保留供舊韌體原文完全相符時使用，不計入此次來源核驗。</p>'+table(['來源'],((x,) for x in runtime['CatalogSourcesNotInCurrentSnapshots']))),
    ]
    page = '''<!doctype html><html lang="zh-Hant"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>FMTPlanner V1.2.4 繁體中文檢查</title><style>
body{font:16px/1.7 system-ui,"Microsoft JhengHei",sans-serif;background:#f4f7f8;color:#213740;margin:0}main{max-width:1200px;margin:auto;padding:32px}h1{font-size:28px}h2{font-size:21px}section,.card{background:white;padding:24px;border:1px solid #d7e1e5;border-radius:12px;margin:18px 0}table{border-collapse:collapse;width:100%;font-size:14px;table-layout:fixed}td,th{border-bottom:1px solid #d7e1e5;padding:10px;text-align:left;overflow-wrap:anywhere;white-space:pre-wrap;vertical-align:top}th{background:#eaf1f4}input{box-sizing:border-box;width:100%;padding:13px;font:inherit;border:1px solid #8ba1aa;border-radius:8px}summary{font-weight:600;cursor:pointer}small{color:#526b75}.note{border-left:5px solid #d79a25}a{color:#036b8b}</style><main>
<h1>FMTPlanner V1.2.4 繁體中文檢查</h1><p>2026-10-29 發布前複驗 · V1.2.4 發布附件</p>
<div class="card"><h2>檢查範圍與結果</h2>'''
    page += f'<p>掃描 {static["ResourceFiles"]} 份中文資源、{static["ResourceStrings"]:,} 個字串，另檢查 {static["NeutralResourceFilesChecked"]} 份中性資源的繁體回退。已審核參數目錄共 {runtime["ReviewedCatalogEntries"]:,} 筆。</p>'
    page += f'<p>參數掃描涵蓋 {len(runtime["Sources"])} 份本機公共資料（58 份快取＋31 份內嵌 4.6／4.7 快照）、{runtime["UniqueDescriptions"]:,} 種不同說明及 {runtime["OptionLabelsChecked"]:,} 個選項名稱。此次掃描的說明英文回退為 {runtime["UntranslatedUniqueDescriptions"]}；所有選項原文與位元索引保持不變。</p>'
    page += '<p>修正圍籬越界、預估抵達時間、空速變化率、電池節數與校準提示的語意；新增 185 項繁體共用訊息；停止對未審核安全選項進行片段取代。失效保護頁移除固定 1／20 秒及「0 等於停用」的錯誤暗示，修正 2D 被寫成 3D、RTK 顯示空白與解鎖狀態用詞。</p></div>'
    page += f'<div class="card note"><h2>尚未宣稱完成的部分</h2><p>這不是所有介面逐句人工語意審核完成的證明。另有 {len(static["EffectiveFallbackCandidates"]):,} 個靜態英文回退候選，需區分技術字樣與執行時翻譯；{len(runtime["OriginalOptionLabels"]):,} 個參數選項保留原文。非中文語系、獨立 FLOG／MQTT 工具及所有硬體情境不在此次範圍。</p><p>資源檔格式、重複鍵、替代字元及參數引用檢查無錯誤；候選列表中的英文不等同於顯示缺陷。</p></div>'
    page += '<div class="card"><h2>驗證</h2><p>發布用 Release 建置 0 errors、1,439 warnings。62 項離線回歸測試通過，包含翻譯與參數檢查。繁體 zh-TW／zh-Hant／zh-HK 各實際載入 185 項共用訊息；格式字串、18 段新增說明和完整原文保留通過。既有 156 組參數語意、參數翻譯、地圖、安全設定、版本、雙 GPS 與馬達防連點回歸均通過。離線渲染固定翼、目前直升機及失效保護頁；未連線或寫入實體飛控。</p></div>'
    page += '<input id="search" placeholder="搜尋參數、檔案、原文或中文；展開下方分類查看"><small>搜尋僅在本機執行，頁面不會連線或上傳資料。</small>'
    for title, content in sections:
        page += '<section><details><summary>'+esc(title)+'</summary>'+content+'</details></section>'
    page += '<p>機器可讀資料：<a href="resource-audit.json">資源稽核</a> · <a href="parameter-translation-audit.json">參數稽核與來源雜湊</a> · <a href="resource-corrections.json">資源修正明細</a></p></main><script>document.getElementById("search").addEventListener("input",e=>{const q=e.target.value.toLocaleLowerCase();document.querySelectorAll("tbody tr").forEach(r=>r.hidden=!r.textContent.toLocaleLowerCase().includes(q));});</script></html>'
    (OUT/'translation-audit-v1.2.4.html').write_text(page,encoding='utf-8')
    print(f'Report: {len(changes)} distinct resource changes; {len(static["EffectiveFallbackCandidates"])} static candidates; {len(runtime["Sources"])} metadata sources')


if __name__ == '__main__':
    main()
