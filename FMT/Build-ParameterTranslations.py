"""Generate offline, explicitly unreviewed zh-TW metadata drafts (no flight values)."""
import argparse
import hashlib
import json
import re
import time
import os
import xml.etree.ElementTree as ET
from pathlib import Path

import ctranslate2
import sentencepiece
from opencc import OpenCC

parser = argparse.ArgumentParser()
parser.add_argument('--model', required=True)
parser.add_argument('--source', action='append', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
texts = set()
sources = []
for pattern in args.source:
    import glob
    for filename in sorted(glob.glob(pattern)):
        path = Path(filename)
        raw = path.read_bytes()
        root = ET.fromstring(raw)
        params = root.findall('.//param')
        if params:
            for p in params:
                texts.update([p.get('documentation', ''), p.get('humanName', '')])
                texts.update(v.text or '' for v in p.findall('values/value'))
                for field in p.findall('field'):
                    if field.get('name') == 'Bitmask':
                        texts.update(v.partition(':')[2] for v in (field.text or '').split(',') if ':' in v)
        else:
            params = root.findall('./*/*')
            for p in params:
                texts.update([p.findtext('Description', ''), p.findtext('DisplayName', '')])
                for field in ('Values', 'Bitmask'):
                    texts.update(v.partition(':')[2] for v in p.findtext(field, '').split(',') if ':' in v)
        sources.append(dict(file=path.name, sha256=hashlib.sha256(raw).hexdigest(), parameters=len(params)))
texts = {t.strip() for t in texts if t.strip()}
output = Path(args.output)
output.parent.mkdir(parents=True, exist_ok=True)
def save_json(path, data):
    temporary = path.with_suffix(path.suffix + '.tmp')
    for attempt in range(10):
        try:
            temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True), encoding='utf-8')
            os.replace(temporary, path)
            return
        except OSError:
            if attempt == 9:
                raise
            time.sleep(0.5)
cache = json.loads(output.read_text(encoding='utf-8')) if output.exists() else {}
pending = sorted(texts - cache.keys(), key=lambda t: (len(t), t))
print(f'SOURCES={len(sources)} STRINGS={len(texts)} PENDING={len(pending)}', flush=True)
model = Path(args.model)
tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(model / 'sentencepiece.model'))
translator = ctranslate2.Translator(str(model / 'model'), device='cpu', compute_type='int8', inter_threads=2, intra_threads=4)
converter = OpenCC('s2twp')
started = time.time()
for offset in range(0, len(pending), 24):
    batch = pending[offset:offset + 24]
    # Split long documentation into sentences so inference never truncates a source paragraph.
    segments = []
    counts = []
    for text in batch:
        pieces = re.split(r'(?<=[.!?])\s+(?=[A-Z])|\n+', text)
        parts = []
        for piece in pieces:
            tokens = tokenizer.encode(piece, out_type=str)
            for start in range(0, len(tokens), 220):
                parts.append(tokens[start:start + 220])
        counts.append(len(parts))
        segments.extend(parts)
    results = translator.translate_batch(segments, beam_size=2, max_batch_size=512, batch_type='tokens', max_decoding_length=512)
    position = 0
    for source, count in zip(batch, counts):
        translated = ''.join(tokenizer.decode(r.hypotheses[0]) for r in results[position:position + count])
        position += count
        translated = converter.convert(translated).replace('\u2581', ' ').strip()
        if not translated:
            raise RuntimeError('Empty translation: ' + source)
        cache[source] = translated
    # Resumable output; generation never touches hardware or the English metadata.
    save_json(output, cache)
    print(f'{min(offset + 24, len(pending))}/{len(pending)} elapsed={time.time()-started:.0f}s', flush=True)
manifest = dict(engine='Argos en_zh 1.9 / CTranslate2 int8 / OpenCC s2twp', reviewed=False,
                sources=sources, strings=len(texts), missing=sorted(texts-cache.keys()))
cache = {k: v.replace('\u2581', ' ').strip() for k, v in cache.items()}
save_json(output, cache)
save_json(output.with_suffix('.manifest.json'), manifest)
print('COMPLETE', flush=True)
