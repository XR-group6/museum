from pathlib import Path
import subprocess, re, json

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Temp' / 'VisualEffectFullAudit'
OUT.mkdir(parents=True, exist_ok=True)
REF = 'origin/VisualEffect'
def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)
def read(path):
    return git('show', REF + ':' + path)

paths = git('ls-tree', '-r', '--name-only', REF).decode().splitlines()
meta = git('grep', '-n', '^guid:', REF, '--', '*.meta').decode()
guids = {}
for line in meta.splitlines():
    match = re.match(r'origin/VisualEffect:(.+)\.meta:\d+:guid: ([0-9a-f]+)', line)
    if match: guids[match[2]] = match[1]

seed = [p for p in paths if p.startswith('Assets/Effect/') or p.startswith('Assets/Project3D/Scenes/')]
for p in seed:
    dest = OUT / 'source' / p
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(read(p))

report = {'missing_references': {}, 'source_scenes': {}, 'changed_files': git('diff','--no-renames','--name-status','HEAD',REF,'--','Assets','Packages','ProjectSettings').decode().splitlines()}
for p in seed:
    if p.endswith('.meta'): continue
    data = (OUT / 'source' / p).read_bytes().decode('utf-8', errors='ignore')
    for guid in set(re.findall(r'guid: ([0-9a-f]{32})', data)):
        if guid not in guids and not guid.startswith('0000000000000000'):
            report['missing_references'].setdefault(guid, []).append(p)
    if p.endswith('.unity'):
        sections = re.split(r'(?=^--- !u!)', data, flags=re.M)[1:]
        rows = []
        for section in sections:
            header = re.match(r'--- !u!(\d+) &(-?\d+)', section)
            name = re.search(r'^  m_Name: (.*)$', section, re.M)
            script = re.search(r'm_Script: .*guid: ([0-9a-f]+)', section)
            if header:
                rows.append({'type': int(header[1]), 'id': header[2], 'name': name[1] if name else '', 'script': guids.get(script[1], script[1]) if script else ''})
        report['source_scenes'][p] = rows
(OUT / 'guid_map.json').write_text(json.dumps(guids, indent=2), encoding='utf-8')
(OUT / 'audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('Source assets exported:', len(seed))
print('Missing GUIDs:', json.dumps(report['missing_references'], ensure_ascii=False))
print('Source effect scripts:')
for p, rows in report['source_scenes'].items():
    print(p, sorted(set(r['script'] for r in rows if r['script'])))
print('Changed source-only non-model files:')
for line in report['changed_files']:
    if line.startswith('A\t') and not any(x in line.lower() for x in ['.fbx', '.png', '.tga', '.jpg', '.wav', '.mp3', '.meta']): print(line)
