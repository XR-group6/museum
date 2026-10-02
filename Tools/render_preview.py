from pathlib import Path
import json, math
import numpy as np
from scipy.spatial.transform import Rotation as R, Slerp
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
FONT='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
def font(s):return ImageFont.truetype(FONT,s)
def skin(d,clip='Idle',t=0):
 cl=next(c for c in d['clips'] if c['name']==clip);ks=cl['keys'];time=t*cl['duration'];a=ks[0];b=ks[-1]
 for j in range(len(ks)-1):
  if ks[j]['time']<=time<=ks[j+1]['time']:a,b=ks[j],ks[j+1];break
 u=(time-a['time'])/max(b['time']-a['time'],1e-6);world=[]
 for i,bo in enumerate(d['bones']):
  q0=a['rotations'][4*i:4*i+4];q1=b['rotations'][4*i:4*i+4]
  q=(1-u)*np.array(q0)+u*np.array(q1);q=q/np.linalg.norm(q)
  p=(1-u)*np.array(a['positions'][3*i:3*i+3])+u*np.array(b['positions'][3*i:3*i+3]);m=np.eye(4);m[:3,:3]=R.from_quat(q).as_matrix();m[:3,3]=p
  if bo['parent']>=0:m=world[bo['parent']]@m
  world.append(m)
 v=np.array(d['positions']).reshape(-1,3);ns=np.array(d['normals']).reshape(-1,3);weights=np.array(d['weights']).reshape(-1,4);joints=np.array(d['joints']).reshape(-1,4)
 out=np.zeros_like(v);normal=np.zeros_like(v)
 for j,bo in enumerate(d['bones']):
  mask=np.sum(weights*(joints==j),axis=1);rest=np.array(bo['position']);m=world[j]
  out+=(((v-rest)@m[:3,:3].T+m[:3,3])*mask[:,None]);normal+=(ns@m[:3,:3].T)*mask[:,None]
 return out,normal

def draw_character(canvas,d,center,bottom,ppm,angle=-18,clip='Idle',t=0,back=False):
 v,ns=skin(d,clip,t);rot=R.from_euler('y',angle,degrees=True).as_matrix();v=v@rot.T;ns=ns@rot.T
 # small top-down elevation
 rot=R.from_euler('x',9,degrees=True).as_matrix();v=v@rot.T;ns=ns@rot.T
 x=center+v[:,0]*ppm;y=bottom-v[:,1]*ppm;depth=v[:,2]
 draw=ImageDraw.Draw(canvas);draw.ellipse((center-.28*ppm,bottom-.028*ppm,center+.28*ppm,bottom+.035*ppm),fill=(215,211,200))
 tris=[]
 light=np.array([-.45,.7,.7]);light=light/np.linalg.norm(light)
 for mid,sub in enumerate(d['submeshes']):
  co=np.array(d['materials'][mid]['color'][:3]);base=(co**(1/2.0))*255
  for ids in np.array(sub['indices']).reshape(-1,3):
   n=ns[ids].mean(0)
   if n[2]<-.02:continue
   shade=.63+.37*max(0,np.dot(n,light));col=tuple(np.minimum(255,base*shade).astype(int));pts=[(float(x[i]),float(y[i])) for i in ids];tris.append((depth[ids].mean(),pts,col))
 # Depth-buffered rasterization: intersecting clothing and hair must occlude per pixel.
 pixels=np.array(canvas);zbuf=np.full((canvas.height,canvas.width),-np.inf)
 for mid,sub in enumerate(d['submeshes']):
  co=np.array(d['materials'][mid]['color'][:3]);base=(co**.5)*255
  for ids in np.array(sub['indices']).reshape(-1,3):
   nn=ns[ids].mean(0)
   if nn[2]<-.02:continue
   px=x[ids];py=y[ids];pz=depth[ids];xmin=max(0,int(math.floor(px.min())));xmax=min(canvas.width-1,int(math.ceil(px.max())));ymin=max(0,int(math.floor(py.min())));ymax=min(canvas.height-1,int(math.ceil(py.max())))
   if xmin>xmax or ymin>ymax:continue
   den=(py[1]-py[2])*(px[0]-px[2])+(px[2]-px[1])*(py[0]-py[2])
   if abs(den)<1e-8:continue
   xx,yy=np.meshgrid(np.arange(xmin,xmax+1)+.5,np.arange(ymin,ymax+1)+.5)
   aa=((py[1]-py[2])*(xx-px[2])+(px[2]-px[1])*(yy-py[2]))/den
   bb=((py[2]-py[0])*(xx-px[2])+(px[0]-px[2])*(yy-py[2]))/den;cc=1-aa-bb;zz=aa*pz[0]+bb*pz[1]+cc*pz[2]
   region=zbuf[ymin:ymax+1,xmin:xmax+1];mask=(aa>=-1e-6)&(bb>=-1e-6)&(cc>=-1e-6)&(zz>region)
   region[mask]=zz[mask];shade=.63+.37*max(0,np.dot(nn,light));col=np.minimum(255,base*shade).astype(np.uint8)
   pixels[ymin:ymax+1,xmin:xmax+1][mask]=col
 canvas.paste(Image.fromarray(pixels))

