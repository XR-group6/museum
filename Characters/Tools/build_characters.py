"""Original procedural low-poly character assets. Python 3 + numpy/scipy/Pillow.
Coordinates: metres, Y up, +Z forward. Run from anywhere; output relative to package.
"""
from pathlib import Path
import numpy as np, math, json, struct, copy
from scipy.spatial.transform import Rotation as R
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Assets/IncenseCharacters/Source'
SOURCE.mkdir(parents=True,exist_ok=True)

def quat(deg): return R.from_euler('xyz',deg,degrees=True).as_quat().tolist()
def norm(v):
 v=np.asarray(v,float); return v/max(np.linalg.norm(v),1e-10)

class Character:
 def __init__(self,name,scale,headscale,female,old,shirt,pants):
  self.name=name; self.s=scale; self.hs=headscale; self.female=female; self.old=old
  self.bones=[]; self.bn={}; self.verts=[]; self.normals=[]; self.weights=[]; self.joints=[]; self.tris=[]; self.mids=[]
  skin=[.63,.405,.245,1] if old else [.72,.49,.31,1]
  self.mats=[('Skin',skin),('Shirt',shirt+[1]),('Trousers',pants+[1]),('Hair',([.40,.41,.38,1] if old else [.065,.047,.035,1])),('Shoe',[.105,.092,.076,1]),('EyeWhite',[.82,.79,.66,1]),('Iris',[.09,.063,.042,1]),('Lip',[.40,.22,.16,1]),('ClothTrim',[v*.76 for v in shirt]+[1]),('Button',[.28,.25,.19,1]),('Sole',[.065,.060,.05,1])]
  self.mats.extend([('InnerCotton',[.70,.65,.52,1]),('Knit',[.20,.22,.20,1]),('HairLight',[.09,.065,.044,1])])
  self.rig(); self.body(); self.actions=self.animate()
 def p(self,p):
  x,y,z=p; ss=self.s
  if y>1.465: return np.array([x*self.hs,1.465*ss+(y-1.465)*self.hs,z*self.hs])
  return np.array(p)*ss
 def bone(self,name,pos,parent=None):
  p=self.p(pos); pi=self.bn[parent] if parent else -1
  i=len(self.bones); self.bn[name]=i
  self.bones.append({'name':name,'parent':pi,'position':p.tolist(),'localPosition':(p-(np.array(self.bones[pi]['position']) if pi>=0 else 0)).tolist()})
 def rig(self):
  for n,p,parent in [('Root',(0,0,0),None),('Hips',(0,.91,0),'Root'),('Spine',(0,1.04,0),'Hips'),('Chest',(0,1.27,0),'Spine'),('Neck',(0,1.445,0),'Chest'),('Head',(0,1.49,0),'Neck')]: self.bone(n,p,parent)
  for side,s in [('Left',1),('Right',-1)]:
   for n,p,parent in [('Shoulder',(.10,1.405,0),'Chest'),('UpperArm',(.19,1.345,0),side+'Shoulder'),('LowerArm',(.305,1.12,.015),side+'UpperArm'),('Hand',(.365,.90,.035),side+'LowerArm'),('UpperLeg',(.09,.90,0),'Hips'),('LowerLeg',(.105,.49,.025),side+'UpperLeg'),('Foot',(.105,.12,0),side+'LowerLeg'),('Toes',(.105,.055,.14),side+'Foot')]:
    self.bone(side+n,(p[0]*s,p[1],p[2]),parent)
   for f,dx,length in [('Index',-.022,.081),('Middle',-.006,.091),('Ring',.011,.083),('Little',.026,.065)]:
    for j in range(3): self.bone(side+f+str(j+1),(s*(.385+dx),.819-j*length/3,.040),side+'Hand' if j==0 else side+f+str(j))
   self.bone(side+'Thumb1',(s*.354,.853,.048),side+'Hand');self.bone(side+'Thumb2',(s*.339,.832,.066),side+'Thumb1')
  self.bone('Grip_R',(-.375,.832,.068),'RightHand');self.bone('Grip_L',(.375,.832,.068),'LeftHand')
 def w(self,*args): return {self.bn[n]:v for n,v in args}
 def mesh(self,vs,fs,mat,ws,scaled=False):
  vs=np.array(vs) if scaled else np.array([self.p(v) for v in vs])
  if isinstance(ws,dict):ws=[ws]*len(vs)
  for original_face in fs:
   face=[]
   for vi in original_face:
    if not any(np.linalg.norm(vs[vi]-vs[vj])<1e-8 for vj in face):face.append(vi)
   if len(face)<3:continue
   if np.linalg.norm(np.cross(vs[face[1]]-vs[face[0]],vs[face[2]]-vs[face[0]]))<1e-10:continue
   # Preserve planar quads with shared normal, then triangulate.
   n=norm(np.cross(vs[face[1]]-vs[face[0]],vs[face[2]]-vs[face[0]])); start=len(self.verts)
   for v in face:
    self.verts.append(vs[v].tolist()); self.normals.append(n.tolist())
    ww=sorted(ws[v].items(),key=lambda a:-a[1])[:4]; total=sum(a[1] for a in ww)
    self.joints.append([a[0] for a in ww]+[0]*(4-len(ww)));self.weights.append([a[1]/total for a in ww]+[0]*(4-len(ww)))
   for j in range(1,len(face)-1):self.tris.append([start,start+j,start+j+1]);self.mids.append(mat)
 def loft(self,rings,mat,ws,n=12):
  # rings: x,y,z, radius x, radius z; order bottom to top
  vs=[]
  for x,y,z,rx,rz in rings:
   for j in range(n):
    a=2*math.pi*j/n;vs.append((x+rx*math.cos(a),y,z+rz*math.sin(a)))
  fs=[tuple(reversed(range(n)))]
  for k in range(len(rings)-1):
   for j in range(n):a=k*n+j;b=k*n+(j+1)%n;fs.append((a,b,b+n,a+n))
  fs.append(tuple((len(rings)-1)*n+j for j in range(n)))
  # Reverse to outward, ring loop in x/z is clockwise when viewed from Y+.
  self.mesh(vs,[tuple(reversed(f)) for f in fs],mat,[w for w in ws for _ in range(n)])
 def ellipsoid(self,center,scale,mat,w,n=10,m=6):
  if mat==9 and center[2]>0 and .93<center[1]<1.44:
   center=(center[0],center[1],self.cloth_z(center[0],center[1])+scale[2]+.002)
  vs=[]
  for k in range(m+1):
   a=-math.pi/2+k*math.pi/m
   for j in range(n):
    b=2*math.pi*j/n;vs.append((center[0]+scale[0]*math.cos(a)*math.cos(b),center[1]+scale[1]*math.sin(a),center[2]+scale[2]*math.cos(a)*math.sin(b)))
  fs=[]
  for k in range(m):
   for j in range(n):fs.append((k*n+j,(k+1)*n+j,(k+1)*n+(j+1)%n,k*n+(j+1)%n))
  self.mesh(vs,fs,mat,w)
 def tube(self,points,radii,mat,ws,n=10):
  if mat in [8,9] and all(abs(p[0])<.18 and .93<p[1]<1.44 and p[2]>0 for p in points):
   points=[(p[0],p[1],self.cloth_z(p[0],p[1])+.003) for p in points]
  vs=[]
  for k,p in enumerate(points):
   axis=norm(np.array(points[min(k+1,len(points)-1)])-np.array(points[max(k-1,0)]));u=norm(np.cross(axis,[0,0,1]));v=norm(np.cross(axis,u))
   for j in range(n):
    a=j*2*math.pi/n;vs.append(np.array(p)+u*radii[k][0]*math.cos(a)+v*radii[k][1]*math.sin(a))
  fs=[tuple(reversed(range(n)))]
  for k in range(len(points)-1):
   for j in range(n):fs.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
  fs.append(tuple((len(points)-1)*n+j for j in range(n)))
  self.mesh(vs,fs,mat,[w for w in ws for _ in range(n)])
 def patch(self,vs,mat,bone):
  if mat in [8,11,12] and all(v[2]>0 and .90<v[1]<1.44 for v in vs):
   # Subdivide the overlay and sample the actual triangulated shirt surface.
   # Reuse interpolated garment weights so neckline and pockets remain attached.
   for k in range(1,len(vs)-1):
    a,b,c=np.array(vs[0]),np.array(vs[k]),np.array(vs[k+1]);verts=[];weights=[];lookup={};n=5
    for i in range(n+1):
     for j in range(n+1-i):
      v=a+(b-a)*i/n+(c-a)*j/n;z,ww=self.cloth_surface(v[0],v[1]);lookup[i,j]=len(verts);verts.append((v[0],v[1],z+.004));weights.append(ww)
    faces=[]
    for i in range(n):
     for j in range(n-i):
      faces.append((lookup[i,j],lookup[i+1,j],lookup[i,j+1]))
      if i+j<n-1:faces.append((lookup[i+1,j],lookup[i+1,j+1],lookup[i,j+1]))
    for ii,f in enumerate(faces):
     pp=np.array([verts[j] for j in f])
     if np.cross(pp[1]-pp[0],pp[2]-pp[0])[2]<0:faces[ii]=tuple(reversed(f))
    self.mesh(verts,faces,mat,weights)
   return
  ids=list(range(len(vs)))
  if np.cross(np.array(vs[1])-vs[0],np.array(vs[2])-vs[0])[2]<0:ids.reverse()
  self.mesh(vs,[tuple(ids)],mat,self.w((bone,1)))
 def cloth_surface(self,x,y):
  pp=self.garment_front;den=(pp[:,1,1]-pp[:,2,1])*(pp[:,0,0]-pp[:,2,0])+(pp[:,2,0]-pp[:,1,0])*(pp[:,0,1]-pp[:,2,1])
  den=np.where(abs(den)<1e-12,1e-12,den)
  aa=((pp[:,1,1]-pp[:,2,1])*(x-pp[:,2,0])+(pp[:,2,0]-pp[:,1,0])*(y-pp[:,2,1]))/den
  bb=((pp[:,2,1]-pp[:,0,1])*(x-pp[:,2,0])+(pp[:,0,0]-pp[:,2,0])*(y-pp[:,2,1]))/den;cc=1-aa-bb
  zz=aa*pp[:,0,2]+bb*pp[:,1,2]+cc*pp[:,2,2];mask=(aa>=-1e-5)&(bb>=-1e-5)&(cc>=-1e-5)
  if np.any(mask):
   i=int(np.argmax(np.where(mask,zz,-np.inf)));ww={}
   for fac,w in zip([aa[i],bb[i],cc[i]],self.garment_front_weights[i]):
    for bone,value in w.items():ww[bone]=ww.get(bone,0)+max(0,float(fac))*value
   return float(zz[i]),ww
  return .05,self.w(('Chest',1))
 def cloth_z(self,x,y):return self.cloth_surface(x,y)[0]
 def garment(self):
  w=self.w;child=self.s<.8;adult=self.female and not child
  width=.163 if self.female else .182
  hem=.848 if child else .858 if not self.old and not adult else .89
  rings=[(hem,width+.010,.108),(.95,width,.111),(1.075,width-.003,.112),(1.19,width+.003,.118),(1.245,width+.005,.119),(1.375,.188 if self.female else .201,.091),(1.439,.059,.048)]
  self.garment_rings=rings
  vs=[];ws=[];n=12
  for k,(y,rx,rz) in enumerate(rings):
   for j in range(n):
    a=2*math.pi*j/n;side='Left' if math.cos(a)>0 else 'Right';y1=y+(.008*abs(math.sin(a)) if k==5 else 0)
    vs.append((rx*math.cos(a),y1,rz*math.sin(a)))
    if k<2:ww=w(('Hips',1))
    elif k==2:ww=w(('Hips',.25),('Spine',.75))
    elif k==3:ww=w(('Spine',.55),('Chest',.45))
    else:
     blend=(.16 if k==4 else .20 if k==5 else .02)*abs(math.cos(a))**4
     ww=w(('Chest',1-blend),(side+'UpperArm',blend))
    ws.append(ww)
  fs=[]
  for k in range(len(rings)-1):
   for j in range(n):
    if k==4 and j in [11,0,5,6]:continue
    a=k*n+j;b=k*n+(j+1)%n;fs.append((a,a+n,b+n,b))
  fs.append(tuple(range(n)));fs.append(tuple(reversed([(len(rings)-1)*n+j for j in range(n)])))
  front=[];fw=[]
  for face in fs:
   for k in range(1,len(face)-1):
    ii=[face[0],face[k],face[k+1]];pp=np.array([vs[j] for j in ii])
    if np.cross(pp[1]-pp[0],pp[2]-pp[0])[2]>1e-10:
     front.append(pp);fw.append([ws[j] for j in ii])
  self.garment_front=np.array(front);self.garment_front_weights=fw
  self.mesh(vs,fs,12 if self.old else 1,ws)
  for side,sig in [('Left',1),('Right',-1)]:
   ua=side+'UpperArm';la=side+'LowerArm'
   # The first sleeve loop reuses the torso armhole positions AND weights.
   js=[11,0,1] if sig==1 else [7,6,5]
   ids=[4*n+j for j in js]+[5*n+j for j in reversed(js)]
   sv=[vs[j] for j in ids];sw=[ws[j] for j in ids]
   points=[(sig*.235,1.28,0),(sig*.286,1.16,.012),(sig*.31,1.105,.017),(sig*.340,1.,.025),(sig*.362,.922,.033)]
   radii=[(.064,.068),(.059,.060),(.053,.056),(.045,.047),(.035,.038)]
   ww=[w((ua,1)),w((ua,.85),(la,.15)),w((ua,.3),(la,.7)),w((la,1)),w((la,1))]
   for k,center in enumerate(points):
    axis=norm(np.array(points[min(k+1,len(points)-1)])-np.array((sig*.19,1.345,0) if k==0 else points[k-1]));u=norm(np.cross(axis,[0,0,sig]));vv=norm(np.cross(axis,u))
    for deg in [60,0,-60,-120,-180,-240]:
     a=math.radians(deg);v=np.array(center)+u*radii[k][0]*math.cos(a)+vv*radii[k][1]*math.sin(a)*sig
     sv.append(v);sw.append(ww[k])
   sf=[]
   for k in range(5):
    for j in range(6):
     face=(k*6+j,k*6+(j+1)%6,(k+1)*6+(j+1)%6,(k+1)*6+j)
     # Orient outward relative to sleeve axis.
     pp=np.array([sv[x] for x in face]);nn=np.cross(pp[1]-pp[0],pp[2]-pp[0]);mid=pp.mean(0);c0=np.array(sv[k*6:(k+1)*6]).mean(0);c1=np.array(sv[(k+1)*6:(k+2)*6]).mean(0);axis=norm(c1-c0);rad=mid-c0-axis*np.dot(mid-c0,axis)
     if np.dot(nn,rad)<0:face=tuple(reversed(face))
     sf.append(face)
   self.mesh(sv,sf,1,sw)
 def clothes_details(self):
  w=self.w;child=self.s<.8;adult=self.female and not child
  if not self.old and not adult:
   # Low stand collar, cotton front and cloth fastenings for the earlier period.
   self.loft([(0,1.416,0,.060,.051),(0,1.452,0,.059,.049)],8,[w(('Neck',1))]*2,12)
   if child:
    line=[(-.022,1.42,.059),(.071,1.347,.095),(.146,1.273,.072),(.157,1.15,.063),(.156,.96,.065)]
    for a,b in zip(line,line[1:]):
     self.tube([a,b],[(.004,.004),(.004,.004)],8,[w(('Chest' if a[1]>1.2 else 'Spine',1)),w(('Chest' if b[1]>1.2 else 'Spine',1))],6)
    for x,y,z in [(.060,1.357,.100),(.134,1.29,.080),(.158,1.165,.063),(.16,1.052,.062)]:
     self.tube([(x-.014,y,z),(x+.014,y,z)],[(.004,.004)]*2,9,[w(('Chest' if y>1.2 else 'Spine',1))]*2,6)
   else:
    self.patch([(-.009,1.419,.057),(.009,1.419,.057),(.009,.87,.115),(-.009,.87,.115)],8,'Spine')
    for y,z in [(1.385,.084),(1.29,.123),(1.19,.123),(1.075,.117),(.965,.116)]:
     bone='Chest' if y>1.2 else 'Spine' if y>1.0 else 'Hips'
     self.tube([(-.016,y,z),(.016,y,z)],[(.0035,.004)]*2,9,[w((bone,1))]*2,6)
     self.ellipsoid((.007,y,z+.003),(.006,.006,.004),9,w((bone,1)),6,4)
   # Plain patch low pockets on the father's cotton short jacket.
   if not child:
    for sign in [-1,1]:
     self.patch([(sign*.066,1.062,.110),(sign*.140,1.062,.077),(sign*.146,.956,.073),(sign*.064,.956,.112)],8,'Spine')
  else:
   # Later period: fold-down collar, visible inner blouse/shirt, knit layering.
   for sig in [-1,1]:
    self.patch([(sig*.006,1.432,.050),(sig*.064,1.429,.052),(sig*.093,1.381,.083),(sig*.044,1.338,.108)],11,'Chest')
   if self.old:
    # Vest is the torso surface itself; an inset V follows the chest contours.
    rr=[(1.432,.057,.053),(1.383,.046,.094),(1.322,.025,.116),(1.255,.001,.126)]
    vv=[]
    for y,half,z in rr:vv.extend([(-half,y,self.cloth_z(-half,y)+.003),(half,y,self.cloth_z(half,y)+.003)])
    
    for face in [(0,2,3,1),(2,4,5,3),(4,6,7,5)]:self.patch([vv[j] for j in face],11,'Chest')
    for y,z in [(1.385,.099),(1.33,.119)]:self.ellipsoid((0,y,z),(.004,.004,.003),9,w(('Chest',1)),6,4)
   else:
    # Cream blouse opening and subtle cardigan borders; everyday 1970s-80s look.
    self.patch([(-.040,1.378,.085),(.040,1.378,.085),(.046,1.245,.129),(-.046,1.245,.129)],11,'Chest')
    self.patch([(-.045,1.244,.128),(.045,1.244,.128),(.043,1.077,.121),(-.043,1.077,.121)],11,'Spine')
    for sig in [-1,1]:
     self.patch([(sig*.045,1.377,.084),(sig*.056,1.377,.081),(sig*.057,1.244,.125),(sig*.046,1.244,.13)],8,'Chest')
     self.patch([(sig*.046,1.244,.129),(sig*.057,1.244,.126),(sig*.057,1.074,.118),(sig*.045,1.074,.122)],8,'Spine')
    for y,z in [(1.185,.127),(1.09,.123),(.989,.12)]:self.ellipsoid((.047,y,z),(.007,.007,.004),9,w(('Spine' if y>1.02 else 'Hips',1)),8,4)
    for sig in [-1,1]:self.patch([(sig*.080,1.06,.111),(sig*.147,1.06,.076),(sig*.147,.978,.078),(sig*.080,.978,.112)],8,'Spine')
 def hair(self):
  w=self.w;adult=self.female and self.s>.8;n=24;vs=[]
  if adult:
   # One fitted bob volume, with an ear-length side/back silhouette.
   for k in range(5):
    for j in range(n):
     a=2*math.pi*j/n;front=max(0,math.sin(a))**.6
     y=[1.777,1.746,1.690,1.610,1.508][k]+[0,0,.008,.056,.149][k]*front
     rx=[.004,.075,.107,.119,.104][k];rz=[.004,.077,.102,.111,.103][k]
     vs.append((rx*math.cos(a),y,-.012+rz*math.sin(a)))
  else:
   for k in range(5):
    for j in range(n):
     a=2*math.pi*j/n;front=max(0,math.sin(a));back=max(0,-math.sin(a));side=math.cos(a)
     y=[1.767,1.748,1.711,1.673,1.615+.062*front-.025*back][k]
     rx=[.004,.063,.090,.105,.100][k];rz=[.004,.060,.085,.098,.091][k]
     vs.append((rx*math.cos(a),y,-.009+rz*math.sin(a)))
  fs=[]
  for k in range(4):
   for j in range(n):fs.append((k*n+j,k*n+(j+1)%n,(k+1)*n+(j+1)%n,(k+1)*n+j))
  # Top-to-bottom rings; orient outward.
  self.mesh(vs,fs,3,w(('Head',1)))
  if adult:
   # Swept fringe lies on the hair volume rather than separate sphere clumps.
   self.patch([(-.078,1.670,.070),(-.049,1.719,.082),(.033,1.733,.077),(.083,1.683,.060),(.012,1.665,.103)],3,'Head')
   self.tube([(-.030,1.756,.040),(-.039,1.725,.086),(-.055,1.689,.098)],[(.002,.002)]*3,13,[w(('Head',1))]*3,5)
  else:
   self.patch([(-.086,1.674,.054),(-.066,1.715,.06),(.031,1.724,.075),(.074,1.685,.066),(.012,1.688,.094)],3,'Head')
   if self.female:
    for sig in [-1,1]:
     self.tube([(sig*.097,1.598,-.029),(sig*.115,1.538,-.043),(sig*.118,1.47,-.047)],[(.023,.024),(.023,.022),(.012,.012)],3,[w(('Head',1))]*3,8)
     self.ellipsoid((sig*.117,1.505,-.045),(.024,.008,.023),8,w(('Head',1)),8,4)
 def body(self):
  w=self.w; old=self.old
  # A single connected shirt surface: armhole boundaries bridge to sleeves.
  self.garment()
  # trouser seat
  self.loft([(0,.795,0,.157,.092),(0,.89,0,.165,.108),(0,.926,0,.161,.10)],2,[w(('Hips',1))]*3)
  for side,s in [('Left',1),('Right',-1)]:
   ul=side+'UpperLeg';ll=side+'LowerLeg';ft=side+'Foot';ua=side+'UpperArm';la=side+'LowerArm';hand=side+'Hand'
   pts=[(s*.084,.865,0),(s*.097,.70,0),(s*.105,.515,.022),(s*.105,.465,.025),(s*.105,.32,.009),(s*.105,.135,0)]
   self.tube(pts,[(.081,.094),(.077,.087),(.068,.072),(.067,.071),(.064,.070),(.058,.064)],2,[w((ul,1)),w((ul,1)),w((ul,.65),(ll,.35)),w((ul,.25),(ll,.75)),w((ll,1)),w((ll,1))])
   self.loft([(s*.105,.130,0,.058,.064),(s*.105,.159,0,.059,.065)],2,[w((ll,1))]*2)
   self.ellipsoid((s*.105,.076,.065),(.061,.046,.126),4,w((ft,1)),12,5)
   self.loft([(s*.105,.014,.065,.058,.12),(s*.105,.033,.065,.061,.125)],10,[w((ft,1))]*2)
   self.tube([(s*.357,.944,.031),(s*.365,.915,.035)],[(.039,.040),(.038,.040)],8,[w((la,1))]*2)
   self.tube([(s*.365,.914,.035),(s*.375,.865,.040),(s*.383,.820,.040)],[(.026,.024),(.034,.023),(.031,.018)],0,[w((hand,.5),(la,.5)),w((hand,1)),w((hand,1))],8)
   for f,dx,length in [('Index',-.022,.081),('Middle',-.006,.091),('Ring',.011,.083),('Little',.026,.065)]:
    pts=[(s*(.385+dx),.821-k*length/3,.04+.005*k) for k in range(4)]
    self.tube(pts,[(.009,.011),(.009,.010),(.008,.009),(.005,.006)],0,[w((side+f+str(min(k+1,3)),1)) for k in range(4)],6)
   self.tube([(s*.355,.856,.049),(s*.339,.832,.067),(s*.331,.811,.074)],[(.013,.013),(.012,.012),(.007,.008)],0,[w((side+'Thumb1',1)),w((side+'Thumb2',1)),w((side+'Thumb2',1))],6)
  # neck
  self.loft([(0,1.399,0,.049,.045),(0,1.475,0,.049,.045),(0,1.507,.008,.055,.05)],0,[w(('Neck',1))]*3)
  self.clothes_details()
  # A sculpted low polygon head: jaw, cheeks, temples, forehead, crown.
  self.loft([(0,1.48,.018,.043,.043),(0,1.50,.018,.066,.066),(0,1.54,.012,.086,.078),(0,1.59,.003,.101,.088),(0,1.64,0,.101,.092),(0,1.69,-.006,.093,.087),(0,1.725,-.006,.072,.073),(0,1.75,-.008,.027,.038)],0,[w(('Head',1))]*8,12)
  for s in [-1,1]:
   self.ellipsoid((s*.101,1.587,0),(.018,.032,.021),0,w(('Head',1)),8,5)
   self.ellipsoid((s*.107,1.588,.009),(.009,.018,.012),7,w(('Head',1)),8,4)
   # eye whites inset almond shapes, tiny dark pupils, defined brows
   ex=s*.040
   self.patch([(ex-.020,1.623,.082),(ex,1.631,.092),(ex+.020,1.624,.083),(ex,1.616,.092)],5,'Head')
   self.ellipsoid((ex,1.624,.092),(.0085,.009,.0025),6,w(('Head',1)),8,4)
   self.patch([(ex-.022,1.641,.083),(ex+.020,1.641,.085),(ex+.021,1.650,.083),(ex-.021,1.650,.080)],3,'Head')
   if old:
    self.patch([(ex-.018,1.609,.088),(ex+.017,1.607,.084),(ex+.016,1.604,.084),(ex-.018,1.606,.088)],7,'Head')
  # nose planes and mouth, avoiding a sphere-based face
  vs=[(-.014,1.626,.09),(.014,1.626,.09),(-.016,1.575,.092),(.016,1.575,.092),(0,1.584,.124),(0,1.622,.102)]
  self.mesh(vs,[(2,4,5,0),(4,3,1,5),(3,4,2),(5,1,0)],0,w(('Head',1)))
  self.patch([(-.029,1.550,.085),(0,1.554,.094),(.029,1.550,.085),(0,1.545,.094)],7,'Head')
  self.hair()
 def animate(self):
  old=self.old;child=self.s<.8
  def pose(**kw):return kw
  neutral={'Chest':[5 if old else 0,0,0],'Neck':[-4 if old else 0,0,0]}
  def arms(x,z=0,elbow=-15):return {'LeftUpperArm':[x,0,-z],'RightUpperArm':[x,0,z],'LeftLowerArm':[elbow,0,0],'RightLowerArm':[elbow,0,0]}
  def ik_arms(target,palm):
   result={}
   def align(a,b):
    a=norm(a);b=norm(b);v=np.cross(a,b);dot=np.clip(np.dot(a,b),-1,1)
    if np.linalg.norm(v)<1e-8:return R.identity() if dot>0 else R.from_euler('z',180,degrees=True)
    return R.from_rotvec(norm(v)*math.acos(dot))
   for side,sig in [('Left',1),('Right',-1)]:
    a=np.array([sig*.19,1.345,0]);e=np.array([sig*.305,1.12,.015]);h=np.array([sig*.365,.9,.035]);dest=np.array([sig*target[0],target[1],target[2]])
    ab=np.linalg.norm(e-a);bc=np.linalg.norm(h-e);v=dest-a;dist=min(np.linalg.norm(v),ab+bc-.001);axis=norm(v)
    pole=np.array([sig*.50,1.12,-.04])-a;perp=norm(pole-axis*np.dot(pole,axis));along=(ab*ab-bc*bc+dist*dist)/(2*dist);el=a+axis*along+perp*math.sqrt(max(0,ab*ab-along*along))
    qu=align(e-a,el-a);ql=align(h-e,dest-el);qh=R.from_euler('xyz',palm,degrees=True)
    result[side+'UpperArm']=qu.as_euler('xyz',degrees=True).tolist();result[side+'LowerArm']=(qu.inv()*ql).as_euler('xyz',degrees=True).tolist();result[side+'Hand']=(ql.inv()*qh).as_euler('xyz',degrees=True).tolist()
   return result
  pray={**ik_arms((.035,1.245,.285),(0,0,180)),'Head':[12,0,0]}
  carry={**ik_arms((.12,1.04,.34),(-90,0,0)),'Head':[8,0,0]}
  bend={**arms(-40,10,-35),'Spine':[28,0,0],'Head':[12,0,0]}
  sit={'Hips':[0,0,0],'LeftUpperLeg':[-86,0,0],'RightUpperLeg':[-86,0,0],'LeftLowerLeg':[88,0,0],'RightLowerLeg':[88,0,0],**arms(-18,0,-38),'Chest':[10,0,0]}
  clips=[]
  def clip(name,dur,keys,loop=False,desc='',root=None):
   # keys: normalized time -> pose. Sparse pose values expand to all bones.
   frames=[]
   for t,p in keys:
    pp={**neutral,**p}; rot=[];pos=[]
    for b in self.bones:
     ang=pp.get(b['name'],[0,0,0]);rot.extend(quat(ang));p0=list(b['localPosition'])
     if b['name']=='Hips' and root: p0[1]+=root(t)*self.s
     pos.extend(p0)
    frames.append({'time':round(t*dur,5),'rotations':rot,'positions':pos})
   clips.append({'name':name,'duration':dur,'loop':loop,'description':desc,'keys':frames})
  clip('Idle',4,[(0,{}),(.5,{'Chest':[7 if old else 1.5,0,0],'Head':[0,2,0]}),(1,{})],True,'自然站立与呼吸')
  walk=[]
  for i in range(9):
   t=i/8;a=math.cos(t*2*math.pi);b=math.sin(t*2*math.pi)
   walk.append((t,{'LeftUpperLeg':[-23*a,0,0],'RightUpperLeg':[23*a,0,0],'LeftLowerLeg':[max(0,b)*38,0,0],'RightLowerLeg':[max(0,-b)*38,0,0],'LeftUpperArm':[16*a,0,0],'RightUpperArm':[-16*a,0,0],'LeftLowerArm':[-10,0,0],'RightLowerArm':[-10,0,0],'Hips':[0,3*b,0]}))
  clip('Walk',1.4 if old else 1.1,walk,True,'原地走路；实际路径由 Unity 移动角色根物体')
  clip('Talk',4,[(0,{}),(.3,{'Head':[3,8,0],**arms(-9,0,-16)}),(.6,{'Head':[-2,-5,0],'RightLowerArm':[-32,0,0]}),(1,{})],True,'说话时点头与轻微手势，无口型')
  clip('Pray',5,[(0,{}),(.3,pray),(.55,{**pray,'Head':[18,0,0]}),(.8,pray),(1,{})],False,'双手举香、低头祈愿；香道具自行挂接')
  clip('CarryBurner',3,[(0,carry),(.5,{**carry,'Chest':[2,0,0]}),(1,carry)],True,'双手托抱姿势；需按香炉大小调整手位')
  clip('WrapBurner',6,[(0,{}),(.22,bend),(.45,{**bend,'LeftUpperArm':[-48,0,-27]}),(.68,{**bend,'RightUpperArm':[-48,0,27]}),(.85,carry),(1,carry)],False,'包裹动作占位；布料遮挡另做，不含布料模拟')
  clip('PlaceBurner',4,[(0,carry),(.5,bend),(.7,bend),(1,{})],False,'把香炉放到桌面后收手')
  clip('Unpack',5,[(0,bend),(.3,{**bend,'RightLowerArm':[-65,0,0]}),(.6,{**bend,'LeftLowerArm':[-65,0,0]}),(1,bend)],True,'俯身整理物品')
  clip('Reassure',4,[(0,{}),(.35,{'Head':[20,20,0],'LeftUpperArm':[-38,0,15],'LeftLowerArm':[-42,0,0]}),(.7,{'Head':[16,20,0],'LeftUpperArm':[-36,0,15],'LeftLowerArm':[-38,0,0]}),(1,{})],False,'转头看女儿并伸手安慰；接触点需调整')
  clip('SitDown',3,[(0,{}),(.4,{'Hips':[15,0,0],'LeftUpperLeg':[-35,0,0],'RightUpperLeg':[-35,0,0],'LeftLowerLeg':[40,0,0],'RightLowerLeg':[40,0,0]}),(1,sit)],False,'坐下；默认成人座高约 0.46 m',lambda t:-.43*t)
  clip('SitWindow',5,[(0,sit),(.5,{**sit,'Head':[0,12,0]}),(1,sit)],True,'坐在窗边，背向香炉',lambda t:-.43)
  clip('TouchFarewell',5,[(0,{}),(.32,{'Chest':[12,0,0],'Head':[15,0,0],'RightUpperArm':[-58,0,7],'RightLowerArm':[-14,0,0]}),(.72,{'Chest':[12,0,0],'Head':[18,0,0],'RightUpperArm':[-58,0,7],'RightLowerArm':[-14,0,0]}),(1,{})],False,'伸右手摸香炉，停顿后收手')
  clip('PickUpBag',4,[(0,{}),(.45,{'Spine':[38,0,-8],'RightUpperArm':[-24,0,-12]}),(.65,{'Spine':[38,0,-8],'RightUpperArm':[-24,0,-12]}),(1,{'RightUpperArm':[0,0,-10],'RightLowerArm':[-8,0,0]})],False,'提行李；在最低点挂到 Grip_R')
  clip('CallFather',4,[(0,{}),(.3,{'Head':[-18,-20,0],'RightUpperArm':[-72,0,-15],'RightLowerArm':[-40,0,0]}),(.55,{'Head':[-18,-20,0],'RightUpperArm':[-74,0,-15],'RightLowerArm':[-55,0,0]}),(1,{})],False,'抬头招呼父亲')
  clip('Worried',4,[(0,{'Head':[-10,10,0],**arms(-8,7,-20)}),(.5,{'Head':[-14,-10,4],**arms(-12,10,-28)}),(1,{'Head':[-10,10,0],**arms(-8,7,-20)})],True,'紧张地看父亲与海面')
  crouch={'LeftUpperLeg':[-55,0,0],'RightUpperLeg':[-55,0,0],'LeftLowerLeg':[90,0,0],'RightLowerLeg':[90,0,0],'Hips':[18,0,0],'Head':[-10,0,0]}
  clip('PeekTable',4,[(0,crouch),(.4,{'Head':[5,0,0],**arms(-30,0,-45)}),(.65,{'Head':[5,12,0],**arms(-30,0,-45)}),(1,{'Head':[5,0,0]})],False,'从桌边下方起身探头；桌高需匹配',lambda t:-.25*max(0,1-t/.4))
  clip('DinnerCall',4,[(0,{}),(.35,{'Head':[0,22,0],'RightUpperArm':[-36,0,-10],'RightLowerArm':[-55,0,0]}),(.65,{'Head':[2,22,0],'RightUpperArm':[-36,0,-10],'RightLowerArm':[-35,0,0]}),(1,{})],False,'成年女儿招呼父亲吃饭')
  # Slight finger flexion on all poses, firmer grasp in carrying poses.
  for cl in clips:
   for k in cl['keys']:
    for n,idx in self.bn.items():
     if any(f in n for f in ['Index','Middle','Ring','Little']):
      deg=-38 if cl['name'] in ['CarryBurner','Pray','PickUpBag'] else -8
      k['rotations'][idx*4:idx*4+4]=quat([deg,0,0])
  return clips
 def data(self):
  return {'name':self.name,'materials':[{'name':n,'color':c} for n,c in self.mats],'bones':self.bones,'positions':np.array(self.verts).flatten().tolist(),'normals':np.array(self.normals).flatten().tolist(),'joints':np.array(self.joints).flatten().tolist(),'weights':np.array(self.weights).flatten().tolist(),'submeshes':[{'indices':np.array([t for t,m in zip(self.tris,self.mids) if m==i],dtype=int).flatten().tolist()} for i in range(len(self.mats))],'clips':self.actions}
 def save(self):
  d=self.data();(SOURCE/(self.name+'.json')).write_text(json.dumps(d,separators=(',',':')),encoding='utf-8');save_glb(self)

