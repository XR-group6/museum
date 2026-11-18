using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class Scene02Finalize{
 static Scene02Finalize(){EditorApplication.delayCall+=Run;}
 static void Run(){if(!File.Exists("Temp/Scene02.build.done")||File.Exists("Temp/Scene02.final.done"))return;try{
 var scene=SceneManager.GetActiveScene();if(scene.name!="scene_04_01"||EditorApplication.isPlaying)return;
 var story=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Scene02Story>(true)).First();var view=story.GetComponent<Scene01CenserView>();
 story.father.position=new Vector3(-7.4f,.05f,3.25f);story.father.rotation=Quaternion.Euler(0,140,0);
 story.mother.position=new Vector3(-5.4f,.05f,2.8f);story.mother.rotation=Quaternion.Euler(0,225,0);
 story.burner.position=new Vector3(-5.95f,.84f,3.9f);view.scene02TurnDegrees=70;
 story.title.GetComponent<TextMesh>().characterSize=.024f;
 var group=story.daughter.parent;
 if(!group.Find("Scene02 warm face fill")){var fill=new GameObject("Scene02 warm face fill",typeof(Light));fill.transform.SetParent(group,false);fill.transform.position=new Vector3(-5.8f,2.3f,3.9f);var light=fill.GetComponent<Light>();light.type=LightType.Point;light.intensity=.65f;light.range=7;light.color=new Color(1,.88f,.72f);light.shadows=LightShadows.None;}
 story.daughter.position=new Vector3(-7.65f,.05f,1.98f);story.daughter.rotation=Quaternion.Euler(0,270,0);
 foreach(var actor in new[]{story.daughter,story.father,story.mother})foreach(var bone in actor.GetComponentsInChildren<Transform>(true))if(bone.name=="Head"||bone.name=="Neck"||bone.name=="Chest"||bone.name=="LeftUpperLeg"||bone.name=="RightUpperLeg"||bone.name=="LeftLowerLeg"||bone.name=="RightLowerLeg"||bone.name=="LeftUpperArm"||bone.name=="RightUpperArm")bone.localRotation=Quaternion.identity;
 story.CapturePose();EditorUtility.SetDirty(story);
 var dir=story.director;dir.time=story.startTime+4;dir.Evaluate();story.Sample(4);view.BeginView();view.ApplyView();
 EditorApplication.delayCall+=()=>Render(story,view);
 }catch(Exception ex){File.WriteAllText("Temp/Scene02.final.error",ex.ToString());}}
 static void Render(Scene02Story story,Scene01CenserView view){try{
 var active=view.headsetCamera.isActiveAndEnabled?view.headsetCamera:view.desktopCamera;
 var go=new GameObject("Scene02 first person preview");var camera=go.AddComponent<Camera>();camera.transform.SetPositionAndRotation(active.transform.position,active.transform.rotation);camera.nearClipPlane=.03f;camera.fieldOfView=100;var rt=new RenderTexture(1200,800,24);camera.targetTexture=rt;
 camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();Directory.CreateDirectory("Previews/Scene02");File.WriteAllBytes("Previews/Scene02/FirstPerson_Goodbye.png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
 // A second simulated load must reproduce the same daughter position and departure.
 story.Sample(17);story.Sample(0);var atStart=story.daughter.position;story.Sample(14);story.Sample(0);if(Vector3.Distance(atStart,story.daughter.position)>.00001f)throw new Exception("Seeking changes rest position");
 story.director.time=0;story.director.Evaluate();story.Sample(0);story.caption.text="";view.ApplyView();view.RestoreRenderers();EditorUtility.SetDirty(story);EditorSceneManager.MarkSceneDirty(story.gameObject.scene);EditorSceneManager.SaveScene(story.gameObject.scene);AssetDatabase.SaveAssets();File.WriteAllText("Temp/Scene02.final.done","OK");
 }catch(Exception ex){File.WriteAllText("Temp/Scene02.final.error",ex.ToString());}}
}

