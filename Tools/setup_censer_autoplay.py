from pathlib import Path
import sys, subprocess, hashlib, json, re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'Library/CenserScenePlayback'
OUT.mkdir(parents=True,exist_ok=True)
if sys.argv[1]=='request':
    path = ROOT/'Assets/Project3D/Scenes/scene_01.unity'
    backup = OUT/'scene_01.before.unity'
    if not backup.exists(): backup.write_bytes(path.read_bytes())
    protected = [ROOT/'Assets/Project3D/Scenes/scene_02.unity',ROOT/'Assets/Project3D/Scenes/scene_03.unity',ROOT/'Assets/Scripts/RightController.cs']
    (OUT/'protected.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}))
    (OUT/'install.request').write_text('Install the authorized scene_01 censer visual replacement.\n')
    print('Installation requested; scene_01 backup saved.')
elif sys.argv[1]=='status':
    for name in ['result.txt','failure.txt','editor-state.txt']:
        p=OUT/name
        if p.exists(): print(name+': '+p.read_text(encoding='utf-8'))
    print('Pending:',(OUT/'install.request').exists())
    for p,h in json.loads((OUT/'protected.json').read_text()).items():
        print(p,'unchanged:',hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h)
    log=Path.home()/'AppData/Local/Unity/Editor/Editor.log'
    if log.exists():
        lines=log.read_text(errors='replace').splitlines()
        print('\n'.join(l for l in lines[-1500:] if 'error CS' in l or 'censer replacement' in l or 'CenserScenePlayback' in l)[-4000:])
        print('\n'.join(lines[-18:]))
elif sys.argv[1]=='verify':
    def blocks(text):
        return {m[2]:(int(m[1]),m[0]) for s in re.split(r'(?=^--- !u!)',text,flags=re.M)[1:] if (m:=re.match(r'--- !u!(\d+) &(-?\d+)[^\n]*\n[\s\S]*',s))}
    before=blocks((OUT/'scene_01.before.unity').read_text(encoding='utf-8-sig'))
    after=blocks((ROOT/'Assets/Project3D/Scenes/scene_01.unity').read_text(encoding='utf-8-sig'))
    types={54,64,65,95,114,135,136,320}
    for key,(typ,block) in before.items():
        if typ in types:
            assert key in after and after[key][1]==block, 'Existing component changed: '+key
    text=(ROOT/'Assets/Project3D/Scenes/scene_01.unity').read_text(encoding='utf-8-sig')
    assert 'playOnStart: 1' in text
    assert '74c2665d01b49b44794f30491ebaf5ec' in text
    for p,h in json.loads((OUT/'protected.json').read_text()).items():
        assert hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h,p
    print('PASS saved autoplay prefab and original script, Collider, Rigidbody, Animator, Timeline serialized components unchanged.')
    print(subprocess.check_output(['git','diff','--stat','--','Assets/Effect/Script/CenserVisualEffect.cs','Assets/Project3D/Scenes/scene_01.unity'],cwd=ROOT).decode())
