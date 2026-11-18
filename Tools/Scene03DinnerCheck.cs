using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class Scene03DinnerCheck{
 static Scene03DinnerCheck(){EditorApplication.delayCall+=Run;}
 static void Run(){if(File.Exists("Temp/Scene03Dinner.check.done")||!File.Exists("Temp/Scene03Dinner.build.done"))return;try{
 var scene=SceneManager.GetActiveScene();if(scene.name!="scene_04_01"||EditorApplication.isPlaying)return;
 var story=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Scene03DinnerStory>(true)).First();var view=story.GetComponent<Scene01CenserView>();var dir=story.director;
 Quaternion[] seated=null;float maxLeg=0,maxEye=0,maxHand=0;Vector3 startPlate=Vector3.zero;float plateHeight=0;string report="";
 var legBones=new[]{story.father,story.mother,story.daughter,story.child}.SelectMany(a=>a.GetComponentsInChildren<Transform>(true)).Where(b=>b.name.Contains("UpperLeg")||b.name.Contains("LowerLeg")||b.name.Contains("Foot")).ToArray();
 for(int i=0;i<=88;i++){
 float t=i*.25f;dir.time=story.startTime+t;dir.Evaluate();if(t>=story.duration)story.Sample(t);view.ApplyView();
 if(i==0){seated=legBones.Select(b=>b.localRotation).ToArray();startPlate=story.fishPlate.position;plateHeight=startPlate.y;}
 for(int k=0;k<legBones.Length;k++)maxLeg=Mathf.Max(maxLeg,Quaternion.Angle(seated[k],legBones[k].localRotation));
 var cam=view.headsetCamera.isActiveAndEnabled?view.headsetCamera:view.desktopCamera;maxEye=Mathf.Max(maxEye,Vector3.Distance(cam.transform.position,view.EyeAt(dir.time)));
 if(t>=3&&t<=6.2f){Vector3 toDaughter=Vector3.ProjectOnPlane(story.daughter.position-story.fishPlate.position,Vector3.up).normalized;Vector3 contact=story.fishPlate.position+toDaughter*.18f+Vector3.up*.045f;maxHand=Mathf.Max(maxHand,Vector3.Distance(story.Bone(story.daughter,"LeftHand").position,contact));}
 if(Mathf.Abs(story.fishPlate.position.y-plateHeight)>.0001)throw new Exception("Fish plate left tabletop");
 }
 float displacement=Vector3.Distance(startPlate,story.fishPlate.position);
 if(maxLeg>.01f||maxEye>.0001f||displacement<.15f||maxHand>.16f)throw new Exception("Dinner verification failed: legs="+maxLeg+" eye="+maxEye+" push="+displacement+" hand="+maxHand);
 dir.time=story.startTime;dir.Evaluate();view.ApplyView();if(Vector3.Distance(startPlate,story.fishPlate.position)>.00001f)throw new Exception("Fish plate does not reset on seek");
 dir.time=story.startTime+7.5;dir.Evaluate();view.ApplyView();var before=story.Bone(story.father,"Head").localRotation;dir.time=story.startTime+8;dir.Evaluate();view.ApplyView();float nod=Quaternion.Angle(before,story.Bone(story.father,"Head").localRotation);if(nod<5)throw new Exception("Nod missing");
 story.PreviewSmile(1);if(story.SmileVertexCount<4)throw new Exception("Smile mouth vertices missing");int smileVertices=story.SmileVertexCount;story.RestoreSmile();
 report="89 samples: seated leg drift="+maxLeg+" degrees; camera eye error="+maxEye+" metres; fish push="+displacement+" metres; hand/plate error="+maxHand+" metres.\nFather nod="+nod+" degrees; smile mouth vertices="+smileVertices+".\nBackward seeking resets fish plate; persistent seated poses.\nTimeline="+story.startTime+" to "+story.EndTime+".\n";
 File.WriteAllText("Logs/Scene03Dinner-validation.txt",report);
 dir.time=story.startTime+4;dir.Evaluate();view.BeginView();view.ApplyView();
 EditorApplication.delayCall+=()=>Render(story,view);
 }catch(Exception ex){File.WriteAllText("Temp/Scene03Dinner.check.error",ex.ToString());Debug.LogException(ex);}}
 static void Render(Scene03DinnerStory story,Scene01CenserView view){try{
 var active=view.headsetCamera.isActiveAndEnabled?view.headsetCamera:view.desktopCamera;
 var go=new GameObject("Scene03 sofa-burner preview");var camera=go.AddComponent<Camera>();camera.transform.SetPositionAndRotation(active.transform.position,active.transform.rotation);camera.nearClipPlane=.025f;camera.fieldOfView=100;camera.aspect=1.5f;var rt=new RenderTexture(1200,800,24);camera.targetTexture=rt;
 string viewport="";foreach(var actor in new[]{story.father,story.mother,story.daughter,story.child}){var p=camera.WorldToViewportPoint(story.Bone(actor,"Head").position);viewport+=actor.name+"="+p+"\n";if(p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1)throw new Exception("Face outside first person viewport: "+actor.name+" "+p);}
 camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();Directory.CreateDirectory("Previews/Scene03Dinner");File.WriteAllBytes("Previews/Scene03Dinner/FirstPerson_Dinner.png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
 File.AppendAllText("Logs/Scene03Dinner-validation.txt",viewport);
 story.director.time=0;story.director.Evaluate();story.Sample(0);story.caption.text="";view.ApplyView();view.RestoreRenderers();story.RestoreSmile();EditorSceneManager.MarkSceneDirty(story.gameObject.scene);EditorSceneManager.SaveScene(story.gameObject.scene);AssetDatabase.SaveAssets();File.WriteAllText("Temp/Scene03Dinner.check.done","OK");
 }catch(Exception ex){File.WriteAllText("Temp/Scene03Dinner.check.error",ex.ToString());Debug.LogException(ex);}}
}
