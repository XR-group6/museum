from pathlib import Path
import re, subprocess, json, uuid

ROOT = Path(__file__).resolve().parents[1]
AUDIT = ROOT / 'Temp/VisualEffectFullAudit'
SOURCE = AUDIT / 'source'
BACKUP = AUDIT / 'before_full_port'
def git(*args): return subprocess.check_output(['git', *args], cwd=ROOT)
def write(path, data):
    path = ROOT / path
    if path.exists():
        backup = BACKUP / path.relative_to(ROOT)
        if not backup.exists():
            backup.parent.mkdir(parents=True, exist_ok=True)
            backup.write_bytes(path.read_bytes())
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data if isinstance(data, bytes) else data.encode('utf-8'))

paths = git('ls-tree', '-r', '--name-only', 'origin/VisualEffect').decode().splitlines()
for path in paths:
    if (path.startswith('Assets/Effect/') or path.startswith('Assets/PandaShaderV2.2')
            or path.startswith('Assets/Project3D/Scripts/LightGroupFader.cs')):
        write(path, git('show', 'origin/VisualEffect:' + path))

scene = (SOURCE / 'Assets/Project3D/Scenes/scene_01.unity').read_text(encoding='utf-8-sig')
sections = {}
types = {}
for section in re.split(r'(?=^--- !u!)', scene, flags=re.M)[1:]:
    match = re.match(r'--- !u!(\d+) &(-?\d+)', section)
    sections[match[2]] = section
    types[match[2]] = int(match[1])
names = {key: re.search(r'^  m_Name: (.*)$', value, re.M)[1]
         for key,value in sections.items() if types[key] == 1}
selected_go = set()
for key,value in sections.items():
    if types[key] in (108, 198, 320):
        selected_go.add(re.search(r'm_GameObject: \{fileID: (\d+)\}',value)[1])
selected_go.update(key for key,name in names.items() if name in ('censer', 'LightControling'))
selected = {key for key,value in sections.items() if key in selected_go or
            any('m_GameObject: {fileID: ' + go + '}' in value for go in selected_go)}
# Ancestors needed by particle groups and source light transforms.
while True:
    parents = set()
    for key in selected:
        if types[key] == 4:
            match = re.search(r'm_Father: \{fileID: (\d+)\}', sections[key])
            if match and match[1] != '0' and match[1] not in selected: parents.add(match[1])
    if not parents: break
    for parent in parents:
        selected.add(parent)
        go = re.search(r'm_GameObject: \{fileID: (\d+)\}', sections[parent])[1]
        selected_go.add(go)
        selected.add(go)

root_go, root_transform = '8800000000000000001', '8800000000000000002'
root_children = []
output = []
for key,value in sections.items():
    if key not in selected: continue
    if types[key] == 4:
        father = re.search(r'm_Father: \{fileID: (\d+)\}', value)[1]
        if father == '0':
            value = value.replace('m_Father: {fileID: 0}', 'm_Father: {fileID: ' + root_transform + '}')
            root_children.append(key)
        # Remove child links to non-effect environment objects.
        value = re.sub(r'(  m_Children:\n)((?:  - \{fileID: \d+\}\n)*)',
            lambda m: m[1] + ''.join(line for line in m[2].splitlines(True)
                if re.search(r'fileID: (\d+)',line)[1] in selected), value)
    if types[key] == 320:
        value = re.sub(r'  m_InitialState: \d+', '  m_InitialState: 0', value)
    output.append(value)

header = f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{root_go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {root_transform}}}
  m_Layer: 0
  m_Name: CenserVisualEffectRig
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 0
--- !u!4 &{root_transform}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root_go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children:
'''
header += ''.join('  - {fileID: ' + key + '}\n' for key in root_children)
header += '  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n'
rig = header + ''.join(output)
# Fail if any local reference was left pointing at a removed scene object.
ids = selected | {root_go,root_transform,'0'}
dangling = set(re.findall(r'\{fileID: (-?\d+)\}',rig)) - ids
if dangling: raise RuntimeError('Dangling prefab references: ' + str(dangling))
write('Assets/Effect/Resources/CenserVisualEffectRig.prefab', rig)
write('Assets/Effect/Resources/CenserVisualEffectRig.prefab.meta',
      'fileFormatVersion: 2\nguid: d148fb51b97f42cbb7b45d7d4b879889\nPrefabImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
print('Rig objects:', [names[x] for x in selected_go])
print('Particles:', sum(types[x] == 198 for x in selected), 'Lights:', sum(types[x] == 108 for x in selected))
print('Original shader, materials, particle prefab, Timeline and light script restored.')
