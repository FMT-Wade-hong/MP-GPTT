"""Reproduce documented parameters using the parser from each official release tag.

Only source archives/public metadata are downloaded. No aircraft data is used.
Run with Python and lxml installed; --tags is git ls-remote output for ArduPilot.
"""
import argparse
import concurrent.futures
import gzip
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
import tarfile
import urllib.request
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument('--tags', required=True)
parser.add_argument('--cache', default='tmp/ardupilot-parameter-sources')
parser.add_argument('--output', default='ExtLibs/Utilities/ParameterMetadata')
args = parser.parse_args()
cache, output = Path(args.cache).resolve(), Path(args.output).resolve()
cache.mkdir(parents=True, exist_ok=True)
output.mkdir(parents=True, exist_ok=True)
releases = []
for line in Path(args.tags).read_text(encoding='utf-8-sig').splitlines():
    match = re.fullmatch(r'([0-9a-f]{40})\s+refs/tags/((Copter|Plane|Rover|Sub|Tracker)-((?:4\.6|4\.7)\.\d+))', line)
    if match:
        releases.append(dict(commit=match[1], tag=match[2], family=match[3], version=match[4]))
if not releases:
    raise RuntimeError('No official stable release tags supplied')

def source(commit):
    directory = cache / commit
    ready = directory / '.source-ready'
    if ready.exists():
        return directory
    archive = cache / (commit + '.tar.gz')
    url = 'https://codeload.github.com/ArduPilot/ardupilot/tar.gz/' + commit
    if not archive.exists():
        temporary = archive.with_suffix('.download')
        urllib.request.urlretrieve(url, temporary)
        temporary.replace(archive)
    directory.mkdir(exist_ok=True)
    # Extract ordinary source files only, never archive links or arbitrary paths.
    with tarfile.open(archive) as tar:
        for entry in tar:
            parts = PurePosixPath(entry.name).parts[1:]
            if not entry.isfile() or not parts or any(p in ('..', '.') for p in parts):
                continue
            relative = PurePosixPath(*parts)
            if parts[0] not in ('libraries', 'ArduCopter', 'ArduPlane', 'Rover', 'ArduSub', 'AntennaTracker', 'Blimp', 'Tools'):
                continue
            if relative.suffix not in ('.cpp', '.h', '.py', '.lua', '.json'):
                continue
            target = directory.joinpath(*parts).resolve()
            if not target.is_relative_to(directory):
                raise RuntimeError('Archive path escapes source directory')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(tar.extractfile(entry).read())
    ready.write_text(url, encoding='utf-8')
    print('Source ready: ' + commit, flush=True)
    return directory

with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
    sources = dict(zip(sorted({r['commit'] for r in releases}), pool.map(source, sorted({r['commit'] for r in releases}))))

manifest = []
for release in releases:
    key = release['family'] + release['version']
    directory = sources[release['commit']]
    generated = cache / ('output-' + key)
    generated.mkdir(exist_ok=True)
    command = [sys.executable, '-X', 'utf8', str(Path(__file__).with_name('Emit-ParameterMetadata.py').resolve()),
               str(directory / 'Tools/autotest/param_metadata/param_parse.py'),
               '--vehicle', release['family'], '--format', 'xml']
    result = subprocess.run(command, cwd=generated, capture_output=True, text=True, encoding='utf-8')
    (generated / 'generator.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    xml = generated / 'apm.pdef.xml'
    # Upstream parser may report documentation validation errors while emitting
    # a complete XML. Record the exit status and diagnostics, never suppress it.
    if not xml.exists():
        raise RuntimeError(key + ': ' + result.stderr)
    raw = xml.read_bytes()
    parameters = ET.fromstring(raw).findall('.//param')
    if len(parameters) < 500:
        raise RuntimeError('Incomplete output: ' + key)
    bundled = output / (key + '.apm.pdef.xml.gz')
    bundled.write_bytes(gzip.compress(raw, mtime=0))
    manifest.append(dict(**release, key=key, file=bundled.name, parameters=len(parameters),
                         sha256=hashlib.sha256(raw).hexdigest(), generator_exit=result.returncode,
                         source='https://github.com/ArduPilot/ardupilot/tree/' + release['commit']))
    print(f'{key}: {len(parameters)} parameters; parser exit {result.returncode}', flush=True)
(output / 'manifest.json').write_text(json.dumps(dict(schema=1, releases=manifest), indent=2) + '\n', encoding='utf-8')
