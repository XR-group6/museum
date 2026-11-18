using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
[InitializeOnLoad]public static class Scene03DinnerProbe{
 static Scene03DinnerProbe(){EditorApplication.delayCall+=Run;}
 static void Run(){
 var s=SceneManager.GetActiveScene();var all=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
 File.WriteAllLines("Temp/Scene03Dinner-probe.txt",new[]{s.path,"playing="+EditorApplication.isPlaying}.Concat(all.Select(t=>Path(t)+" pos="+t.position+" rot="+t.eulerAngles+" local="+t.localPosition)));
 var d=all.Select(t=>t.GetComponent<PlayableDirector>()).FirstOrDefault(t=>t);var timeline=d?d.playableAsset as TimelineAsset:null;if(!timeline)return;
 var lines=new List<string>();
 foreach(var t in timeline.GetOutputTracks()){
 lines.Add(t.name+" type="+t.GetType().Name+" bind="+d.GetGenericBinding(t)+" clips="+string.Join(",",t.GetClips().Select(c=>c.start+"/"+c.duration)));
 var a=t as AnimationTrack;if(!a||!a.infiniteClip)continue;
 foreach(var b in AnimationUtility.GetCurveBindings(a.infiniteClip).Take(3))lines.Add(b.path+":"+b.propertyName+" "+string.Join(";",AnimationUtility.GetEditorCurve(a.infiniteClip,b).keys.Select(k=>k.time+"="+k.value)));
 }
 File.WriteAllLines("Temp/Scene03Dinner-timeline.txt",lines);
 }
 static string Path(Transform t)=>t.parent?Path(t.parent)+"/"+t.name:t.name;
}

