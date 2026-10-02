import json,struct
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
results=[]
for f in sorted((ROOT/'Assets/IncenseCharacters/Source').glob('*.json')):
 d=json.loads(f.read_text());n=len(d['positions'])//3;nb=len(d['bones']);v=np.array(d['positions']).reshape(-1,3);weights=np.array(d['weights']).reshape(-1,4);joints=np.array(d['joints']).reshape(-1,4);normals=np.array(d['normals']).reshape(-1,3)
 assert np.isfinite(v).all() and np.isfinite(normals).all()
 assert np.allclose(np.linalg.norm(normals,axis=1),1,atol=1e-5)
 assert np.allclose(weights.sum(1),1) and np.all(weights>=0)
 assert joints.min()>=0 and joints.max()<nb
 assert all(b['parent']<i for i,b in enumerate(d['bones']))
 for sub in d['submeshes']:
  a=np.array(sub['indices']);
  if not len(a):continue
  assert len(a)%3==0 and a.min()>=0 and a.max()<n
 for c in d['clips']:
  times=[k['time'] for k in c['keys']];assert times[0]==0 and times[-1]==c['duration'] and all(a<b for a,b in zip(times,times[1:]))
  for k in c['keys']:
   q=np.array(k['rotations']).reshape(-1,4);assert len(q)==nb and np.allclose(np.linalg.norm(q,axis=1),1)
   assert len(k['positions'])==nb*3
  if c['loop']:assert np.allclose(c['keys'][0]['rotations'],c['keys'][-1]['rotations']) and np.allclose(c['keys'][0]['positions'],c['keys'][-1]['positions'])
 raw=(ROOT/'GLB'/(d['name']+'.glb')).read_bytes();magic,ver,size=struct.unpack_from('<III',raw);assert magic==0x46546c67 and ver==2 and size==len(raw)
 jl,jt=struct.unpack_from('<II',raw,12);g=json.loads(raw[20:20+jl]);bl,bt=struct.unpack_from('<II',raw,20+jl);assert bt==0x004e4942
 assert len(g['animations'])==17 and len(g['skins'][0]['joints'])==nb
 for bv in g['bufferViews']:assert bv.get('byteOffset',0)+bv['byteLength']<=bl
 for ac in g['accessors']:
  dim={'SCALAR':1,'VEC3':3,'VEC4':4,'MAT4':16}[ac['type']];unit={5123:2,5125:4,5126:4}[ac['componentType']];assert ac['count']*dim*unit<=g['bufferViews'][ac['bufferView']]['byteLength']
 results.append({'model':d['name'],'status':'passed','triangles':sum(len(s['indices'])//3 for s in d['submeshes']),'bones':nb,'clips':len(d['clips']),'checks':['GLB chunk sizes and accessor bounds','finite geometry and unit normals','triangle indices','skin weights sum to one','acyclic parent hierarchy','unit rotation quaternions','increasing key times','matching loop endpoints']})
report={'scope':'Data validation only; Unity compilation, editor import and XR device playback NOT tested. Preview images rendered from actual model and skin-animation data.','assets':results}
(ROOT/'validation_report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
