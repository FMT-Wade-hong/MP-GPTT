"""Check full offline metadata coverage, not semantic correctness of machine drafts."""
import glob
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

catalog_path = Path('FMT/Localization/Parameters.zh-TW.draft.json')
catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
manifest = json.loads(catalog_path.with_suffix('.manifest.json').read_text(encoding='utf-8'))
missing = []
descriptions = 0
labels = 0
for entry in manifest['sources']:
    path = Path('ParameterMetaDataBackup.xml') if entry['file'] == 'ParameterMetaDataBackup.xml' else Path('C:/ProgramData/Mission Planner') / entry['file']
    raw = path.read_bytes()
    assert hashlib.sha256(raw).hexdigest() == entry['sha256'], path
    root = ET.fromstring(raw)
    params = root.findall('.//param')
    for p in params:
        text = p.get('documentation', '').strip()
        if text:
            descriptions += 1
            if not catalog.get(text): missing.append((p.get('name'), text))
        values = [p.get('humanName', '')] + [v.text or '' for v in p.findall('values/value')]
        for field in p.findall('field'):
            if field.get('name') == 'Bitmask': values += [v.partition(':')[2] for v in (field.text or '').split(',') if ':' in v]
        for label in values:
            if label.strip():
                labels += 1
                if not catalog.get(label.strip()): missing.append((p.get('name'), label))
    if not params:
        for p in root.findall('./*/*'):
            for tag in ['Description', 'DisplayName']:
                text = p.findtext(tag, '').strip()
                if text and not catalog.get(text): missing.append((p.tag, text))
            for tag in ['Values', 'Bitmask']:
                for v in p.findtext(tag, '').split(','):
                    text = v.partition(':')[2].strip()
                    if text and not catalog.get(text): missing.append((p.tag, text))
assert not missing, missing[:10]
assert not manifest['missing']
assert all(v.strip() for v in catalog.values())
# Surface suspicious drafts for human review; coverage must not be confused with correctness.
review = []
for source, translated in catalog.items():
    refs = set(re.findall(r'\b[A-Z][A-Z0-9]*_[A-Z0-9_]+\b', source))
    lost = sorted(ref for ref in refs if ref not in translated)
    if lost: review.append(dict(source=source, translation=translated, missing_references=lost))
Path('FMT/Localization/Parameters.review.json').write_text(json.dumps(dict(
    semantic_review_complete=False, entries=review), ensure_ascii=False, indent=2), encoding='utf-8')
print(f'PASS: {len(manifest["sources"])} sources; {len(catalog)} unique strings; zero missing/empty entries')
print(f'PASS: {descriptions} versioned descriptions and {labels} versioned name/option entries covered')
print(f'REVIEW REQUIRED: {len(review)} machine drafts have parameter references requiring verification')
