using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
[InitializeOnLoad]public static class Scene04HarborSetup
{
 const double Start=88.0833333333333,Duration=22;
 static Scene04HarborSetup(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){if(File.Exists("Temp/Scene04Harbor.build.done"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}if(SceneManager.GetActiveScene().name!="scene_04_01")return;try{Build();File.WriteAllText("Temp/Scene04Harbor.build.done","OK");}catch(Exception ex){File.WriteAllText("Temp/Scene04Harbor.build.error",ex.ToString());Debug.LogException(ex);}}
 static TextMesh MakeText(string name,Transform parent,Font font,float size){var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(parent,false);var text=go.GetComponent<TextMesh>();text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=48;text.characterSize=size;text.color=new Color(1,.94f,.80f);if(font){text.font=font;text.GetComponent<MeshRenderer>().sharedMaterial=font.material;}return text;}
 static void Build()
 {
  var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();Func<string,Transform> find=n=>all.First(t=>t.name==n);
  if(all.Any(t=>t.name=="Scene04_HarborDusk"))throw new Exception("Scene04 already exists; do not insert time twice.");
  EditorSceneManager.SaveScene(scene,"Logs/scene_04_01_before_harbor.unity",true);
  var director=find("TimeLine").GetComponent<PlayableDirector>();var timeline=director.playableAsset as TimelineAsset;
  foreach(var track in timeline.GetOutputTracks().ToArray())
  {
   foreach(var tc in track.GetClips()){if(tc.start>=Start-.001)tc.start+=Duration;else if(tc.end>Start+.001)tc.duration+=Duration;}
   var anim=track as AnimationTrack;if(!anim||!anim.infiniteClip)continue;
   foreach(var binding in AnimationUtility.GetCurveBindings(anim.infiniteClip))
   {
    var c=AnimationUtility.GetEditorCurve(anim.infiniteClip,binding);if(c.length==0||c.keys[c.length-1].time<Start-.001)continue;
    float value=c.Evaluate((float)Start);var keys=new List<Keyframe>();
    foreach(var k in c.keys){if(k.time<Start-.001)keys.Add(k);else if(k.time>Start+.001){var shifted=k;shifted.time+=(float)Duration;keys.Add(shifted);}}
    keys.Add(new Keyframe((float)Start,value,0,0));keys.Add(new Keyframe((float)(Start+Duration),value,0,0));c.keys=keys.OrderBy(k=>k.time).ToArray();AnimationUtility.SetEditorCurve(anim.infiniteClip,binding,c);
   }
   EditorUtility.SetDirty(anim.infiniteClip);
  }
  var house=find("scene_09_models");var group=new GameObject("Scene04_HarborDusk");group.transform.SetParent(house,false);
  var father=find("Father_Elder_04");father.SetParent(group.transform,true);
  foreach(var animator in father.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);
  foreach(var player in father.GetComponentsInChildren<IncenseCharacters.IncenseCharacterPlayer>(true))player.enabled=false;
  father.position=new Vector3(7.2f,.05f,4.1f);father.rotation=Quaternion.Euler(0,115,0);
  foreach(var bone in father.GetComponentsInChildren<Transform>(true))if(bone!=father&&bone.name!="CharacterMesh")bone.localRotation=Quaternion.identity;
  var chair=find("1990s_rocking_chair");chair.position=father.position;chair.rotation=Quaternion.Euler(-90,115,0);
  var story=director.gameObject.AddComponent<Scene04HarborStory>();story.director=director;story.startTime=Start;story.duration=Duration;story.father=father;story.chair=chair;
  story.Seat(.48f);story.CapturePose();
  var burner=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project3D/Prefabs/prop/incense_burner_tripod.prefab"));burner.name="Scene04_DiningTableBurner";burner.transform.SetParent(house,true);burner.transform.position=new Vector3(6.95f,.84f,2.85f);story.burner=burner.transform;
  var view=director.GetComponent<Scene01CenserView>();view.scene04=story;view.scene04TurnDegrees=-124;
  story.caption=MakeText("Scene04_HarborDialogue",house,view.dialogue.font,.016f);
  var title=MakeText("Scene04_HarborHistory",group.transform,view.dialogue.font,.023f);title.text="约1980年｜黄昏\n那时，两岸还未开放探亲。父亲惦记着海那边的老屋，\n也在海这边的屋檐下，看着女儿和外孙两代人长大。";story.title=title.transform;
  var oldTitle=find("Tips").GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t=>t.text.StartsWith("Scene 4"));oldTitle.gameObject.SetActive(false);
  AddLight(group.transform,"Scene04 sunset through doorway",new Vector3(8.8f,2.4f,3.3f),new Color(1,.58f,.30f),.8f,7);
  AddLight(group.transform,"Scene04 soft interior fill",new Vector3(6.3f,2.1f,3.0f),new Color(.60f,.68f,.88f),.45f,5);
  var motion=timeline.CreateTrack<Scene04HarborTrack>(null,"Scene04 · 黄昏望海 / 双手搭膝 / 微微抬眼");var clip=motion.CreateDefaultClip();clip.start=Start;clip.duration=Duration+1;clip.displayName="1980 · 黄昏望海（22秒及留帧）";director.SetGenericBinding(motion,story);
  var active=timeline.CreateTrack<ActivationTrack>(null,"Scene04 · 父亲及黄昏字幕");var activation=active.CreateDefaultClip();activation.start=Start;activation.duration=Duration+1;active.postPlaybackState=ActivationTrack.PostPlaybackState.Active;director.SetGenericBinding(active,group);
  var bell=MakeBell();story.clockBell=bell;
  var soundGo=new GameObject("Scene04 two clock strikes",typeof(AudioSource));soundGo.transform.SetParent(group.transform,true);soundGo.transform.position=find("1990s_wall_clock").position;
  var source=soundGo.GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=3;source.maxDistance=15;source.volume=.55f;
  var audio=timeline.CreateTrack<AudioTrack>(null,"Scene04 · 两声钟响 → 留白 → 香炉回应");director.SetGenericBinding(audio,source);
  foreach(double at in new[]{10d,12d}){var strike=audio.CreateDefaultClip();var asset=(AudioPlayableAsset)strike.asset;asset.clip=bell;asset.loop=false;strike.start=Start+at;strike.duration=bell.length;strike.displayName=at==10?"第一声钟响":"第二声钟响";}
  EditorUtility.SetDirty(timeline);EditorUtility.SetDirty(view);EditorUtility.SetDirty(story);AssetDatabase.SaveAssets();director.RebuildGraph();story.Sample(0);director.time=0;director.Evaluate();view.ApplyView();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 }
 static void AddLight(Transform parent,string name,Vector3 point,Color color,float intensity,float range){var go=new GameObject(name,typeof(Light));go.transform.SetParent(parent,true);go.transform.position=point;var light=go.GetComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;}
 static AudioClip MakeBell()
 {
  const string path="Assets/Scene04HarborAnimation/黄昏_钟响.wav";Directory.CreateDirectory("Assets/Scene04HarborAnimation");int rate=44100,n=(int)(rate*1.6);byte[] bytes=new byte[44+n*2];
  using(var stream=new MemoryStream(bytes))using(var w=new BinaryWriter(stream)){
   w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+n*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(n*2);
   for(int i=0;i<n;i++){double t=i/(double)rate;double v=Math.Sin(2*Math.PI*523.25*t)*Math.Exp(-2.8*t)+.28*Math.Sin(2*Math.PI*1048.2*t)*Math.Exp(-5*t)+.12*Math.Sin(2*Math.PI*1319*t)*Math.Exp(-7*t);double envelope=Math.Min(1,t*160)*Math.Min(1,(1.6-t)*12);w.Write((short)(4800*v*envelope));}
  }
  File.WriteAllBytes(path,bytes);AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
 }
}