if __name__=='__main__':
 ds=[json.loads((ROOT/'Assets/IncenseCharacters/Source'/f'{n}.json').read_text()) for n in ['Father_Young','Father_Elder','Daughter_Child','Daughter_Adult']]
 im=Image.new('RGB',(1800,1040),(242,240,233));dr=ImageDraw.Draw(im)
 dr.text((70,40),'THE INCENSE BURNER REMEMBERS',font=font(32),fill=(37,46,42));dr.text((70,94),'REVISED V2  /  continuous shoulders  /  period-informed wardrobe',font=font(21),fill=(100,105,94))
 for d,c in zip(ds,[250,680,1110,1540]):draw_character(im,d,c,890,405)
 for d,c,label in zip(ds,[250,680,1110,1540],['YOUNG FATHER','ELDERLY FATHER','CHILD DAUGHTER','ADULT DAUGHTER']):
  dr.text((c-160,930),label,font=font(22),fill=(37,46,42));dr.text((c-160,966),f"{len(d['bones'])} bones  |  {sum(len(s['indices'])//3 for s in d['submeshes']):,} triangles",font=font(17),fill=(100,105,94))
 im.save(ROOT/'Previews/Cast_Preview.png')
 # action contact sheet generated from actual skinned model geometry
 names=['Pray','WrapBurner','CarryBurner','Reassure','PlaceBurner','Unpack','SitWindow','TouchFarewell','CallFather','Worried','PeekTable','DinnerCall']
 im=Image.new('RGB',(1800,1600),(242,240,233));dr=ImageDraw.Draw(im);dr.text((60,30),'STORY ACTIONS / ACTUAL SKINNED-MESH POSES',font=font(28),fill=(37,46,42));dr.text((60,74),'Props and contact alignment are added in the Unity scene.',font=font(19),fill=(100,105,94))
 for i,n in enumerate(names):
  row=i//4;col=i%4;d=ds[2] if i in [8,9,10] else ds[3] if i==11 else ds[1] if i in [6,7] else ds[0]
  cx=col*450+225;by=130+row*480+400;draw_character(im,d,cx,by,205 if i not in [8,9,10] else 280,angle=-28,clip=n,t=.5)
  dr.text((cx-155,by+32),n,font=font(23),fill=(37,46,42))
 im.save(ROOT/'Previews/Action_Preview.png')
 # animated proof: young father prayer + walking and child calling
 frames=[]
 for k in range(24):
  im=Image.new('RGB',(960,610),(242,240,233));dr=ImageDraw.Draw(im)
  dr.text((35,25),'ACTUAL MODEL ANIMATION / PROTOTYPE',font=font(21),fill=(37,46,42))
  for d,c,n,p in [(ds[0],180,'Pray',240),(ds[1],480,'Walk',240),(ds[2],780,'CallFather',300)]:draw_character(im,d,c,530,p,angle=-20,clip=n,t=k/23)
  dr.text((70,558),'Pray',font=font(19),fill=(37,46,42));dr.text((395,558),'Walk (in place)',font=font(19),fill=(37,46,42));dr.text((705,558),'CallFather',font=font(19),fill=(37,46,42));frames.append(im)
 frames[0].save(ROOT/'Previews/Animation_Preview.gif',save_all=True,append_images=frames[1:],duration=130,loop=0)
 print('Previews rendered')
