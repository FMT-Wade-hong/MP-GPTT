"""Verify every bundled release and export the complete documented parameter diff."""
import gzip
import hashlib
import html
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
bundle = root / 'ExtLibs/Utilities/ParameterMetadata'
manifest = json.loads((bundle / 'manifest.json').read_text(encoding='utf-8'))
snapshots, inventories = {}, []
for release in manifest['releases']:
    raw = gzip.decompress((bundle / release['file']).read_bytes())
    assert hashlib.sha256(raw).hexdigest() == release['sha256'], release['key']
    assert release['generator_exit'] == 0, release['key']
    nodes = ET.fromstring(raw).findall('.//param')
    assert len(nodes) == release['parameters']
    params, duplicates, variants = {}, [], {}
    for node in nodes:
        name = node.attrib['name'].split(':')[-1]
        data = dict(node.attrib)
        data['fields'] = {f.attrib['name']:f.text or '' for f in node.findall('field')}
        data['values'] = {v.attrib['code']:v.text or '' for v in node.findall('values/value')}
        variants.setdefault(name, []).append(data)
        if name in params:
            duplicates.append(name)
        else:
            params[name] = data
    duplicate_definitions = {name: definitions for name, definitions in variants.items() if len(definitions)>1}
    # Compare all alternatives as well as the default, so a non-default hardware
    # definition cannot silently disappear from the audit.
    snapshots[release['key']] = {name:dict(data, alternatives=variants[name][1:]) for name,data in params.items()}
    inventories.append(dict(**release, unique_parameters=len(params), duplicate_names=duplicates,
                            duplicate_definitions=duplicate_definitions))

comparisons = []
for family in ('Copter', 'Plane', 'Rover', 'Sub', 'Tracker'):
    versions = sorted((r for r in manifest['releases'] if r['family']==family),
                      key=lambda r:tuple(map(int,r['version'].split('.'))))
    pairs = list(zip(versions, versions[1:]))
    if family != 'Sub':
        pairs.append((next(r for r in versions if r['version']=='4.6.3'), versions[-1]))
    for before, after in pairs:
        a, b = snapshots[before['key']], snapshots[after['key']]
        changes = []
        for name in sorted(a.keys() & b.keys()):
            # Compare every attribute, field and option, not only names or ranges.
            diff = {k:[a[name].get(k),b[name].get(k)] for k in set(a[name]) | set(b[name])
                    if a[name].get(k)!=b[name].get(k)}
            if diff: changes.append(dict(name=name, differences=diff))
        comparisons.append(dict(before=before['key'], after=after['key'],
            removed=sorted(a.keys()-b.keys()), added=sorted(b.keys()-a.keys()), changed=changes))

all_names = set().union(*(set(p) for p in snapshots.values()))
references = []
for directory in ('GCSViews/ConfigurationView', 'Controls', 'FMT'):
    for path in sorted((root / directory).rglob('*.cs')):
        if path.name.endswith('.Designer.cs'): continue
        for line_number, line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(),1):
            for name in sorted(set(re.findall(r'"([A-Z][A-Z0-9_]{1,24})"',line)) & all_names):
                references.append(dict(file=path.relative_to(root).as_posix(), line=line_number, parameter=name,
                    present_in=[key for key,p in snapshots.items() if name in p]))
audit = dict(schema=1, scope='Official documented parameters in existing stable 4.6.x and 4.7.x release tags; not board-specific runtime enumeration or flight validation.',
    releases=inventories, comparisons=comparisons, ui_parameter_references=references)
out = root / 'FMT/ParameterAudit'
out.mkdir(exist_ok=True)
(out / 'ArduPilot-4.6-4.7-audit.json').write_text(json.dumps(audit, ensure_ascii=False, indent=2),encoding='utf-8')

def esc(x): return html.escape(str(x))
rows = ''.join('<tr><td>'+esc(r['key'])+'</td><td>'+str(r['unique_parameters'])+'</td><td>'+str(len(r['duplicate_names']))+
    '</td><td><a href="'+esc(r['source'])+'">'+esc(r['commit'][:12])+'</a></td><td>通過</td></tr>' for r in inventories)
