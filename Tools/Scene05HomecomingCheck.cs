using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
[InitializeOnLoad]public static class Scene05HomecomingCheck
{
 static Scene05HomecomingCheck(){EditorApplication.delayCall+=Run;}
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Run(){if(EditorApplication.isPlaying||!File.Exists("Temp/scene05-build.done")||File.Exists("Temp/scene05-check.done"))return;try{
 var story=UnityEngine.Object.FindObjectOfType<Scene05HomecomingStory>();if(!story)return;story.Sample(0);if(!File.Exists("Temp/scene05-pose.done")){story.father.position=new Vector3(-.72f,.15f,-3.85f);story.CapturePose();EditorUtility.SetDirty(story);File.WriteAllText("Temp/scene05-pose.done","OK");}
 Assert(story.prayer&&story.expectation&&story.leave&&story.home&&story.finalVoice&&story.chime&&story.ticking&&story.doorSound,"Missing audio");
 Assert(story.homeAt+story.home.length<story.withdrawAt,"Father dialogue overlaps departure");Assert(story.leaveAt+story.leave.length<story.homeAt,"Farewell voices overlap");
 Assert(story.closedAt<story.exploreAt&&story.outsideAt<story.closeAt,"Door/exit ordering");
 Directory.CreateDirectory("Previews/Scene05Homecoming");var report=new System.Collections.Generic.List<string>();
 story.director.RebuildGraph();
 foreach(float t in new[]{0f,11.5f,18f,26.8f,31f,38f,40f}){
 story.director.time=t;story.director.Evaluate();story.Sample(t);
 if(t==11.5f||t==18f){float distance=Vector3.Distance(story.Bone("RightMiddle3").position,story.touchPoint);report.Add("Touch t="+t+": "+distance+"m");Assert(distance<.015f,"Hand cannot reach burner: "+distance);}
 if(t==26.8f){float distance=Vector3.Distance(story.Bone("LeftHand").position,story.luggageHandle.position);report.Add("Luggage grip="+distance);Assert(distance<.01f,"Luggage grip drift");}
 if(t==40)Assert(!story.father.gameObject.activeSelf,"Father still inside after departure");
 if(t==0||t==11.5f||t==26.8f||t==38f)Render(story,t);
 }
 story.Sample(0);var p=story.father.position;story.Sample(32);story.Sample(0);Assert(Vector3.Distance(p,story.father.position)<.00001f,"Seek drift");
 Assert(!story.permit.Ready,"Permit interactive before exploration");Assert(!UnityEngine.Object.FindObjectOfType<MuseumSceneTour>(true).enabled,"Legacy auto progression still active");
 report.Add("Audio clips all assigned; dialogue timing, exit-before-close, close-before-exploration PASS");report.Add("Timeline backwards seek stable; pre-exploration permit locked PASS");
 story.director.time=0;story.director.Evaluate();story.Sample(0);EditorSceneManager.MarkSceneDirty(story.gameObject.scene);EditorSceneManager.SaveScene(story.gameObject.scene);File.WriteAllLines("Logs/Scene05Homecoming-validation.txt",report);File.WriteAllText("Temp/scene05-check.done","OK");
 }catch(Exception e){File.WriteAllText("Temp/scene05-check.error",e.ToString());Debug.LogException(e);}}
 static void Render(Scene05HomecomingStory story,float t){var go=new GameObject("Scene05 QA camera");var camera=go.AddComponent<Camera>();Vector3 eye=t<37?PermitInspection.BoundsOf(story.burner.gameObject).center+Vector3.up*.035f:story.explorationEye;Vector3 look=t<28?story.Bone("Head").position:new Vector3(0,1,-5.4f);camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye));camera.fieldOfView=85;camera.nearClipPlane=.025f;camera.aspect=1.5f;var rs=story.burner.GetComponentsInChildren<Renderer>();foreach(var r in rs)r.forceRenderingOff=t<37;var rt=new RenderTexture(1200,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();File.WriteAllBytes("Previews/Scene05Homecoming/Frame_"+t.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png",tex.EncodeToPNG());foreach(var r in rs)r.forceRenderingOff=false;camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
}