def save_glb(c):
 buf=bytearray();views=[];acc=[]
 def add(a,typ,ctype=5126):
  dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[ctype];a=np.array(a,dtype=dtype)
  while len(buf)%4:buf.append(0)
  off=len(buf);buf.extend(a.tobytes());views.append({'buffer':0,'byteOffset':off,'byteLength':a.nbytes})
  v={'bufferView':len(views)-1,'componentType':ctype,'count':len(a),'type':typ}
  if typ=='VEC3':v.update(min=a.min(axis=0).tolist(),max=a.max(axis=0).tolist())
  if typ=='SCALAR' and ctype==5126:v.update(min=[float(a.min())],max=[float(a.max())])
  acc.append(v);return len(acc)-1
 pos=add(c.verts,'VEC3');ns=add(c.normals,'VEC3');js=add(c.joints,'VEC4',5123);ws=add(c.weights,'VEC4')
 prim=[]
 for i in range(len(c.mats)):
  indices=np.array([t for t,m in zip(c.tris,c.mids) if m==i]).flatten()
  if len(indices)==0:continue
  prim.append({'attributes':{'POSITION':pos,'NORMAL':ns,'JOINTS_0':js,'WEIGHTS_0':ws},'indices':add(indices,'SCALAR',5125),'material':i})
 nodes=[{'name':b['name'],'translation':b['localPosition']} for b in c.bones]
 for i,b in enumerate(c.bones):
  if b['parent']>=0:nodes[b['parent']].setdefault('children',[]).append(i)
 nodes.append({'name':c.name+'_Skin','mesh':0,'skin':0})
 ib=[]
 for b in c.bones:
  mat=np.eye(4);mat[:3,3]=-np.array(b['position']);ib.append(mat.T.flatten())
 animations=[]
 for cl in c.actions:
  times=add([k['time'] for k in cl['keys']],'SCALAR');sam=[];channels=[]
  for i,b in enumerate(c.bones):
   for key,path,size,typ in [('rotations','rotation',4,'VEC4'),('positions','translation',3,'VEC3')]:
    values=[k[key][i*size:i*size+size] for k in cl['keys']]
    if all(np.allclose(v,values[0]) for v in values) and path=='translation': continue
    va=add(values,typ);sam.append({'input':times,'output':va,'interpolation':'LINEAR'});channels.append({'sampler':len(sam)-1,'target':{'node':i,'path':path}})
  animations.append({'name':cl['name'],'samplers':sam,'channels':channels})
 gltf={'asset':{'version':'2.0','generator':'Incense Characters original procedural mesh v1'},'scene':0,'scenes':[{'nodes':[0,len(nodes)-1]}],'nodes':nodes,'meshes':[{'name':c.name,'primitives':prim}],'skins':[{'joints':list(range(len(c.bones))),'skeleton':0,'inverseBindMatrices':add(ib,'MAT4')}],'animations':animations,'materials':[{'name':n,'pbrMetallicRoughness':{'baseColorFactor':co,'metallicFactor':0,'roughnessFactor':.9},'doubleSided':True} for n,co in c.mats],'buffers':[{'byteLength':len(buf)}],'bufferViews':views,'accessors':acc}
 j=json.dumps(gltf,separators=(',',':')).encode();j+=b' '*((-len(j))%4);buf+=b'\0'*((-len(buf))%4)
 out=struct.pack('<III',0x46546c67,2,12+8+len(j)+8+len(buf))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(buf),0x004e4942)+buf
 (ROOT/'GLB'/(c.name+'.glb')).write_bytes(out)

if __name__=='__main__':
 chars=[Character('Father_Young',1,1,False,False,[.245,.285,.29],[.19,.205,.21]),Character('Father_Elder',.97,1,False,True,[.67,.635,.53],[.235,.235,.215]),Character('Daughter_Child',.61,.80,True,False,[.48,.28,.225],[.235,.245,.24]),Character('Daughter_Adult',.92,.96,True,False,[.36,.435,.43],[.18,.215,.23])]
 stats=[]
 for c in chars:
  c.save();stats.append({'name':c.name,'vertices':len(c.verts),'triangles':len(c.tris),'bones':len(c.bones),'clips':len(c.actions),'height_m':round(max(v[1] for v in c.verts),3)})
 (ROOT/'asset_stats.json').write_text(json.dumps(stats,indent=2));print(json.dumps(stats,indent=2))
