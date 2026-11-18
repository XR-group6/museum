using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class Scene04HarborFinalize
{
 static Scene04HarborFinalize(){EditorApplication.delayCall+=Run;}
 static void Run(){if(!File.Exists("Temp/Scene04Harbor.build.done")||File.Exists("Temp/Scene04Harbor.final.done"))return;try{
 var scene=SceneManager.GetActiveScene();if(scene.name!="scene_04_01"||EditorApplication.isPlaying)return;
 var story=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Scene04HarborStory>(true)).First();var view=story.GetComponent<Scene01CenserView>();
 story.father.position=new Vector3(6.7f,.05f,4.45f);story.father.rotation=Quaternion.Euler(0,100,0);
 story.chair.position=story.father.position;story.chair.rotation=Quaternion.Euler(-90,100,0);
 var vertices=story.chair.GetComponentsInChildren<MeshFilter>(true).SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).ToArray();float top=vertices.Max(v=>v.y);var tops=vertices.Where(v=>v.y>top-.08f).ToArray();Vector3 centre=tops.Aggregate(Vector3.zero,(a,b)=>a+b)/tops.Length;float backDot=Vector3.Dot(centre-story.chair.position,story.father.forward);
 if(backDot>0)story.chair.Rotate(Vector3.up,180,Space.World);
 story.seaTarget=new Vector3(10.5f,1.35f,3.9f);view.scene04TurnDegrees=-144;
 story.Seat(.48f);story.CapturePose();story.Sample(0);story.director.time=0;story.director.Evaluate();view.ApplyView();EditorUtility.SetDirty(story);EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("Logs/Scene04Harbor-chair.txt","Chair/root="+story.chair.position+" backrest direction check="+backDot+" flipped="+(backDot>0)+"\nDoorway is at east wall x~7.65, z~3.9..4.9; chair moved inside to clear wall and doorway.\n");File.WriteAllText("Temp/Scene04Harbor.final.done","OK");EditorApplication.delayCall+=Scene04HarborCheck.Run;
 }catch(Exception ex){File.WriteAllText("Temp/Scene04Harbor.final.error",ex.ToString());Debug.LogException(ex);}}
}
