using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
[InitializeOnLoad] public static class Scene01Verify {
 static Scene01Verify(){EditorApplication.delayCall+=Run;}
 static void Run(){
 if(!File.Exists("Temp/Scene01.build.done")||File.Exists("Temp/Scene01.qa.done"))return;
 try{
 var scene=SceneManager.GetActiveScene(); if(scene.name!="scene_04_01")return;
 var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 var dir=all.First(x=>x.name=="TimeLine").GetComponent<PlayableDirector>();
 var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Scene01Animation/Scene01_Characters.anim");
 foreach(var bind in AnimationUtility.GetCurveBindings(clip)) {
 var c=AnimationUtility.GetEditorCurve(clip,bind); var keys=c.keys; if(keys.Length>2) { var reduced=keys.Where((k,i)=>i==0||i==keys.Length-1||Mathf.Abs(k.value-keys[i-1].value)>.000001f||Mathf.Abs(k.value-keys[i+1].value)>.000001f).ToArray(); c.keys=reduced; AnimationUtility.SetEditorCurve(clip,bind,c); }
 if(keys.All(k=>Mathf.Abs(k.value-keys[0].value)<.000001f)) AnimationUtility.SetEditorCurve(clip,bind,AnimationCurve.Constant(0,26,keys[0].value));
 }
 EditorUtility.SetDirty(clip); AssetDatabase.SaveAssets();
 Directory.CreateDirectory("Previews/Scene01");
 var camGo=new GameObject("Scene01 QA camera"); var cam=camGo.AddComponent<Camera>();
 camGo.transform.position=new Vector3(0,1.75f,.7f);camGo.transform.LookAt(new Vector3(-.2f,1,-3.35f));cam.fieldOfView=73;cam.nearClipPlane=.05f;
 var rt=new RenderTexture(1200,800,24);cam.targetTexture=rt;
 foreach(var a in all.Where(x=>x.name=="Father_Young"||x.name=="Daughter_Child_Schoolbag"||x.name=="Mother_Young")) { var animator=a.GetComponent<Animator>(); if(animator) UnityEngine.Object.DestroyImmediate(animator); } var timeline=dir.playableAsset as TimelineAsset;
 var motion=dir.GetComponent<Scene01Motion>()??dir.gameObject.AddComponent<Scene01Motion>();
 motion.characters=clip;motion.burner=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Scene01Animation/Scene01_Burner.anim");motion.actors=all.First(x=>x.name=="Scene01_Morning").gameObject;motion.prop=all.First(x=>x.name=="Scene01_PropMotion").gameObject;
 foreach(var tr in timeline.GetOutputTracks().OfType<AnimationTrack>().Where(tr=>tr.name.StartsWith("Scene01")))tr.muted=true;
 var motionTrack=timeline.GetOutputTracks().OfType<Scene01MotionTrack>().FirstOrDefault()??timeline.CreateTrack<Scene01MotionTrack>(null,"Scene01 · 动作采样 / 可拖动预览");
 if(!motionTrack.GetClips().Any()){var tc=motionTrack.CreateDefaultClip();tc.start=0;tc.duration=26;}
 dir.SetGenericBinding(motionTrack,motion); EditorUtility.SetDirty(timeline);AssetDatabase.SaveAssets();
 dir.RebuildGraph(); AnimationMode.StartAnimationMode(); var report="";
 foreach(float t in new[]{0f,6f,10f,15f,21f,25f,28f}){
 dir.time=t;dir.Evaluate();
 var dialogue=all.First(x=>x.name=="Scene01_Dialogue").GetComponent<Scene01Dialogue>(); dialogue.SendMessage("Update");
 cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();File.WriteAllBytes("Previews/Scene01/"+t.ToString("00")+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
 report+="arm="+all.First(x=>x.name=="Father_Young").GetComponentsInChildren<Transform>().First(x=>x.name=="LeftUpperArm").localEulerAngles+" "+"t="+t+" father="+all.First(x=>x.name=="Father_Young").position+" daughter="+all.First(x=>x.name=="Daughter_Child_Schoolbag").position+" burner="+all.First(x=>x.name=="Scene01_Burner").position+" actors="+all.First(x=>x.name=="Scene01_Morning").gameObject.activeSelf+"\n";
 }
 cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camGo);dir.time=0;dir.Evaluate();
 AnimationMode.StopAnimationMode(); dir.time=0; dir.Evaluate(); EditorSceneManager.SaveScene(scene);File.WriteAllText("Logs/Scene01-sampled.txt",report);File.WriteAllText("Temp/Scene01.qa.done","OK");
 }catch(Exception ex){File.WriteAllText("Temp/Scene01.qa.error",ex.ToString());}
 }
}




