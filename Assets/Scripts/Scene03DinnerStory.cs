using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public sealed class Scene03DinnerStory : MonoBehaviour
{
 public PlayableDirector director;
 public Transform father, mother, daughter, child, burner, fishPlate, title;
 public TextMesh caption;
 public double startTime=62, duration=22;
 public AudioClip daughterVoice, fatherVoice, burnerVoice;
 public Transform[] poseBones;
 public Vector3[] initialPositions;
 public Quaternion[] initialRotations;
 public Vector3 plateStart, plateEnd;
 public double EndTime => startTime+duration;
 Dictionary<Transform,Vector3> positions;
 Dictionary<Transform,Quaternion> rotations;
 AudioSource voice;
 double previous=-1;
 SkinnedMeshRenderer fatherSkin;
 Mesh originalFace, smileFace;
 Vector3[] faceVertices;
 public int SmileVertexCount { get; private set; }
 public void PreviewSmile(float amount) { Smile(amount); }
 public Transform Bone(Transform root,string name){foreach(var b in root.GetComponentsInChildren<Transform>(true))if(b.name==name)return b;return null;}
 public void CapturePose(){var list=new List<Transform>();foreach(var root in new[]{father,mother,daughter,child})list.AddRange(root.GetComponentsInChildren<Transform>(true));poseBones=list.ToArray();initialPositions=new Vector3[poseBones.Length];initialRotations=new Quaternion[poseBones.Length];for(int i=0;i<poseBones.Length;i++){initialPositions[i]=poseBones[i].localPosition;initialRotations[i]=poseBones[i].localRotation;}positions=null;rotations=null;}
 void Prepare(){if(positions!=null)return;if(poseBones==null||poseBones.Length==0)CapturePose();positions=new Dictionary<Transform,Vector3>();rotations=new Dictionary<Transform,Quaternion>();for(int i=0;i<poseBones.Length;i++){positions[poseBones[i]]=initialPositions[i];rotations[poseBones[i]]=initialRotations[i];}}
 static float S(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
 void Pose(Transform actor,string name,float x,float y=0,float z=0){var b=Bone(actor,name);if(b)b.localRotation*=Quaternion.Euler(x,y,z);}
 void Look(Transform actor,Vector3 target){var head=Bone(actor,"Head");if(head)head.rotation=Quaternion.LookRotation(target-head.position,Vector3.up);}
 public void Seat(Transform actor,float height){var hips=Bone(actor,"Hips");hips.position+=Vector3.up*(height+.08f-hips.position.y);Pose(actor,"LeftUpperLeg",-88);Pose(actor,"RightUpperLeg",-88);Pose(actor,"LeftLowerLeg",88);Pose(actor,"RightLowerLeg",88);}
 public void RestHands(Transform actor){foreach(string side in new[]{"Left","Right"}){var thigh=Bone(actor,side+"UpperLeg");var knee=Bone(actor,side+"LowerLeg");Vector3 lap=Vector3.Lerp(thigh.position,knee.position,.65f)+Vector3.up*.06f;Arm(actor,side,lap);}}
 void Arm(Transform actor,string side,Vector3 target){var upper=Bone(actor,side+"UpperArm");var lower=Bone(actor,side+"LowerArm");var hand=Bone(actor,side+"Hand");Vector3 pole=actor.TransformPoint(new Vector3(side=="Left"?.45f:-.45f,.65f,.08f));SolveArm(upper,lower,hand,target,pole,actor.rotation*Quaternion.Euler(85,0,0));}
 static void SolveArm(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole,Quaternion palm){Vector3 a=upper.position;float ab=Vector3.Distance(a,lower.position),bc=Vector3.Distance(lower.position,hand.position);Vector3 axis=(target-a).normalized;float d=Mathf.Clamp(Vector3.Distance(a,target),Mathf.Abs(ab-bc)+.001f,ab+bc-.001f);Vector3 side=Vector3.ProjectOnPlane(pole-a,axis).normalized;float along=(ab*ab-bc*bc+d*d)/(2*d);Vector3 elbow=a+axis*along+side*Mathf.Sqrt(Mathf.Max(0,ab*ab-along*along));upper.rotation=Quaternion.FromToRotation(lower.position-a,elbow-a)*upper.rotation;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,a+axis*d-lower.position)*lower.rotation;hand.rotation=palm;}
 public void Sample(double localTime)
 {
  if(!father||!mother||!daughter||!child||!fishPlate)return;
  Prepare();float t=Mathf.Clamp((float)localTime,0,(float)duration);
  foreach(var p in positions)if(p.Key)p.Key.localPosition=p.Value;
  foreach(var p in rotations)if(p.Key)p.Key.localRotation=p.Value;
  float push=S(3,6.2f,t);fishPlate.localPosition=Vector3.Lerp(plateStart,plateEnd,push);
  var fatherHead=Bone(father,"Head");var daughterHead=Bone(daughter,"Head");
  Look(daughter,fatherHead.position);
  Look(father,Vector3.Lerp(fishPlate.position,daughterHead.position,S(6.4f,7.1f,t)));
  Look(mother,Vector3.Lerp(daughterHead.position,fatherHead.position,S(6.6f,7.4f,t)));
  Look(child,Vector3.Lerp(daughterHead.position,fatherHead.position,S(7.2f,8,t)));
  // One warm nod, with a brief second, smaller settling motion.
  float nod=11*(S(7.5f,8,t)-S(8,8.6f,t))+3*(S(8.7f,9,t)-S(9,9.35f,t));Pose(father,"Head",nod);
  float lean=22*(S(2.5f,3.2f,t)-S(6.2f,7.2f,t));Pose(daughter,"Spine",lean);
  RestHands(daughter);
  var rightHand=Bone(daughter,"LeftHand");Vector3 restHand=rightHand.position;
  Vector3 towardDaughter=Vector3.ProjectOnPlane(daughter.position-fishPlate.position,Vector3.up).normalized;
  Vector3 contact=fishPlate.position+towardDaughter*.18f+Vector3.up*.045f;
  float reach=S(2.2f,3,t)*(1-S(6.3f,7.3f,t));Arm(daughter,"Left",Vector3.Lerp(restHand,contact,reach));
  if (Application.isPlaying) Smile(S(6.8f,7.5f,t)*(1-S(10.5f,12,t)));
  if(caption)caption.text=CaptionAt(t);
 }
 void Smile(float amount){
  if(!fatherSkin){fatherSkin=father.GetComponentInChildren<SkinnedMeshRenderer>();if(!fatherSkin)return;originalFace=fatherSkin.sharedMesh;smileFace=Instantiate(originalFace);smileFace.name="Scene03 father smile";smileFace.hideFlags=HideFlags.HideAndDontSave;faceVertices=originalFace.vertices;fatherSkin.sharedMesh=smileFace;}
  var vertices=(Vector3[])faceVertices.Clone();
  SmileVertexCount=0;
  for(int i=0;i<vertices.Length;i++){var p=faceVertices[i];float y=p.y-1.465f*.97f+1.465f;if(Mathf.Abs(p.x)<.032f&&y>=1.544f&&y<=1.556f&&p.z>.08f){vertices[i].y+=amount*(.006f*Mathf.Clamp01(Mathf.Abs(p.x)/.029f)-.001f);SmileVertexCount++;}}
  smileFace.vertices=vertices;
 }
 public void RestoreSmile(){if(fatherSkin&&originalFace)fatherSkin.sharedMesh=originalFace;if(smileFace){if(Application.isPlaying)Destroy(smileFace);else DestroyImmediate(smileFace);}fatherSkin=null;smileFace=null;}
 void OnDisable(){RestoreSmile();}
 public static string CaptionAt(float t)=>t<2||t>=20?"":t<6.6f?"女儿：爸，尝尝我做的鱼。":t<10.8f?"父亲：好，你也趁热吃。":"香炉：这些年，我又听见了许多心愿。\n我还在等。";
 public void Present(Camera camera){if(!director||!camera)return;double t=director.time-startTime;if(t<0||t>=duration){if(caption)caption.text="";return;}Sample(t);if(caption){caption.transform.position=camera.transform.TransformPoint(new Vector3(0,-.21f,1.7f));caption.transform.rotation=camera.transform.rotation;}if(title){title.position=camera.transform.TransformPoint(new Vector3(0,1.85f,2.6f));title.rotation=camera.transform.rotation;}}
 void Update(){if(!Application.isPlaying||!director)return;double t=director.time-startTime;if(director.state==PlayState.Playing){if(!voice){voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;}Cue(2,daughterVoice,t);Cue(7,fatherVoice,t);Cue(11,burnerVoice,t);}previous=t;}
 void Cue(double at,AudioClip clip,double t){if(clip&&previous<at&&t>=at&&t-at<.5)voice.PlayOneShot(clip);}
}



