using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class Scene02Check{
 static Scene02Check(){EditorApplication.delayCall+=Run;}
 static void Run(){if(File.Exists("Temp/Scene02.check.done")||!File.Exists("Temp/Scene02.final.done"))return;try{
 var scene=SceneManager.GetActiveScene();if(scene.name!="scene_04_01"||EditorApplication.isPlaying)return;
 var story=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Scene02Story>(true)).First();var view=story.GetComponent<Scene01CenserView>();var dir=story.director;float maxLeg=0,maxEye=0;Vector3 opening=Vector3.zero;
 for(int i=0;i<=40;i++){float t=i*.25f;dir.time=story.startTime+t;dir.Evaluate();view.ApplyView();if(i==0)opening=story.daughter.position;
 var legs=story.daughter.GetComponentsInChildren<Transform>(true).Where(b=>b.name.Contains("UpperLeg")||b.name.Contains("LowerLeg"));foreach(var b in legs)maxLeg=Mathf.Max(maxLeg,Quaternion.Angle(b.localRotation,Quaternion.identity));var cam=view.headsetCamera.isActiveAndEnabled?view.headsetCamera:view.desktopCamera;maxEye=Mathf.Max(maxEye,Vector3.Distance(cam.transform.position,view.EyeAt(dir.time)));if(Vector3.Distance(opening,story.daughter.position)>.00001f)throw new Exception("Daughter moved while talking");}
 if(maxLeg>.01f||maxEye>.0001f)throw new Exception("Standing / eye verification failed");
 dir.time=story.startTime+17;dir.Evaluate();view.ApplyView();var exit=story.daughter.position;if(Vector3.Distance(exit,opening)<1)throw new Exception("Departure missing");
 dir.time=story.startTime;dir.Evaluate();view.ApplyView();if(Vector3.Distance(opening,story.daughter.position)>.00001f)throw new Exception("Backward seek changed opening pose");
 var fatherHead=story.father.GetComponentsInChildren<Transform>(true).First(b=>b.name=="Head");dir.time=story.startTime+6.4;dir.Evaluate();view.ApplyView();var before=fatherHead.localRotation;dir.time=story.startTime+7;dir.Evaluate();view.ApplyView();float nod=Quaternion.Angle(before,fatherHead.localRotation);if(nod<5)throw new Exception("Father nod missing");
 dir.time=0;dir.Evaluate();story.Sample(0);story.caption.text="";view.ApplyView();view.RestoreRenderers();EditorSceneManager.SaveScene(scene);
 File.WriteAllText("Logs/Scene02-final-validation.txt","Persistent rest pose verified after script reload.\n41 dialogue samples: max leg rotation="+maxLeg+" degrees; max camera/eye error="+maxEye+" metres.\nDaughter opening="+opening+" exit="+exit+"; backward seeking restored opening.\nFather single nod="+nod+" degrees.\nTimeline segment="+story.startTime+" to "+story.EndTime+".\n");File.WriteAllText("Temp/Scene02.check.done","OK");
 }catch(Exception ex){File.WriteAllText("Temp/Scene02.check.error",ex.ToString());}}
}
