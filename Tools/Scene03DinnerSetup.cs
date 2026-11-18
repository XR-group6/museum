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
[InitializeOnLoad]public static class Scene03DinnerSetup{
 const double Start=62,Duration=22;
 static Scene03DinnerSetup(){EditorApplication.delayCall+=Run;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Run;};}
 static void Run(){if(File.Exists("Temp/Scene03Dinner.build.done"))return;if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}if(SceneManager.GetActiveScene().name!="scene_04_01")return;try{Build();File.WriteAllText("Temp/Scene03Dinner.build.done","OK");}catch(Exception ex){File.WriteAllText("Temp/Scene03Dinner.build.error",ex.ToString());Debug.LogException(ex);}}
 static TextMesh Text(string name,Transform parent,Font font,float size){var go=new GameObject(name,typeof(TextMesh));go.transform.SetParent(parent,false);var text=go.GetComponent<TextMesh>();text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=48;text.characterSize=size;text.color=new Color(1,.94f,.80f);if(font){text.font=font;text.GetComponent<MeshRenderer>().sharedMaterial=font.material;}return text;}
 static void Build(){
 var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();Func<string,Transform> find=n=>all.First(t=>t.name==n);
 if(all.Any(t=>t.name=="Scene03_ReunionDinner"))throw new Exception("Dinner already exists; time must only be inserted once.");
 EditorSceneManager.SaveScene(scene,"Logs/scene_04_01_before_dinner.unity",true);
 var dir=find("TimeLine").GetComponent<PlayableDirector>();var timeline=dir.playableAsset as TimelineAsset;
 foreach(var track in timeline.GetOutputTracks().ToArray()){
 foreach(var tc in track.GetClips()){if(tc.start>=Start-.001)tc.start+=Duration;else if(tc.end>Start+.001)tc.duration+=Duration;}
 var anim=track as AnimationTrack;if(!anim||!anim.infiniteClip)continue;
 foreach(var binding in AnimationUtility.GetCurveBindings(anim.infiniteClip)){
 var c=AnimationUtility.GetEditorCurve(anim.infiniteClip,binding);if(c.length==0||c.keys[c.length-1].time<Start-.001)continue;float value=c.Evaluate((float)Start);var keys=new List<Keyframe>();
 foreach(var k in c.keys){if(k.time<Start-.001)keys.Add(k);else if(k.time>Start+.001){var shifted=k;shifted.time+=(float)Duration;keys.Add(shifted);}}
 keys.Add(new Keyframe((float)Start,value,0,0));keys.Add(new Keyframe((float)(Start+Duration),value,0,0));c.keys=keys.OrderBy(k=>k.time).ToArray();AnimationUtility.SetEditorCurve(anim.infiniteClip,binding,c);
 }EditorUtility.SetDirty(anim.infiniteClip);
 }
 var house=find("scene_09_models");var group=new GameObject("Scene03_ReunionDinner");group.transform.SetParent(house,false);
 var story=dir.gameObject.AddComponent<Scene03DinnerStory>();story.director=dir;story.startTime=Start;story.duration=Duration;
 story.father=find("Father_Elder_03");story.mother=find("Mother_Elder_03");story.daughter=find("Daughter_Adult_03");story.child=find("Daughter_Child_03");
 var actors=new[]{story.father,story.daughter,story.mother,story.child};
 var points=new[]{new Vector3(-.22f,.05f,8.76f),new Vector3(1.53f,.05f,8.76f),new Vector3(.22f,.05f,9.93f),new Vector3(1.36f,.05f,9.80f)};
 var headings=new[]{85f,275f,155f,200f};
 for(int i=0;i<actors.Length;i++){
 var actor=actors[i];actor.SetParent(group.transform,true);foreach(var animator in actor.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);foreach(var player in actor.GetComponentsInChildren<IncenseCharacters.IncenseCharacterPlayer>(true))player.enabled=false;
 actor.position=points[i];actor.rotation=Quaternion.Euler(0,headings[i],0);
 foreach(var bone in actor.GetComponentsInChildren<Transform>(true))if(bone!=actor&&bone.name!="CharacterMesh")bone.localRotation=Quaternion.identity;
 story.Seat(actor,i==3?.67f:.47f);story.RestHands(actor);
 }
 // Reuse the furnished room's four existing stools; the viewer-facing seat stays empty.
 string[] stoolNames={"1980s_stool_4","1980s_stool_6","1980s_stool_3","1980s_stool_1"};
 for(int i=0;i<4;i++){var stool=find(stoolNames[i]);stool.position=new Vector3(points[i].x,stool.position.y,points[i].z);}
 var booster=GameObject.CreatePrimitive(PrimitiveType.Cube);booster.name="Scene03 child seat cushion";booster.transform.SetParent(group.transform,true);booster.transform.position=new Vector3(points[3].x,.57f,points[3].z);booster.transform.localScale=new Vector3(.34f,.20f,.34f);UnityEngine.Object.DestroyImmediate(booster.GetComponent<Collider>());
 var cushionMat=new Material(Shader.Find("Standard"));cushionMat.color=new Color(.33f,.12f,.08f);System.IO.Directory.CreateDirectory("Assets/Scene03DinnerAnimation");AssetDatabase.CreateAsset(cushionMat,"Assets/Scene03DinnerAnimation/ChildCushion.mat");booster.GetComponent<Renderer>().sharedMaterial=cushionMat;
 story.fishPlate=find("1980s_steamed_fish");story.plateStart=house.InverseTransformPoint(new Vector3(1.06f,.85f,9.14f));story.plateEnd=house.InverseTransformPoint(new Vector3(.82f,.85f,9.12f));story.fishPlate.localPosition=story.plateStart;
 var burner=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project3D/Prefabs/prop/incense_burner_tripod.prefab"));burner.name="Scene03_SofaTableBurner";burner.transform.SetParent(house,true);burner.transform.position=new Vector3(.9f,.47f,6.98f);story.burner=burner.transform;
 var view=dir.GetComponent<Scene01CenserView>();view.scene03=story;view.scene03TurnDegrees=-146;
 var caption=Text("Scene03_DinnerDialogue",house,view.dialogue.font,.016f);story.caption=caption;
 var title=Text("Scene03_DinnerHistory",group.transform,view.dialogue.font,.024f);title.text="约1970年｜除夕夜\n在台湾，除夕一家人同桌吃年夜饭，叫作“围炉”。\n如今，女儿也带着自己的孩子回来过年，\n一张桌旁坐了三代人。";story.title=title.transform;
 var oldTitle=find("Tips").GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t=>t.text.StartsWith("Scene 3"));oldTitle.gameObject.SetActive(false);
 var lightGo=new GameObject("Scene03 warm dinner fill",typeof(Light));lightGo.transform.SetParent(group.transform,true);lightGo.transform.position=new Vector3(.7f,2.65f,8.2f);var fill=lightGo.GetComponent<Light>();fill.type=LightType.Point;fill.intensity=.8f;fill.range=8;fill.color=new Color(1,.78f,.52f);fill.shadows=LightShadows.None;
 story.CapturePose();
 var trackStory=timeline.CreateTrack<Scene03DinnerTrack>(null,"Scene03 · 年夜饭 / 推鱼盘 / 父亲微笑点头");var clip=trackStory.CreateDefaultClip();clip.start=Start;clip.duration=Duration;clip.displayName="1970 · 三代围炉（22秒）";dir.SetGenericBinding(trackStory,story);
 var trackActive=timeline.CreateTrack<ActivationTrack>(null,"Scene03 · 四人坐席及年代字幕");var activation=trackActive.CreateDefaultClip();activation.start=Start;activation.duration=Duration;trackActive.postPlaybackState=ActivationTrack.PostPlaybackState.Inactive;dir.SetGenericBinding(trackActive,group);
 EditorUtility.SetDirty(view);EditorUtility.SetDirty(story);EditorUtility.SetDirty(timeline);AssetDatabase.SaveAssets();dir.RebuildGraph();story.Sample(0);dir.time=0;dir.Evaluate();view.ApplyView();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 }
}


