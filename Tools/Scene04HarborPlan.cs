using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class Scene04HarborPlan{
 static Scene04HarborPlan(){EditorApplication.delayCall+=Run;}
 static void Run(){if(File.Exists("Temp/Scene04Harbor.plan.done")||!File.Exists("Temp/Scene04Harbor.build.done"))return;try{
 var all=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var house=all.First(t=>t.name=="1990s_house");var story=all.Select(t=>t.GetComponent<Scene04HarborStory>()).First(t=>t);var root=new GameObject("Harbor QA floorplan");root.layer=31;
 var mat=new Material(Shader.Find("Sprites/Default"));
 foreach(var filter in house.GetComponentsInChildren<MeshFilter>(true)){
 var v=filter.sharedMesh.vertices;var tri=filter.sharedMesh.triangles;
 for(int i=0;i<tri.Length;i+=3){Vector3[] p={filter.transform.TransformPoint(v[tri[i]]),filter.transform.TransformPoint(v[tri[i+1]]),filter.transform.TransformPoint(v[tri[i+2]])};var hits=new System.Collections.Generic.List<Vector3>();
 for(int k=0;k<3;k++){var a=p[k];var b=p[(k+1)%3];if((a.y-1.2f)*(b.y-1.2f)<0){var h=Vector3.Lerp(a,b,(1.2f-a.y)/(b.y-a.y));h.y=0;hits.Add(h);}}
 if(hits.Count==2)Line(root.transform,mat,hits[0],hits[1],Color.white,.025f);
 }}
 Mark(root.transform,mat,story.father.position,Color.red,"Father");Mark(root.transform,mat,story.burner.position,Color.green,"Burner");Mark(root.transform,mat,new Vector3(6.96f,0,1.05f),Color.yellow,"OldChair");Mark(root.transform,mat,story.seaTarget,Color.cyan,"Sea");
 foreach(var t in all.Where(t=>t.name.StartsWith("1990s_")&&(t.name.Contains("table")||t.name.Contains("sofa"))))Mark(root.transform,mat,t.position,Color.gray,t.name.Replace("1990s_",""));
 for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;Line(root.transform,mat,new Vector3(6.24f+Mathf.Sin(a),0,2.33f+Mathf.Cos(a)),new Vector3(6.24f+Mathf.Sin(b),0,2.33f+Mathf.Cos(b)),Color.gray,.015f);}
 var go=new GameObject("Harbor plan camera");var camera=go.AddComponent<Camera>();camera.cullingMask=1<<31;camera.orthographic=true;camera.orthographicSize=4;camera.transform.SetPositionAndRotation(new Vector3(6,10,3),Quaternion.LookRotation(Vector3.down,Vector3.forward));camera.backgroundColor=new Color(.04f,.04f,.04f);camera.clearFlags=CameraClearFlags.SolidColor;var rt=new RenderTexture(1200,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes("Temp/Scene04Harbor-plan.png",tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(mat);File.WriteAllText("Temp/Scene04Harbor.plan.done","OK");
 }catch(Exception ex){File.WriteAllText("Temp/Scene04Harbor.plan.error",ex.ToString());}}
 static void Line(Transform parent,Material mat,Vector3 a,Vector3 b,Color color,float width){var go=new GameObject("plan line");go.layer=31;go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=mat;line.positionCount=2;line.SetPositions(new[]{a,b});line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;}
 static void Mark(Transform parent,Material mat,Vector3 p,Color color,string label){p.y=.1f;Line(parent,mat,p-Vector3.right*.12f,p+Vector3.right*.12f,color,.05f);Line(parent,mat,p-Vector3.forward*.12f,p+Vector3.forward*.12f,color,.05f);var go=new GameObject(label,typeof(TextMesh));go.layer=31;go.transform.SetParent(parent,false);go.transform.position=p+Vector3.right*.15f;go.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);var t=go.GetComponent<TextMesh>();t.text=label+" "+p.x.ToString("F1")+","+p.z.ToString("F1");t.fontSize=24;t.characterSize=.05f;t.color=color;}
}
