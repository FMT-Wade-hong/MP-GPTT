"""Download the two CAA layers into a validated, compressed release resource."""
import gzip
import json
import pathlib
import urllib.request
from datetime import datetime, timezone

base = 'https://dronegis.caa.gov.tw/server/rest/services/Hosted/UAV_fs/FeatureServer'

def query(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0 FMTPlanner/1.2.3'})
    with urllib.request.urlopen(request, timeout=90) as response:
        result = json.load(response)
    if 'error' in result:
        raise RuntimeError(result['error'])
    return result

layers = {}
for layer in (1, 2):
    url = f'{base}/{layer}/query?where=1%3D1'
    count_url = url + '&returnCountOnly=true&f=json'
    expected = query(count_url)['count']
    features = []
    while len(features) < expected:
        page = query(url + '&outSR=4326&returnGeometry=true&outFields=*&orderByFields=objectid'
                     f'&resultRecordCount=2000&resultOffset={len(features)}&f=geojson')
        batch = page.get('features', [])
        if not batch:
            raise RuntimeError('Incomplete layer')
        features.extend(batch)
        print(f'Layer {layer}: {len(features)}/{expected}', flush=True)
    ids = [f['properties'].get('objectid', f['properties'].get('OBJECTID')) for f in features]
    assert expected > 0 and len(features) == expected == query(count_url)['count']
    assert None not in ids and len(set(ids)) == expected
    assert all(f.get('geometry', {}).get('type') in ('Polygon', 'MultiPolygon') for f in features)
    layers[str(layer)] = {'expectedCount': expected, 'features': features}
root = {'schema': 1, 'downloadedUtc': datetime.now(timezone.utc).isoformat(),
        'source': base, 'layers': layers}
target = pathlib.Path(__file__).parent / 'Assets' / 'taiwan-caa-v1.json.gz'
data = gzip.compress(json.dumps(root, ensure_ascii=False, separators=(',', ':')).encode('utf-8'))
target.write_bytes(data)
print(f'Saved {target}: {len(data)} bytes')
