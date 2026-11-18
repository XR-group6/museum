using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
[InitializeOnLoad]public static class Scene04HarborCheck
{
 static bool running;
 static Scene04HarborCheck(){EditorApplication.delayCall+=Run;}
 static float Pitch(Transform head)=>Mathf.Atan2(head.forward.y,Vector3.ProjectOnPlane(head.forward,Vector3.up).magnitude)*Mathf.Rad2Deg;
 public static void Run()
 {
  if(running||File.Exists("Temp/Scene04Harbor.check.done")||!File.Exists("Temp/Scene04Harbor.build.done")||!File.Exists("Temp/Scene04Harbor.final.done"))return;
  try{
   var scene=SceneManager.GetActiveScene();if(scene.name!="scene_04_01"||EditorApplication.isPlaying)return;
   var story=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Scene04HarborStory>(true)).First();var view=story.GetComponent<Scene01CenserView>();var dir=story.director;
   story.burner.position=new Vector3(6.95f,.84f,2.85f);view.scene04TurnDegrees=-144;EditorUtility.SetDirty(view);
   var fixedBones=story.father.GetComponentsInChildren<Transform>(true).Where(b=>b.name.Contains("UpperLeg")||b.name.Contains("LowerLeg")||b.name.Contains("Foot")||b.name.Contains("Hand")||b.name.Contains("UpperArm")||b.name.Contains("LowerArm")).ToArray();
   Quaternion[] restRotations=null;Vector3[] restPositions=null;float maxAngle=0,maxDistance=0,maxEye=0;float pitch0=0,pitchUp=0;
   for(int i=0;i<=88;i++){
    float t=i*.25f;dir.time=story.startTime+t;dir.Evaluate();view.ApplyView();
    if(i==0){restRotations=fixedBones.Select(b=>b.localRotation).ToArray();restPositions=fixedBones.Select(b=>b.position).ToArray();pitch0=Pitch(story.Bone("Head"));}
    if(i==16)pitchUp=Pitch(story.Bone("Head"));
    for(int k=0;k<fixedBones.Length;k++){maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(restRotations[k],fixedBones[k].localRotation));maxDistance=Mathf.Max(maxDistance,Vector3.Distance(restPositions[k],fixedBones[k].position));}
    var cam=view.headsetCamera.isActiveAndEnabled?view.headsetCamera:view.desktopCamera;maxEye=Mathf.Max(maxEye,Vector3.Distance(cam.transform.position,view.EyeAt(dir.time)));
   }
   if(maxAngle>.01||maxDistance>.0001||maxEye>.0001||pitchUp-pitch0<3)throw new Exception("Harbor pose verification failed: angle="+maxAngle+" distance="+maxDistance+" eye="+maxEye+" raise="+(pitchUp-pitch0));
   foreach(string side in new[]{"Left","Right"})if(Vector3.Distance(story.Bone(side+"Hand").position,story.Bone(side+"LowerLeg").position)>.18)throw new Exception("Hand not resting near knee: "+side);
   var timeline=dir.playableAsset as TimelineAsset;var audio=timeline.GetOutputTracks().OfType<AudioTrack>().First(t=>t.name.StartsWith("Scene04"));var bells=audio.GetClips().OrderBy(c=>c.start).ToArray();
   if(bells.Length!=2||Math.Abs(bells[0].start-story.startTime-10)>.001||Math.Abs(bells[1].start-story.startTime-12)>.001||bells[1].end>=story.startTime+14)throw new Exception("Two clock strikes do not finish before narration");
   if(Scene04HarborStory.CaptionAt(11)!=""||Scene04HarborStory.CaptionAt(13)!=""||!Scene04HarborStory.CaptionAt(14).Contains("我还在等"))throw new Exception("Quiet narration timing failed");
   dir.time=story.startTime;dir.Evaluate();view.ApplyView();if(Mathf.Abs(Pitch(story.Bone("Head"))-pitch0)>.01)throw new Exception("Backward seek failed");
   string report="89 samples: hands/legs rotation drift="+maxAngle+" degrees; world drift="+maxDistance+" metres; eye error="+maxEye+" metres.\nGaze upward change="+(pitchUp-pitch0)+" degrees.\nClock strike 1="+bells[0].start+".."+bells[0].end+"; strike 2="+bells[1].start+".."+bells[1].end+"; narrator="+(story.startTime+14)+".\nBackward seek restores gaze.\nTimeline="+story.startTime+".."+story.EndTime+".\n";
   File.WriteAllText("Logs/Scene04Harbor-validation.txt",report);
   dir.time=story.startTime+4;dir.Evaluate();view.BeginView();view.ApplyView();EditorApplication.delayCall+=()=>Render(story,view);
  }catch(Exception ex){File.WriteAllText("Temp/Scene04Harbor.check.error",ex.ToString());Debug.LogException(ex);}
 }
 static void Render(Scene04HarborStory story,Scene01CenserView view)
 {
  try{
   var active=view.headsetCamera.isActiveAndEnabled?view.headsetCamera:view.desktopCamera;
   var go=new GameObject("Scene04 dining burner preview");var camera=go.AddComponent<Camera>();camera.transform.SetPositionAndRotation(active.transform.position,active.transform.rotation);camera.nearClipPlane=.025f;camera.fieldOfView=90;camera.aspect=1.5f;var rt=new RenderTexture(1200,800,24);camera.targetTexture=rt;
   var head=story.Bone("Head");var point=camera.WorldToViewportPoint(head.position);float profile=Vector3.Angle(head.forward,(camera.transform.position-head.position).normalized);
   if(point.z<=0||point.x<.1||point.x>.9||point.y<.1||point.y>.9||profile>100)throw new Exception("Father profile not visible: viewport="+point+" profile="+profile);
   camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();Directory.CreateDirectory("Previews/Scene04Harbor");File.WriteAllBytes("Previews/Scene04Harbor/FirstPerson_HarborDusk.png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
   File.AppendAllText("Logs/Scene04Harbor-validation.txt","Father head viewport="+point+"; profile angle="+profile+" degrees.\n");
   story.director.time=0;story.director.Evaluate();story.Sample(0);story.caption.text="";view.ApplyView();view.RestoreRenderers();EditorSceneManager.MarkSceneDirty(story.gameObject.scene);EditorSceneManager.SaveScene(story.gameObject.scene);AssetDatabase.SaveAssets();File.WriteAllText("Temp/Scene04Harbor.check.done","OK");
  }catch(Exception ex){File.WriteAllText("Temp/Scene04Harbor.check.error",ex.ToString());Debug.LogException(ex);}
 }
}


