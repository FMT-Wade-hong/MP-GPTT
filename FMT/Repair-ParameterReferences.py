"""Repair draft reference loss by retaining source IDs verbatim between translated spans."""
import json
import re
import os
import time
from pathlib import Path
import ctranslate2
import sentencepiece
from opencc import OpenCC

path = Path('FMT/Localization/Parameters.zh-TW.draft.json')
data = json.loads(path.read_text(encoding='utf-8'))
pattern = re.compile(r'(\b[A-Z][A-Z0-9]*_[A-Z0-9_]+\b)')
pending = [s for s, t in data.items() if any(ref not in t for ref in pattern.findall(s))]
model = Path('bin/TranslationRuntime/model/translate-en_zh-1_9')
sp = sentencepiece.SentencePieceProcessor(model_file=str(model / 'sentencepiece.model'))
tr = ctranslate2.Translator(str(model / 'model'), device='cpu', compute_type='int8', inter_threads=2, intra_threads=4)
cc = OpenCC('s2twp')
for start in range(0, len(pending), 12):
    batch = pending[start:start+12]
    fragments = {}
    for source in batch:
        for fragment in pattern.split(source)[::2]:
            if fragment.strip() and not re.fullmatch(r'[\W\d_]+', fragment):
                for sentence in re.split(r'(?<=[.!?])\s+(?=[A-Z])|\n+', fragment):
                    if sentence.strip(): fragments[sentence] = None
    for fragment in fragments:
        tokens = sp.encode(fragment, out_type=str)
        chunks = [tokens[i:i+220] for i in range(0, len(tokens), 220)]
        results = tr.translate_batch(chunks, beam_size=2, max_decoding_length=512)
        fragments[fragment] = cc.convert(''.join(sp.decode(r.hypotheses[0]) for r in results)).replace('\u2581', ' ').strip()
    for source in batch:
        output = []
        for index, fragment in enumerate(pattern.split(source)):
            if index % 2 or not fragment.strip() or re.fullmatch(r'[\W\d_]+', fragment):
                output.append(fragment)
            else:
                output.append(''.join(fragments[s] for s in re.split(r'(?<=[.!?])\s+(?=[A-Z])|\n+', fragment) if s.strip()))
        data[source] = ' '.join(output).strip()
        assert all(ref in data[source] for ref in pattern.findall(source))
    temporary = path.with_suffix('.repair.tmp')
    for attempt in range(10):
        try:
            temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True), encoding='utf-8')
            os.replace(temporary, path)
            break
        except OSError:
            if attempt == 9: raise
            time.sleep(0.5)
    print(f'REPAIRED {min(start+12,len(pending))}/{len(pending)}', flush=True)
