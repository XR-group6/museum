using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
[InitializeOnLoad] public static class Scene01ViewSetup {
 static Scene01ViewSetup(){EditorApplication.delayCall+=Run; EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall+=Run;};}
 static void Run(){
 if(File.Exists("Temp/Scene01.view.done")||SceneManager.GetActiveScene().name!="scene_04_01")return;
 if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;} try{
 var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
 Func<string,Transform> find=n=>all.First(t=>t.name==n);
 var director=find("TimeLine").GetComponent<PlayableDirector>();
 var view=director.GetComponent<Scene01CenserView>()??director.gameObject.AddComponent<Scene01CenserView>();
 view.director=director;view.storyBurner=find("Scene01_Burner");view.carouselBurner=find("court_censer");
 view.daughterHead=find("Daughter_Child_Schoolbag").GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
 view.headsetRig=find("[CameraRig]");view.headsetCamera=view.headsetRig.GetComponentInChildren<Camera>(true);
 view.desktopRig=find("Desktop Test Player");view.desktopCamera=view.desktopRig.GetComponentInChildren<Camera>(true);
 view.dialogue=find("Scene01_Dialogue").GetComponent<TextMesh>();
 view.scene01Title=find("Tips").GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t=>t.text.Contains("1951")).transform;
 director.time=0;director.RebuildGraph();director.Evaluate();
 var eye=view.EyeAt(0); var direction=view.daughterHead.position-eye;
 foreach(var camera in new[]{view.headsetCamera,view.desktopCamera}){
 var rig=camera==view.headsetCamera?view.headsetRig:view.desktopRig;
 rig.Rotate(Vector3.up,Mathf.DeltaAngle(camera.transform.eulerAngles.y,Quaternion.LookRotation(direction).eulerAngles.y),Space.World);
 rig.position+=eye-camera.transform.position;
 if(camera==view.desktopCamera)camera.transform.rotation=Quaternion.LookRotation(direction);
 }
 EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 Directory.CreateDirectory("Previews/Scene01/FirstPerson");
 // Verify camera/eye equality at the same sampled animation times, using a dedicated preview camera.
 var go=new GameObject("Scene01 First Person QA");var cam=go.AddComponent<Camera>();cam.nearClipPlane=.025f;cam.fieldOfView=90;cam.aspect=1.5f;
 var rt=new RenderTexture(1200,800,24);cam.targetTexture=rt;
 foreach(var root in new[]{view.storyBurner,view.carouselBurner})foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=true;
 view.BeginView(); string report="";
 foreach(float t in new[]{0f,6f,11f,21f,28f}){
 director.time=t;director.Evaluate();var expected=view.EyeAt(t);view.ApplyView();var active=view.headsetCamera.isActiveAndEnabled?view.headsetCamera:view.desktopCamera;cam.transform.position=active.transform.position;
 cam.transform.rotation=active.transform.rotation;
 cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();File.WriteAllBytes("Previews/Scene01/FirstPerson/"+t.ToString("00")+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
 report+="t="+t+" burner="+view.storyBurner.position+" eye="+expected+" cameraError="+Vector3.Distance(expected,cam.transform.position)+"\n";
 }
 foreach(var root in new[]{view.storyBurner,view.carouselBurner})foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=false;
 cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
 view.RestoreRenderers(); director.time=0;director.Evaluate();view.ApplyView();EditorSceneManager.SaveScene(scene);
 File.WriteAllText("Logs/Scene01-firstperson.txt",report);File.WriteAllText("Temp/Scene01.view.done","OK");
 }catch(Exception ex){File.WriteAllText("Temp/Scene01.view.error",ex.ToString());Debug.LogException(ex);}
 }
}