sections = []
for c in comparisons:
    details = ''.join('<details><summary>'+esc(item['name'])+'</summary><pre>'+esc(json.dumps(item['differences'],ensure_ascii=False,indent=2))+'</pre></details>' for item in c['changed'])
    sections.append('<details class="release"><summary>'+esc(c['before']+' → '+c['after'])+
        f"：新增 {len(c['added'])}、移除 {len(c['removed'])}、定義變更 {len(c['changed'])}</summary>"+
        '<h3>新增</h3><pre>'+esc('\n'.join(c['added']))+'</pre><h3>移除</h3><pre>'+esc('\n'.join(c['removed']))+
        '</pre><h3>逐項定義差異</h3>'+details+'</details>')
document = '''<!doctype html><html lang="zh-Hant"><meta charset="utf-8"><title>FMTPlanner V1.2.4 參數核對</title>
<style>body{font:16px system-ui;max-width:1100px;margin:40px auto;padding:0 20px;color:#17382f}table{border-collapse:collapse;width:100%}td,th{border:1px solid #cadad5;padding:8px;text-align:left}summary{cursor:pointer;padding:8px;background:#edf4f1}details{margin:8px 0}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:13px;background:#f6f8f7;padding:12px}h1{font-size:28px}</style>
<h1>FMTPlanner V1.2.4｜ArduPilot 4.6–4.7 參數核對</h1><p>2026-10-27・本機候選版，未發布。</p>
<p>從官方發布標籤執行各版本自己的 param_parse.py，包含所有文件化的共用函式庫、機型參數與腳本參數。下表逐一列出來源；附帶完整新增、移除、說明、單位、範圍、步進、選項及位元遮罩差異。</p>
<p>專用設定頁依機型提供常用項目；完整參數頁顯示飛控實際回傳的所有參數。官方文件可能包含該硬體未編入或未啟用的功能，不能把文件數量視為實機應回傳數量。Sub 沒有本次查到的 4.6 發布標籤，因此只列實際存在的 4.7 版本；Blimp／AP_Periph 沒有對應標籤，未冒用其他機型版本。未進行實機寫入或試飛。</p>
<p>修正：完整快照內嵌、依目前機型與版本選擇、缺欄位不混用其他版本、PID 與起飛名稱／倍率／小數、直升機爬升／下降／返航高度、OSD 的 SRx→MAVx 對應。4.7 ARMING_SKIPCHK 的位元代表略過檢查，沿用新版定義，不將 ARMING_CHECK 值自動轉入。</p>
<p>一般參數頁依飛控回傳的數值型別處理整數與浮點數，保留精細小數輸入；滑桿同步不回寫截斷值，切換飛控／版本／直升機類別會重建範圍及選項。</p>
<p>官方產生器將部分硬體或機架的同名定義放在一起。已按 MAVLink 機型選擇多旋翼／直升機的姿態控制與自動調參定義；其餘硬體相依的同名項目保留上游順序，未宣稱能自動判定全部硬體變體。JSON 的 duplicate_definitions 完整列出所有重複定義，版本差異亦包含非預設定義。來源 source 欄位由包裝程式加入，只記錄原解析器的 real_path，不改動上游參數內容。</p>
<h2>完整來源清單</h2><table><tr><th>機型版本</th><th>不重複參數</th><th>重複定義</th><th>官方 commit</th><th>產生器／雜湊</th></tr>'''+rows+'''</table><h2>完整版本差異</h2>'''+''.join(sections)+'''<p>機器可讀清單：<a href="ArduPilot-4.6-4.7-audit.json">完整 JSON（含設定頁參數引用位置）</a></p></html>'''
(out / 'ArduPilot-4.6-4.7-audit.html').write_text(document,encoding='utf-8')
print(f"PASS: {len(inventories)} releases; {sum(r['unique_parameters'] for r in inventories)} parameter definitions; {len(comparisons)} complete diffs; {len(references)} UI references inventoried")
print('Report: '+str(out / 'ArduPilot-4.6-4.7-audit.html'))
