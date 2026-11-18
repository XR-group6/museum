using System;using System.IO;using UnityEditor;using UnityEngine;
[InitializeOnLoad]public static class Scene05HomecomingPlayCheck
{
 static double at;static int stage;static Scene05HomecomingStory story;
 static Scene05HomecomingPlayCheck(){EditorApplication.update+=Tick;EditorApplication.delayCall+=Start;}
 static void Start(){if(File.Exists("Temp/scene05-play.done")||File.Exists("Temp/scene05-play.error")||!File.Exists("Temp/scene05-check.done")||!File.Exists("Temp/scene05-final.done"))return;if(EditorApplication.isPlaying)return;SessionState.SetBool("Scene05PlayTest",true);EditorApplication.isPlaying=true;}
 static void Capture(string name,Camera camera){var rt=new RenderTexture(1200,800,24);var old=camera.targetTexture;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,800),0,0);tex.Apply();File.WriteAllBytes("Previews/Scene05Homecoming/"+name+".png",tex.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=null;UnityEngine.Object.Destroy(tex);UnityEngine.Object.Destroy(rt);}
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Tick(){if(!SessionState.GetBool("Scene05PlayTest",false)||!EditorApplication.isPlaying)return;try{
 if(stage==0){story=UnityEngine.Object.FindObjectOfType<Scene05HomecomingStory>();if(!story||!story.dialogue)return;at=EditorApplication.timeSinceStartup;stage=1;return;}
 if(EditorApplication.timeSinceStartup-at<1)return;
 if(stage==1){Assert(!story.ExplorationStarted&&!story.permit.Ready,"Exploration unlocked early");story.permit.Open();Assert(!story.permit.IsOpen,"Permit opened during story");story.director.Pause();story.director.time=39.5;story.director.Evaluate();stage=2;at=EditorApplication.timeSinceStartup;return;}
 if(stage==2){Assert(story.ExplorationStarted&&story.permit.Ready,"No exploration after closed door");Assert(story.desktopMovement.enabled,"Desktop movement not enabled");Assert(!story.father.gameObject.activeSelf,"Actor did not leave");Assert(Quaternion.Angle(story.door.localRotation,Quaternion.identity)<.1f,"Timeline door pose did not evaluate");Assert(!story.FinalNarrationPlayed,"Final line played before inspection");Capture("Exploration",story.Viewer);story.permit.Open();Assert(story.permit.IsOpen,"Permit failed to open");story.permit.Close();Assert(!story.FinalNarrationPlayed,"Unread close triggered final line");story.permit.Open();story.permit.ToggleFold();stage=3;at=EditorApplication.timeSinceStartup;return;}
 if(stage==3){if(EditorApplication.timeSinceStartup-at<3.5)return;Assert(story.permit.IsUnfolded,"Permit did not unfold");Assert(!story.FinalNarrationPlayed,"Final voice played before returning document");Capture("Certificate_Read",story.Viewer);story.permit.Close();Assert(story.FinalNarrationPlayed,"Final voice missing after read and return");stage=4;at=EditorApplication.timeSinceStartup;return;}
 if(stage==4){Assert(story.Viewer.transform.position.y>1.5f&&story.Viewer.transform.position.y<1.95f,"Viewer fell through floor");File.WriteAllText("Logs/Scene05Homecoming-play-validation.txt","PASS: story locks movement/permit; closed door unlocks exploration; father exits; unread close stays silent; unfolded reading alone stays silent; returning the read document triggers final narration.\nViewer="+story.Viewer.name+"; eye="+story.Viewer.transform.position+"; duration="+story.director.duration);File.WriteAllText("Temp/scene05-play.done","OK");SessionState.SetBool("Scene05PlayTest",false);EditorApplication.isPlaying=false;}
 }catch(Exception e){File.WriteAllText("Temp/scene05-play.error",e.ToString());SessionState.SetBool("Scene05PlayTest",false);EditorApplication.isPlaying=false;}}
}



