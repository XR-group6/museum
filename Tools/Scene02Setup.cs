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
[InitializeOnLoad]public static class Scene02Setup{
 const double Start=37.2333333333333,Duration=18;
 static Scene02Setup(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){
 if(File.Exists("Temp/Scene02.build.done"))return;
 if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
 if(SceneManager.GetActiveScene().name!="scene_04_01")return;
 try{Build();File.WriteAllText("Temp/Scene02.build.done","OK");}catch(Exception ex){File.WriteAllText("Temp/Scene02.build.error",ex.ToString());Debug.LogException(ex);}
 }
 static Transform Bone(Transform root,string n)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);
 static TextMesh Text(string name,Transform parent,Font font,float size){var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(parent,false);var text=go.GetComponent<TextMesh>();text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=48;text.characterSize=size;text.color=new Color(1,.94f,.80f);if(font){text.font=font;text.GetComponent<MeshRenderer>().sharedMaterial=font.material;}return text;}
 static void Build(){
 var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();Func<string,Transform> find=n=>all.First(t=>t.name==n);
 if(all.Any(t=>t.name=="Scene02_FirstWorkDay"))throw new Exception("Scene02 already built; do not insert time twice.");
 EditorSceneManager.SaveScene(scene,"Logs/scene_04_01_before_scene02.unity",true);
 var dir=find("TimeLine").GetComponent<PlayableDirector>();var timeline=dir.playableAsset as TimelineAsset;
 // Insert an actual hold, preserving the first turn's ending and all later authored transitions.
 foreach(var track in timeline.GetOutputTracks().ToArray()){
 foreach(var tc in track.GetClips()){
 if(tc.start>=Start-.001)tc.start+=Duration;else if(tc.end>Start+.001)tc.duration+=Duration;
 }
 var anim=track as AnimationTrack;if(!anim||!anim.infiniteClip)continue;
 foreach(var binding in AnimationUtility.GetCurveBindings(anim.infiniteClip)){
 var c=AnimationUtility.GetEditorCurve(anim.infiniteClip,binding);if(c.length==0||c.keys[c.length-1].time<Start-.001)continue;
 float value=c.Evaluate((float)Start);var keys=new List<Keyframe>();
 foreach(var k in c.keys){if(k.time<Start-.001)keys.Add(k);else if(k.time>Start+.001){var shifted=k;shifted.time+=(float)Duration;keys.Add(shifted);}}
 keys.Add(new Keyframe((float)Start,value,0,0));keys.Add(new Keyframe((float)(Start+Duration),value,0,0));c.keys=keys.OrderBy(k=>k.time).ToArray();AnimationUtility.SetEditorCurve(anim.infiniteClip,binding,c);
 }
 EditorUtility.SetDirty(anim.infiniteClip);
 }
 var house=find("scene_09_models");var group=new GameObject("Scene02_FirstWorkDay");group.transform.SetParent(house,false);
 Transform daughter=find("Daughter_Adult_02"),father=find("Father_Elder_02"),mother=find("Mother_Elder_02");
 foreach(var actor in new[]{daughter,father,mother}){actor.SetParent(group.transform,true);foreach(var animator in actor.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);foreach(var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))if(behaviour is IncenseCharacters.IncenseCharacterPlayer)behaviour.enabled=false;}
 daughter.position=new Vector3(-7.65f,.05f,1.98f);daughter.rotation=Quaternion.Euler(0,270,0);
 father.position=new Vector3(-7.03f,.05f,2.89f);father.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(daughter.position-father.position,Vector3.up));
 mother.position=new Vector3(-5.30f,.05f,3.12f);mother.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(daughter.position-mother.position,Vector3.up));
 var burner=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project3D/Prefabs/prop/incense_burner_tripod.prefab"));burner.name="Scene02_Burner";burner.transform.SetParent(house,true);burner.transform.position=new Vector3(-6.25f,.84f,3.38f);
 var story=dir.gameObject.AddComponent<Scene02Story>();story.director=dir;story.father=father;story.mother=mother;story.daughter=daughter;story.burner=burner.transform;story.startTime=Start;story.duration=Duration;story.exitLocalPosition=group.transform.InverseTransformPoint(new Vector3(-9.05f,.05f,1.86f));
 var scene01=dir.GetComponent<Scene01CenserView>();var font=scene01.dialogue.font;
 var caption=Text("Scene02_Dialogue",house,font,.014f);story.caption=caption;
 var title=Text("Scene02_History",group.transform,font,.014f);title.text="约1960年｜白天\n两岸相隔，返乡的日子一直定不下来。\n当年牵着父亲的手上船的女孩，已经在台湾长大，开始工作了。";story.title=title.transform;
 var oldTitle=find("Tips").GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t=>t.text.StartsWith("Scene 2"));oldTitle.gameObject.SetActive(false);
 scene01.scene02=story;
 var trackStory=timeline.CreateTrack<Scene02StoryTrack>(null,"Scene02 · 第一天上班 / 回头告别 / 父亲点头");var clip=trackStory.CreateDefaultClip();clip.start=Start;clip.duration=Duration;clip.displayName="1960 · 第一天上班（18秒）";dir.SetGenericBinding(trackStory,story);
 var activation=timeline.CreateTrack<ActivationTrack>(null,"Scene02 · 人物及年代字幕");var active=activation.CreateDefaultClip();active.start=Start;active.duration=Duration;activation.postPlaybackState=ActivationTrack.PostPlaybackState.Inactive;dir.SetGenericBinding(activation,group);
 EditorUtility.SetDirty(timeline);EditorUtility.SetDirty(scene01);EditorUtility.SetDirty(story);AssetDatabase.SaveAssets();dir.RebuildGraph();
 // Check the new segment and the existing scene01 after inserting the hold.
 string report="";Quaternion before=Quaternion.identity;float nodAngle=0;
 foreach(float t in new[]{0f,2f,4f,6.4f,7f,8f,9.5f,11f,14f,17f}){
 dir.time=Start+t;dir.Evaluate();story.Sample(t);scene01.ApplyView();
 var cam=scene01.headsetCamera.isActiveAndEnabled?scene01.headsetCamera:scene01.desktopCamera;
 float leg=Mathf.Max(Quaternion.Angle(Bone(daughter,"LeftUpperLeg").localRotation,Quaternion.identity),Quaternion.Angle(Bone(daughter,"RightUpperLeg").localRotation,Quaternion.identity));
 float err=Vector3.Distance(cam.transform.position,scene01.EyeAt(dir.time));
 var q=Bone(father,"Head").localRotation;if(t==6.4f)before=q;if(t==7)nodAngle=Quaternion.Angle(before,q);
 if(t<10.8&&leg>.1f)throw new Exception("Scene02 idle leg moved at "+t);if(err>.0001f)throw new Exception("Scene02 eye position mismatch");
 report+="local="+t+" timeline="+dir.time+" daughter="+daughter.position+" leg="+leg+" eyeError="+err+" caption="+caption.text+"\n";
 }
 if(nodAngle<5)throw new Exception("Father nod missing");report+="fatherSingleNod="+nodAngle+" degrees\n";
 dir.time=6;dir.Evaluate();scene01.ApplyView();if(!dir.GetComponent<Scene01Motion>().actors.activeSelf)throw new Exception("Scene01 activation regression");
 dir.time=0;dir.Evaluate();scene01.ApplyView();
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("Logs/Scene02-validation.txt",report);
 }
}
