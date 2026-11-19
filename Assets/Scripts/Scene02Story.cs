using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[DefaultExecutionOrder(19000)]
public sealed class Scene02Story : MonoBehaviour
{
 public Transform father, mother, daughter, burner, title;
 public TextMesh caption;
 public PlayableDirector director;
 public double startTime=37.2333333333, duration=18;
 public AudioClip daughterVoice, fatherVoice, burnerVoice;
 public Vector3 exitLocalPosition;
 public Transform[] poseBones;
 public Quaternion[] initialRotations;
 public Vector3[] initialPositions;
 public double EndTime => startTime+duration;
 Dictionary<Transform,Quaternion> rotations;
 Dictionary<Transform,Vector3> positions;
 AudioSource source;
 double previous=-1;
 Transform Bone(Transform root,string n){foreach(var b in root.GetComponentsInChildren<Transform>(true))if(b.name==n)return b;return null;}
 public void CapturePose(){var bones=new List<Transform>();foreach(var actor in new[]{father,mother,daughter})bones.AddRange(actor.GetComponentsInChildren<Transform>(true));poseBones=bones.ToArray();initialRotations=new Quaternion[poseBones.Length];initialPositions=new Vector3[poseBones.Length];for(int i=0;i<poseBones.Length;i++){initialRotations[i]=poseBones[i].localRotation;initialPositions[i]=poseBones[i].localPosition;}rotations=null;positions=null;}
 void Prepare(){if(rotations!=null)return;if(poseBones==null||poseBones.Length==0)CapturePose();rotations=new Dictionary<Transform,Quaternion>();positions=new Dictionary<Transform,Vector3>();for(int i=0;i<poseBones.Length;i++){rotations[poseBones[i]]=initialRotations[i];positions[poseBones[i]]=initialPositions[i];}}
 static float S(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
 void Pose(Transform actor,string n,float x,float y=0,float z=0){var b=Bone(actor,n);if(b)b.localRotation*=Quaternion.Euler(x,y,z);}
 void Look(Transform actor,Vector3 target,float weight){var head=Bone(actor,"Head");if(!head)return;var local=Quaternion.Inverse(head.parent.rotation)*Quaternion.LookRotation(target-head.position,Vector3.up);head.localRotation=Quaternion.Slerp(head.localRotation,local,weight);}
 public void Sample(double localTime)
 {
  if(!father||!mother||!daughter)return;Prepare();float t=Mathf.Clamp((float)localTime,0,(float)duration);
  foreach(var p in rotations)if(p.Key)p.Key.localRotation=p.Value;
  foreach(var p in positions)if(p.Key)p.Key.localPosition=p.Value;
  var fh=Bone(father,"Head");var dh=Bone(daughter,"Head");var mh=Bone(mother,"Head");
  var parents=(fh.position+mh.position)*.5f;
  float turn=S(.6f,2,t)*(1-S(10,11.5f,t));
  daughter.localRotation=Quaternion.Slerp(rotations[daughter],Quaternion.Euler(0,30,0),turn);
  Look(daughter,parents,turn);
  float speech=(S(2,2.3f,t)-S(5.7f,6,t));Pose(daughter,"Head",Mathf.Sin(t*4)*1.1f*speech);
  // A single deliberate nod in reply; no repeated nodding or idle leg animation.
  float nod=(S(6.4f,7,t)-S(7,7.7f,t))*12;
  Look(father,dh.position,1);Pose(father,"Head",nod);
  Look(mother,t<6.2f?dh.position:Vector3.Lerp(dh.position,fh.position,S(6.2f,6.8f,t)),1);
  if(t>=10){float walk=S(10.8f,16,t);daughter.localPosition=Vector3.Lerp(positions[daughter],exitLocalPosition,walk);daughter.localRotation=Quaternion.Slerp(daughter.localRotation,rotations[daughter],S(10,11.5f,t));float gait=Mathf.Sin((t-10.8f)*8.2f)*(S(10.8f,11.3f,t)-S(15.5f,16,t));Pose(daughter,"LeftUpperLeg",24*gait);Pose(daughter,"RightUpperLeg",-24*gait);Pose(daughter,"LeftLowerLeg",Mathf.Max(0,-gait)*30);Pose(daughter,"RightLowerLeg",Mathf.Max(0,gait)*30);Pose(daughter,"LeftUpperArm",-12*gait);Pose(daughter,"RightUpperArm",12*gait);}
  if(caption)caption.text=SyncedCaptionAt(t);
 }
 string SyncedCaptionAt(float t) => t >= 2f && t < 2f + (daughterVoice ? daughterVoice.length : 2.795f) ? "女儿：爸、妈，我上班去了。" :
        t >= 6.4f && t < 6.4f + (fatherVoice ? fatherVoice.length : 1.411f) ? "父亲：路上小心。" :
        t >= 9f && t < 9f + (burnerVoice ? burnerVoice.length : 2.064f) ? "香炉：我还在等。" : "";
    public static string CaptionAt(float t)=>t<2||t>=13?"":t<6.3f?"女儿：爸、妈，我上班去了。":t<8.8f?"父亲：路上小心。":"香炉：我还在等。";
 public void Present(Camera view)
 {
  if(!director||!view)return;double t=director.time-startTime;
  if(t<0||t>=duration){if(caption)caption.text="";return;}
  // Sampling after Timeline evaluation also makes editor seeking and repeated playback deterministic.
  Sample(t);
  if(caption){caption.transform.position=view.transform.TransformPoint(new Vector3(0,-.19f,1.6f));caption.transform.rotation=view.transform.rotation;}
  if(title){title.position=view.transform.TransformPoint(new Vector3(0,.48f,2.4f));title.rotation=view.transform.rotation;}
 }
 void Update(){if(!Application.isPlaying||!director)return;double t=director.time-startTime;if(director.state==PlayState.Playing){if(!source){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;}Cue(2,daughterVoice,t);Cue(6.4,fatherVoice,t);Cue(9, burnerVoice,t);}previous=t;}
 void Cue(double at,AudioClip clip,double t){if(clip&&previous<at&&t>=at&&t-at<.5)source.PlayOneShot(clip);}
}
